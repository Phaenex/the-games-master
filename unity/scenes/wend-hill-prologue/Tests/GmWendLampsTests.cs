// Guards the gap-lamp placement rule.
//
// This is the only change measured to move p5, the darkest frames of the walk, which sat at 0.012
// through eight lighting brackets including a five times increase in practical intensity. Turning
// lamps up lifted only frames that already had a lamp; the dark stretches have none. So the rule that
// decides WHERE a lamp goes is the load-bearing part, and it is pure so it can be tested here rather
// than by rebuilding a scene and walking it.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendLampsTests
{
    [Test]
    public void APointAlreadyNearALampGetsNothing()
    {
        var route = new List<Vector3> { new Vector3(0f, 0f, 0f) };
        var existing = new List<Vector3> { new Vector3(5f, 0f, 0f) };
        CollectionAssert.IsEmpty(GmWendLamps.Gaps(route, existing, 30f));
    }

    [Test]
    public void APointBeyondReachGetsALamp()
    {
        var route = new List<Vector3> { new Vector3(100f, 0f, 0f) };
        var existing = new List<Vector3> { new Vector3(0f, 0f, 0f) };
        Assert.AreEqual(1, GmWendLamps.Gaps(route, existing, 30f).Count);
    }

    [Test]
    public void HeightIsIgnoredWhenMeasuringReach()
    {
        // The street runs over a hill and lamps sit on posts and brackets, so a lamp 8m above a point
        // is still lighting it. Measuring in 3D would call that a gap and stack a second lamp on top
        // of the first.
        var route = new List<Vector3> { new Vector3(0f, 0f, 0f) };
        var existing = new List<Vector3> { new Vector3(0f, 25f, 0f) };
        CollectionAssert.IsEmpty(GmWendLamps.Gaps(route, existing, 10f));
    }

    [Test]
    public void APlacedLampCoversThePointsBehindIt()
    {
        // Greedy on purpose. Without this a straight unlit run gets one lamp per waypoint, which on a
        // route with 15m spacing is a lamp every 15m and a corridor brighter than the village.
        var route = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, 20f),
            new Vector3(0f, 0f, 30f),    // exactly at reach, so still covered
            new Vector3(0f, 0f, 45f),    // beyond it
        };

        List<Vector3> placed = GmWendLamps.Gaps(route, new List<Vector3>(), 30f);
        Assert.AreEqual(2, placed.Count, "one lamp should cover everything out to 30m");
        Assert.AreEqual(new Vector3(0f, 0f, 0f), placed[0]);
        Assert.AreEqual(new Vector3(0f, 0f, 45f), placed[1]);
    }

    [Test]
    public void ReachIsInclusiveAtItsExactDistance()
    {
        // Pinned because the first version of the test above assumed it was exclusive and failed
        // against correct code. A point exactly `reach` from a lamp is lit, not a gap; treating it as
        // a gap puts a second lamp 30m from the first all the way down a straight street.
        var atReach = new List<Vector3> { new Vector3(0f, 0f, 30f) };
        var lamp = new List<Vector3> { new Vector3(0f, 0f, 0f) };
        CollectionAssert.IsEmpty(GmWendLamps.Gaps(atReach, lamp, 30f));

        var justPast = new List<Vector3> { new Vector3(0f, 0f, 30.5f) };
        Assert.AreEqual(1, GmWendLamps.Gaps(justPast, lamp, 30f).Count);
    }

    [Test]
    public void ARouteWithNoExistingLightsIsLitFromScratch()
    {
        var route = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 200f),
        };
        Assert.AreEqual(2, GmWendLamps.Gaps(route, new List<Vector3>(), 30f).Count);
    }

    [Test]
    public void NoRouteMeansNoLamps()
    {
        CollectionAssert.IsEmpty(GmWendLamps.Gaps(null, new List<Vector3>(), 30f));
        CollectionAssert.IsEmpty(GmWendLamps.Gaps(new List<Vector3>(), null, 30f));
    }

    [Test]
    public void CheckingOnlySparseWaypointsMissesA152mDarkMiddle()
    {
        // The actual bug, reproduced. Two waypoints 152m apart, each individually within 30m of a
        // light, so a gap check run against the raw waypoints alone reports the whole leg "covered".
        var sparseRoute = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 152f) };
        var existing = new List<Vector3> { new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 147f) };
        CollectionAssert.IsEmpty(GmWendLamps.Gaps(sparseRoute, existing, 30f),
            "reproduces the bug: the sparse waypoints alone report no gap");

        // Densified to walk-probe resolution, the same lights and the same route DO find the gap in the
        // middle -- this is the fix, proven with the exact same Gaps() function, unchanged.
        List<Vector3> dense = GmWendRoute.Densify(sparseRoute, GmWendLamps.GapSampleSpacing);
        List<Vector3> found = GmWendLamps.Gaps(dense, existing, 30f);
        Assert.IsNotEmpty(found, "densified sampling must find the gap the sparse check missed");
        Assert.IsTrue(found.Exists(p => p.z > 35f && p.z < 117f),
            "the placed lamp(s) should fall in the unlit middle of the leg, not near either end");
    }
}
