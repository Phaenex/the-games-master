// Guards the NavMesh bake corridor and the "did it cover anything" assertion.
//
// The defect these exist for: the walk covered 188m of a 580m route with 9 stalls, because a real
// CharacterController steering in a straight line jams on the pack's prop scatter. The bake is the
// fix, and the two ways it can quietly fail to be a fix are baking the wrong box and baking nothing
// at all. Both are checked here rather than by rebuilding a scene and watching a player walk.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendNavMeshTests
{
    [Test]
    public void TheCorridorEnclosesEveryWaypoint()
    {
        var route = new[]
        {
            new Vector3(0f, 10f, 0f),
            new Vector3(200f, 14f, -60f),
            new Vector3(-30f, 8f, 180f),
        };

        Bounds corridor = GmWendNavMesh.Corridor(route);
        foreach (Vector3 point in route)
            Assert.IsTrue(corridor.Contains(point), $"corridor must contain {point}");
    }

    [Test]
    public void TheCorridorAddsMarginSoADetourStaysOnTheMesh()
    {
        // A bake sized exactly to the route is a mesh one waypoint wide, and a path around a building
        // needs somewhere to go. The margin is what makes the detour possible at all.
        var route = new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f) };
        Bounds corridor = GmWendNavMesh.Corridor(route);

        Assert.AreEqual(100f + GmWendNavMesh.CorridorMargin * 2f, corridor.size.x, 0.001f);
        Assert.AreEqual(GmWendNavMesh.CorridorMargin * 2f, corridor.size.z, 0.001f,
            "a dead straight route still needs width to path around anything");
    }

    [Test]
    public void TheCorridorIsTallEnoughForAHill()
    {
        // The village street runs over a hill. A corridor as flat as the waypoints would clip the
        // ground out of the bake wherever the terrain rises between them.
        var route = new[] { new Vector3(0f, 0f, 0f), new Vector3(50f, 0f, 0f) };
        Assert.AreEqual(GmWendNavMesh.CorridorHeight, GmWendNavMesh.Corridor(route).size.y, 0.001f);
    }

    [Test]
    public void AnEmptyRouteIsRefusedRatherThanBakedAsAPoint()
    {
        // An empty route means the road meshes did not match. Baking a zero-sized corridor would
        // succeed, cover nothing, and leave the walk exactly as stuck as before while every log line
        // said the NavMesh step had run.
        Assert.Throws<System.ArgumentException>(() => GmWendNavMesh.Corridor(new Vector3[0]));
        Assert.Throws<System.ArgumentException>(() => GmWendNavMesh.Corridor(null));
    }

    [Test]
    public void AreaIsTheSumOfTheTriangles()
    {
        // Two unit right triangles forming a 1x1 square, laid flat.
        var vertices = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 1f),
        };
        var indices = new[] { 0, 1, 2, 1, 3, 2 };

        Assert.AreEqual(1f, GmWendNavMesh.TriangulatedArea(vertices, indices), 0.0001f);
    }

    [Test]
    public void AnEmptyTriangulationMeasuresZeroRatherThanThrowing()
    {
        // Zero is the value the bake asserts against, so it has to be reachable and not an exception.
        Assert.AreEqual(0f, GmWendNavMesh.TriangulatedArea(new Vector3[0], new int[0]), 0.0001f);
        Assert.AreEqual(0f, GmWendNavMesh.TriangulatedArea(null, null), 0.0001f);
    }
}
