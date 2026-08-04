// Survey of the PURCHASED Haunted Village source scene, written because M1's first spawn was placed
// from Terrain-bounds centre on the untested assumption that the dressed village fills its terrain.
// It does not: village-elevated-overview.png shows a small dressed cluster on a large terrain, and
// the player landed in the empty field beside it (village-spawn-preview.png -- bottom half of frame
// is bare ground). This tool replaces that assumption with measurements.
//
// What it harvests, in one headless pass (no rendering needed, so no window required):
//   * the 21 showcase camera transforms -- these are the ASSET AUTHOR'S OWN chosen vantage points,
//     the framings used to sell the pack, so they mark where composed content actually is;
//   * a renderer-density grid over the XZ plane, so dense ground can be found numerically rather
//     than guessed at;
//   * every light in the source scene -- a night village with no practical lights is just
//     silhouettes; warm pools from windows/lanterns are what make night read as inhabited, so we
//     need to know what the pack ships before authoring the night recipe;
//   * the prop vocabulary (distinct mesh names + instance counts) to inform M3 zone mapping.
//
// Reads only. Writes one JSON report; never modifies the purchased scene.
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

public static class GmVillageSurvey
{
    const string LogTag = "GmVillageSurvey";
    const float CellSize = 8f;          // metres; a village block reads at roughly this scale
    const int TopHotspots = 40;
    const int TopPropNames = 60;

    public static string ReportPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Screens", "DemoScenes", "village-survey.json");

    [MenuItem("GamesMaster/Village/Survey Source Scene")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(GmVillageBuilder.SourceScenePath, OpenSceneMode.Single);
        var sb = new StringBuilder();
        sb.Append("{\n");

        AppendTerrain(sb);
        AppendContentBounds(sb, out Bounds content, out Renderer[] renderers);
        AppendCameras(sb);
        AppendLights(sb);
        AppendDensity(sb, renderers, content);
        AppendPropVocabulary(sb, renderers);
        AppendStructures(sb, renderers);
        AppendBuildings(sb, renderers);
        AppendMeshesHierarchy(sb);
        AppendRoots(sb);

        sb.Append("  \"schema\": 1\n}\n");

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"[{LogTag}] PASS: wrote {ReportPath} bytes={new FileInfo(ReportPath).Length}");
        EditorApplication.Exit(0);
    }

    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string V3(Vector3 v) => $"[{F(v.x)}, {F(v.y)}, {F(v.z)}]";

    static void AppendTerrain(StringBuilder sb)
    {
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            sb.Append("  \"terrain\": null,\n");
            return;
        }
        Vector3 min = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        sb.Append("  \"terrain\": {");
        sb.Append($"\"min\": {V3(min)}, \"size\": {V3(size)}, \"max\": {V3(min + size)}, ");
        sb.Append($"\"treeInstances\": {terrain.terrainData.treeInstanceCount}, ");
        sb.Append($"\"treePrototypes\": {terrain.terrainData.treePrototypes.Length}, ");
        sb.Append($"\"detailPrototypes\": {terrain.terrainData.detailPrototypes.Length}");
        sb.Append("},\n");
    }

    // Terrain is excluded deliberately: it spans the whole map by definition, so including it would
    // drag the "content" bounds back out to the terrain bounds and reproduce the original mistake.
    static void AppendContentBounds(StringBuilder sb, out Bounds content, out Renderer[] renderers)
    {
        renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy)
            .Where(r => r.GetComponent<Terrain>() == null && !(r is ParticleSystemRenderer))
            .ToArray();

        if (renderers.Length == 0)
        {
            content = new Bounds(Vector3.zero, Vector3.zero);
            sb.Append("  \"content\": null,\n");
            return;
        }

        content = renderers[0].bounds;
        foreach (Renderer r in renderers) content.Encapsulate(r.bounds);
        sb.Append("  \"content\": {");
        sb.Append($"\"rendererCount\": {renderers.Length}, ");
        sb.Append($"\"center\": {V3(content.center)}, \"size\": {V3(content.size)}, ");
        sb.Append($"\"min\": {V3(content.min)}, \"max\": {V3(content.max)}");
        sb.Append("},\n");
    }

    // The showcase rig is the single most valuable thing in the source scene for our purposes: each
    // camera is a human art director's answer to "where does this environment look composed?".
    static void AppendCameras(StringBuilder sb)
    {
        // Inactive INCLUDED on purpose: the showcase rig ships with 20 of its 21 cameras disabled,
        // and a disabled camera is still a human-chosen framing of this environment.
        Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        sb.Append("  \"showcaseCameras\": [\n");
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            Transform t = c.transform;
            sb.Append("    {");
            sb.Append($"\"name\": \"{Escape(c.name)}\", ");
            sb.Append($"\"path\": \"{Escape(PathOf(t))}\", ");
            sb.Append($"\"pos\": {V3(t.position)}, ");
            sb.Append($"\"euler\": {V3(t.rotation.eulerAngles)}, ");
            sb.Append($"\"fov\": {F(c.fieldOfView)}, ");
            sb.Append($"\"active\": {(c.isActiveAndEnabled ? "true" : "false")}");
            sb.Append(i == cams.Length - 1 ? "}\n" : "},\n");
        }
        sb.Append("  ],\n");
    }

    static void AppendLights(StringBuilder sb)
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        sb.Append("  \"lights\": [\n");
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            sb.Append("    {");
            sb.Append($"\"name\": \"{Escape(l.name)}\", ");
            sb.Append($"\"type\": \"{l.type}\", ");
            sb.Append($"\"pos\": {V3(l.transform.position)}, ");
            sb.Append($"\"euler\": {V3(l.transform.rotation.eulerAngles)}, ");
            sb.Append($"\"intensity\": {F(l.intensity)}, ");
            sb.Append($"\"range\": {F(l.range)}, ");
            sb.Append($"\"color\": [{F(l.color.r)}, {F(l.color.g)}, {F(l.color.b)}], ");
            sb.Append($"\"activeInHierarchy\": {(l.gameObject.activeInHierarchy ? "true" : "false")}, ");
            sb.Append($"\"enabled\": {(l.enabled ? "true" : "false")}");
            sb.Append(i == lights.Length - 1 ? "}\n" : "},\n");
        }
        sb.Append("  ],\n");
    }

    // Bin renderer centres into XZ cells and report the densest. "Dense" here means "lots of
    // authored objects near this ground position", which is the closest numeric proxy we have for
    // "standing here, the player sees a composed scene rather than an empty field".
    static void AppendDensity(StringBuilder sb, Renderer[] renderers, Bounds content)
    {
        var counts = new Dictionary<(int, int), int>();
        foreach (Renderer r in renderers)
        {
            Vector3 c = r.bounds.center;
            var key = (Mathf.FloorToInt(c.x / CellSize), Mathf.FloorToInt(c.z / CellSize));
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }

        var ranked = counts.OrderByDescending(kv => kv.Value).Take(TopHotspots).ToArray();
        sb.Append($"  \"densityCellSize\": {F(CellSize)},\n");
        sb.Append($"  \"occupiedCells\": {counts.Count},\n");
        sb.Append("  \"hotspots\": [\n");
        for (int i = 0; i < ranked.Length; i++)
        {
            var (cx, cz) = ranked[i].Key;
            float wx = (cx + 0.5f) * CellSize;
            float wz = (cz + 0.5f) * CellSize;
            sb.Append($"    {{\"x\": {F(wx)}, \"z\": {F(wz)}, \"count\": {ranked[i].Value}}}");
            sb.Append(i == ranked.Length - 1 ? "\n" : ",\n");
        }
        sb.Append("  ],\n");

        // Centroid of authored content, weighted by renderer count -- a far better "middle of the
        // village" than the terrain centre, because it follows where objects actually are.
        double sx = 0, sz = 0; int total = 0;
        foreach (var kv in counts)
        {
            sx += (kv.Key.Item1 + 0.5) * CellSize * kv.Value;
            sz += (kv.Key.Item2 + 0.5) * CellSize * kv.Value;
            total += kv.Value;
        }
        if (total > 0)
            sb.Append($"  \"contentCentroid\": [{F((float)(sx / total))}, {F((float)(sz / total))}],\n");
    }

    static void AppendPropVocabulary(StringBuilder sb, Renderer[] renderers)
    {
        var byName = new Dictionary<string, int>();
        foreach (Renderer r in renderers)
        {
            string n = r.name;
            byName.TryGetValue(n, out int c);
            byName[n] = c + 1;
        }
        var ranked = byName.OrderByDescending(kv => kv.Value).Take(TopPropNames).ToArray();
        sb.Append($"  \"distinctPropNames\": {byName.Count},\n");
        sb.Append("  \"propVocabulary\": [\n");
        for (int i = 0; i < ranked.Length; i++)
        {
            sb.Append($"    {{\"name\": \"{Escape(ranked[i].Key)}\", \"count\": {ranked[i].Value}}}");
            sb.Append(i == ranked.Length - 1 ? "\n" : ",\n");
        }
        sb.Append("  ],\n");
    }

    // Practical lights have to go somewhere specific -- inside buildings, under eaves -- so the
    // night recipe needs real structure positions, not a guess. LOD siblings are collapsed (a house
    // ships LOD0..LOD4 stacked at one position) so each building is reported once.
    static void AppendStructures(StringBuilder sb, Renderer[] renderers)
    {
        var structures = renderers
            .Where(r => !Regex.IsMatch(r.name, @"_LOD[1-9]$"))
            .Where(r => r.bounds.size.y >= 3f)
            .OrderByDescending(r => r.bounds.size.x * r.bounds.size.y * r.bounds.size.z)
            .Take(60)
            .ToArray();

        sb.Append($"  \"structureCount\": {structures.Length},\n");
        sb.Append("  \"structures\": [\n");
        for (int i = 0; i < structures.Length; i++)
        {
            Renderer r = structures[i];
            sb.Append("    {");
            sb.Append($"\"name\": \"{Escape(r.name)}\", ");
            sb.Append($"\"center\": {V3(r.bounds.center)}, ");
            sb.Append($"\"size\": {V3(r.bounds.size)}, ");
            sb.Append($"\"groundY\": {F(r.bounds.min.y)}");
            sb.Append(i == structures.Length - 1 ? "}\n" : "},\n");
        }
        sb.Append("  ],\n");
    }

    // Every habitable structure by name, regardless of size rank -- these are the anchors for
    // practical lights, and the top-60-by-volume list is dominated by trees.
    static void AppendBuildings(StringBuilder sb, Renderer[] renderers)
    {
        var buildings = renderers
            .Where(r => !Regex.IsMatch(r.name, @"_LOD[1-9]$"))
            .Where(r => Regex.IsMatch(
                r.name, @"House|Church|Barn|Shed|Hut|Cabin|Chapel|Tower|Mill", RegexOptions.IgnoreCase))
            .OrderBy(r => r.bounds.center.z)
            .ToArray();

        sb.Append($"  \"buildingCount\": {buildings.Length},\n");
        sb.Append("  \"buildings\": [\n");
        for (int i = 0; i < buildings.Length; i++)
        {
            Renderer r = buildings[i];
            sb.Append("    {");
            sb.Append($"\"name\": \"{Escape(r.name)}\", ");
            sb.Append($"\"center\": {V3(r.bounds.center)}, ");
            sb.Append($"\"size\": {V3(r.bounds.size)}, ");
            sb.Append($"\"groundY\": {F(r.bounds.min.y)}");
            sb.Append(i == buildings.Length - 1 ? "}\n" : "},\n");
        }
        sb.Append("  ],\n");
    }

    // The density grid found 243 renderers in one cell at world origin while the village floor sits
    // at y = -148. Dumping the Meshes children identifies whether that is an asset-display stash
    // (which would float in the sky over gameplay and must be removed) or legitimate distant scenery.
    static void AppendMeshesHierarchy(StringBuilder sb)
    {
        GameObject meshes = GameObject.Find("Meshes");
        sb.Append("  \"meshesChildren\": [\n");
        if (meshes != null)
        {
            var kids = new List<Transform>();
            foreach (Transform child in meshes.transform) kids.Add(child);
            for (int i = 0; i < kids.Count; i++)
            {
                Transform c = kids[i];
                Renderer[] rs = c.GetComponentsInChildren<Renderer>(true);
                Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(c.position, Vector3.zero);
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                sb.Append("    {");
                sb.Append($"\"name\": \"{Escape(c.name)}\", ");
                sb.Append($"\"renderers\": {rs.Length}, ");
                sb.Append($"\"pos\": {V3(c.position)}, ");
                sb.Append($"\"boundsCenter\": {V3(b.center)}, ");
                sb.Append($"\"boundsSize\": {V3(b.size)}");
                sb.Append(i == kids.Count - 1 ? "}\n" : "},\n");
            }
        }
        sb.Append("  ],\n");
    }

    static void AppendRoots(StringBuilder sb)
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        sb.Append("  \"roots\": [\n");
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject g = roots[i];
            Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
            sb.Append("    {");
            sb.Append($"\"name\": \"{Escape(g.name)}\", ");
            sb.Append($"\"active\": {(g.activeSelf ? "true" : "false")}, ");
            sb.Append($"\"renderers\": {rs.Length}, ");
            sb.Append($"\"pos\": {V3(g.transform.position)}");
            sb.Append(i == roots.Length - 1 ? "}\n" : "},\n");
        }
        sb.Append("  ],\n");
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (Transform c = t; c != null; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

