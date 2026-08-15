// Guards attempts four through six at answering "is this waypoint dry" from GmWendRoute.cs. None of
// them is wired into Build() -- see the comment at the call site for why -- but each is correct code
// tested against the specific real-scene bug it was built to catch, so the next attempt starts from
// what is already known rather than rediscovering it.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendRouteGroundTests
{
    GameObject ground;
    GameObject water;

    [TearDown]
    public void Cleanup()
    {
        if (ground != null) Object.DestroyImmediate(ground);
        if (water != null) Object.DestroyImmediate(water);
    }

    static GameObject Box(string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.position = center;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        return go;
    }

    [Test]
    public void FindsTheSolidSurfaceDirectlyBelow()
    {
        ground = Box("RoadPiece", new Vector3(0f, 2f, 0f), new Vector3(20f, 1f, 20f));
        float? y = GmWendRoute.RaycastGroundY(new Vector3(0f, 50f, 0f));
        Assert.IsTrue(y.HasValue);
        Assert.AreEqual(2.5f, y.Value, 0.01f, "should land on the box's top face");
    }

    [Test]
    public void SkipsTheWaterColliderAndFindsTheBedBeneathIt()
    {
        // The case that made the previous three attempts fail: a ray straight down over open water
        // must not stop at the water's own surface, or a submerged waypoint would compare its own
        // surface to itself and never get cut.
        water = Box("Lake_Water", new Vector3(0f, 0f, 0f), new Vector3(50f, 1f, 50f));
        // Deliberately not named with "Lake"/"Water"/"River"/"Pond" -- WaterPattern would skip this
        // collider too and the test would pass by skipping BOTH hits, hiding the real bug it exists
        // to catch: that RaycastGroundY finds a solid surface at all once water is out of the way.
        ground = Box("SubmergedRock", new Vector3(0f, -10f, 0f), new Vector3(50f, 1f, 50f));

        float? y = GmWendRoute.RaycastGroundY(new Vector3(0f, 50f, 0f));
        Assert.IsTrue(y.HasValue);
        Assert.AreEqual(-9.5f, y.Value, 0.01f, "must skip the water collider and report the bed beneath it");
    }

    [Test]
    public void NothingBelowReturnsNull()
    {
        Assert.IsFalse(GmWendRoute.RaycastGroundY(new Vector3(999f, 999f, 999f)).HasValue);
    }

    [Test]
    public void GroundNearWaterIsNullWhenNoWaterIsInTheColumnAtAll()
    {
        // The bug this guards against: waypoint 4 on the real route sits at terrain y=-1.46, which is
        // 0.66m under the lake's absolute y=-0.8, but nothing wet is anywhere near it. A dry low spot
        // must never be compared against a lake it isn't part of, no matter how far below the lake's
        // own surface it happens to measure.
        ground = Box("SubmergedRock", new Vector3(0f, -10f, 0f), new Vector3(50f, 1f, 50f));
        Assert.IsFalse(GmWendRoute.GroundNearWater(new Vector3(0f, 50f, 0f)).HasValue,
            "no water collider in this column, so this point cannot be judged underwater");
    }

    [Test]
    public void GroundNearWaterReportsTheBedWhenWaterIsActuallyThere()
    {
        water = Box("Lake_Water", new Vector3(0f, 0f, 0f), new Vector3(50f, 1f, 50f));
        ground = Box("SubmergedRock", new Vector3(0f, -10f, 0f), new Vector3(50f, 1f, 50f));

        float? y = GmWendRoute.GroundNearWater(new Vector3(0f, 50f, 0f));
        Assert.IsTrue(y.HasValue, "the water collider IS in this column, so this must report the bed beneath it");
        Assert.AreEqual(-9.5f, y.Value, 0.01f);
    }

    [Test]
    public void GroundInFootprintIsNullOutsideTheWaterBounds()
    {
        // The dry-low-spot case again, this time against attempt six: a point outside the water's
        // rendered XZ bounds must never be judged submerged no matter how low its ground actually is.
        var farAway = new Bounds(new Vector3(500f, 0f, 500f), new Vector3(50f, 2f, 50f));
        Assert.IsFalse(GmWendRoute.GroundInFootprint(new Vector3(0f, 0f, 0f), new[] { farAway }).HasValue);
    }

    [Test]
    public void GroundInFootprintReportsGroundInsideTheWaterBounds()
    {
        ground = Box("SubmergedRock", new Vector3(0f, -10f, 0f), new Vector3(50f, 1f, 50f));
        var here = new Bounds(new Vector3(0f, -0.8f, 0f), new Vector3(50f, 2f, 50f));

        float? y = GmWendRoute.GroundInFootprint(new Vector3(0f, 0f, 0f), new[] { here });
        Assert.IsTrue(y.HasValue, "this point IS inside the water's rendered bounds");
        Assert.AreEqual(-9.5f, y.Value, 0.01f);
    }

    [Test]
    public void GroundInFootprintWithNoWaterBoundsIsAlwaysNull()
    {
        Assert.IsFalse(GmWendRoute.GroundInFootprint(new Vector3(0f, 0f, 0f), null).HasValue);
        Assert.IsFalse(GmWendRoute.GroundInFootprint(new Vector3(0f, 0f, 0f), new Bounds[0]).HasValue);
    }
}
