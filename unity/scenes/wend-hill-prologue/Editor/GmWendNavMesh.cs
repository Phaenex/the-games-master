// Bakes a NavMesh over the prologue's route corridor, so the walk can go around the pack's props
// instead of jamming on them.
//
// The defect: the walk covered 188m of a 580m route with 9 stalls. The player drives a real
// CharacterController on purpose, because a free camera passes straight through the mansion, the gate
// and 189 scattered props and every one of those frames looks correct. The cost of that honesty is
// that a straight line at the next waypoint jams on hay bales, fences and cart wheels. The existing
// two-sidestep hack clears a prop and cannot clear a building, and its own comment says so: if
// sidesteps are still stalling then the coverage number is asking for a NavMesh.
//
// WHY A BOUNDED VOLUME AND NOT THE WHOLE SCENE: the terrain is about a kilometre across and the scene
// carries 4997 renderers, while the prologue is a walk down one street. Baking the corridor the route
// actually runs through is the difference between a bake measured in seconds and one measured in
// minutes, and minutes would be paid on every build.
//
// WHY THIS IS NOT IN BuildBase: GmWendLadder rebuilds the scene from the purchased source on EVERY
// rung, ten times a run. Putting a bake in the shared build path would multiply it by ten to produce
// frames that nobody walks. The ladder renders stills from a fixed rig; only the committed build and
// the walk need to path anywhere.
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public static class GmWendNavMesh
{
    const string LogTag = "GmWendNavMesh";

    public const string RootName = "GmWendNavMesh";

    /// Margin around the route, in metres. Wide enough that a detour around a building stays on the
    /// mesh, narrow enough that the bake stays cheap.
    public const float CorridorMargin = 45f;

    /// Vertical padding above and below the route, for the hill the street runs over.
    public const float CorridorHeight = 120f;

    /// These must match the player's CharacterController in GmWendBuilder. A NavMesh baked for a
    /// narrower agent than the body that walks it produces paths through gaps the controller cannot
    /// fit, which is a harder failure to read than a stall: the player jams while standing on a route
    /// that claims to be clear.
    public const float AgentRadius = 0.35f;
    public const float AgentHeight = 1.8f;
    public const float AgentSlope = 50f;
    public const float AgentStep = 0.4f;

    /// The box to bake, derived from the route rather than from the map.
    ///
    /// Pure, so the corridor can be checked without a scene.
    public static Bounds Corridor(IList<Vector3> route)
    {
        if (route == null || route.Count == 0)
            throw new System.ArgumentException("cannot derive a bake corridor from an empty route");

        var bounds = new Bounds(route[0], Vector3.zero);
        for (int i = 1; i < route.Count; i++) bounds.Encapsulate(route[i]);

        bounds.Expand(new Vector3(CorridorMargin * 2f, CorridorHeight, CorridorMargin * 2f));
        return bounds;
    }

    /// Bakes the corridor into the open scene. Returns the baked area in square metres, which is the
    /// number worth asserting on: a surface that builds successfully but covers nothing is the silent
    /// version of this failing, and it would leave the walk exactly where it started.
    public static float Apply()
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) Object.DestroyImmediate(stale);

        List<Vector3> route = GmWendRoute.BuildEstate(out _);
        if (route.Count == 0)
            throw new System.InvalidOperationException(
                "no route to bake a corridor around; the road meshes did not match");

        Bounds corridor = Corridor(route);

        var root = new GameObject(RootName);
        var surface = root.AddComponent<NavMeshSurface>();

        // The purchased road is a set of disconnected display meshes. The canonical opening owns a
        // continuous collision ribbon on a dedicated layer; bake that exact walk contract instead of
        // allowing nearby terrain islands to win SamplePosition and silently create partial paths.
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.collectObjects = CollectObjects.Volume;
        surface.layerMask = 1 << GmWendOpening.WalkDeckLayer;
        surface.center = corridor.center - root.transform.position;
        surface.size = corridor.size;

        surface.agentTypeID = 0;
        surface.overrideVoxelSize = true;
        surface.voxelSize = AgentRadius / 3f;   // the usual 3 voxels per radius
        surface.overrideTileSize = false;

        surface.BuildNavMesh();

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        float area = TriangulatedArea(tri);

        if (tri.indices.Length == 0)
            throw new System.InvalidOperationException(
                "the NavMesh baked to zero triangles. The walk would be no better off than before, " +
                "and a surface that exists but covers nothing looks like success in every log line.");

        Debug.Log($"[{LogTag}] baked {area:0}m^2 over a " +
                  $"{corridor.size.x:0}x{corridor.size.z:0}m corridor from {route.Count} waypoint(s), " +
                  $"{tri.indices.Length / 3} triangle(s), agent radius {AgentRadius}");
        return area;
    }

    /// Total area of the triangulation, in square metres.
    ///
    /// Pure and public so the "did it actually cover anything" assertion can be tested with known
    /// triangles rather than by trusting a baked result.
    public static float TriangulatedArea(NavMeshTriangulation tri) =>
        TriangulatedArea(tri.vertices, tri.indices);

    public static float TriangulatedArea(Vector3[] vertices, int[] indices)
    {
        if (vertices == null || indices == null) return 0f;

        float total = 0f;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Vector3 a = vertices[indices[i]];
            Vector3 b = vertices[indices[i + 1]];
            Vector3 c = vertices[indices[i + 2]];
            total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
        return total;
    }
}
