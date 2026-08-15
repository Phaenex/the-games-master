// Guards the wildlife-anchor placement rule -- the one genuinely Wend Hill-specific part of the
// ambience fix. The clips and cadence are reused verbatim from the already-reviewed retired-scene
// recipe (GmAmbience.cs); this is what is NOT reused, because it is the part that would be wrong to
// reuse: hand-placed coordinates from a different scene's geometry.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendAmbienceTests
{
    [Test]
    public void TooFewPointsProducesNoAnchors()
    {
        CollectionAssert.IsEmpty(GmWendAmbience.Anchors(null, 1, 5f));
        CollectionAssert.IsEmpty(GmWendAmbience.Anchors(new List<Vector3> { Vector3.zero }, 1, 5f));
    }

    [Test]
    public void AnchorsSitOffsetFromTheRouteNotOnIt()
    {
        var dense = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, 20f),
        };
        List<Vector3> anchors = GmWendAmbience.Anchors(dense, 1, 8f);
        Assert.IsNotEmpty(anchors);
        foreach (Vector3 a in anchors)
            Assert.AreNotEqual(0f, a.x, "an anchor sitting at x=0 is standing on the walked line");
    }

    [Test]
    public void StrideControlsSpacingAlongTheRoute()
    {
        // 21 points spaced 1m apart on a straight line. Stride 5 should place a fifth as many anchors
        // as stride 1, not the same count.
        var dense = new List<Vector3>();
        for (int i = 0; i <= 20; i++) dense.Add(new Vector3(0f, 0f, i));

        List<Vector3> tight = GmWendAmbience.Anchors(dense, 1, 5f);
        List<Vector3> sparse = GmWendAmbience.Anchors(dense, 5, 5f);
        Assert.Less(sparse.Count, tight.Count);
        Assert.AreEqual(4, sparse.Count);   // indices 5, 10, 15, 20
    }

    [Test]
    public void ConsecutiveAnchorsAlternateSides()
    {
        var dense = new List<Vector3>();
        for (int i = 0; i <= 30; i++) dense.Add(new Vector3(0f, 0f, i));

        List<Vector3> anchors = GmWendAmbience.Anchors(dense, 5, 8f);
        Assert.GreaterOrEqual(anchors.Count, 2);
        for (int i = 1; i < anchors.Count; i++)
            Assert.AreNotEqual(Mathf.Sign(anchors[i - 1].x), Mathf.Sign(anchors[i].x),
                "consecutive anchors should not sit on the same side of the route");
    }

    [Test]
    public void ADegenerateZeroLengthLegIsSkippedRatherThanProducingANaNAnchor()
    {
        // Two coincident points can happen at the seam of a densified route; the perpendicular of a
        // zero-length direction is undefined and must not produce a NaN anchor.
        var dense = new List<Vector3>
        {
            new Vector3(0f, 0f, 5f),
            new Vector3(0f, 0f, 5f),
            new Vector3(0f, 0f, 15f),
        };
        List<Vector3> anchors = GmWendAmbience.Anchors(dense, 1, 8f);
        foreach (Vector3 a in anchors)
        {
            Assert.IsFalse(float.IsNaN(a.x));
            Assert.IsFalse(float.IsNaN(a.z));
        }
    }
}
