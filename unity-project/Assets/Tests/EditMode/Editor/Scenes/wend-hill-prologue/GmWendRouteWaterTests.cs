// Guards the route's water truncation.
//
// The defect: a walk spent its last ~45m submerged, looking up at the underside of the lake surface.
// The final waypoint sat at y=-22.80 against a spawn at -0.81, and the frames were a golden band of
// refracted light. No lighting work fixes a prologue that ends underwater.
//
// The reason these tests are worth having rather than just reading the code: filtering on water has
// failed twice in this project already, both times by excluding dry land. The second attempt used the
// water's axis-aligned bounding box and threw away 43 of 57 road pieces including the whole village
// street, because a winding river's AABB covers most of a valley. The fix here tests HEIGHT only, and
// the test that matters most is the one proving it does not touch a road that runs beside water.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendRouteWaterTests
{
    static Bounds[] Lake(float surfaceY) =>
        new[] { new Bounds(new Vector3(0f, surfaceY - 1f, 200f), new Vector3(300f, 2f, 300f)) };

    [Test]
    public void WaypointsThatDescendBelowTheSurfaceAreCut()
    {
        var route = new List<Vector3>
        {
            new Vector3(0f, 5f, 0f),
            new Vector3(0f, 4f, 50f),
            new Vector3(0f, 3f, 100f),
            new Vector3(0f, -20f, 150f),   // under
            new Vector3(0f, -22f, 200f),   // further under
        };

        List<Vector3> cut = GmWendRoute.TruncateAtWater(route, Lake(0f), p => p.y, null);
        Assert.AreEqual(3, cut.Count);
        Assert.AreEqual(new Vector3(0f, 3f, 100f), cut[cut.Count - 1]);
    }

    [Test]
    public void ARoadBesideWaterIsLeftAlone()
    {
        // THE regression test. The lake's footprint covers the whole route in XZ, and every waypoint is
        // above its surface. A footprint-based filter deletes this route; a height-based one must not
        // touch it. This is the failure that cost two earlier attempts.
        var route = new List<Vector3>
        {
            new Vector3(0f, 5f, 0f),
            new Vector3(0f, 6f, 100f),
            new Vector3(0f, 4f, 200f),
        };

        List<Vector3> kept = GmWendRoute.TruncateAtWater(route, Lake(0f), p => p.y, null);
        CollectionAssert.AreEqual(route, kept);
    }

    [Test]
    public void GroundJustAboveTheWaterIsKept()
    {
        // No safety margin is added above the surface, and this test exists to stop one being added
        // back. The village ground clears the water by a little over half a metre, measured: surface
        // at y=-0.8, village terrain a touch above it. A 1m margin, which is what the first version
        // used, condemns the whole village as underwater. Ground barely above the water is still dry
        // ground and the walk must keep it.
        var route = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 50f),
            new Vector3(0f, 0f, 100f),
        };

        CollectionAssert.AreEqual(route,
            GmWendRoute.TruncateAtWater(route, Lake(0f), _ => 0.2f, null),
            "ground 0.2m above the surface is dry and must survive");
    }

    [Test]
    public void TheGroundDecidesAndNotTheWaypointsOwnHeight()
    {
        // The bug this replaced. Route waypoints are road-mesh bounding-box CENTRES, not surface
        // heights: on the real scene the spawn's road mesh centres at y=-0.81 while the terrain under
        // it is at +0.68, and the lake surface is -0.8. Judging by waypoint Y therefore called the
        // entire village submerged and tried to cut the route to nothing.
        var route = new List<Vector3>
        {
            new Vector3(0f, -0.81f, 0f),     // below the water line by its own Y...
            new Vector3(0f, -0.81f, 50f),
            new Vector3(0f, -0.81f, 100f),
        };

        // ...but standing on ground well above it.
        CollectionAssert.AreEqual(route, GmWendRoute.TruncateAtWater(route, Lake(0f), _ => 5f, null),
            "dry road whose mesh centre dips below the water line must survive");
    }

    [Test]
    public void GroundBelowTheWaterCutsEvenWhenTheWaypointSitsAboveIt()
    {
        // The converse, and the case that actually matters for the prologue: a waypoint floating above
        // a submerged lake bed is still somewhere the player would be underwater.
        var route = new List<Vector3>
        {
            new Vector3(0f, 20f, 0f),
            new Vector3(0f, 20f, 50f),
            new Vector3(0f, 20f, 100f),
        };

        List<Vector3> cut = GmWendRoute.TruncateAtWater(
            route, Lake(0f), p => p.z >= 100f ? -20f : 5f, null);
        Assert.AreEqual(2, cut.Count);
    }

    [Test]
    public void ARouteWithNoWaterIsUnchanged()
    {
        var route = new List<Vector3> { new Vector3(0f, -50f, 0f), new Vector3(0f, -60f, 50f) };
        CollectionAssert.AreEqual(route, GmWendRoute.TruncateAtWater(route, new Bounds[0], p => p.y, null));
        CollectionAssert.AreEqual(route, GmWendRoute.TruncateAtWater(route, null, p => p.y, null));
    }

    [Test]
    public void ACutThatWouldLeaveNoWalkIsRefused()
    {
        // If the water surface somehow sits above the entire road, obeying it would return a route of
        // one point, which is not a walk. That is a scene problem to report, not to act on.
        var route = new List<Vector3>
        {
            new Vector3(0f, -5f, 0f),
            new Vector3(0f, -6f, 50f),
            new Vector3(0f, -7f, 100f),
        };

        CollectionAssert.AreEqual(route, GmWendRoute.TruncateAtWater(route, Lake(50f), p => p.y, null));
    }
}
