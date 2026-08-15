// Neutralises the green bias in the pack's `_BaseTint` wall materials, for the same reason
// GmWendFoliage neutralises the ungated emissive foliage: a multiply that is a rounding error under
// the pack's own 2000 lux daylight becomes the dominant colour of the frame at a night exposure.
//
// FOUND BY WALKING THE ROUTE: walk-0330m.png, walk-0345m.png and walk-0360m.png all show the same
// building's wall reading distinctly mint-green -- not just in the 360m close pass, which was first
// blamed on the camera being jammed against the wall's collision mesh, but at normal viewing distance
// twice before it. `M_Wall_02`, the single most used wall material in the village at 153 renderers,
// carries `_BaseTint = (0.596, 0.635, 0.525, 0)`: green channel highest, blue lowest, a bias small
// enough (about 6%) to disappear under daylight and large enough to read as coloured stone once the
// scene is this much darker.
//
// DERIVED, NOT NAMED. `M_Wall_02` is not hardcoded. Every material shipping a `_BaseTint` is surveyed
// and the ones with a measurable green skew are neutralised; the ones that are already neutral
// (`M_Wall_01`, `M_Wall_03`, both within 0.01 of grey) or warm-biased on purpose (`M_Church_Wall`,
// sandstone) are left alone. A future pack update or a different building set gets judged by the same
// rule rather than by a name that may not exist in it.
//
// PURCHASED ASSETS ARE NEVER EDITED. Repointed onto owned copies in GmWendFoliage.OwnedDir, the same
// scene-owned directory the foliage fix already uses -- one place for every material this scene owns
// a corrected copy of, not two.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GmWendWallTint
{
    const string LogTag = "GmWendWallTint";
    public const string TintProp = "_BaseTint";

    /// How far the green channel has to lead both red and blue before a tint counts as green-biased.
    /// 0.596/0.635/0.525 (the measured defect) leads by 0.039 and 0.110; 0.02 catches that with room
    /// to spare while leaving genuinely neutral tints (M_Wall_01, M_Wall_03, within 0.01 of grey) alone.
    ///
    /// Pure and public so the rule is testable without a real material or shader: this is the part a
    /// pack update could silently break, by shipping a new green-biased material this never sees
    /// because nothing exercises the THRESHOLD without a live scene to find it in.
    public const float GreenBiasThreshold = 0.02f;

    public static bool IsGreenBiased(Color t) =>
        t.g > t.r + GreenBiasThreshold && t.g > t.b + GreenBiasThreshold;

    /// Luma-preserving neutralisation: the tint's overall brightness contribution survives, only the
    /// hue bias is removed. A flat (1,1,1) would also brighten the wall relative to its neutral
    /// siblings (M_Wall_01 at 0.66, M_Wall_03 at 0.60), which is a second, unmeasured change this rule
    /// is not here to make.
    public static Color Neutralize(Color t)
    {
        float luma = 0.2126f * t.r + 0.7152f * t.g + 0.0722f * t.b;
        return new Color(luma, luma, luma, t.a);
    }

    /// Every material in the currently open scene that would be repointed. Read-only, so the audit and
    /// the fix cannot disagree about which materials are affected.
    public static Material[] AffectedMaterials() =>
        Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null && m.HasProperty(TintProp) && IsGreenBiased(m.GetColor(TintProp)))
            .Distinct()
            .ToArray();

    /// Repoints every renderer using a green-biased `_BaseTint` material onto an owned copy with the
    /// tint neutralised to a same-luma grey. Returns how many renderer slots were repointed.
    public static int Apply()
    {
        var owned = new Dictionary<Material, Material>();
        int slots = 0, renderersTouched = 0;

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
            renderersTouched++;
        }

        AssetDatabase.SaveAssets();

        string listing = owned.Count == 0
            ? "none (all already owned or none green-biased)"
            : string.Join(", ", owned.Select(kv => kv.Key.name));
        Debug.Log($"[{LogTag}] this pass neutralised {owned.Count} material(s): {listing}\n" +
                  $"  repointed {slots} renderer slot(s) across {renderersTouched} renderer(s)");

        return slots;
    }

    static Material OwnedNeutral(Material src, Dictionary<Material, Material> cache)
    {
        if (src == null || !src.HasProperty(TintProp)) return null;
        Color tint = src.GetColor(TintProp);
        if (!IsGreenBiased(tint)) return null;
        if (cache.TryGetValue(src, out Material hit)) return hit;

        Color neutral = Neutralize(tint);

        Material copy = LoadOrCopy(src);
        copy.SetColor(TintProp, neutral);
        EditorUtility.SetDirty(copy);

        cache[src] = copy;
        Debug.Log($"[{LogTag}] owned material '{src.name}' tint " +
                  $"({tint.r:0.###},{tint.g:0.###},{tint.b:0.###}) -> " +
                  $"({neutral.r:0.###},{neutral.g:0.###},{neutral.b:0.###}) at {AssetDatabase.GetAssetPath(copy)}");
        return copy;
    }

    static Material LoadOrCopy(Material src)
    {
        if (!AssetDatabase.IsValidFolder(GmWendFoliage.OwnedDir))
        {
            string parent = System.IO.Path.GetDirectoryName(GmWendFoliage.OwnedDir).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(GmWendFoliage.OwnedDir));
        }

        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath))
            throw new System.InvalidOperationException($"'{src.name}' has no asset path, cannot own a copy of it");

        string dstPath = $"{GmWendFoliage.OwnedDir}/{System.IO.Path.GetFileNameWithoutExtension(srcPath)}.mat";

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
