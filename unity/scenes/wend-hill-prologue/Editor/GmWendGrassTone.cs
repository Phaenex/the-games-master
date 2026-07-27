// Neutralises two independent problems on the pack's S_Wind grass materials, both invisible under the
// pack's own 2000 lux daylight and both dominant at this scene's night exposure. Found by looking
// harder at frames that already passed the written standard, because passing a luma/green-cast check
// is not the same as looking right: walk-0105m.png, walk-0135m.png, walk-0180m.png and walk-0210m.png
// all show the same saturated orange-red grass filling the foreground, long before any of them are
// near the 360m collision event that a different fix already accounted for.
//
// GmWendFoliage.cs already owns copies of these exact materials to zero their EMISSIVE term. Neither
// problem here is that: emission makes a surface glow on its own, these change what colour it reflects.
//
//   M_grass    _Albedo_Tint=(0.881,1.00,0.627) spread 0.373  _Albedo_Intensity=2.2
//   M_grass 2  _Albedo_Tint=(0.479,0.84,0.486) spread 0.361  _Albedo_Intensity=-6
//   M_Leaf     _Albedo_Tint=(1.00,1.00,1.00)   spread 0.000  _Albedo_Intensity=-0.17, left alone
//
// TWO ROUNDS, because the first one was not enough and shipping it without checking would have been
// exactly the mistake the starfield exposure guess already made once this session. Round one
// neutralised the tint alone and rebuilt: walk-0105m/135m were unchanged, still saturated orange.
// _Albedo_Intensity=2.2 -- more than doubling the albedo's contribution -- is what is actually driving
// it, confirmed by resetting it to 1.0 and re-walking the same short segment.
//
// RENDERERS AND TERRAIN TREE PROTOTYPES BOTH. M_grass and M_grass 2 alone cover 728 of the scene's
// renderer material slots and are what actually fills the frame in every walk screenshot; those were
// fixed and verified first. `M_grass 1` (tree-attached undergrowth) only reaches the scene through
// terrain tree prototypes, and carries the same measured bias -- tint (0.679,0.565,0.516) spread 0.163,
// intensity 2.0 -- so it gets the same fix via the prefab-owning path, mirrored from
// GmWendFoliage.OwnedPrefab. `M_Leaf`'s intensity sits BELOW 1 rather than above it and its tint is
// already neutral: no measured evidence of the same bug, so it is checked by the same rule and left
// alone by the same rule, not hand-excluded.
//
// DERIVED, NOT NAMED, same as the wall fix: any `_Albedo_Tint` far enough from grey, or any
// `_Albedo_Intensity` elevated above 1, is neutralised, regardless of which material carries it or
// which of the two paths (renderer, tree prototype) it reaches the scene through.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GmWendGrassTone
{
    const string LogTag = "GmWendGrassTone";
    public const string TintProp = "_Albedo_Tint";
    public const string IntensityProp = "_Albedo_Intensity";
    const string ShaderName = "Shader Graphs/S_Wind";

    /// How far a tint's brightest and dimmest channel may differ before it counts as biased. The
    /// measured defects spread 0.36-0.37; M_Leaf's neutral tint spreads 0. 0.05 catches real colour
    /// bias with room to spare while leaving a genuinely neutral tint alone.
    public const float SpreadThreshold = 0.05f;

    /// The shader's own implied neutral for a multiplier property named "intensity": 1x, no change.
    public const float NeutralIntensity = 1.0f;

    /// How far above NeutralIntensity a value has to sit before it counts as elevated. The measured
    /// defects sit at 2.0-2.2, more than double; 0.15 catches that with room to spare. Deliberately
    /// one-sided: M_Leaf's -0.17 is BELOW neutral, which this does not flag, because there is no
    /// measured evidence that direction is broken, only that this one is.
    public const float IntensityAboveThreshold = 0.15f;

    public static bool IsBiased(Color t) =>
        Mathf.Max(t.r, t.g, t.b) - Mathf.Min(t.r, t.g, t.b) > SpreadThreshold;

    public static bool IsIntensityElevated(float f) => f > NeutralIntensity + IntensityAboveThreshold;

    public static Color Neutralize(Color t)
    {
        float luma = 0.2126f * t.r + 0.7152f * t.g + 0.0722f * t.b;
        return new Color(luma, luma, luma, t.a);
    }

    /// Repoints every renderer AND every terrain tree prototype using a biased S_Wind material onto an
    /// owned copy with the defect neutralised. One shared cache across both passes, so a material that
    /// happens to reach the scene both ways (it does not currently, but nothing here assumes it cannot)
    /// is only ever owned and logged once.
    public static int Apply()
    {
        var owned = new Dictionary<Material, Material>();
        int rendererSlots = ApplyToRenderers(owned);
        int protoSlots = ApplyToTreePrototypes(owned);

        AssetDatabase.SaveAssets();

        string listing = owned.Count == 0
            ? "none (all already owned or none biased)"
            : string.Join(", ", owned.Select(kv => kv.Key.name));
        Debug.Log($"[{LogTag}] this pass neutralised {owned.Count} material(s): {listing}\n" +
                  $"  repointed {rendererSlots} renderer slot(s), {protoSlots} tree prototype slot(s)");

        return rendererSlots + protoSlots;
    }

    static int ApplyToRenderers(Dictionary<Material, Material> owned)
    {
        int slots = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material fixedMat = OwnedNeutral(mats[i], owned);
                if (fixedMat == null) continue;
                mats[i] = fixedMat;
                changed = true;
                slots++;
            }
            if (!changed) continue;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
        }
        return slots;
    }

    /// Mirrors GmWendFoliage.OwnedPrefab: prototype ORDER and COUNT are preserved (placed tree
    /// instances refer to a prototype by index), and an owned prefab copy is opened, edited and saved
    /// back rather than edited through the loaded root, because prefab ASSET contents cannot be.
    static int ApplyToTreePrototypes(Dictionary<Material, Material> owned)
    {
        int slots = 0;
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            TerrainData td = t.terrainData;
            if (td == null) continue;

            TreePrototype[] protos = td.treePrototypes;
            bool changed = false;
            for (int i = 0; i < protos.Length; i++)
            {
                GameObject swapped = OwnedNeutralPrefab(protos[i].prefab, owned, ref slots);
                if (swapped == null) continue;
                protos[i].prefab = swapped;
                changed = true;
            }
            if (!changed) continue;

            td.treePrototypes = protos;
            td.RefreshPrototypes();
            t.Flush();
            EditorUtility.SetDirty(td);
            EditorUtility.SetDirty(t);
        }
        return slots;
    }

    /// Owned copy of a tree prototype prefab, with any biased S_Wind materials inside it repointed to
    /// their own owned copies via the SAME OwnedNeutral used for scene renderers. Returns null when
    /// nothing in the prefab needs fixing, which is how M_Leaf-only prototypes keep pointing at the
    /// purchased original.
    static GameObject OwnedNeutralPrefab(GameObject src, Dictionary<Material, Material> cache, ref int slots)
    {
        if (src == null) return null;
        bool anyAffected = src.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Any(m => m != null && m.shader.name == ShaderName &&
                      ((m.HasProperty(TintProp) && IsBiased(m.GetColor(TintProp))) ||
                       (m.HasProperty(IntensityProp) && IsIntensityElevated(m.GetFloat(IntensityProp)))));
        if (!anyAffected) return null;

        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath))
            throw new System.InvalidOperationException($"'{src.name}' has no asset path, cannot own a copy of it");
        string dstPath = $"{GmWendFoliage.OwnedDir}/{System.IO.Path.GetFileNameWithoutExtension(srcPath)}.prefab";

        EnsureOwnedDir();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dstPath) == null)
        {
            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
                throw new System.InvalidOperationException($"failed to copy {srcPath} -> {dstPath}");
            AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport);
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(dstPath);
        try
        {
            foreach (Renderer r in contents.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material fixedMat = OwnedNeutral(mats[i], cache);
                    if (fixedMat == null) continue;
                    mats[i] = fixedMat;
                    changed = true;
                    slots++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, dstPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        Debug.Log($"[{LogTag}] owned tree prototype prefab '{src.name}' at {dstPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
    }

    static void EnsureOwnedDir()
    {
        if (AssetDatabase.IsValidFolder(GmWendFoliage.OwnedDir)) return;
        string parent = System.IO.Path.GetDirectoryName(GmWendFoliage.OwnedDir).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(GmWendFoliage.OwnedDir));
    }

    static Material OwnedNeutral(Material src, Dictionary<Material, Material> cache)
    {
        if (src == null || src.shader.name != ShaderName) return null;

        bool tintBiased = src.HasProperty(TintProp) && IsBiased(src.GetColor(TintProp));
        bool intensityElevated = src.HasProperty(IntensityProp) && IsIntensityElevated(src.GetFloat(IntensityProp));
        if (!tintBiased && !intensityElevated) return null;
        if (cache.TryGetValue(src, out Material hit)) return hit;

        Material copy = LoadOrCopy(src);
        var log = new System.Text.StringBuilder($"[{LogTag}] owned material '{src.name}'");

        if (tintBiased)
        {
            Color tint = src.GetColor(TintProp);
            Color neutral = Neutralize(tint);
            copy.SetColor(TintProp, neutral);
            log.Append($" tint ({tint.r:0.###},{tint.g:0.###},{tint.b:0.###}) -> " +
                       $"({neutral.r:0.###},{neutral.g:0.###},{neutral.b:0.###})");
        }
        if (intensityElevated)
        {
            float before = src.GetFloat(IntensityProp);
            copy.SetFloat(IntensityProp, NeutralIntensity);
            log.Append($" intensity {before:0.##} -> {NeutralIntensity:0.##}");
        }
        EditorUtility.SetDirty(copy);

        cache[src] = copy;
        Debug.Log(log.Append($" at {AssetDatabase.GetAssetPath(copy)}").ToString());
        return copy;
    }

    static Material LoadOrCopy(Material src)
    {
        EnsureOwnedDir();

        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath))
            throw new System.InvalidOperationException($"'{src.name}' has no asset path, cannot own a copy of it");

        string dstPath = $"{GmWendFoliage.OwnedDir}/{System.IO.Path.GetFileNameWithoutExtension(srcPath)}.mat";

        // Already owned (GmWendFoliage repointed this renderer to the same path already): the source
        // IS the destination, so this returns the existing asset without attempting to copy it onto
        // itself.
        var existing = AssetDatabase.LoadAssetAtPath<Material>(dstPath);
        if (existing != null) return existing;

        if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            throw new System.InvalidOperationException($"failed to copy {srcPath} -> {dstPath}");
        AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport);

        var copy = AssetDatabase.LoadAssetAtPath<Material>(dstPath);
        if (copy == null)
            throw new System.InvalidOperationException($"copy of {srcPath} missing at {dstPath}");
        return copy;
    }
}
