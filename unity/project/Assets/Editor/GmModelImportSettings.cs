// Forces raw-unit import for our own converted models.
//
// WHY THIS EXISTS: scripts/gltf-to-fbx-blender.py (web repo) exports geometry already sized in
// metres, but the FBX it writes declares its unit as centimetres. Unity believes the declaration and
// applies fileScale=0.01, so a 16 x 17 x 13 metre mansion imports as a 0.16 x 0.17 x 0.13 speck --
// present, correctly positioned, and completely invisible. It looks like nothing rendered.
//
// Fixing it at the exporter was tried first (apply_unit_scale / apply_scale_options=FBX_SCALE_UNITS)
// and did not move Unity's fileScale. Rather than keep guessing at Blender flags, the unit
// declaration is overridden here: useFileScale=false makes Unity take the numbers at face value,
// which is what they are.
//
// SCOPED ON PURPOSE to Assets/GamesMaster/ -- the Leartes packs are authored correctly and must keep
// their own file scale. Do not widen this path.
using UnityEditor;

public class GmModelImportSettings : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/GamesMaster/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.useFileScale = false;   // the FBX's cm declaration is wrong; the numbers are metres
        importer.globalScale = 1f;
    }

    /// The postprocessor only runs on import, so an asset already in the project keeps its old
    /// settings until something forces it through again.
    ///
    /// Also extracts embedded textures. Blender writes them INTO the FBX (path_mode='COPY',
    /// embed_textures=True), but Unity does not unpack embedded media automatically -- every
    /// material silently falls back to white default, which reads as "the model imported wrong"
    /// when the geometry is in fact perfect.
    [MenuItem("GamesMaster/Reimport GamesMaster Models")]
    public static void ForceReimport()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:model", new[] { "Assets/GamesMaster" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var dir = System.IO.Path.GetDirectoryName(path);
            var texDir = System.IO.Path.Combine(dir, "Textures");
            System.IO.Directory.CreateDirectory(texDir);
            importer.ExtractTextures(texDir);
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                             ModelImporterMaterialSearch.Everywhere);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            int n = System.IO.Directory.Exists(texDir) ? System.IO.Directory.GetFiles(texDir, "*.png").Length : 0;
            UnityEngine.Debug.Log($"[GmModelImport] {path}: useFileScale=false, extracted {n} texture(s) -> {texDir}");
        }
        AssetDatabase.Refresh();
    }
}

