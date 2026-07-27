// Answers the question the walk probe cannot: WHY does a NavMesh-complete path still stall the
// CharacterController short of the waypoint. GmWendWalkProbe reports WHERE (a stall position and a
// waypoint index); this reports WHAT is actually there, the same question the waypoint-2 mystery in
// docs/WEND-NIGHT-LADDER.md was answered by hand for. Kept as a permanent tool rather than a one-off
// script, because this is the second time the same question has needed asking and it will not be the
// last: any future route change can produce a new stall, and re-deriving "what's near this point" by
// hand each time is exactly the kind of investigation this project has learned to keep as code.
//
// -gmWendStallAt <x,y,z>  the position to inspect, usually copied straight from a STALLED log line.
// -gmWendStallRadius <r>  search radius in metres, default 8.
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class GmWendStallDiagnostic
{
    const string LogTag = "GmWendStallDiag";

    [MenuItem("GamesMaster/Wend/Diagnose a stall position")]
    public static void RunMenu()
    {
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        Run();
        EditorApplication.Exit(0);
    }

    public static void Run()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int at = System.Array.IndexOf(args, "-gmWendStallAt");
        if (at < 0 || at + 1 >= args.Length)
            throw new System.InvalidOperationException(
                "-gmWendStallAt <x,y,z> is required, e.g. -gmWendStallAt -11.11,0.22,-20.24");

        string[] parts = args[at + 1].Split(',');
        if (parts.Length != 3)
            throw new System.InvalidOperationException($"-gmWendStallAt wants x,y,z, got '{args[at + 1]}'");

        var position = new Vector3(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture));

        int radiusAt = System.Array.IndexOf(args, "-gmWendStallRadius");
        float radius = 8f;
        if (radiusAt >= 0 && radiusAt + 1 < args.Length)
            float.TryParse(args[radiusAt + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out radius);

        Debug.Log($"[{LogTag}] inspecting {position} within {radius}m");

        // The route itself, so a waypoint index in a log line can be read straight off this list.
        var route = GmWendRoute.Build(out _);
        for (int i = 0; i < route.Count; i++)
        {
            float d = Vector3.Distance(route[i], position);
            Debug.Log($"[{LogTag}] waypoint {i}: {route[i]} ({d:0.0}m from the inspected position)");
        }

        // Every collider within radius, with whether it participates in physics at all. This is the
        // exact question the waypoint-2 investigation answered by hand: "nothing here has a Collider"
        // vs "something here does and the mesh routed through it anyway".
        Collider[] colliders = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Collide);
        Debug.Log($"[{LogTag}] {colliders.Length} collider(s) within {radius}m:");
        foreach (Collider c in colliders.OrderBy(c => Vector3.Distance(c.transform.position, position)))
        {
            Debug.Log($"[{LogTag}]   '{c.name}' type={c.GetType().Name} isTrigger={c.isTrigger} " +
                      $"bounds={c.bounds} dist={Vector3.Distance(c.ClosestPoint(position), position):0.00}m " +
                      $"path={ScenePath(c.transform)}");
        }

        // Renderers with no matching collider are the pack's usual failure mode: a mesh that looks
        // solid and blocks nothing. Reported when there is no collider at all nearby, so "visually
        // there, physically absent" is its own line rather than buried in a collider list that has
        // nothing to say about it.
        if (colliders.Length == 0)
        {
            var nearby = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.gameObject.activeInHierarchy &&
                            Vector3.Distance(r.bounds.ClosestPoint(position), position) <= radius)
                .OrderBy(r => Vector3.Distance(r.bounds.ClosestPoint(position), position))
                .Take(15)
                .ToArray();
            Debug.Log($"[{LogTag}] no colliders at all within {radius}m. {nearby.Length} renderer(s) " +
                      "nearby with no collider of their own (visually there, physically absent):");
            foreach (Renderer r in nearby)
                Debug.Log($"[{LogTag}]   '{r.name}' hasCollider={r.GetComponent<Collider>() != null} " +
                          $"dist={Vector3.Distance(r.bounds.ClosestPoint(position), position):0.00}m " +
                          $"path={ScenePath(r.transform)}");
        }

        // NavMesh coverage AT the inspected point, not just at the waypoints either side of it. A
        // corner-to-corner path can cross a stretch of ground the bake never reached even when both
        // ends sample cleanly, which is the gap between "the mesh says this leg is fine" and "the
        // capsule that walks it agrees".
        bool onMesh = NavMesh.SamplePosition(position, out NavMeshHit hit, radius, NavMesh.AllAreas);
        Debug.Log(onMesh
            ? $"[{LogTag}] NavMesh: {hit.distance:0.00}m from the inspected point at {hit.position}"
            : $"[{LogTag}] NavMesh: nothing within {radius}m of the inspected point");

        Debug.Log($"[{LogTag}] DIAGNOSE COMPLETE");
    }

    static string ScenePath(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (Transform cur = t; cur != null; cur = cur.parent) parts.Insert(0, cur.name);
        return string.Join("/", parts);
    }
}
