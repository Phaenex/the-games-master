using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Small deterministic adapter around the locally-owned MetalMan Victorian Interiors package.
/// It keeps imported art out of gameplay ownership: the builder creates stable logical roots,
/// while this class supplies fitted, floor-contact-correct, collider-free visual children.
/// </summary>
public static class GmVictorianInteriorKit
{
    public const string AssetRoot = "Assets/ThirdParty/MetalManVictorianInteriors";
    const string TextureRoot = AssetRoot + "/Materials/";
    const string PortraitRoot = "Assets/GamesMaster/Portraits/";
    public const string VisualPrefix = "VictorianArt_";

    sealed class Family
    {
        public string Albedo;
        public string Normal;
        public string Emission;
        public float Smoothness;
        public Color Tint;
    }

    static readonly Dictionary<string, Family> Families = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase)
    {
        { "bookcase", new Family { Albedo = "Bookshelf_Albedo.psd", Normal = "Bookshelf_Normal.psd", Smoothness = 0.27f, Tint = new Color(0.62f, 0.55f, 0.46f) } },
        { "carpet", new Family { Albedo = "Carpets_Albedo.psd", Normal = "Carpets_Normal.psd", Smoothness = 0.16f, Tint = new Color(0.58f, 0.40f, 0.36f) } },
        { "chair", new Family { Albedo = "Chairs_Albedo.psd", Normal = "Chairs_Normal.psd", Smoothness = 0.25f, Tint = new Color(0.55f, 0.46f, 0.40f) } },
        { "couch", new Family { Albedo = "Couch_2_Brown_Albedo.psd", Normal = "Couch_2_Normal.psd", Smoothness = 0.22f, Tint = new Color(0.50f, 0.38f, 0.32f) } },
        { "door", new Family { Albedo = "Doors_Albedo.psd", Normal = "Doors_Normal.psd", Smoothness = 0.30f, Tint = new Color(0.52f, 0.43f, 0.35f) } },
        { "lamp", new Family { Albedo = "Lamp_Albedo.psd", Normal = "Lamp_Normal.psd", Emission = "Lamp_Emission.psd", Smoothness = 0.43f, Tint = new Color(0.72f, 0.62f, 0.49f) } },
        { "mantel", new Family { Albedo = "Mantel_Albedo.psd", Normal = "Mantel_Normal.psd", Smoothness = 0.24f, Tint = new Color(0.62f, 0.56f, 0.49f) } },
        { "mirror", new Family { Albedo = "Mirrors_Albedo.psd", Normal = "Mirrors_Normal.psd", Smoothness = 0.74f, Tint = new Color(0.70f, 0.72f, 0.70f) } },
        { "table", new Family { Albedo = "Tables_2_Albedo.psd", Normal = "Tables_2_Normal.psd", Smoothness = 0.28f, Tint = new Color(0.55f, 0.45f, 0.36f) } },
        { "picture", new Family { Albedo = "Frames_Albedo.psd", Normal = "Frames_Normal.psd", Smoothness = 0.34f, Tint = new Color(0.66f, 0.56f, 0.40f) } },
        { "stair", new Family { Albedo = "Stair_Albedo.png", Normal = "Stair_Normal.png", Smoothness = 0.24f, Tint = new Color(0.52f, 0.43f, 0.34f) } },
        { "wall", new Family { Albedo = "Wall_1_Albedo.psd", Normal = "Wall_1_Normal.png", Smoothness = 0.18f, Tint = new Color(0.53f, 0.46f, 0.39f) } },
        { "floor", new Family { Albedo = "Floor_Albedo.png", Normal = "Floor_Normal.png", Smoothness = 0.24f, Tint = new Color(0.46f, 0.38f, 0.31f) } },
        { "ceiling", new Family { Albedo = "Roof_Albedo.png", Normal = "Roof_Normal.png", Smoothness = 0.14f, Tint = new Color(0.48f, 0.43f, 0.37f) } },
    };

    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    static readonly string[] RequiredModels =
    {
        "BookShelf_1", "Carpet_1", "Chair_1", "Chair_2", "Couch_2", "Door_1",
        "Lamp_1_LOD0", "Lamp_2", "Mantel", "Mirror_1", "Table_2", "Table_3", "Stair",
        "Picture_1", "Picture_8",
    };

    public static bool Ready => RequiredModels.All(name =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetRoot}/{name}.fbx") != null);

    public static void Prepare()
    {
        Materials.Clear();
        string[] missingModels = RequiredModels.Where(name =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetRoot}/{name}.fbx") == null).ToArray();
        string[] missingTextures = Families.Values.SelectMany(family =>
                new[] { family.Albedo, family.Normal }.Where(name => !string.IsNullOrEmpty(name)))
            .Distinct()
            .Where(name => AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + name) == null)
            .ToArray();
        if (missingModels.Length > 0 || missingTextures.Length > 0)
        {
            Debug.LogError("[GmVictorianKit] missing proof-room assets. Run `npm run unity:assets:victorian`. " +
                           $"models=[{string.Join(", ", missingModels)}], textures=[{string.Join(", ", missingTextures)}]");
            return;
        }
        Debug.Log($"[GmVictorianKit] READY: {RequiredModels.Length} model families and {Families.Count} HDRP material recipes");
    }

    public static Material Surface(string family, string name, Vector2 tiling, Color? tintOverride = null)
    {
        string key = $"{family}|{name}|{tiling.x:F2},{tiling.y:F2}|{tintOverride}";
        if (Materials.TryGetValue(key, out Material cached)) return cached;
        if (!Families.TryGetValue(family, out Family recipe))
            throw new ArgumentException($"Unknown Victorian material family '{family}'", nameof(family));

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new InvalidOperationException("HDRP/Lit is unavailable");
        var material = new Material(shader) { name = name };
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + recipe.Albedo);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + recipe.Normal);
        if (albedo != null)
        {
            material.SetTexture("_BaseColorMap", albedo);
            material.SetTextureScale("_BaseColorMap", tiling);
        }
        if (normal != null)
        {
            material.SetTexture("_NormalMap", normal);
            material.SetTextureScale("_NormalMap", tiling);
        }
        material.SetColor("_BaseColor", tintOverride ?? recipe.Tint);
        material.SetFloat("_Smoothness", recipe.Smoothness);
        material.SetFloat("_Metallic", 0f);
        if (!string.IsNullOrEmpty(recipe.Emission))
        {
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + recipe.Emission);
            if (emission != null)
            {
                material.SetTexture("_EmissiveColorMap", emission);
                material.SetColor("_EmissiveColor", new Color(0.055f, 0.018f, 0.004f));
            }
        }
        if (!HDShaderUtils.ResetMaterialKeywords(material))
            Debug.LogWarning($"[GmVictorianKit] HDRP keyword reset rejected '{name}'");
        Materials.Add(key, material);
        return material;
    }

    public static Material Portrait(string slug)
    {
        string key = "portrait|" + slug;
        if (Materials.TryGetValue(key, out Material cached)) return cached;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitRoot + slug + ".png");
        if (texture == null)
            throw new InvalidOperationException($"Missing portrait '{slug}'. Run `npm run unity:assets:victorian`.");
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new InvalidOperationException("HDRP/Lit is unavailable");
        var material = new Material(shader) { name = "House_Portrait_" + slug };
        material.SetTexture("_BaseColorMap", texture);
        material.SetColor("_BaseColor", new Color(0.82f, 0.78f, 0.70f));
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Metallic", 0f);
        HDShaderUtils.ResetMaterialKeywords(material);
        Materials.Add(key, material);
        return material;
    }

    public static GameObject Place(string modelName, string objectName, Transform parent,
        Vector3 targetCenter, Vector3 targetSize, Quaternion worldRotation, string materialFamily,
        Color? tintOverride = null)
    {
        string path = $"{AssetRoot}/{modelName}.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[GmVictorianKit] no model at {path}");
            return null;
        }

        Material material = Surface(materialFamily, "House_Imported_" + materialFamily, Vector2.one, tintOverride);
        return PlacePrefab(prefab, VisualPrefix + objectName, parent, targetCenter, targetSize, worldRotation, material);
    }

    public static GameObject PlaceWithMaterial(string assetPath, string objectName, Transform parent,
        Vector3 targetCenter, Vector3 targetSize, Quaternion worldRotation, Material material)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError($"[GmVictorianKit] no model at {assetPath}");
            return null;
        }
        return PlacePrefab(prefab, "AuthoredArt_" + objectName, parent, targetCenter, targetSize, worldRotation, material);
    }

    static GameObject PlacePrefab(GameObject prefab, string instanceName, Transform parent,
        Vector3 targetCenter, Vector3 targetSize, Quaternion worldRotation, Material material)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = instanceName;
        instance.transform.SetPositionAndRotation(Vector3.zero, worldRotation);
        instance.transform.localScale = Vector3.one;

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            int slots = Mathf.Max(1, renderer.sharedMaterials.Length);
            renderer.sharedMaterials = Enumerable.Repeat(material, slots).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true))
            UnityEngine.Object.DestroyImmediate(camera);
        foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            UnityEngine.Object.DestroyImmediate(light);

        if (!TryBounds(instance, out Bounds bounds))
        {
            Debug.LogError($"[GmVictorianKit] '{prefab.name}' imported without renderer bounds");
            return instance;
        }
        float scale = float.PositiveInfinity;
        if (targetSize.x > 0.001f && bounds.size.x > 0.001f) scale = Mathf.Min(scale, targetSize.x / bounds.size.x);
        if (targetSize.y > 0.001f && bounds.size.y > 0.001f) scale = Mathf.Min(scale, targetSize.y / bounds.size.y);
        if (targetSize.z > 0.001f && bounds.size.z > 0.001f) scale = Mathf.Min(scale, targetSize.z / bounds.size.z);
        if (float.IsInfinity(scale) || scale <= 0f) scale = 1f;
        instance.transform.localScale *= scale;
        TryBounds(instance, out bounds);
        instance.transform.position += targetCenter - bounds.center;
        return instance;
    }

    public static bool IsImportedVisual(Renderer renderer) =>
        renderer != null && renderer.transform.GetComponentsInParent<Transform>(true)
            .Any(transform => transform.name.StartsWith(VisualPrefix, StringComparison.Ordinal));

    static bool TryBounds(GameObject root, out Bounds result)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            result = default;
            return false;
        }
        result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return true;
    }
}
