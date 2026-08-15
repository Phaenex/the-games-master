using System.Collections.Generic;
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

        // 3. Cemetery Branch Dressing (160m to 215m)
        for (float m = 165f; m <= 210f; m += 12f)
        {
            Vector3 center = route.PointAt(m);
            Vector3 tangent = route.TangentAt(m);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            propsPlaced += PlaceScenicProp(crossPrefabs, forestRoot, terrain, center - right * 6.5f, 1.0f, 1.2f, route, 4.5f);
            propsPlaced += PlaceScenicProp(crossPrefabs, forestRoot, terrain, center - right * 9.0f, 0.9f, 1.1f, route, 4.5f);
        }

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

        foreach (Collider c in tree.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
        }

        return 1;
    }

    static int PlaceScenicProp(List<GameObject> prefabs, Transform parent, Terrain terrain, Vector3 targetPos,
        float minScale, float maxScale, GmRouteSpline route, float minRouteDistance)
    {
        if (prefabs.Count == 0) return 0;

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
