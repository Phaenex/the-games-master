// Guards the water lookup the walk probe stops on.
//
// This replaced three attempts at predicting submersion from the route at build time, each of which
// picked a surface signal that was wrong on this scene. At runtime there is nothing to predict, so the
// only thing left that can be wrong is FINDING the water, which is what these cover.
//
// The thin-surface filter is the part worth pinning down: a lake is a flat plane and a water tower is
// a tall box, and treating the tower's roof as a water surface would stop the walk in a dry street.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendWaterSurfaceTests
{
    static GameObject Slab(string name, float y, float thickness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = new Vector3(0f, y, 0f);
        go.transform.localScale = new Vector3(50f, thickness, 50f);
        return go;
    }

    [Test]
    public void TheHighestThinWaterSurfaceIsFound()
    {
        var low = Slab("Water_Low", -10f, 0.5f);
        var high = Slab("Lake_Surface", -2f, 0.5f);
        try
        {
            float? surface = GmWendRoute.WaterSurfaceY();
            Assert.IsTrue(surface.HasValue);
            // Top face of the higher slab: centre -2 plus half of 0.5.
            Assert.AreEqual(-1.75f, surface.Value, 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(low);
            Object.DestroyImmediate(high);
        }
    }

    [Test]
    public void ATallBoxIsNotAWaterSurface()
    {
        // A water TOWER matches the name pattern and is nothing to drown in. Its roof would sit metres
        // above the street and stop the walk on dry ground.
        var tower = Slab("WaterTower", 20f, 12f);
        try
        {
            Assert.IsFalse(GmWendRoute.WaterSurfaceY().HasValue,
                "a 12m tall box named Water must not be read as a water surface");
        }
        finally { Object.DestroyImmediate(tower); }
    }

    [Test]
    public void SomethingThatIsNotWaterIsIgnored()
    {
        var road = Slab("Road_Cobble_01", 3f, 0.5f);
        try { Assert.IsFalse(GmWendRoute.WaterSurfaceY().HasValue); }
        finally { Object.DestroyImmediate(road); }
    }

    [Test]
    public void AnInactiveWaterSurfaceDoesNotCount()
    {
        // A disabled water plane is not in the scene the player walks through.
        var off = Slab("River", 5f, 0.5f);
        off.SetActive(false);
        try { Assert.IsFalse(GmWendRoute.WaterSurfaceY().HasValue); }
        finally { Object.DestroyImmediate(off); }
    }

    [Test]
    public void ASceneWithNoWaterReturnsNothingRatherThanZero()
    {
        // Zero would be a plausible looking height and would stop the walk wherever the ground dipped
        // below sea level, which on this map is most of it.
        Assert.IsFalse(GmWendRoute.WaterSurfaceY().HasValue);
    }
}
