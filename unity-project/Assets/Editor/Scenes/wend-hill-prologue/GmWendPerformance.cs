using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendPerformance
{
    public const float RenderCorridorMetres = 140f;

    public static int Apply(GmRouteSpline route)
    {
        if (route == null) throw new System.ArgumentNullException(nameof(route));
        List<Vector3> samples = new List<Vector3>();
        for (float metres = 0f; metres <= route.Length; metres += 15f) samples.Add(route.PointAt(metres));
        samples.Add(route.PointAt(route.Length));

        int disabled = 0;
        int laneCleared = 0;
        int terrainTreesCleared = ClearTerrainTrees(route);
        (int carvedColliders, int carvedRenderers) = CarveFalseRouteObstacles(route);
        int doorwayCollidersCleared = ClearFalseDoorwayColliders(route);
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (renderer.GetComponent<Terrain>() != null || renderer.GetComponentInParent<GmWorldAnchor>() != null ||
                renderer.transform.root.name == GmWendOpening.RootName ||
                renderer.transform.root.name == "WakeRoom" ||
                renderer.transform.root.name == GmHouseBeginningBuilder.RootName) continue;
            Bounds bounds = renderer.bounds;
            if (Mathf.Max(bounds.size.x, bounds.size.z) > 250f) continue;
            float nearest = float.MaxValue;
            int nearestSample = -1;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 point = samples[i];
                float distance = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z),
                    new Vector2(point.x, point.z));
                if (distance < nearest) { nearest = distance; nearestSample = i; }
            }

            // The purchased showcase placed decorative trees and ground cover directly through the
            // road. Besides blocking the controller, the first player frame was a wall of trunks and
            // grass with the authored car and gate hidden behind it. Clear only vegetation renderers
            // whose bounds intersect the walk lane; buildings, walls and evidence props remain.
            string hierarchy = HierarchyName(renderer.transform).ToLowerInvariant();
            bool isModularRuinOrDebris = new[] { "shack", "frame", "roof", "fence", "wall", "plank", "sm_wall",
                "sm_house", "sm_roof", "sm_wood_frame", "sm_wood_fence", "sm_wood_plank", "sm_wood_0" }
                .Any(token => hierarchy.Contains(token));
            bool isVegetation = new[] { "tree", "grass", "bush", "shrub", "plant", "fern", "weed",
                "reed", "flower", "ivy", "branch", "trunk", "foliage", "sapling" }
                .Any(token => hierarchy.Contains(token));

            float clearance = isModularRuinOrDebris ? 18.0f : (nearestSample >= 0 && nearestSample * 15f <= 30f ? 5.5f : 3.5f);
            if ((isModularRuinOrDebris || isVegetation) && Mathf.Max(bounds.size.x, bounds.size.z) <= 30f &&
                nearest <= clearance + Mathf.Max(bounds.extents.x, bounds.extents.z))
            {
                renderer.enabled = false;
                DisableIntersectingColliders(renderer);
                disabled++;
                laneCleared++;
                continue;
            }
            if (nearest <= RenderCorridorMetres + Mathf.Max(bounds.extents.x, bounds.extents.z))
            {
                renderer.allowOcclusionWhenDynamic = true;
                continue;
            }
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
            foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            disabled++;
        }

        Camera playerCamera = GameObject.Find(GmWendBuilder.PlayerName)?.GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            playerCamera.farClipPlane = 220f;
            playerCamera.allowDynamicResolution = true;
            EditorUtility.SetDirty(playerCamera);
            HDAdditionalCameraData hd = playerCamera.GetComponent<HDAdditionalCameraData>();
            if (hd != null)
            {
                hd.allowDynamicResolution = true;
                hd.customRenderingSettings = true;
                foreach (FrameSettingsField field in new[]
                {
                    FrameSettingsField.Decals,
                    FrameSettingsField.Refraction,
                    FrameSettingsField.Water,
                    FrameSettingsField.MotionVectors,
                    FrameSettingsField.ObjectMotionVectors,
                    FrameSettingsField.TransparentsWriteMotionVector,
                    FrameSettingsField.Distortion,
                    FrameSettingsField.DepthOfField,
                    FrameSettingsField.MotionBlur,
                    FrameSettingsField.TransparentPrepass,
                    FrameSettingsField.TransparentPostpass,
                    FrameSettingsField.CustomPostProcess,
                    FrameSettingsField.PaniniProjection,
                    FrameSettingsField.Bloom,
                    FrameSettingsField.LensFlareScreenSpace,
                    FrameSettingsField.LensFlareDataDriven,
                    FrameSettingsField.LensDistortion,
                    FrameSettingsField.ChromaticAberration,
                    FrameSettingsField.FilmGrain,
                    FrameSettingsField.Dithering,
                    FrameSettingsField.ShadowMaps,
                    FrameSettingsField.Shadowmask,
                    FrameSettingsField.ContactShadows,
                    FrameSettingsField.ScreenSpaceShadows,
                    FrameSettingsField.SSR,
                    FrameSettingsField.TransparentSSR,
                    FrameSettingsField.SSAO,
                    FrameSettingsField.SSGI,
                    FrameSettingsField.SubsurfaceScattering,
                    FrameSettingsField.VolumetricClouds,
                    FrameSettingsField.Volumetrics,
                    FrameSettingsField.PlanarProbe,
                    FrameSettingsField.ReflectionProbe,
                    FrameSettingsField.SkyReflection,
                    FrameSettingsField.LightLayers,
                })
                {
                    hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
                    hd.renderingPathCustomFrameSettings.SetEnabled(field, false);
                }
                EditorUtility.SetDirty(hd);
            }
        }
        Debug.Log($"[GmWendPerformance] disabled {disabled} renderer(s), including {laneCleared} " +
                  $"vegetation renderer(s), and removed {terrainTreesCleared} terrain vegetation " +
                  $"instance(s), {carvedColliders} false obstacle collider(s) / {carvedRenderers} " +
                  $"matching renderer(s), plus {doorwayCollidersCleared} doorway blocker(s) from the " +
                  $"walk lane; corridor=" +
                  $"{RenderCorridorMetres:0}m farClip={playerCamera?.farClipPlane:0}m");
        return disabled;
    }

    static (int colliders, int renderers) CarveFalseRouteObstacles(GmRouteSpline route)
    {
        string[] tokens = { "wall", "fence", "door", "wood", "barrel", "crate", "cart", "wagon",
            "bench", "table", "chair", "rock", "debris", "prop", "roof", "frame", "house", "building",
            "cabin", "barn", "shack", "plank", "beam", "ceiling", "post", "sm_" };
        var blockers = new List<Collider>();
        var hidden = new HashSet<Renderer>();
        foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!collider.enabled || collider.isTrigger || collider is TerrainCollider ||
                collider.transform.root.name == GmWendOpening.RootName ||
                collider.transform.root.name == GmWendBounds.RootName ||
                collider.transform.root.name == "WakeRoom" ||
                collider.transform.root.name == GmHouseBeginningBuilder.RootName)
                continue;
            string hierarchy = HierarchyName(collider.transform).ToLowerInvariant();
            if (!tokens.Any(hierarchy.Contains)) continue;
            if (!IntersectsRouteCapsule(collider.bounds, route)) continue;
            blockers.Add(collider);

            foreach (Renderer renderer in collider.GetComponentsInChildren<Renderer>(true)
                         .Concat(collider.GetComponentsInParent<Renderer>(true)))
            {
                if (renderer.transform.root.name != GmWendOpening.RootName &&
                    renderer.transform.root.name != GmWendBounds.RootName)
                    hidden.Add(renderer);
            }
        }
        foreach (Renderer renderer in hidden)
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
        foreach (Collider collider in blockers)
        {
            if (collider != null)
            {
                GmWendColliderProxy proxy = collider.GetComponent<GmWendColliderProxy>();
                if (proxy != null) Object.DestroyImmediate(proxy);
                Object.DestroyImmediate(collider);
            }
        }
        return (blockers.Count, hidden.Count);
    }

    public static bool IntersectsRouteCapsule(Bounds bounds, GmRouteSpline route)
    {
        Bounds padded = bounds;
        padded.Expand(new Vector3(1.8f, 0.4f, 1.8f));
        for (float metres = 0f; metres <= route.Length; metres += 1f)
        {
            Vector3 foot = route.PointAt(metres);
            bool horizontal = foot.x >= padded.min.x && foot.x <= padded.max.x &&
                              foot.z >= padded.min.z && foot.z <= padded.max.z;
            bool vertical = foot.y + 1.8f >= padded.min.y && foot.y + 0.05f <= padded.max.y;
            if (horizontal && vertical) return true;
        }
        Vector3 end = route.PointAt(route.Length);
        return end.x >= padded.min.x && end.x <= padded.max.x &&
               end.z >= padded.min.z && end.z <= padded.max.z &&
               end.y + 1.8f >= padded.min.y && end.y + 0.05f <= padded.max.y;
    }

    static int ClearFalseDoorwayColliders(GmRouteSpline route)
    {
        var blockers = new List<Collider>();
        foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
        {
            if (!collider.enabled || collider.isTrigger ||
                collider.name.IndexOf("Wall_Door", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            Bounds bounds = collider.bounds;
            float metres = route.ProjectDistance(bounds.center);
            Vector3 eye = route.PointAt(metres) + Vector3.up;
            Bounds padded = bounds;
            padded.Expand(new Vector3(0.8f, 0f, 0.8f));
            if (padded.Contains(eye)) blockers.Add(collider);
        }
        foreach (Collider collider in blockers) Object.DestroyImmediate(collider);
        return blockers.Count;
    }

    // REVERTED 2026-08-03: forcing mesh-only tree rendering (treeBillboardDistance past the estate)
    // was tried against a magenta defect that turned out to be the proof harness's own interaction
    // target, not Terrain at all. It did not fix anything and it cost real performance: route p95
    // 8.71ms -> 16.10ms, followed by a fatal walk-probe crash. Terrain billboards stay at engine
    // defaults. If billboards ever DO need authoring, price it against the route budget first.

    static int ClearTerrainTrees(GmRouteSpline route)
    {
        int cleared = 0;
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            TerrainData data = terrain.terrainData;
            if (data == null || data.treeInstanceCount == 0) continue;
            var kept = new List<TreeInstance>(data.treeInstanceCount);
            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 world = terrain.transform.position + Vector3.Scale(tree.position, data.size);
                float metres = route.ProjectDistance(world);
                Vector3 point = route.PointAt(metres);
                float nearest = Vector2.Distance(new Vector2(world.x, world.z),
                    new Vector2(point.x, point.z));
                float clearance = metres <= 30f ? 6.5f : 3.6f;
                if (nearest <= clearance) { cleared++; continue; }
                kept.Add(tree);
            }
            if (kept.Count == data.treeInstanceCount) continue;
            data.SetTreeInstances(kept.ToArray(), false);
            data.RefreshPrototypes();
            terrain.Flush();
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
        }
        return cleared;
    }

    static void DisableIntersectingColliders(Renderer renderer)
    {
        Bounds area = renderer.bounds;
        area.Expand(0.5f);
        foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Collider collider in renderer.GetComponentsInParent<Collider>(true))
            if (collider.enabled && collider.bounds.Intersects(area)) collider.enabled = false;
        LODGroup lod = renderer.GetComponentInParent<LODGroup>();
        if (lod != null)
            foreach (Collider collider in lod.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && collider.bounds.Intersects(area)) collider.enabled = false;
    }

    static string HierarchyName(Transform transform)
    {
        var names = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Add(current.name);
        return string.Join("/", names);
    }
}
