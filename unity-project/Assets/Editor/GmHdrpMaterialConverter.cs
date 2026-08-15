// Route-2 fix for assets that have no HDRP twin anywhere in the imported packs. PreferHdrp (see
// GmEstateBuilderV2) tries route 1 first -- prefer an existing HDRP-flavoured copy of the same asset,
// which is how the Leartes packs ship duplicates under the same name. That structurally cannot help
// an asset that never shipped an HDRP twin at all: SM_House_02 is that case in this project (every
// other placed prop has a real HDRP copy somewhere in the imported packs). This converts the built-in
// Standard material(s) found on such an asset to HDRP/Lit in place -- same GUID, same file, so every
// existing reference (the FBX's external material link) keeps working with no other change needed.
//
// SCOPED BY CONSTRUCTION, not by a name list: GmEstateBuilderV2.PreferHdrp only calls
// ConvertAllBuiltinMaterials() from its fallback branch, which only runs when hdrp.Count == 0 for that
// asset -- i.e. no HDRP-flavoured candidate exists ANYWHERE in the project for it. WitchVillage's 141
// built-in materials never reach this code: PreferHdrp already finds their real HDRP twins and prefers
// those, so this path is structurally unreachable for them. Converting those in place would create
// duplicates that diverge from the pack's own maintained HDRP twins -- this cannot do that, by
// construction, not by convention. Do not call ConvertAllBuiltinMaterials from anywhere else.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

public static class GmHdrpMaterialConverter
{
    /// Same test GmEstateBuilderV2.IsBuiltinShader uses: built-in shaders resolve to no asset path
    /// (or one under Unity's own resource bundles); everything else -- Packages/, Assets/ -- is real.
    static bool IsHdrp(Material m)
    {
        if (m == null || m.shader == null) return false;
        var p = AssetDatabase.GetAssetPath(m.shader);
        return !string.IsNullOrEmpty(p) && !p.StartsWith("Resources/") && !p.StartsWith("Library/");
    }

    /// Converts one material asset's shader and the three properties the plan calls out explicitly
    /// (_BaseColorMap<-_MainTex, _NormalMap<-_BumpMap, _BaseColor<-_Color), handling any of the three
    /// being absent. Idempotent: forces a reimport first so a material edited outside the running
    /// Editor process (e.g. a hand edit from an earlier session) can't leave a stale Library-cached
    /// "still built-in" read behind, then no-ops if the shader already resolves as HDRP.
    public static bool ConvertInPlace(Material mat)
    {
        if (mat == null) return false;
        var path = AssetDatabase.GetAssetPath(mat);
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            mat = AssetDatabase.LoadAssetAtPath<Material>(path); // ForceUpdate can hand back a fresh instance
        }
        if (mat == null) return false;

        if (IsHdrp(mat))
        {
            Debug.Log($"[GmHdrpConvert] {mat.name}: already HDRP ({mat.shader.name}), no conversion needed");
            return true;
        }

        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null) { Debug.LogError("[GmHdrpConvert] HDRP/Lit shader not found -- is HDRP installed?"); return false; }

        // Read the built-in Standard properties before the shader swap; handle any being absent.
        var mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        var bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        Color? color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : (Color?)null;

        mat.shader = hdrpLit;
        if (mainTex != null) mat.SetTexture("_BaseColorMap", mainTex);
        if (bumpMap != null) mat.SetTexture("_NormalMap", bumpMap);
        if (color.HasValue) mat.SetColor("_BaseColor", color.Value);

        // The official reset path: derives _NORMALMAP / _NORMALMAP_TANGENT_SPACE etc. from which
        // texture slots are actually populated. Setting shader + textures via script and skipping
        // this is exactly how a converted material ends up non-magenta but still flat/unlit -- the
        // shader compiles fine but the sampling keyword for the map was never turned on.
        if (!HDShaderUtils.ResetMaterialKeywords(mat))
            Debug.LogWarning($"[GmHdrpConvert] {mat.name}: ResetMaterialKeywords did not recognise the shader");

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GmHdrpConvert] {mat.name}: converted Standard -> HDRP/Lit " +
                  $"(baseColorMap={mainTex != null}, normalMap={bumpMap != null}, baseColor={color.HasValue})");
        return true;
    }

    /// Called from GmEstateBuilderV2.PreferHdrp's fallback branch -- see that method, and the header
    /// comment above, for why this is safe to run unconditionally rather than gated on an asset name.
    public static int ConvertAllBuiltinMaterials(List<GameObject> prefabs)
    {
        var seen = new HashSet<Material>();
        int converted = 0;
        foreach (var prefab in prefabs)
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m) || IsHdrp(m)) continue;
                    if (ConvertInPlace(m)) converted++;
                }
        return converted;
    }

    // ------------------------------------------------------------------------------------------
    // graveyard_gate: a SECOND, distinct failure mode from SM_House_02's. Confirmed against
    // cli-rebuild.log (Jul 17 04:07): "graveyard_gate" produces ZERO [GmV2] PreferHdrp log output --
    // neither the "using N HDRP asset(s)" success line nor the "no HDRP variant exists, EXPECT
    // MAGENTA" warning, both of which are unconditional whenever hdrp.Count < all.Count or == 0.
    // The only code path that logs nothing is hdrp.Count == all.Count, i.e. RendersUnderHdrp()
    // ALREADY reports every material on graveyard_gate.fbx as non-builtin-shader before this file's
    // ConvertAllBuiltinMaterials ever runs. IsHdrp()/RendersUnderHdrp() only check shader IDENTITY
    // (does AssetDatabase.GetAssetPath resolve outside Resources/Library), never property sanity --
    // a material can carry a real "HDRP/Lit" shader reference while _BaseColor and _BaseColorMap sit
    // at the shader's own unset defaults (white, white) and no HDRP setup keyword was ever applied.
    // That reads as exactly what the review found in tour-03-gate.png: not magenta (real HDRP shader,
    // so the detection is satisfied), but flat, textureless, unnaturally bright white (nothing was
    // ever actually configured on it). Because the shader already reads as "HDRP", this state is
    // invisible to PreferHdrp's fallback AND to ConvertInPlace's own "if (IsHdrp(mat)) return" skip --
    // both would happily leave it exactly as broken on every future rebuild, forever.
    //
    // This method does not gate on IsHdrp() at all -- "already looks HDRP" is precisely the state
    // being fixed here. Values come from the authoritative source, not from reading the (already
    // possibly-clobbered) live material: assets/models/sourced/graveyard_gate.glb declares all three
    // materials with pbrMetallicRoughness.baseColorFactor [0.5, 0.5, 0.5, 1], metallic 0, roughness 1,
    // and zero images/textures -- confirmed by direct inspection of the glTF JSON chunk this session.
    // Scoped to this one named asset by construction (a fixed lookup, not a shared heuristic), so it
    // cannot reach WitchVillage or any other placement the way a change to PreferHdrp's shared
    // detection logic could.
    //
    // The car (RealisticCar03_HD_Exterior_LOD0) shows the identical zero-log-output signature in the
    // same rebuild log, but the review's direct visual check of tour-02-car.png called it correctly
    // lit and fine -- so it is deliberately NOT included here. Forcing flat known-correct values onto
    // six already-good car materials on the strength of a log-silence pattern alone, with no visual
    // evidence anything is wrong, is a risk this fix has no reason to take. If a future verified tour
    // ever shows the car regressed to flat/white too, extend GateBaseColors-style table for it then --
    // do not pre-emptively touch a placement the reviewer already signed off on.
    static readonly Dictionary<string, Color> GraveyardGateBaseColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
    {
        { "IronBars_GraveyardGate",       new Color(0.5f, 0.5f, 0.5f, 1f) },
        { "IronBarsScreen_GraveyardGate", new Color(0.5f, 0.5f, 0.5f, 1f) },
        { "Bricks_GraveyardGate",         new Color(0.5f, 0.5f, 0.5f, 1f) },
        // Current Blender export consolidates the three source materials into one external HDRP
        // material. Treating that valid renamed output as "0 matched" made every healthy rebuild
        // announce a false failure even though the gate rendered correctly.
        { "HDRP_WroughtIron",              new Color(0.055f, 0.050f, 0.045f, 1f) },
    };

    /// Called unconditionally from GmEstateBuilderV2.Build() every rebuild -- see the header comment
    /// above for why this cannot gate on "already HDRP" the way ConvertAllBuiltinMaterials safely can
    /// for SM_House_02. Idempotent: re-applying the same known-correct values and re-resetting
    /// keywords on an already-fixed material is a harmless no-op, so running it every build is safe.
    public static int ForceFixGraveyardGate()
    {
        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null) { Debug.LogError("[GmHdrpConvert] HDRP/Lit shader not found -- is HDRP installed?"); return 0; }

        int fixedCount = 0;
        var seen = new HashSet<Material>();
        foreach (var guid in AssetDatabase.FindAssets("graveyard_gate t:model"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), "graveyard_gate", StringComparison.OrdinalIgnoreCase))
                continue;

            // Force a fresh read of the embedded materials before touching them, same reasoning as
            // ConvertInPlace: whatever left the shader already-HDRP happened inside a cached import,
            // and a stale Library read should not be trusted over the source-of-truth values below.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m)) continue;
                    if (!GraveyardGateBaseColors.TryGetValue(m.name, out var baseColor))
                    {
                        Debug.LogWarning($"[GmHdrpConvert] graveyard_gate: unrecognised material '{m.name}' -- not in the known table, left untouched");
                        continue;
                    }
                    m.shader = hdrpLit;
                    m.SetColor("_BaseColor", baseColor);
                    if (!HDShaderUtils.ResetMaterialKeywords(m))
                        Debug.LogWarning($"[GmHdrpConvert] {m.name}: ResetMaterialKeywords did not recognise the shader");
                    EditorUtility.SetDirty(m);
                    fixedCount++;
                }
        }
        if (fixedCount > 0) AssetDatabase.SaveAssets();
        Debug.Log(fixedCount > 0
            ? $"[GmHdrpConvert] graveyard_gate: force-set {fixedCount} material(s) to known-correct HDRP/Lit values (bypassed already-HDRP skip)"
            : "[GmHdrpConvert] graveyard_gate: FAILED — 0 materials matched, asset missing or renamed");
        return fixedCount;
    }

    /// Standalone entry point -- callable via `-executeMethod GmHdrpMaterialConverter.ConvertOffendingAssets`
    /// and as a menu item for manual use in the editor. The automatic hook in PreferHdrp means a normal
    /// `rebuild` fixes this without a separate step; this stays so the known offender can be converted
    /// (and verified) in isolation, e.g. before a rebuild, or if the pack ever adds another one.
    static readonly string[] KnownOffendingMaterialPaths =
    {
        "Assets/LeartesStudios/HauntedVillage/Art/Meshes/Materials/MI_VP_Base_Inst1.mat", // SM_House_02
    };

    [MenuItem("GamesMaster/Convert Offending Materials To HDRP")]
    public static void ConvertOffendingAssets()
    {
        int ok = 0;
        foreach (var path in KnownOffendingMaterialPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { Debug.LogError($"[GmHdrpConvert] no material at {path}"); continue; }
            if (ConvertInPlace(mat)) ok++;
        }
        Debug.Log($"[GmHdrpConvert] DONE — {ok}/{KnownOffendingMaterialPaths.Length} offending material(s) verified HDRP");
    }
}

