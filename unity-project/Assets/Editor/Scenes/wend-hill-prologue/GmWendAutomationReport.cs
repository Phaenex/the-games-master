// Machine-readable evidence for the canonical Wend Hill scene. The retired generated-scene report
// expects a GmSceneComposition manifest, while this hand-authored opening is governed by its saved
// scene contract. Keep the command name stable, but report the contract that actually ships.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmWendAutomationReport
{
    public const string ReportFileName = "wend-hill-audit.json";

    public static GmSceneAuditReport BuildReport(IReadOnlyList<string> failures, string fingerprint)
    {
        var report = new GmSceneAuditReport {
            sceneId = GmSceneCatalog.WendHillId,
            fingerprint = fingerprint,
        };
        foreach (string failure in failures)
            report.Add("saved-scene-contract", GmAuditSeverity.Error,
                GmSceneCatalog.WendHillId, failure);
        return report;
    }

    public static void WriteWendHillReport()
    {
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        GmSceneAuditReport report = BuildReport(
            GmWendSceneContract.Audit(), GmSceneFingerprint.Current());
        string directory = Path.Combine(GmSceneIntelligencePaths.LibraryRoot, "audits");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, ReportFileName);
        File.WriteAllText(file, JsonUtility.ToJson(report, true) + "\n");
        if (!report.Passed)
            throw new InvalidOperationException(
                $"Wend Hill report contains {report.findings.Count} blocking contract finding(s).");
        Debug.Log($"[GmSceneReport] WEND HILL PASS -> {file}");
    }

    /// Read-only visual-remediation evidence. Structural terrain checks can prove layers exist, but
    /// they cannot reveal that one layer owns nearly every route pixel or that a pale sandstone cliff
    /// is using an untinted daytime material beside the gate. This report names both with route-zone
    /// coverage so art changes are made against the scene that rendered, not against assumptions.
    public static void ReportEnvironmentSurfaces()
    {
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        GmRouteSpline route = UnityEngine.Object.FindAnyObjectByType<GmRouteSpline>();
        Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (route == null || terrain == null || terrain.terrainData == null)
            throw new InvalidOperationException("environment report needs the saved route and terrain");

        TerrainData data = terrain.terrainData;
        var output = new StringBuilder();
        output.AppendLine($"terrain='{terrain.name}' data='{AssetDatabase.GetAssetPath(data)}' " +
                          $"size={data.size} alphamap={data.alphamapWidth}x{data.alphamapHeight} " +
                          $"layers={data.alphamapLayers} trees={data.treeInstanceCount}");
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            TerrainLayer layer = data.terrainLayers[i];
            output.AppendLine($"  layer[{i}] '{layer?.name ?? "<null>"}' " +
                              $"asset='{AssetDatabase.GetAssetPath(layer)}' " +
                              $"diffuse='{AssetDatabase.GetAssetPath(layer?.diffuseTexture)}' " +
                              $"tile={(layer != null ? layer.tileSize.ToString("F2") : "n/a")}");
        }

        foreach (float lateral in new[] { -20f, -16f, -12f, -8f, 8f, 12f, 16f, 20f })
        {
            Vector3 point = route.PointAt(338f);
            Vector3 right = Vector3.Cross(Vector3.up, route.TangentAt(338f)).normalized;
            Vector3 candidate = point + right * lateral;
            float nearestMetres = route.ProjectDistance(candidate);
            Vector3 nearest = route.PointAt(nearestMetres);
            float clearance = Vector2.Distance(new Vector2(candidate.x, candidate.z),
                new Vector2(nearest.x, nearest.z));
            output.AppendLine($"  coach-candidate lateral={lateral:0}m position={candidate} " +
                              $"nearestRoute={nearestMetres:0}m clearance={clearance:0.0}m");
        }
        foreach (float lateral in new[] { -18f, -14f, -10f, -7f, 7f, 10f, 14f, 18f })
        {
            Vector3 point = route.PointAt(GmWendOpening.ChapelMetres);
            Vector3 right = Vector3.Cross(Vector3.up,
                route.TangentAt(GmWendOpening.ChapelMetres)).normalized;
            Vector3 candidate = point + right * lateral;
            float nearestMetres = route.ProjectDistance(candidate);
            Vector3 nearest = route.PointAt(nearestMetres);
            float clearance = Vector2.Distance(new Vector2(candidate.x, candidate.z),
                new Vector2(nearest.x, nearest.z));
            output.AppendLine($"  chapel-candidate lateral={lateral:0}m position={candidate} " +
                              $"nearestRoute={nearestMetres:0}m clearance={clearance:0.0}m");
        }
        foreach (float lateral in new[] { -24f, -20f, -16f, -12f, 10f, 12f, 14f, 16f, 18f })
        {
            Vector3 point = route.PointAt(282f);
            Vector3 forward = route.TangentAt(282f).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 candidate = point + right * lateral;
            float nearestMetres = route.ProjectDistance(candidate);
            Vector3 nearest = route.PointAt(nearestMetres);
            float clearance = Vector2.Distance(new Vector2(candidate.x, candidate.z),
                new Vector2(nearest.x, nearest.z));
            float returningClearance = float.MaxValue;
            float returningMetres = 0f;
            for (float metres = 310f; metres <= route.Length; metres += 0.5f)
            {
                Vector3 delta = route.PointAt(metres) - candidate;
                float localX = Mathf.Abs(Vector3.Dot(delta, right));
                float localZ = Mathf.Abs(Vector3.Dot(delta, forward));
                float rectangleDistance = new Vector2(Mathf.Max(0f, localX - 7f),
                    Mathf.Max(0f, localZ - 10f)).magnitude;
                if (rectangleDistance >= returningClearance) continue;
                returningClearance = rectangleDistance;
                returningMetres = metres;
            }
            output.AppendLine($"  garden-candidate lateral={lateral:0}m position={candidate} " +
                              $"nearestRoute={nearestMetres:0}m clearance={clearance:0.0}m " +
                              $"returningFootprintRoute={returningMetres:0.0}m " +
                              $"returningFootprintClearance={returningClearance:0.0}m");
        }

        float[] zoneStarts = { 0f, 60f, 120f, 180f, 240f, 300f, 360f, 420f };
        foreach (float start in zoneStarts)
        {
            float end = Mathf.Min(route.Length, start + 60f);
            if (end <= start) continue;
            var sums = new float[data.alphamapLayers];
            int samples = 0;
            for (float metres = start; metres <= end; metres += 5f)
            {
                Vector3 onRoute = route.PointAt(metres);
                Vector3 forward = route.TangentAt(metres);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                foreach (float lateral in new[] { -5f, -2.5f, 0f, 2.5f, 5f })
                {
                    Vector3 world = onRoute + right * lateral;
                    float nx = Mathf.InverseLerp(terrain.transform.position.x,
                        terrain.transform.position.x + data.size.x, world.x);
                    float nz = Mathf.InverseLerp(terrain.transform.position.z,
                        terrain.transform.position.z + data.size.z, world.z);
                    int x = Mathf.Clamp(Mathf.RoundToInt(nx * (data.alphamapWidth - 1)), 0,
                        data.alphamapWidth - 1);
                    int z = Mathf.Clamp(Mathf.RoundToInt(nz * (data.alphamapHeight - 1)), 0,
                        data.alphamapHeight - 1);
                    float[,,] alpha = data.GetAlphamaps(x, z, 1, 1);
                    for (int layer = 0; layer < sums.Length; layer++) sums[layer] += alpha[0, 0, layer];
                    samples++;
                }
            }
            output.AppendLine($"  zone {start:000}-{end:000}m samples={samples} coverage=[" +
                              string.Join(", ", sums.Select((sum, i) => $"{i}:{sum / samples:P1}")) + "]");
        }

        string[] surfaceTokens = { "cliff", "canyon", "sandstone", "rock", "boulder" };
        foreach (float metres in new[] { 330f, 345f, 360f, 375f, 390f, 405f, 420f, 435f })
        {
            Vector3 point = route.PointAt(metres);
            Vector3 tangent = route.TangentAt(metres);
            output.AppendLine($"  late-route {metres:000}m point={point} tangent={tangent}");
        }

        Renderer[] activeRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        foreach (Renderer renderer in activeRenderers
                     .Where(item => surfaceTokens.Any(token => item.name.Contains(token,
                         StringComparison.OrdinalIgnoreCase))))
        {
            float metres = route.ProjectDistance(renderer.bounds.center);
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z),
                new Vector2(onRoute.x, onRoute.z));
            if (lateral > 50f) continue;
            string materials = string.Join("; ", renderer.sharedMaterials.Where(material => material != null)
                .Select(material =>
                {
                    Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                        material.HasProperty("_BaseColor_Value") ? material.GetColor("_BaseColor_Value") : Color.clear;
                    Texture texture = material.HasProperty("_BaseColorMap") ? material.GetTexture("_BaseColorMap") :
                        material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    return $"{material.name}|shader={material.shader?.name}|asset={AssetDatabase.GetAssetPath(material)}|" +
                           $"color={color}|base={AssetDatabase.GetAssetPath(texture)}";
                }));
            output.AppendLine($"  surface '{renderer.name}' path='{HierarchyPath(renderer.transform)}' " +
                              $"route={metres:0}m lateral={lateral:0.0}m bounds={renderer.bounds.size} " +
                              $"materials=[{materials}]");
        }

        string[] groundTokens = { "road", "path", "street", "track", "ground", "terrain", "mud",
            "grass", "moss", "field", "soil" };
        var groundUsage = new Dictionary<Material, List<(Renderer renderer, float metres, float lateral)>>();
        foreach (Renderer renderer in activeRenderers.Where(item => groundTokens.Any(token =>
                     item.name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                     item.sharedMaterials.Any(material => material != null &&
                         material.name.Contains(token, StringComparison.OrdinalIgnoreCase)))))
        {
            float metres = route.ProjectDistance(renderer.bounds.center);
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z),
                new Vector2(onRoute.x, onRoute.z));
            if (lateral > 35f) continue;
            foreach (Material material in renderer.sharedMaterials.Where(material => material != null))
            {
                if (!groundUsage.TryGetValue(material, out var uses))
                    groundUsage[material] = uses = new List<(Renderer, float, float)>();
                uses.Add((renderer, metres, lateral));
            }
        }
        foreach ((Material material, List<(Renderer renderer, float metres, float lateral)> uses) in groundUsage
                     .OrderByDescending(pair => pair.Value.Count))
        {
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                material.HasProperty("_BaseColor_Value") ? material.GetColor("_BaseColor_Value") :
                material.HasProperty("_Albedo_Tint") ? material.GetColor("_Albedo_Tint") : Color.clear;
            Texture texture = material.HasProperty("_BaseColorMap") ? material.GetTexture("_BaseColorMap") :
                material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            output.AppendLine($"  ground-material '{material.name}' uses={uses.Count} " +
                              $"route={uses.Min(use => use.metres):0}-{uses.Max(use => use.metres):0}m " +
                              $"nearest={uses.Min(use => use.lateral):0.0}m shader='{material.shader?.name}' " +
                              $"asset='{AssetDatabase.GetAssetPath(material)}' color={color} " +
                              $"base='{AssetDatabase.GetAssetPath(texture)}' examples=[" +
                              string.Join(", ", uses.Select(use => use.renderer.name).Distinct().Take(5)) + "]");
        }

        // Renderer counts hide the actual offender because one grass clump owns four LOD renderers.
        // Group the 130-180m trouble zone by placed scene root so an opaque slab can be traced to the
        // specific authored instance instead of guessed at from a material total.
        foreach (IGrouping<Transform, Renderer> group in activeRenderers
                     .Where(renderer => renderer.name.Contains("Grass", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(renderer => PlacedRoot(renderer.transform)))
        {
            Renderer[] renderers = group.ToArray();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float metres = route.ProjectDistance(bounds.center);
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z),
                new Vector2(onRoute.x, onRoute.z));
            if (metres < 130f || metres > 180f || lateral > 35f) continue;
            output.AppendLine($"  grass-root '{group.Key.name}' path='{HierarchyPath(group.Key)}' " +
                              $"route={metres:0}m lateral={lateral:0.0}m bounds={bounds.size} " +
                              $"renderers={renderers.Length}");
        }

        foreach (IGrouping<Transform, Renderer> group in activeRenderers
                     .Where(renderer => renderer.GetComponent<Terrain>() == null)
                     .GroupBy(renderer => PlacedRoot(renderer.transform)))
        {
            Renderer[] renderers = group.ToArray();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float metres = route.ProjectDistance(bounds.center);
            if (metres < 70f || metres > 450f) continue;
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z),
                new Vector2(onRoute.x, onRoute.z));
            if (lateral > 30f) continue;
            string materials = string.Join(",", renderers.SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Select(material => material.name).Distinct());
            output.AppendLine($"  route-root '{group.Key.name}' path='{HierarchyPath(group.Key)}' " +
                              $"route={metres:0}m lateral={lateral:0.0}m center={bounds.center} " +
                              $"bounds={bounds.size} renderers={renderers.Length} " +
                              $"visible={renderers.Count(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)} " +
                              $"mats=[{materials}]");
        }

        // Night screenshots can show a pool of light without enough context to identify its owner.
        // Report every practical by route position and its nearest visible renderer so a missing or
        // badly separated fixture is an object-level defect, not a visual guess.
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None).Where(item => item.type != LightType.Directional))
        {
            float metres = route.ProjectDistance(light.transform.position);
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(light.transform.position.x, light.transform.position.z),
                new Vector2(onRoute.x, onRoute.z));
            Renderer nearest = activeRenderers.OrderBy(renderer =>
                Vector3.Distance(renderer.bounds.ClosestPoint(light.transform.position), light.transform.position))
                .FirstOrDefault();
            float nearestDistance = nearest != null
                ? Vector3.Distance(nearest.bounds.ClosestPoint(light.transform.position), light.transform.position)
                : float.PositiveInfinity;
            output.AppendLine($"  practical-light '{light.name}' path='{HierarchyPath(light.transform)}' " +
                              $"route={metres:0}m lateral={lateral:0.0}m pos={light.transform.position} " +
                              $"enabled={light.enabled && light.gameObject.activeInHierarchy} " +
                              $"type={light.type} range={light.range:0.0} intensity={light.intensity:0.0} " +
                              $"nearestRenderer='{nearest?.name ?? "<none>"}' " +
                              $"nearestDistance={nearestDistance:0.0}m");
        }

        string[] fragmentTokens = { "door", "window", "chimney", "chimeny", "wall", "roof" };
        foreach (Renderer renderer in activeRenderers.Where(item => fragmentTokens.Any(token =>
                     item.name.Contains(token, StringComparison.OrdinalIgnoreCase))))
        {
            float metres = route.ProjectDistance(renderer.bounds.center);
            if (metres > 200f) continue;
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z),
                new Vector2(onRoute.x, onRoute.z));
            if (lateral > 15f) continue;
            output.AppendLine($"  architecture-fragment '{renderer.name}' " +
                              $"path='{HierarchyPath(renderer.transform)}' route={metres:0}m " +
                              $"lateral={lateral:0.0}m center={renderer.bounds.center} " +
                              $"bounds={renderer.bounds.size}");
        }

        GameObject routeSurface =
            GameObject.Find($"{GmWendOpening.RootName}/{GmWendOpening.WalkDeckName}");
        MeshFilter routeFilter = routeSurface != null ? routeSurface.GetComponent<MeshFilter>() : null;
        MeshRenderer routeRenderer = routeSurface != null ? routeSurface.GetComponent<MeshRenderer>() : null;
        if (routeFilter != null && routeFilter.sharedMesh != null && routeRenderer != null)
        {
            Mesh mesh = routeFilter.sharedMesh;
            Vector3 normalMean = mesh.normals.Length == 0 ? Vector3.zero :
                mesh.normals.Aggregate(Vector3.zero, (sum, normal) => sum + normal) / mesh.normals.Length;
            Material material = routeRenderer.sharedMaterial;
            output.AppendLine($"  route-surface mesh='{mesh.name}' vertices={mesh.vertexCount} " +
                              $"bounds={routeRenderer.bounds.size} normalMean={normalMean} " +
                              $"shader='{material?.shader?.name}' renderQueue={material?.renderQueue} " +
                              $"baseColor={(material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").ToString() : "n/a")} " +
                              $"smoothness={(material != null && material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness").ToString("0.00") : "n/a")} " +
                              $"keywords=[{string.Join(",", material?.shaderKeywords ?? Array.Empty<string>())}]");
            foreach (float metres in new[] { 0f, 45f, 75f, 120f, 150f })
            {
                Vector3 point = route.PointAt(metres);
                float terrainY = terrain.SampleHeight(point) + terrain.transform.position.y;
                float surfaceY = GmWendOpening.RoadSurfaceY(point.y, terrainY);
                output.AppendLine($"    route-height {metres:000}m centerY={point.y:0.000} " +
                                  $"terrainY={terrainY:0.000} " +
                                  $"surfaceClearance={surfaceY - terrainY:0.000}");
            }
        }

        Debug.Log($"[GmWendEnvironment] SURFACES\n{output}");
        Debug.Log("[GmWendEnvironment] PASS: terrain coverage and nearby cliff materials reported");
        EditorApplication.Exit(0);
    }

    static string HierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        for (Transform current = transform; current != null; current = current.parent) names.Push(current.name);
        return string.Join("/", names);
    }

    static Transform PlacedRoot(Transform transform)
    {
        Transform current = transform;
        while (current.parent != null && current.parent.name != "Foilage") current = current.parent;
        if (current.parent != null && current.parent.name == "Foilage") return current;
        current = transform;
        while (current.parent != null && current.parent.name != "Prefabs" &&
               current.parent.name != GmWendOpening.RootName)
            current = current.parent;
        return current;
    }
}
