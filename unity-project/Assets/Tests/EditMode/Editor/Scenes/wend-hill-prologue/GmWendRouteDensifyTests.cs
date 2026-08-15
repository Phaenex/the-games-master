// Guards GmWendRoute.Densify, which exists because a gap check run against the raw 11-waypoint route
// (average ~58m apart) missed a real 152m dark stretch between two waypoints that individually read
// "covered". See the call site in GmWendLamps.Apply and the header comment on Densify itself for the
// full story.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendRouteDensifyTests
{
    [Test]
    public void PreservesTheFirstAndLastPoint()
    {
        var route = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f) };
        List<Vector3> dense = GmWendRoute.Densify(route, 15f);
        Assert.AreEqual(route[0], dense[0]);
        Assert.AreEqual(route[^1], dense[^1]);
    }

    [Test]
    public void IncludesEveryOriginalWaypointExactly()
    {
        // Not just endpoints: a route with a bend in the middle must still land exactly ON the bend,
        // not skip past it because it happened to fall between two evenly-spaced samples.
        var route = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 47f), new Vector3(0f, 0f, 100f),
        };
        List<Vector3> dense = GmWendRoute.Densify(route, 15f);
        foreach (Vector3 waypoint in route)
            CollectionAssert.Contains(dense, waypoint, $"missing original waypoint {waypoint}");
    }

    [Test]
    public void SpacesIntermediatePointsNoFartherApartThanTheSpacing()
    {
        var route = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 152f) };
        List<Vector3> dense = GmWendRoute.Densify(route, 15f);

        for (int i = 1; i < dense.Count; i++)
        {
            float gap = Vector3.Distance(dense[i - 1], dense[i]);
            Assert.LessOrEqual(gap, 15f + 0.01f, $"gap of {gap:0.0}m between samples {i - 1} and {i}");
        }
    }

    [Test]
    public void ThisIsWhatTheRealBugLookedLike()
    {
        // The actual measured case: a ~152m leg whose two endpoints are each individually within lamp
        // reach of a light, so the OLD sparse-waypoint check never sampled anywhere in the middle.
        // Densify must produce points there for a gap check to have any chance of finding them.
        var route = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 152f) };
        List<Vector3> dense = GmWendRoute.Densify(route, 15f);

        bool sampledTheMiddle = dense.Exists(p => p.z > 40f && p.z < 112f);
        Assert.IsTrue(sampledTheMiddle, "must sample somewhere in the middle third of a 152m leg");
    }

    [Test]
    public void ARouteAlreadyDenserThanTheSpacingIsUnchangedInSubstance()
    {
        var route = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 10f),
        };
        List<Vector3> dense = GmWendRoute.Densify(route, 15f);
        foreach (Vector3 waypoint in route)
            CollectionAssert.Contains(dense, waypoint);
    }

    [Test]
    public void EmptyOrNullRouteIsHandledWithoutThrowing()
    {
        CollectionAssert.IsEmpty(GmWendRoute.Densify(new List<Vector3>(), 15f));
        CollectionAssert.IsEmpty(GmWendRoute.Densify(null, 15f));
    }

    [Test]
    public void SinglePointRouteIsReturnedAsIs()
    {
        var route = new List<Vector3> { new Vector3(1f, 2f, 3f) };
        CollectionAssert.AreEqual(route, GmWendRoute.Densify(route, 15f));
    }

    [Test]
    public void ZeroOrNegativeSpacingReturnsTheRouteUnchanged()
    {
        var route = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f) };
        CollectionAssert.AreEqual(route, GmWendRoute.Densify(route, 0f));
        CollectionAssert.AreEqual(route, GmWendRoute.Densify(route, -5f));
    }
}
