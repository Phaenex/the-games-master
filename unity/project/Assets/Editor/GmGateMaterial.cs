// Gives the driveway gate a real dark wrought-iron material.
//
// WHY: graveyard_gate.fbx imports with an untextured default material and renders BRIGHT WHITE -- the
// single most prominent object in the opening shot, reading as white plastic lattice instead of an
// iron estate gate. Wrought iron is near-uniform dark metal, so a good PBR material looks right with
// NO albedo texture at all; this authors one and remaps the gate's slot to it.
//
// Remap via the importer (externalObjects) so the gate's own embedded material is left alone and
// nothing else in the project is touched -- same pattern GmMansion/GmModelImportSettings use.
//
// Menu: GamesMaster -> Fix Gate Material   (or -executeMethod GmGateMaterial.Run)
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

public static class GmGateMaterial
{
    const string Fbx = "Assets/GamesMaster/Props/graveyard_gate.fbx";
    const string OutDir = "Assets/GamesMaster/Converted";

    [MenuItem("GamesMaster/Fix Gate Material")]
    public static void Run()
    {
        var importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (importer == null) { Fail($"no ModelImporter at {Fbx}"); return; }

        var hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null) { Fail("HDRP/Lit not found — pipeline not assigned?"); return; }

        var iron = new Material(hdrpLit) { name = "HDRP_WroughtIron" };
        // Weathered wrought iron: near-black with a faint cold-brown cast, fully metallic, low-ish
        // smoothness so it catches the moon as a dull sheen rather than a mirror.
        iron.SetColor("_BaseColor", new Color(0.035f, 0.032f, 0.030f));
        iron.SetFloat("_Metallic", 0.95f);
        iron.SetFloat("_Smoothness", 0.32f);
        iron.SetColor("_EmissiveColor", Color.black);
        iron.SetColor("_EmissionColor", Color.black);
        iron.DisableKeyword("_EMISSION");
        iron.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (!HDShaderUtils.ResetMaterialKeywords(iron))
            Debug.LogWarning("[GmGate] ResetMaterialKeywords did not recognise the shader");

        Directory.CreateDirectory(OutDir);
        var path = AssetDatabase.GenerateUniqueAssetPath($"{OutDir}/HDRP_WroughtIron.mat");
        AssetDatabase.CreateAsset(iron, path);

        // Remap every material slot the FBX declares to the iron material. A gate is one material.
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (go == null) { Fail($"could not load {Fbx}"); return; }
        int slots = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(m), iron);
                    slots++;
                }

        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        AssetDatabase.Refresh();
        Debug.Log($"[GmGate] DONE — remapped {slots} slot(s) on the gate to dark wrought iron ({path})");
        if (slots == 0) Debug.LogError("[GmGate] FAILED: gate FBX exposed no material slots to remap");
    }

    static void Fail(string msg)
    {
        Debug.LogError($"[GmGate] FAILED: {msg}");
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}

