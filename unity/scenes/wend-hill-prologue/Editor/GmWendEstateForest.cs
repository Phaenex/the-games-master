using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Authors dense towering forest tree canopies along the 435m estate avenue, understory vegetation,
/// weathered cemetery crosses, fallen timber scatter, yard clutter, and period-authentic Victorian flickering lamp posts.
/// </summary>
public static class GmWendEstateForest
{
    public const string RootName = "EstateForest";

    static readonly string[] TreePrefabPaths =
    {
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Terrain/SM_Tree_01_Foliage.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Terrain/SM_Tree_02 Foilage.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Terrain/SM_Tree_03 foilage.prefab",
    };

    static readonly string[] BushPrefabPaths =
    {
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_DeadBush_01.prefab",
    };

    static readonly string[] TimberPrefabPaths =
    {
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Wood_02.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Wood_03.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Wood_04.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Wood_05.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Wood_06.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Wood_07.prefab",
    };

    static readonly string[] GraveCrossPrefabPaths =
    {
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_GraveCross_01.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_GraveCross_02.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_GraveCross_03.prefab",
    };
    internal const string CemeteryMonumentPrefabPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Church_C_Cross.prefab";
    internal static Vector3 CemeteryMonumentSize => new Vector3(0f, 2.6f, 0f);

    static readonly (float metres, float lateral, float scale)[] CemeteryDressing =
    {
        (138f, 10.5f, 1.06f),
        (146f, 12.5f, 0.92f),
        (158f, 10.8f, 1.00f),
        (170f, 13.0f, 0.86f),
        (182f, 10.2f, 1.10f),
        (194f, 12.4f, 0.94f),
        (206f, 9.8f, 1.02f),
        (166f, -6.5f, 1.00f),
        (178f, -9.0f, 0.90f),
        (190f, -6.8f, 1.08f),
        (202f, -9.2f, 0.94f),
        (212f, -7.2f, 1.00f),
    };

    internal static IReadOnlyList<(float metres, float lateral, float scale)> CemeteryDressingSlots =>
        CemeteryDressing;

    static readonly string[] ClutterPrefabPaths =
    {
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Barrel_01.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Crate_01.prefab",
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Bucket.prefab",
    };

    const string LightPolePrefabPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_LightPole.prefab";
    const string LanternPrefabPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab";

    public static int Apply(Transform openingRoot, GmRouteSpline route)
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) Object.DestroyImmediate(stale);

        var forestRoot = new GameObject(RootName).transform;
        forestRoot.SetParent(openingRoot, true);

        Random.State incomingRandomState = Random.state;
        Random.InitState(0x5EED2026);

        List<GameObject> treePrefabs = LoadPrefabs(TreePrefabPaths);
        List<GameObject> bushPrefabs = LoadPrefabs(BushPrefabPaths);
        List<GameObject> timberPrefabs = LoadPrefabs(TimberPrefabPaths);
        List<GameObject> crossPrefabs = LoadPrefabs(GraveCrossPrefabPaths);
        List<GameObject> clutterPrefabs = LoadPrefabs(ClutterPrefabPaths);

        GameObject lightPolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightPolePrefabPath);
        GameObject lanternPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefabPath);

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        int treesPlaced = 0;
        int undergrowthPlaced = 0;
        int timberPlaced = 0;
        int propsPlaced = 0;
        int lampsPlaced = 0;

        float routeLength = route.Length;

        // 1. Avenue Tree Framing (both flanks every 10m with double layers)
        for (float m = 4f; m <= routeLength - 8f; m += 10f)
        {
            Vector3 center = route.PointAt(m);
            Vector3 tangent = route.TangentAt(m);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            // Left side inner & outer tree
            treesPlaced += PlaceTree(treePrefabs, forestRoot, terrain, center - right * 5.8f, 1.15f, route);
            treesPlaced += PlaceTree(treePrefabs, forestRoot, terrain, center - right * 10.5f, 1.35f, route);

            // Right side inner & outer tree
            treesPlaced += PlaceTree(treePrefabs, forestRoot, terrain, center + right * 5.8f, 1.20f, route);
            treesPlaced += PlaceTree(treePrefabs, forestRoot, terrain, center + right * 11.0f, 1.40f, route);

            // Undergrowth shrubs along tree lines
            undergrowthPlaced += PlaceScenicProp(bushPrefabs, forestRoot, terrain, center - right * 4.9f, 0.9f, 1.3f, route, 4.0f);
            undergrowthPlaced += PlaceScenicProp(bushPrefabs, forestRoot, terrain, center + right * 4.9f, 0.9f, 1.3f, route, 4.0f);
        }

        // 2. Fallen Timber & Log Clutter along Road Borders (every 16m)
        for (float m = 8f; m <= routeLength - 12f; m += 16f)
        {
            Vector3 center = route.PointAt(m);
            Vector3 tangent = route.TangentAt(m);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float side = (m % 32f == 0) ? -1f : 1f;

            timberPlaced += PlaceScenicProp(timberPrefabs, forestRoot, terrain, center + right * (4.7f * side), 0.8f, 1.2f, route, 4.2f);
        }

        // 3. Cemetery Branch Dressing. The named graves sit on the positive-lateral side before
        // the path turns toward the negative-lateral chapel, so the anonymous field supports both
        // parts of that read instead of forming an unrelated row across the avenue.
        foreach ((float metres, float lateral, float scale) slot in CemeteryDressing)
        {
            Vector3 center = route.PointAt(slot.metres);
            Vector3 tangent = route.TangentAt(slot.metres);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            propsPlaced += PlaceScenicProp(crossPrefabs, forestRoot, terrain,
                center + right * slot.lateral, slot.scale * 0.94f, slot.scale * 1.06f, route, 4.5f);
        }
        propsPlaced += PlaceCemeteryMonument(forestRoot, terrain, route);

        // 4. Garden & Outbuilding Courtyard Dressing (245m to 310m)
        for (float m = 250f; m <= 305f; m += 14f)
        {
            Vector3 center = route.PointAt(m);
            Vector3 tangent = route.TangentAt(m);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            propsPlaced += PlaceScenicProp(clutterPrefabs, forestRoot, terrain, center + right * 5.5f, 0.9f, 1.1f, route, 4.2f);
        }

        // 5. Period Victorian Lamp Posts with organic flickering
        float[] lampDistances = { 18f, 55f, 95f, 138f, 185f, 230f, 280f, 325f, 375f, 415f };
        for (int i = 0; i < lampDistances.Length; i++)
        {
            float m = lampDistances[i];
            if (m > routeLength - 5f) continue;

            Vector3 center = route.PointAt(m);
            Vector3 tangent = route.TangentAt(m);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float side = (i % 2 == 0) ? -1f : 1f;

            Vector3 postPos = center + right * (3.6f * side);
            lampsPlaced += PlaceLampPost(lightPolePrefab, lanternPrefab, forestRoot, terrain, postPos, tangent, side, i);
        }

        // 6. Ground Mist Sheets (LocalVolumetricFog clinging to cemetery soil & gardens)
        int mistPlaced = PlaceGroundMistVolumes(forestRoot, route, terrain);

        Debug.Log($"[GmWendEstateForest] PASS: placed {treesPlaced} trees, {undergrowthPlaced} shrubs, {timberPlaced} fallen logs, {propsPlaced} cemetery/yard props, {lampsPlaced} Victorian flickering lamp posts, and {mistPlaced} ground mist volumes");
        Random.state = incomingRandomState;
        return treesPlaced + undergrowthPlaced + timberPlaced + propsPlaced + lampsPlaced + mistPlaced;
    }

    static int PlaceGroundMistVolumes(Transform parent, GmRouteSpline route, Terrain terrain)
    {
        float[] mistDistances = { 175f, 198f, 265f, 310f };
        int count = 0;

        for (int i = 0; i < mistDistances.Length; i++)
        {
            float m = mistDistances[i];
            if (m >= route.Length) continue;

            Vector3 pos = route.PointAt(m);
            float groundY = terrain != null ? terrain.SampleHeight(pos) + terrain.transform.position.y : pos.y;

            var mistGo = new GameObject($"GroundMist_{i:D2}");
            mistGo.transform.SetParent(parent, false);
            mistGo.transform.position = new Vector3(pos.x, groundY + 0.8f, pos.z);

            var fog = mistGo.AddComponent<LocalVolumetricFog>();
            fog.parameters.albedo = new Color(0.12f, 0.14f, 0.18f);
            fog.parameters.meanFreePath = 14f;
            fog.parameters.size = new Vector3(32f, 2.2f, 32f);
            fog.parameters.positiveFade = new Vector3(0.45f, 0.85f, 0.45f);
            fog.parameters.negativeFade = new Vector3(0.45f, 0.35f, 0.45f);
            count++;
        }
        return count;
    }

    static int PlaceCemeteryMonument(Transform parent, Terrain terrain, GmRouteSpline route)
    {
        const float Metres = 184f;
        const float Lateral = 14.2f;
        Vector3 center = route.PointAt(Metres);
        Vector3 tangent = route.TangentAt(Metres);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Vector3 target = center + right * Lateral;
        float groundY = terrain != null
            ? terrain.SampleHeight(target) + terrain.transform.position.y
            : target.y;
        GameObject monument = GmOwnedPropFactory.PlacePrefab(
            CemeteryMonumentPrefabPath, "CemeteryMonument", parent,
            new Vector3(target.x, groundY + CemeteryMonumentSize.y * 0.5f, target.z),
            CemeteryMonumentSize, Quaternion.LookRotation(-right, Vector3.up), true, groundY);
        Bounds bounds = GmPropPlacementEngine.EncapsulateBounds(monument);
        if (bounds.size.y < 2.55f || bounds.size.y > 2.65f)
            throw new System.InvalidOperationException(
                $"cemetery monument measured {bounds.size}, expected 2.6m high after placement");
        Debug.Log($"[GmWendEstateForest] cemetery monument bounds={bounds.size} center={bounds.center}");
        return 1;
    }

    static List<GameObject> LoadPrefabs(string[] paths)
    {
        var list = new List<GameObject>();
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
            else Debug.LogWarning($"[GmWendEstateForest] ASSET MISSING: {path} — scatter pool short one entry, no placeholder substituted");
        }
        return list;
    }

    static int PlaceTree(List<GameObject> prefabs, Transform parent, Terrain terrain, Vector3 targetPos, float baseScale, GmRouteSpline route)
    {
        if (prefabs.Count == 0) return 0;
        if (!IsOutsideManorApproach(targetPos, route)) return 0;
        GameObject coachHouse = GameObject.Find("CoachHouse");
        if (coachHouse != null && !GmWendOutbuildings.IsOutsideCoachHouseFootprint(
                targetPos, coachHouse.transform, 1.5f)) return 0;

        for (float sm = 0; sm <= route.Length; sm += 4f)
        {
            Vector3 pt = route.PointAt(sm);
            if (Vector2.Distance(new Vector2(pt.x, pt.z), new Vector2(targetPos.x, targetPos.z)) < 4.2f)
                return 0;
        }

        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (tree == null) return 0;

        float groundY = terrain != null ? terrain.SampleHeight(targetPos) + terrain.transform.position.y : targetPos.y;
        Vector3 position = new Vector3(targetPos.x, groundY, targetPos.z);
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        tree.transform.position = position;
        tree.transform.rotation = rotation;
        float scale = baseScale * Random.Range(0.95f, 1.25f);
        tree.transform.localScale = Vector3.one * scale;

        // The purchased tree prefabs register their single mesh renderer as LOD0, LOD1 and LOD2.
        // Unity warns once per duplicate registration and pays the associated LOD bookkeeping even
        // though every level draws the exact same mesh. Collapse those entries on the authored scene
        // instance while retaining the smallest transition height as the original far cull cutoff.
        // Some variants also contain a nested LODGroup with no renderers at all; remove that inert
        // component. The source prefab remains untouched.
        NormalizeTreeLodGroups(tree);

        foreach (Collider c in tree.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
        }

        return 1;
    }

    internal static bool IsOutsideManorApproach(Vector3 targetPos, GmRouteSpline route,
        float corridorLength = 34f, float halfWidth = 8.5f)
    {
        if (route == null) throw new System.ArgumentNullException(nameof(route));
        Vector3 porch = route.PointAt(route.Length);
        Vector3 towardAvenue = -route.TangentAt(route.Length);
        Vector3 right = Vector3.Cross(Vector3.up, towardAvenue).normalized;
        Vector3 delta = targetPos - porch;
        float alongApproach = Vector3.Dot(delta, towardAvenue);
        float lateral = Mathf.Abs(Vector3.Dot(delta, right));
        return alongApproach < -3f || alongApproach > corridorLength || lateral > halfWidth;
    }

    internal static bool IsOutsideSightlineCorridor(Vector3 point, Vector3 from, Vector3 to,
        float halfWidth)
    {
        Vector2 start = new Vector2(from.x, from.z);
        Vector2 end = new Vector2(to.x, to.z);
        Vector2 candidate = new Vector2(point.x, point.z);
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.0001f) return Vector2.Distance(candidate, start) > halfWidth;
        float t = Vector2.Dot(candidate - start, segment) / lengthSquared;
        if (t < 0f || t > 1f) return true;
        return Vector2.Distance(candidate, Vector2.Lerp(start, end, t)) > halfWidth;
    }

    internal static Vector3 SightlineOrigin(Vector3 from, Vector3 to, float back, float side = 0f)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            throw new System.ArgumentException("sightline endpoints must differ horizontally");
        direction.Normalize();
        return from - direction * back + Vector3.Cross(Vector3.up, direction) * side;
    }

    internal static int ClearCemeterySightline(float halfWidth = 3.2f)
    {
        GmWorldAnchor weathered = GmWorldAnchor.Find("weathered-marker");
        GmWorldAnchor child = GmWorldAnchor.Find("child-marker");
        if (weathered == null || child == null)
            throw new System.InvalidOperationException(
                "cemetery sightline requires the weathered-marker and child-marker anchors");
        Vector3 from = SightlineOrigin(weathered.transform.position, child.transform.position, 3.5f);
        return ClearVegetationCentreline(from, child.transform.position, halfWidth,
            "cemetery marker reveal");
    }

    internal static int CountCemeterySightlineIntrusions(float halfWidth = 3.2f)
    {
        GmWorldAnchor weathered = GmWorldAnchor.Find("weathered-marker");
        GmWorldAnchor child = GmWorldAnchor.Find("child-marker");
        if (weathered == null || child == null) return -1;
        Vector3 from = SightlineOrigin(weathered.transform.position, child.transform.position, 3.5f);
        return CountVegetationCentrelineIntrusions(from, child.transform.position, halfWidth);
    }

    internal static int ClearChapelSightline(GmRouteSpline route, float halfWidth = 3.2f)
    {
        if (route == null) throw new System.ArgumentNullException(nameof(route));
        GmWorldAnchor chapel = GmWorldAnchor.Find("chapel");
        if (chapel == null) throw new System.InvalidOperationException("chapel sightline requires the chapel anchor");
        Vector3 from = route.PointAt(180f);
        Vector3 to = chapel.transform.position;
        return ClearVegetationSightline(from, to, halfWidth, "chapel reveal");
    }

    static int ClearVegetationSightline(Vector3 from, Vector3 to, float halfWidth, string label)
    {
        int cleared = 0;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData data = terrain.terrainData;
            TreeInstance[] trees = data.treeInstances;
            TreeInstance[] retained = trees.Where(instance =>
            {
                Vector3 world = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                return IsOutsideSightlineCorridor(world, from, to, halfWidth);
            }).ToArray();
            if (retained.Length != trees.Length)
            {
                cleared += trees.Length - retained.Length;
                data.treeInstances = retained;
            }
        }

        var prefabRoots = new HashSet<GameObject>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer == null || !LooksLikeSightlineVegetation(renderer.transform)) continue;
            if (!BoundsIntrudesSightline(renderer.bounds, from, to, halfWidth)) continue;
            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            if (prefabRoot != null && LooksLikeSightlineVegetation(prefabRoot.transform))
                prefabRoots.Add(prefabRoot);
            else
            {
                renderer.enabled = false;
                foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                cleared++;
            }
        }
        foreach (GameObject prefabRoot in prefabRoots)
        {
            if (prefabRoot == null) continue;
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            cleared++;
        }
        Debug.Log($"[GmWendEstateForest] cleared {cleared} vegetation intrusion(s) from the {label}");
        return cleared;
    }

    static int CountVegetationSightlineIntrusions(Vector3 from, Vector3 to, float halfWidth)
    {
        int count = 0;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData data = terrain.terrainData;
            count += data.treeInstances.Count(instance =>
            {
                Vector3 world = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                return !IsOutsideSightlineCorridor(world, from, to, halfWidth);
            });
        }

        var prefabRoots = new HashSet<GameObject>();
        var looseRenderers = new HashSet<Renderer>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.enabled || !LooksLikeSightlineVegetation(renderer.transform))
                continue;
            if (!BoundsIntrudesSightline(renderer.bounds, from, to, halfWidth)) continue;
            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            if (prefabRoot != null && LooksLikeSightlineVegetation(prefabRoot.transform))
                prefabRoots.Add(prefabRoot);
            else looseRenderers.Add(renderer);
        }
        return count + prefabRoots.Count + looseRenderers.Count;
    }

    static int ClearVegetationCentreline(Vector3 from, Vector3 to, float halfWidth, string label)
    {
        int cleared = 0;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData data = terrain.terrainData;
            TreeInstance[] trees = data.treeInstances;
            TreeInstance[] retained = trees.Where(instance =>
            {
                Vector3 world = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                return IsOutsideSightlineCorridor(world, from, to, halfWidth);
            }).ToArray();
            if (retained.Length != trees.Length)
            {
                cleared += trees.Length - retained.Length;
                data.treeInstances = retained;
            }
        }

        var prefabRoots = new HashSet<GameObject>();
        var looseRenderers = new HashSet<Renderer>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.enabled || !LooksLikeSightlineVegetation(renderer.transform))
                continue;
            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            if (prefabRoot != null && LooksLikeSightlineVegetation(prefabRoot.transform))
            {
                if (!IsOutsideSightlineCorridor(renderer.bounds.center, from, to, halfWidth))
                    prefabRoots.Add(prefabRoot);
            }
            else if (!IsOutsideSightlineCorridor(renderer.bounds.center, from, to, halfWidth))
                looseRenderers.Add(renderer);
        }
        foreach (GameObject prefabRoot in prefabRoots)
        {
            if (prefabRoot == null) continue;
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            cleared++;
        }
        foreach (Renderer renderer in looseRenderers)
        {
            if (renderer == null) continue;
            renderer.enabled = false;
            foreach (Collider collider in renderer.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            cleared++;
        }
        Debug.Log($"[GmWendEstateForest] cleared {cleared} trunk/instance intrusion(s) from the {label}");
        return cleared;
    }

    static int CountVegetationCentrelineIntrusions(Vector3 from, Vector3 to, float halfWidth)
    {
        int count = 0;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData data = terrain.terrainData;
            count += data.treeInstances.Count(instance =>
            {
                Vector3 world = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                return !IsOutsideSightlineCorridor(world, from, to, halfWidth);
            });
        }

        var prefabRoots = new HashSet<GameObject>();
        var looseRenderers = new HashSet<Renderer>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.enabled || !LooksLikeSightlineVegetation(renderer.transform))
                continue;
            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            if (prefabRoot != null && LooksLikeSightlineVegetation(prefabRoot.transform))
            {
                if (!IsOutsideSightlineCorridor(renderer.bounds.center, from, to, halfWidth))
                    prefabRoots.Add(prefabRoot);
            }
            else if (!IsOutsideSightlineCorridor(renderer.bounds.center, from, to, halfWidth))
                looseRenderers.Add(renderer);
        }
        return count + prefabRoots.Count + looseRenderers.Count;
    }

    static bool BoundsIntrudesSightline(Bounds bounds, Vector3 from, Vector3 to, float halfWidth)
    {
        float distance = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        int steps = Mathf.Max(2, Mathf.CeilToInt(distance));
        for (int step = 0; step <= steps; step++)
        {
            Vector3 sample = Vector3.Lerp(from, to, step / (float)steps);
            if (GmReviewShotProtection.HorizontalDistanceToBounds(sample, bounds) <= halfWidth) return true;
        }
        return false;
    }

    static bool LooksLikeSightlineVegetation(Transform transform)
    {
        for (Transform candidate = transform; candidate != null; candidate = candidate.parent)
        {
            string lower = candidate.name.ToLowerInvariant();
            if (lower.Contains("tree") || lower.Contains("foliage") || lower.Contains("foilage") ||
                lower.Contains("bush") || lower.Contains("grass") || lower.Contains("shrub") ||
                lower.Contains("plant") || lower.Contains("fern")) return true;
        }
        return false;
    }

    public static int NormalizeTreeLodGroups(GameObject tree)
    {
        if (tree == null) throw new System.ArgumentNullException(nameof(tree));
        int repaired = 0;
        foreach (LODGroup group in tree.GetComponentsInChildren<LODGroup>(true))
            repaired += NormalizeLodGroup(group, tree.transform);
        return repaired;
    }

    public static bool HasMalformedTreeLodGroups(GameObject tree)
    {
        if (tree == null) throw new System.ArgumentNullException(nameof(tree));
        foreach (LODGroup group in tree.GetComponentsInChildren<LODGroup>(true))
            if (LodGroupIsMalformed(group, tree.transform)) return true;
        return false;
    }

    public static int NormalizeSceneLodGroups()
    {
        int repaired = 0;
        // Snapshot first because repairing an empty group destroys its component.
        foreach (LODGroup group in Object.FindObjectsByType<LODGroup>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            repaired += NormalizeLodGroup(group, null);
        return repaired;
    }

    static int NormalizeLodGroup(LODGroup group, Transform ownerRoot)
    {
        if (group == null) return 0;
        LOD[] lods = group.GetLODs();
        bool repairedExternalReference = false;
        if (ownerRoot != null && lods.Any(lod => lod.renderers.Any(renderer =>
                renderer != null && !IsOwnedBy(renderer.transform, ownerRoot))))
        {
            var localMeshes = new List<Renderer>();
            foreach (Renderer renderer in ownerRoot.GetComponentsInChildren<Renderer>(true))
                if (RendererHasMesh(renderer)) localMeshes.Add(renderer);
            if (localMeshes.Count != 1)
                throw new System.InvalidOperationException(
                    $"tree '{ownerRoot.name}' has a cross-prefab LOD renderer reference and " +
                    $"{localMeshes.Count} plausible local mesh renderers; refusing to guess");

            for (int i = 0; i < lods.Length; i++)
            {
                Renderer[] renderers = lods[i].renderers;
                for (int j = 0; j < renderers.Length; j++)
                {
                    if (renderers[j] == null || IsOwnedBy(renderers[j].transform, ownerRoot)) continue;
                    renderers[j] = localMeshes[0];
                    repairedExternalReference = true;
                }
                lods[i].renderers = renderers;
            }
            group.SetLODs(lods);
        }
        var unique = new List<Renderer>();
        int registrations = 0;
        float farCutoff = float.MaxValue;
        foreach (LOD lod in lods)
        {
            bool ownsRenderer = false;
            foreach (Renderer renderer in lod.renderers)
            {
                if (renderer == null) continue;
                registrations++;
                ownsRenderer = true;
                if (!unique.Contains(renderer)) unique.Add(renderer);
            }
            if (ownsRenderer)
                farCutoff = Mathf.Min(farCutoff, lod.screenRelativeTransitionHeight);
        }

        if (unique.Count == 0)
        {
            Object.DestroyImmediate(group);
            return 1;
        }
        if (unique.Count != 1 || registrations <= 1)
        {
            if (repairedExternalReference)
            {
                group.RecalculateBounds();
                EditorUtility.SetDirty(group);
                return 1;
            }
            return 0;
        }

        group.SetLODs(new[] { new LOD(farCutoff, new[] { unique[0] }) });
        group.RecalculateBounds();
        EditorUtility.SetDirty(group);
        return 1;
    }

    static bool LodGroupIsMalformed(LODGroup group, Transform ownerRoot)
    {
        if (group == null) return false;
        var unique = new List<Renderer>();
        int registrations = 0;
        foreach (LOD lod in group.GetLODs())
        foreach (Renderer renderer in lod.renderers)
        {
            if (renderer == null) continue;
            if (ownerRoot != null && !IsOwnedBy(renderer.transform, ownerRoot)) return true;
            registrations++;
            if (!unique.Contains(renderer)) unique.Add(renderer);
        }
        return unique.Count == 0 || (unique.Count == 1 && registrations > 1);
    }

    static bool IsOwnedBy(Transform candidate, Transform ownerRoot) =>
        candidate == ownerRoot || candidate.IsChildOf(ownerRoot);

    static bool RendererHasMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh != null;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null;
    }

    static int PlaceScenicProp(List<GameObject> prefabs, Transform parent, Terrain terrain, Vector3 targetPos,
        float minScale, float maxScale, GmRouteSpline route, float minRouteDistance)
    {
        if (prefabs.Count == 0) return 0;
        if (!IsOutsideManorApproach(targetPos, route)) return 0;
        GameObject coachHouse = GameObject.Find("CoachHouse");
        if (coachHouse != null && !GmWendOutbuildings.IsOutsideCoachHouseFootprint(
                targetPos, coachHouse.transform, 1.5f)) return 0;

        for (float sm = 0; sm <= route.Length; sm += 4f)
        {
            Vector3 pt = route.PointAt(sm);
            if (Vector2.Distance(new Vector2(pt.x, pt.z), new Vector2(targetPos.x, targetPos.z)) < minRouteDistance)
                return 0;
        }

        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (prop == null) return 0;

        float groundY = terrain != null ? terrain.SampleHeight(targetPos) + terrain.transform.position.y : targetPos.y;
        Vector3 position = new Vector3(targetPos.x, groundY, targetPos.z);
        Quaternion rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));

        prop.transform.position = position;
        prop.transform.rotation = rotation;
        prop.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);

        foreach (Collider c in prop.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
        }

        return 1;
    }

    static int PlaceLampPost(GameObject polePrefab, GameObject lanternPrefab, Transform parent, Terrain terrain,
        Vector3 targetPos, Vector3 tangent, float side, int index)
    {
        float groundY = terrain != null ? terrain.SampleHeight(targetPos) + terrain.transform.position.y : targetPos.y;
        Vector3 position = new Vector3(targetPos.x, groundY, targetPos.z);
        Quaternion rotation = Quaternion.LookRotation(tangent * side, Vector3.up);

        var lampGroup = new GameObject($"EstateLampPost_{index:D2}");
        lampGroup.transform.SetParent(parent, false);
        lampGroup.transform.position = position;
        lampGroup.transform.rotation = rotation;

        if (polePrefab != null)
        {
            GameObject pole = (GameObject)PrefabUtility.InstantiatePrefab(polePrefab, lampGroup.transform);
            pole.transform.localPosition = Vector3.zero;
            pole.transform.localRotation = Quaternion.identity;
            pole.transform.localScale = Vector3.one * 0.9f;
            foreach (Collider c in pole.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        var lightGo = new GameObject("LanternLight");
        lightGo.transform.SetParent(lampGroup.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 3.4f, 0.3f);

        if (lanternPrefab == null)
            throw new System.InvalidOperationException(
                $"[GmWendEstateForest] period lantern prefab is missing: {LanternPrefabPath}");
        GmOwnedPropFactory.PlacePrefab(LanternPrefabPath, $"EstateLanternFixture_{index:D2}",
            lampGroup.transform, lightGo.transform.position, new Vector3(0.56f, 0.82f, 0.56f),
            lampGroup.transform.rotation);

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.useColorTemperature = true;
        light.colorTemperature = 2100f;
        light.color = new Color(1.0f, 0.88f, 0.72f);
        light.range = 14f;
        light.shadows = (index % 3 == 0) ? LightShadows.Soft : LightShadows.None;

        var hd = lightGo.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.intensity = 180f;
        hd.range = 14f;
        hd.affectsVolumetric = true;

        var flicker = lightGo.AddComponent<GmPeriodLampFlicker>();
        flicker.baseIntensity = 180f;
        flicker.flickerSpeed = 3.2f;
        flicker.flickerAmount = 0.18f;

        return 1;
    }
}
