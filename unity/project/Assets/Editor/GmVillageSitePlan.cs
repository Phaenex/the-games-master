// Measures the BUILT village scene to decide where the estate goes, so M2 places the mansion, gate,
// drive and car from geometry rather than from arithmetic done in a chat window.
//
// The estate is authored as a straight north-south corridor in its own space (car z=+78, gate z=+65,
// mansion z=-58, cemetery east, garden west -- Assets/StreamingAssets/prologue-design.json). The
// village is a strip of buildings running at some other angle, somewhere else. Dropping the estate
// in at its native coordinates would put the mansion wherever z=-58 happens to land. So this tool
// finds three things:
//
//   1. the village SPINE -- principal axis through the building line, i.e. the road the village was
//      built along, which is what the drive has to become;
//   2. CLEARANCE along that spine, sampled outward from the road, so the mansion lands on ground
//      that is actually empty instead of inside a church;
//   3. the terrain height at each candidate, since the estate assumes flat ground at y=0 while the
//      village floor sits near y=-149 and rolls about 6m across the site.
//
// Reads only; writes one JSON report. Headless.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmVillageSitePlan
{
    const string LogTag = "GmVillageSitePlan";
    const float SampleStep = 6f;        // metres along the spine
    const float SampleFrom = -170f;     // behind the village (mansion end)
    const float SampleTo = 110f;        // ahead of the village (gate/car end)
    const float ObstacleMinHeight = 2.5f;   // ignore bushes; count buildings, walls and real trees

    static readonly Regex BuildingPattern =
        new Regex(@"House|Church|Barn|Shed|Hut|Cabin|Chapel|Tower|Mill", RegexOptions.IgnoreCase);
    static readonly Regex LodSuffix = new Regex(@"_LOD[1-9]$");

    public static string ReportPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Screens", "DemoScenes", "village-siteplan.json");

    [MenuItem("GamesMaster/Village/Site Plan (measure estate placement)")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(GmVillageBuilder.VillageScenePath, OpenSceneMode.Single);

        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) throw new InvalidOperationException("village scene has no Terrain");

        Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.gameObject.activeInHierarchy && r.GetComponent<Terrain>() == null)
            .Where(r => !LodSuffix.IsMatch(r.name))
            .ToArray();

        Renderer[] buildings = all.Where(r => BuildingPattern.IsMatch(r.name)).ToArray();
        if (buildings.Length < 2) throw new InvalidOperationException("not enough buildings to fit a spine");

        Vector2[] obstacles = all
            .Where(r => r.bounds.size.y >= ObstacleMinHeight)
            .Select(r => new Vector2(r.bounds.center.x, r.bounds.center.z))
            .ToArray();
        // Obstacle radius is approximated per-renderer from its own footprint so a 20m tree crown is
        // not treated as the same hazard as a 2m post.
        float[] obstacleRadii = all
            .Where(r => r.bounds.size.y >= ObstacleMinHeight)
            .Select(r => Mathf.Max(r.bounds.size.x, r.bounds.size.z) * 0.5f)
            .ToArray();

        FitSpine(buildings, out Vector2 centroid, out Vector2 axis);

        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"buildingCount\": {buildings.Length},\n");
        sb.Append($"  \"obstacleCount\": {obstacles.Length},\n");
        sb.Append($"  \"spineCentroid\": [{F(centroid.x)}, {F(centroid.y)}],\n");
        sb.Append($"  \"spineAxis\": [{F(axis.x)}, {F(axis.y)}],\n");
        // Yaw of the spine's "south" (mansion-ward) direction, in Unity Y-euler degrees.
        float southYaw = Mathf.Atan2(-axis.x, -axis.y) * Mathf.Rad2Deg;
        sb.Append($"  \"spineSouthYawDeg\": {F(southYaw)},\n");

        sb.Append("  \"samples\": [\n");
        var rows = new List<string>();
        for (float t = SampleFrom; t <= SampleTo; t += SampleStep)
        {
            Vector2 p = centroid + axis * t;
            float clear = Clearance(p, obstacles, obstacleRadii);
            float groundY = terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            rows.Add($"    {{\"t\": {F(t)}, \"x\": {F(p.x)}, \"z\": {F(p.y)}, " +
                     $"\"clearance\": {F(clear)}, \"groundY\": {F(groundY)}}}");
        }
        sb.Append(string.Join(",\n", rows));
        sb.Append("\n  ],\n");

        sb.Append("  \"buildings\": [\n");
        var brows = buildings.OrderBy(r => Project(new Vector2(r.bounds.center.x, r.bounds.center.z), centroid, axis))
            .Select(r => $"    {{\"name\": \"{r.name}\", \"t\": {F(Project(new Vector2(r.bounds.center.x, r.bounds.center.z), centroid, axis))}, " +
                         $"\"lateral\": {F(Lateral(new Vector2(r.bounds.center.x, r.bounds.center.z), centroid, axis))}, " +
                         $"\"center\": [{F(r.bounds.center.x)}, {F(r.bounds.center.y)}, {F(r.bounds.center.z)}], " +
                         $"\"size\": [{F(r.bounds.size.x)}, {F(r.bounds.size.y)}, {F(r.bounds.size.z)}]}}");
        sb.Append(string.Join(",\n", brows));
        sb.Append("\n  ],\n");
        sb.Append("  \"schema\": 1\n}\n");

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"[{LogTag}] PASS: spineCentroid=({F(centroid.x)},{F(centroid.y)}) " +
                  $"axis=({F(axis.x)},{F(axis.y)}) southYaw={F(southYaw)} -> {ReportPath}");
        EditorApplication.Exit(0);
    }

    /// Principal axis of the building line (2x2 covariance eigenvector). The village was dressed
    /// along its road, so the direction its buildings scatter least across IS the road.
    static void FitSpine(Renderer[] buildings, out Vector2 centroid, out Vector2 axis)
    {
        centroid = Vector2.zero;
        foreach (Renderer r in buildings) centroid += new Vector2(r.bounds.center.x, r.bounds.center.z);
        centroid /= buildings.Length;

        double cxx = 0, czz = 0, cxz = 0;
        foreach (Renderer r in buildings)
        {
            double dx = r.bounds.center.x - centroid.x;
            double dz = r.bounds.center.z - centroid.y;
            cxx += dx * dx; czz += dz * dz; cxz += dx * dz;
        }

        double theta = 0.5 * Math.Atan2(2 * cxz, cxx - czz);
        axis = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)).normalized;
        // Orient the axis so +t runs NORTH (increasing z), matching the estate's own convention
        // where the car sits at +z and the mansion at -z.
        if (axis.y < 0f) axis = -axis;
    }

    static float Project(Vector2 p, Vector2 centroid, Vector2 axis) => Vector2.Dot(p - centroid, axis);

    static float Lateral(Vector2 p, Vector2 centroid, Vector2 axis)
    {
        Vector2 right = new Vector2(axis.y, -axis.x);
        return Vector2.Dot(p - centroid, right);
    }

    /// Distance from p to the nearest obstacle's edge. Large values mean buildable ground.
    static float Clearance(Vector2 p, Vector2[] obstacles, float[] radii)
    {
        float best = float.MaxValue;
        for (int i = 0; i < obstacles.Length; i++)
        {
            float d = Vector2.Distance(p, obstacles[i]) - radii[i];
            if (d < best) best = d;
        }
        return best == float.MaxValue ? 999f : best;
    }

    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}

