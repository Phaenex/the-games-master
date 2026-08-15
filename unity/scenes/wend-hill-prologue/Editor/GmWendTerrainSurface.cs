using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Owns and paints the purchased scene's otherwise untextured embedded TerrainData.
/// </summary>
public static class GmWendTerrainSurface
{
    public const float RouteNormalScale = 0.22f;
    public const float RouteSmoothness = 0.015f;
    const string OwnedTerrainPath = "Assets/Scenes/Generated/GmWendTerrain.asset";
    const string LayerPrefix = "Assets/Scenes/Generated/GmWendTerrainLayer";
    const string SoilAlbedo =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Muddy_Ground02_B.PNG";
    const string SoilNormal =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Muddy_Ground02_N.png";
    const string MossAlbedo =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Moss_Ground_02_Albedo.PNG";
    const string MossNormal =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Moss_Ground_02_Normal.png";
    const string RoadAlbedo =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Muddy_Road_01_Albedo.PNG";
    const string RoadNormal =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Muddy_Road_01_Normal.png";

    public static Vector3 LayerWeights(float lateralMetres, float habitat)
    {
        float lateral = Mathf.Abs(lateralMetres);
        float road = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.45f, 4.2f, lateral));
        road *= Mathf.Lerp(0.60f, 0.76f, Mathf.Clamp01(habitat));
        float moss = Mathf.Lerp(0.10f, 0.58f, Mathf.Clamp01(habitat)) * (1f - road);
        float soil = Mathf.Max(0f, 1f - road - moss);
        float sum = soil + moss + road;
        return sum > 0f ? new Vector3(soil, moss, road) / sum : Vector3.right;
    }

    public static int Apply(GmRouteSpline route)
    {
        Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>(FindObjectsInactive.Include);
        if (terrain == null || terrain.terrainData == null || route == null)
            throw new InvalidOperationException("terrain surface pass requires the saved terrain and route");

        TerrainData owned = UnityEngine.Object.Instantiate(terrain.terrainData);
        owned.name = "GmWendTerrain";
        if (AssetDatabase.LoadMainAssetAtPath(OwnedTerrainPath) != null)
            AssetDatabase.DeleteAsset(OwnedTerrainPath);
        AssetDatabase.CreateAsset(owned, OwnedTerrainPath);
        terrain.terrainData = owned;
        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null) terrainCollider.terrainData = owned;

        owned.terrainLayers = new[]
        {
            MakeLayer(1, "Estate Soil", SoilAlbedo, SoilNormal, new Vector2(7.5f, 7.5f), 0.34f, 0.04f),
            MakeLayer(2, "Damp Moss", MossAlbedo, MossNormal, new Vector2(6.2f, 6.2f), 0.30f, 0.03f),
            MakeLayer(3, "Muddy Route", RoadAlbedo, RoadNormal, new Vector2(4.2f, 4.2f),
                RouteNormalScale, RouteSmoothness),
        };
        EditorUtility.SetDirty(owned);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(OwnedTerrainPath, ImportAssetOptions.ForceSynchronousImport);
        owned = AssetDatabase.LoadAssetAtPath<TerrainData>(OwnedTerrainPath);
        if (owned == null || owned.alphamapLayers != 3)
            throw new InvalidOperationException("owned terrain did not persist its three painted layers");
        terrain.terrainData = owned;
        if (terrainCollider != null) terrainCollider.terrainData = owned;

        int width = owned.alphamapWidth;
        int height = owned.alphamapHeight;
        var alpha = new float[height, width, 3];
        Vector3 origin = terrain.transform.position;
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            float worldX = origin.x + x / (float)(width - 1) * owned.size.x;
            float worldZ = origin.z + z / (float)(height - 1) * owned.size.z;
            var world = new Vector3(worldX, 0f, worldZ);
            float metres = route.ProjectDistance(world);
            Vector3 onRoute = route.PointAt(metres);
            float lateral = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(onRoute.x, onRoute.z));
            float habitat = Mathf.Clamp01(
                Mathf.PerlinNoise((worldX + 71f) * 0.022f, (worldZ - 29f) * 0.021f) * 0.68f +
                Mathf.PerlinNoise((worldX - 13f) * 0.071f, (worldZ + 47f) * 0.067f) * 0.32f);
            Vector3 weights = LayerWeights(lateral, habitat);
            alpha[z, x, 0] = weights.x;
            alpha[z, x, 1] = weights.y;
            alpha[z, x, 2] = weights.z;
        }
        owned.SetAlphamaps(0, 0, alpha);
        RetireOpaqueRouteRibbon();
        terrain.Flush();
        EditorUtility.SetDirty(owned);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GmWendTerrainSurface] painted {width}x{height} soil/moss/route layers on owned terrain");
        return owned.terrainLayers.Length;
    }

    /// Hides the walk deck's render pass, if it has one, now that the painted route band under it is
    /// the thing meant to be seen. It searched '.../RouteSurface' for a while, which no version of
    /// GmWendOpening has ever built, so it could only ever return false. The deck as currently built
    /// is collision-only, so false is still the honest answer -- but it is now an answer about the
    /// object that actually exists rather than about a name.
    public static bool RetireOpaqueRouteRibbon()
    {
        GameObject routeSurface =
            GameObject.Find($"{GmWendOpening.RootName}/{GmWendOpening.WalkDeckName}");
        MeshRenderer renderer = routeSurface != null ? routeSurface.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return false;
        renderer.enabled = false;
        EditorUtility.SetDirty(renderer);
        return true;
    }

    static TerrainLayer MakeLayer(int index, string name, string albedoPath, string normalPath,
        Vector2 tileSize, float normalScale, float smoothness)
    {
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (albedo == null || normal == null)
            throw new InvalidOperationException($"terrain layer '{name}' is missing its purchased texture pair");
        string path = $"{LayerPrefix}_{index:00}.terrainlayer";
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        var layer = new TerrainLayer
        {
            name = $"GmWend {name}",
            diffuseTexture = albedo,
            normalMapTexture = normal,
            tileSize = tileSize,
            normalScale = normalScale,
            smoothness = smoothness,
            metallic = 0f,
            diffuseRemapMin = new Vector4(0.012f, 0.009f, 0.006f, 0f),
            diffuseRemapMax = new Vector4(0.72f, 0.68f, 0.60f, 1f),
        };
        AssetDatabase.CreateAsset(layer, path);
        return layer;
    }
}
