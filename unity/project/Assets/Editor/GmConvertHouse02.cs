// Gives SM_House_02 (the coach house at z=50) a real textured HDRP material.
//
// HISTORY: HauntedVillage is the one Leartes pack with no HDRP folder, so SM_House_02 has no HDRP
// twin -- GmEstateBuilderV2.PreferHdrp() correctly logs EXPECT MAGENTA and falls back.
// GmHdrpMaterialConverter.ConvertInPlace already flipped its resolved material (MI_VP_Base_Inst1.mat)
// from Standard to HDRP/Lit, so it stopped being magenta -- but the ORIGINAL Standard material had no
// _MainTex assigned (a flat grey placeholder from the pack), so the conversion had nothing to copy and
// the house went from magenta straight to flat white. GmHouse02Fix.RetextureWalls (below) finishes
// that half: it authors real wood wall textures onto that material, read from DISK (via AssetDatabase,
// not from the material) because a flat already-converted material has no _MainTex to copy -- reading
// off it would keep producing flat white forever.
//
// WHY EDIT MI_VP_Base_Inst1.mat DIRECTLY (not an importer externalObjects remap): the FBX's material
// location is ModelImporterMaterialLocation.External, which Unity 6 logs as obsolete and no longer
// populates ModelImporter.GetExternalObjectMap() for -- confirmed empty (count 0) against this exact
// asset. ModelImporter.sourceMaterials also does not exist as a public property on this Editor version
// (confirmed via reflection: get_sourceMaterials exists only as an internal binding). So there is no
// live "material slot" list to remap against. What DOES work, and is what this does: walk the FBX's own
// renderers to find which material it actually resolves to (all four LODs point at the same
// MI_VP_Base_Inst1.mat instance) and texture that asset in place.
//
// SAFE TO EDIT IN PLACE, VERIFIED, NOT A "SHARED PACK MATERIAL": grepped MI_VP_Base_Inst1.mat's own
// GUID across the entire Assets/ tree -- it appears nowhere except its own .mat/.meta. SM_House_02 is
// its only user.
//
// SECOND, INDEPENDENT BUG FOUND WHILE VERIFYING THE FIRST: retexturing the walls left the roof and one
// big wall panel still reading flat white in tour-11-coach-yard.png -- and neither a UV-checker swap
// nor a blown-out emissive swap on MI_VP_Base_Inst1 changed those faces AT ALL. They are not part of
// that material, or even of this mesh's four LODs. Direct inspection found the real cause:
// SM_House_02_ConvexHulls (Unity's auto-generated convex collision proxy for this FBX) has an ENABLED
// MeshRenderer using the pack's generic blank "No Name" material, its bounds nearly exactly matching
// the whole building, and it is NOT registered in the LODGroup's renderer lists -- so it renders
// unconditionally, forever, on top of the real mesh, regardless of camera distance. That flat pale mass
// IS the convex hull, not the wall. "No Name.mat" genuinely is a shared pack material (its GUID collides
// with ~40 other packs' extracted-but-unnamed materials -- confirmed by grep), so it is never touched;
// GmHouse02ConvexHullFix instead disables just this one FBX's stray renderer via OnPostprocessModel,
// the same importer-hook pattern GmModelImportSettings already uses elsewhere in this project. The
// MeshCollider stays enabled -- only the renderer was ever the bug.
//
// Menu: GamesMaster -> Fix SM_House_02 Materials   (or -executeMethod GmConvertHouse02.Run)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

public static class GmConvertHouse02
{
    const string Fbx = "Assets/LeartesStudios/HauntedVillage/Art/Meshes/SM_House_02@000001696C6F9D00.fbx";
    const string TargetMaterialName = "MI_VP_Base_Inst1";

    [MenuItem("GamesMaster/Fix SM_House_02 Materials")]
    public static void Run()
    {
        RetextureWalls();

        // The convex-hull fix lives in an AssetPostprocessor (OnPostprocessModel), which only runs
        // when the model actually goes through import. Force that now so the fix is live immediately
        // rather than waiting for some unrelated future reimport to trigger it.
        AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }

    static void RetextureWalls()
    {
        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null) { Fail("HDRP/Lit shader not found — is the pipeline assigned?"); return; }

        var baseTex = FindTex("T_WoodWall1_B") ?? FindTex("T_WoodWall_B");
        var normTex = FindTex("T_WoodWall1_N") ?? FindTex("T_WoodWall_N");
        if (baseTex == null) { Fail("no T_WoodWall*_B texture found in the pack — cannot texture the house"); return; }

        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (go == null) { Fail($"could not load FBX root GameObject at {Fbx}"); return; }

        // Walk the FBX's own renderers rather than trusting importer material-slot APIs: this asset's
        // materialLocation is External, which Unity 6 no longer surfaces through
        // GetExternalObjectMap()/sourceMaterials (see header). The renderers themselves are the one
        // source of truth for what this FBX actually resolves to at runtime.
        var targets = new HashSet<Material>();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name == TargetMaterialName)
                    targets.Add(m);

        if (targets.Count == 0)
        {
            Fail($"no renderer on {Fbx} resolves a material named '{TargetMaterialName}' — has the pack layout changed? Refusing to guess.");
            return;
        }

        int fixedCount = 0;
        foreach (var mat in targets)
        {
            var path = AssetDatabase.GetAssetPath(mat);
            Debug.Log($"[GmHouse02] slot '{mat.name}' ({path}): base='{baseTex.name}' normal='{(normTex != null ? normTex.name : "none")}'");

            if (mat.shader != hdrpLit) mat.shader = hdrpLit;   // GmHdrpMaterialConverter already did this; belt & suspenders
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseColorMap", baseTex);
            if (normTex != null)
            {
                // Leartes normals ship without the isReadable/Normal-map import flag we might want,
                // but assigning as _NormalMap + keyword is correct for HDRP/Lit regardless.
                mat.SetTexture("_NormalMap", normTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", 0.15f);   // weathered coach-house wood, not wet plastic

            // Fully define emission rather than leaving it ambiguous: guards against this material
            // ever being left mid-diagnostic (e.g. a manual test swap) with a stray emissive glow.
            mat.SetColor("_EmissiveColor", Color.black);
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            // The official reset path: derives _NORMALMAP / _NORMALMAP_TANGENT_SPACE etc. from which
            // texture slots are actually populated. Skipping this is exactly how a material ends up
            // non-magenta but still flat/unlit -- the shader compiles fine but the sampling keyword for
            // the map was never turned on.
            if (!HDShaderUtils.ResetMaterialKeywords(mat))
                Debug.LogWarning($"[GmHouse02] {mat.name}: ResetMaterialKeywords did not recognise the shader");

            EditorUtility.SetDirty(mat);
            fixedCount++;
        }

        // Save-or-it-didn't-happen: a material edit that is not persisted dies with this process.
        AssetDatabase.SaveAssets();
        Debug.Log($"[GmHouse02] DONE — {fixedCount} material(s) textured with real wood maps");
    }

    static Texture FindTex(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{name} t:texture"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.Contains("HauntedVillage")) continue;
            if (Path.GetFileNameWithoutExtension(p) != name) continue;
            return AssetDatabase.LoadAssetAtPath<Texture>(p);
        }
        return null;
    }

    static void Fail(string msg)
    {
        Debug.LogError($"[GmHouse02] FAILED: {msg}");
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}

/// Disables the stray visible MeshRenderer on SM_House_02's auto-generated convex-hull collision
/// proxy. Scoped to this one FBX path by construction (an exact-path guard, not a name heuristic), so
/// it cannot reach any other asset's convex hull -- see the header comment on GmConvertHouse02 above
/// for the discovery. Same OnPostprocess* importer-hook pattern as GmModelImportSettings.cs.
public class GmHouse02ConvexHullFix : AssetPostprocessor
{
    const string TargetFbxPath = "Assets/LeartesStudios/HauntedVillage/Art/Meshes/SM_House_02@000001696C6F9D00.fbx";
    const string ConvexHullObjectName = "SM_House_02_ConvexHulls";

    void OnPostprocessModel(GameObject g)
    {
        if (assetPath != TargetFbxPath) return;

        int disabled = 0;
        foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name != ConvexHullObjectName) continue;
            mr.enabled = false;   // collision-only proxy: the MeshCollider (untouched) still works fine
            disabled++;
        }
        Debug.Log(disabled > 0
            ? $"[GmHouse02] convex-hull renderer disabled on {ConvexHullObjectName} ({disabled} found)"
            : $"[GmHouse02] WARNING: expected a '{ConvexHullObjectName}' renderer on {TargetFbxPath} but found none — has the mesh hierarchy changed?");
    }
}

