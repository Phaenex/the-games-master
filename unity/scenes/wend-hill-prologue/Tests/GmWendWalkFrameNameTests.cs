// Guards the walk probe's frame-naming contract.
//
// The defect these exist for: frames used to be numbered by capture index, so walk-05 meant "the
// sixth shot of whatever run this was" rather than a place on the road. Two walks of different length
// could not be compared frame-to-frame, and a table built by pairing them off had to be retracted.
// Nothing about a single run looks wrong when this is broken, which is why it survived until someone
// tried to compare two, and why it belongs in a test rather than in a reviewer's memory.
using NUnit.Framework;

public sealed class GmWendWalkFrameNameTests
{
    [Test]
    public void WalkFramesAreNamedByDistanceNotByCaptureIndex()
    {
        // Same place, different point in the run. The index must not reach the name.
        Assert.AreEqual("walk-0045m.png", GmWendWalkProbe.FrameName("walk", 45f, 3));
        Assert.AreEqual("walk-0045m.png", GmWendWalkProbe.FrameName("walk", 45f, 17));
    }

    [Test]
    public void TheSameMilestoneInTwoRunsOfDifferentLengthProducesTheSameName()
    {
        // The property the retraction was about. A run that stalls early reaches 90m on its second
        // shot; a clean run reaches it on its sixth. Both frames are the same stretch of road, so
        // both must be named for it.
        string stalling = GmWendWalkProbe.FrameName("walk", 90f, 1);
        string clean = GmWendWalkProbe.FrameName("walk", 90f, 5);
        Assert.AreEqual(stalling, clean);
    }

    [Test]
    public void FramesSortIntoWalkOrderLexicographically()
    {
        // Contact sheets and directory listings are read in sort order, so zero padding is load
        // bearing: without it walk-300m sorts before walk-45m and the sheet reads out of order.
        var names = new[]
        {
            GmWendWalkProbe.FrameName("walk", 300f, 0),
            GmWendWalkProbe.FrameName("walk", 45f, 0),
            GmWendWalkProbe.FrameName("walk", 0f, 0),
        };
        System.Array.Sort(names, System.StringComparer.Ordinal);

        CollectionAssert.AreEqual(
            new[] { "walk-0000m.png", "walk-0045m.png", "walk-0300m.png" }, names);
    }

    [Test]
    public void StallAndFallFramesKeepAnIndexSoTwoAtTheSameMetreDoNotCollide()
    {
        // These are off-milestone by nature and there can be several. Naming them by distance alone
        // would have the second silently overwrite the first, losing the frame that shows what the
        // player was pressed against.
        string first = GmWendWalkProbe.FrameName("stall", 120f, 4);
        string second = GmWendWalkProbe.FrameName("stall", 120f, 5);
        Assert.AreNotEqual(first, second);
        Assert.AreEqual("stall-0120m-04.png", first);
        Assert.AreEqual("fell-0690m-11.png", GmWendWalkProbe.FrameName("fell", 690f, 11));
    }

    [Test]
    public void DistanceRoundsToTheNearestMetre()
    {
        // Milestones land within a fraction of a frame's movement of the exact value, so rounding
        // rather than truncating is what keeps two runs landing on the same integer.
        Assert.AreEqual("walk-0045m.png", GmWendWalkProbe.FrameName("walk", 44.6f, 0));
        Assert.AreEqual("walk-0045m.png", GmWendWalkProbe.FrameName("walk", 45.4f, 0));
    }
}
