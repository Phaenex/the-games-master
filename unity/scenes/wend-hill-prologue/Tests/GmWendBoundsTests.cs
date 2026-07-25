// Guards the map-edge geometry.
//
// The defect these exist for: the prologue had no boundary at all, and a walk went off the edge and
// fell 690m. The frames it captured on the way down read mean luma 0.005, which a luma check calls a
// darker night rather than an absent world, so nothing in the review pipeline noticed.
//
// The geometry is pure maths deliberately, so the corner-sealing and the catch height can be checked
// here rather than by rebuilding a scene and walking at a wall.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendBoundsTests
{
    static Bounds World()
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(-500f, -10f, -500f), new Vector3(500f, 90f, 500f));
        return b;
    }

    [Test]
    public void FourWallsOneOnEachSide()
    {
        GmWendBounds.WallBox[] walls = GmWendBounds.WallBoxes(World());
        Assert.AreEqual(4, walls.Length);
        CollectionAssert.AreEquivalent(
            new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West" },
            System.Array.ConvertAll(walls, w => w.name));
    }

    [Test]
    public void WallsOverlapAtTheCornersSoTheBoundaryHasNoDiagonalGap()
    {
        // The failure this pins down: four walls each sized to their exact edge leave a
        // thickness-wide hole at every corner, and a corner is precisely where someone walking a
        // perimeter ends up. Each span must exceed its edge by a full thickness.
        Bounds world = World();
        GmWendBounds.WallBox[] walls = GmWendBounds.WallBoxes(world);

        GmWendBounds.WallBox north = System.Array.Find(walls, w => w.name == "Wall_North");
        GmWendBounds.WallBox east = System.Array.Find(walls, w => w.name == "Wall_East");

        Assert.AreEqual(world.size.x + GmWendBounds.WallThickness, north.size.x, 0.001f,
            "north/south walls must overhang in X to close the corners");
        Assert.AreEqual(world.size.z + GmWendBounds.WallThickness, east.size.z, 0.001f,
            "east/west walls must overhang in Z to close the corners");
    }

    [Test]
    public void WallsSpanTheFullHeightOfTheWorldPlusClearance()
    {
        // A wall only as tall as the average ground is a wall you can walk over wherever the terrain
        // rises. Height is measured from the lowest point of the world, not from zero.
        Bounds world = World();
        GmWendBounds.WallBox wall = GmWendBounds.WallBoxes(world)[0];

        float expected = world.size.y + GmWendBounds.WallClearance;
        Assert.AreEqual(expected, wall.size.y, 0.001f);

        float bottom = wall.centre.y - wall.size.y * 0.5f;
        Assert.AreEqual(world.min.y, bottom, 0.001f, "walls must start at the floor of the world");
        Assert.Greater(wall.centre.y + wall.size.y * 0.5f, world.max.y,
            "walls must finish above the highest ground");
    }

    [Test]
    public void TheCatchHeightSitsUnderTheWorldNotInsideIt()
    {
        // If the catch height were inside the terrain's range it would fire while the player was
        // standing on the ground, which turns a failsafe into a teleport loop.
        Bounds world = World();
        float catchY = GmWendBounds.CatchY(world);
        Assert.Less(catchY, world.min.y);
        Assert.AreEqual(world.min.y - GmWendBounds.CatchDepth, catchY, 0.001f);
    }

    [Test]
    public void RecoveryPicksTheNearestWaypointInXZAndIgnoresHeight()
    {
        // Height is excluded on purpose. The route runs over a hill, so a waypoint far up the slope
        // can be nearer in 3D than the one actually walked past, and recovering to it would put the
        // player somewhere they had never been.
        var route = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(100f, 400f, 0f),   // far in height, near in XZ
            new Vector3(300f, 0f, 0f),
        };

        Vector3 chosen = GmWendCatchPlane.NearestPoint(route, new Vector3(110f, -600f, 0f), Vector3.zero);
        Assert.AreEqual(new Vector3(100f, 400f, 0f), chosen);
    }

    [Test]
    public void RecoveryFallsBackWhenThereIsNoRoute()
    {
        // A scene whose road meshes did not match leaves an empty route. Recovering to the origin
        // would drop the player at world zero, which on this map is not the village.
        var fallback = new Vector3(12f, 3f, -4f);
        Assert.AreEqual(fallback, GmWendCatchPlane.NearestPoint(new Vector3[0], Vector3.zero, fallback));
        Assert.AreEqual(fallback, GmWendCatchPlane.NearestPoint(null, Vector3.zero, fallback));
    }
}
