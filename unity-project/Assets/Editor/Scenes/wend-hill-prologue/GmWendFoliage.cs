// Turns off the one thing in the purchased pack that cannot survive a night exposure: an ungated
// emission term on its wind-driven foliage shader.
//
// S_Wind.shadergraph multiplies `_Emmisive` (a texture) by `_Emmisive_Tint` and `_Emmisive_Intensity`
// straight into Emission with no on/off switch, and on every material that uses it the texture slot is
// EMPTY, so it samples white and the whole blade emits its tint:
//
//   M_grass    2.21   tint (0.96, 1.00, 0.72)      M_grass 1  3.93
//   M_grass 2  2.60                                M_Leaf     2.78   tint (0.38, 0.42, 0.00)
//
// Under the pack's own 2000 lux daylight that is a rounding error, which is why the artist never saw
// it. At a night exposure it is the brightest thing in frame. Measured off the pre-fix ladder frame
// Screens/WendLadder-preFoliageFix/step3-000.png: grass 215,220,188 against a sky of 0,0,0, and the
// canopy 44,54,6 with essentially no blue in it at all, while the only light in the scene is a BLUE
// moon (0.62, 0.70, 0.92). Foliage that colour is not being lit, it is emitting.
//
// This was originally diagnosed as an unregistered HDRP diffusion profile, because the materials are
// `_MaterialID: 1` with `_TransmissionEnable: 1`. They are, but they reference no diffusion profile at
// all and `_RequireSplitLighting` is 0, so subsurface scattering is not the mechanism. The colour is
// the giveaway: scattered moonlight cannot come out warmer and greener than the moon.
//
// WHAT IS DELIBERATELY LEFT ALONE: the pack's other emissive shader has an `_Emissive_1` on/off float,
// and its 21 lamp posts set it to 1 at intensity 21.95. Those are meant to glow. Every other gated
// material has the switch at 0 and is inert. Only the ungated family is touched.
//
// Both knobs are zeroed, not just the intensity. The graph's wiring is not worth trusting on read: if
// emission is `tex * tint * intensity` then either one alone is enough, and if it is a lerp toward the
// tint then only the tint kills it. Zeroing both is correct under any wiring the pack's own inspector
// could turn off.
//
// PURCHASED ASSETS ARE NEVER EDITED. Affected materials are copied into Assets/Scenes/WendHill_Night/
// and the scene is repointed, the same way GmWendNight owns the volume profile. Two routes need it and
// missing either leaves half the frame glowing:
//
//   1. Scene renderers          M_grass on 640 of them, M_grass 2 on 88
//   2. Terrain tree prototypes  7 prototypes, of which 5 carry M_grass, M_grass 1 or M_Leaf
//
// The terrain's TerrainData is embedded in the scene rather than being a separate purchased asset (its
// asset path comes back empty), so repointing its prototypes only ever writes to our own scene copy.
// The prototype PREFABS are purchased, so those get owned copies too.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmWendFoliage
{
    const string LogTag = "GmWendFoliage";

    /// The misspelling is the pack's, not a typo here. It is the actual serialized property name, and
    /// it is what distinguishes the ungated family from the gated one.
    public const string IntensityProp = "_Emmisive_Intensity";
    public const string TextureProp = "_Emmisive";
    public const string TintProp = "_Emmisive_Tint";

    public const string OwnedDir = "Assets/Scenes/WendHill_Night";

    /// Read-only. Reports how the emissive foliage materials reach the scene, which is what decides
    /// whether repointing renderers is enough. Kept because the answer was not guessable: most of the
    /// grass and all of the glowing canopy arrive as terrain trees, not as scene renderers.
    [MenuItem("GamesMaster/Wend/Survey foliage emission")]
    public static void Survey()
    {
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        var usage = new Dictionary<Material, int>();
        int slots = 0;

        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                slots++;
                usage.TryGetValue(m, out int n);
                usage[m] = n + 1;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{renderers.Length} renderers, {slots} material slots, {usage.Count} distinct materials");

        foreach (KeyValuePair<Material, int> kv in usage
                     .Where(kv => kv.Key.HasProperty(IntensityProp))
                     .OrderByDescending(kv => kv.Key.GetFloat(IntensityProp)))
        {
            Material m = kv.Key;
            Texture tex = m.HasProperty(TextureProp) ? m.GetTexture(TextureProp) : null;
            sb.AppendLine($"  UNGATED {m.name,-14} intensity={m.GetFloat(IntensityProp):0.00} " +
                          $"tex={(tex == null ? "NONE (samples white)" : tex.name)} " +
                          $"renderers={kv.Value} path={AssetDatabase.GetAssetPath(m)}");
        }

        // The gated family, for contrast. Anything here with the switch on is an intentional lamp.
        foreach (KeyValuePair<Material, int> kv in usage
                     .Where(kv => kv.Key.HasProperty("_Emissive_1"))
                     .OrderByDescending(kv => kv.Key.GetFloat("_Emissive_1")))
        {
            Material m = kv.Key;
            sb.AppendLine($"  gated   {m.name,-14} switch={m.GetFloat("_Emissive_1"):0} " +
                          $"intensity={m.GetFloat("_Emissive_Intensity"):0.00} renderers={kv.Value}");
        }

        Debug.Log($"[{LogTag}] RENDERER MATERIALS\n{sb}");

        sb.Clear();
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        sb.AppendLine($"{terrains.Length} terrain(s)");
        foreach (Terrain t in terrains)
        {
            TerrainData td = t.terrainData;
            if (td == null) { sb.AppendLine($"  '{t.name}' has no TerrainData"); continue; }

            string dataPath = AssetDatabase.GetAssetPath(td);
            sb.AppendLine($"  '{t.name}' data={(string.IsNullOrEmpty(dataPath) ? "EMBEDDED IN SCENE" : dataPath)} " +
                          $"details={td.detailPrototypes.Length} trees={td.treePrototypes.Length} " +
                          $"instances={td.treeInstanceCount}");

            foreach (DetailPrototype d in td.detailPrototypes)
                sb.AppendLine($"      detail proto={(d.prototype != null ? d.prototype.name : "texture-based")} " +
                              $"mats=[{MaterialNames(d.prototype)}]");

            foreach (TreePrototype tp in td.treePrototypes)
                sb.AppendLine($"      tree proto={(tp.prefab != null ? tp.prefab.name : "null")} " +
                              $"mats=[{MaterialNames(tp.prefab)}]");
        }
        Debug.Log($"[{LogTag}] TERRAIN\n{sb}");

        EditorApplication.Exit(0);
    }

    /// Repoints every use of an ungated-emissive material at a project-owned copy with the emission
    /// off. Idempotent: an owned copy already reads intensity 0, so a second pass matches nothing.
    ///
    /// Returns the number of material uses repointed, so the caller can assert it actually did work.
    /// Silently finding nothing is the failure mode that matters here, because the frame would still
    /// glow and the cause would look like something else.
    public static int Apply()
    {
        var owned = new Dictionary<Material, Material>();
        int rendererSlots = 0, renderersTouched = 0, protoSlots = 0;

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material fixedMat = OwnedMaterial(mats[i], owned);
                if (fixedMat == null) continue;
                mats[i] = fixedMat;
                changed = true;
                rendererSlots++;
            }
            if (!changed) continue;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
            renderersTouched++;
        }

        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            TerrainData td = t.terrainData;
            if (td == null) continue;

            // Prototype ORDER and COUNT are preserved. Every placed tree instance refers to its
            // prototype by index, so reordering or dropping one would move or delete vegetation.
            TreePrototype[] protos = td.treePrototypes;
            bool changed = false;
            for (int i = 0; i < protos.Length; i++)
            {
                GameObject swapped = OwnedPrefab(protos[i].prefab, owned);
                if (swapped == null) continue;
                protos[i].prefab = swapped;
                changed = true;
                protoSlots++;
            }
            if (!changed) continue;

            td.treePrototypes = protos;
            td.RefreshPrototypes();
            t.Flush();
            EditorUtility.SetDirty(td);
            EditorUtility.SetDirty(t);
        }

        AssetDatabase.SaveAssets();

        // Worded as "this pass" deliberately. Owned copies survive between ladder rungs, so a later
        // rung legitimately reports fewer materials than the first: the rest were already owned and had
        // nothing left to zero. Reading that as "only 2 of the 4 got fixed" would send the next reader
        // hunting a bug that is not there.
        string listing = owned.Count == 0
            ? "none (all already owned)"
            : string.Join(", ", owned.Select(kv => kv.Key.name));
        Debug.Log($"[{LogTag}] this pass zeroed {owned.Count} material(s): {listing}\n" +
                  $"  repointed {rendererSlots} renderer slot(s) across {renderersTouched} renderer(s) " +
                  $"and {protoSlots} terrain tree prototype(s) onto owned copies in {OwnedDir}");

        return rendererSlots + protoSlots;
    }

    /// The GATED emissive family, correctly spelled, which the pack's lamp glass uses.
    public const string LampGateProp = "_Emissive_1";
    public const string LampIntensityProp = "_Emissive_Intensity";

    /// Brings the lamp glass down from its daylight value.
    ///
    /// The 21 lamp posts ship `_Emissive_1: 1` at intensity 21.95. That is the same class of value as the
    /// grass's 2.21: sized to register against a 2000 lux sun in a daylight showcase, and ten times
    /// larger. Unlike the grass this emission is deliberate, the lamps are meant to glow, so it is scaled
    /// rather than zeroed.
    ///
    /// Purchased materials are untouched; this repoints onto owned copies exactly like the foliage.
    public static int DimLamps(float target)
    {
        var owned = new Dictionary<Material, Material>();
        int slots = 0;

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                if (!m.HasProperty(LampGateProp) || m.GetFloat(LampGateProp) < 0.5f) continue;
                if (!m.HasProperty(LampIntensityProp) || m.GetFloat(LampIntensityProp) <= target) continue;

                if (!owned.TryGetValue(m, out Material copy))
                {
                    float was = m.GetFloat(LampIntensityProp);
                    copy = LoadOrCopy<Material>(m, ".mat");
                    copy.SetFloat(LampIntensityProp, target);
                    EditorUtility.SetDirty(copy);
                    owned[m] = copy;
                    Debug.Log($"[{LogTag}] lamp '{m.name}' emissive {was:0.##} -> {target:0.##} " +
                              $"at {AssetDatabase.GetAssetPath(copy)}");
                }
                mats[i] = copy;
                changed = true;
                slots++;
            }
            if (!changed) continue;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[{LogTag}] dimmed {owned.Count} lamp material(s) across {slots} slot(s)");
        return slots;
    }

    static bool Affected(Material m) =>
        m != null && m.HasProperty(IntensityProp) && m.GetFloat(IntensityProp) > 0f;

    /// Every still-emitting material reachable in the open scene, by both routes. The scene contract
    /// audit uses this, so the check that guards a build is the same code that performs the fix rather
    /// than a second implementation that can drift away from it.
    public static Material[] RemainingEmitters()
    {
        IEnumerable<Material> fromRenderers = Object
            .FindObjectsByType<Renderer>(FindObjectsInactive.Include)
            .SelectMany(r => r.sharedMaterials);

        IEnumerable<Material> fromTerrain = Object
            .FindObjectsByType<Terrain>(FindObjectsInactive.Include)
            .Where(t => t.terrainData != null)
            .SelectMany(t => t.terrainData.treePrototypes
                .Concat<object>(t.terrainData.detailPrototypes.Cast<object>())
                .Select(p => p is TreePrototype tp ? tp.prefab : ((DetailPrototype)p).prototype)
                .Where(go => go != null)
                .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r => r.sharedMaterials));

        return fromRenderers.Concat(fromTerrain).Where(Affected).Distinct().ToArray();
    }

    /// Copies an affected material into project-owned space with the emission zeroed, once per
    /// original. Returns null when the material is not one of ours to fix, so callers can treat null
    /// as "leave this slot alone".
    static Material OwnedMaterial(Material src, Dictionary<Material, Material> cache)
    {
        if (!Affected(src)) return null;
        if (cache.TryGetValue(src, out Material hit)) return hit;

        Material copy = LoadOrCopy<Material>(src, ".mat");
        copy.SetFloat(IntensityProp, 0f);
        if (copy.HasProperty(TintProp)) copy.SetColor(TintProp, Color.black);
        EditorUtility.SetDirty(copy);

        cache[src] = copy;
        Debug.Log($"[{LogTag}] owned material '{src.name}' " +
                  $"intensity {src.GetFloat(IntensityProp):0.00} -> 0 at {AssetDatabase.GetAssetPath(copy)}");
        return copy;
    }

    /// Owned copy of a terrain tree prototype prefab, with its renderers repointed at owned materials.
    /// Returns null when nothing in the prefab is affected, which is how the three clean prototypes
    /// keep pointing at the purchased prefab.
    static GameObject OwnedPrefab(GameObject src, Dictionary<Material, Material> cache)
    {
        if (src == null) return null;
        if (!src.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(Affected))
            return null;

        GameObject copy = LoadOrCopy<GameObject>(src, ".prefab");
        string copyPath = AssetDatabase.GetAssetPath(copy);

        // Prefab asset contents cannot be edited through the loaded root directly; they have to be
        // opened, modified and saved back.
        GameObject contents = PrefabUtility.LoadPrefabContents(copyPath);
        try
        {
            int swaps = 0;
            foreach (Renderer r in contents.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material fixedMat = OwnedMaterial(mats[i], cache);
                    if (fixedMat == null) continue;
                    mats[i] = fixedMat;
                    changed = true;
                    swaps++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            // An owned prefab reused from an earlier rung correctly reports zero swaps, because its
            // renderers already point at owned materials. What must never happen is reusing a copy that
            // still has an emitter in it: that would ship a glowing prototype while the log claimed
            // success. Cheap to assert, and this project has already lost a build to a stale asset that
            // looked fine.
            Material[] leftover = contents.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(Affected).Distinct().ToArray();
            if (leftover.Length > 0)
                throw new System.InvalidOperationException(
                    $"owned prototype {copyPath} still uses emissive material(s) " +
                    $"{string.Join(", ", leftover.Select(m => m.name))}. Delete {OwnedDir} and rerun.");

            PrefabUtility.SaveAsPrefabAsset(contents, copyPath);
            Debug.Log($"[{LogTag}] owned prototype prefab '{src.name}' ({swaps} material slot(s) " +
                      $"swapped this pass) at {copyPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(copyPath);
    }

    /// Returns the owned copy of a purchased asset, reusing it if a previous run already made it.
    static T LoadOrCopy<T>(Object src, string extension) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(OwnedDir))
        {
            string parent = System.IO.Path.GetDirectoryName(OwnedDir).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(OwnedDir));
        }

        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath))
            throw new System.InvalidOperationException($"'{src.name}' has no asset path, cannot own a copy of it");

        string dstPath = $"{OwnedDir}/{System.IO.Path.GetFileNameWithoutExtension(srcPath)}{extension}";

        var existing = AssetDatabase.LoadAssetAtPath<T>(dstPath);
        if (existing != null) return existing;

        if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            throw new System.InvalidOperationException($"failed to copy {srcPath} -> {dstPath}");
        AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport);

        var copy = AssetDatabase.LoadAssetAtPath<T>(dstPath);
        if (copy == null)
            throw new System.InvalidOperationException($"copy of {srcPath} missing at {dstPath}");
        return copy;
    }

    static string MaterialNames(GameObject prefab)
    {
        if (prefab == null) return "";
        return string.Join(", ", prefab.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null)
            .Select(m => m.HasProperty(IntensityProp) ? m.name + "*UNGATED*" : m.name)
            .Distinct());
    }
}
