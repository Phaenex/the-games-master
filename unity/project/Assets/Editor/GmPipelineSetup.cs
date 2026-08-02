// One-click HDRP activation: assigns Leartes' own HDRP asset as the project pipeline.
// In the editor: menu GamesMaster → Setup HDRP Pipeline. Headless: -executeMethod GmPipelineSetup.Apply
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class GmPipelineSetup
{
    const string LeartesAsset = "Assets/LeartesStudios/Settings/HDRP High Fidelity.asset";

    [MenuItem("GamesMaster/Setup HDRP Pipeline")]
    public static void Apply()
    {
        var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LeartesAsset);
        if (rp == null)
        {
            // fall back to any HDRP asset in the project
            foreach (var guid in AssetDatabase.FindAssets("t:RenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (candidate != null && candidate.GetType().Name.Contains("HDRenderPipelineAsset")) { rp = candidate; Debug.Log($"[GmPipelineSetup] using fallback {path}"); break; }
            }
        }
        if (rp == null) { Debug.LogError("[GmPipelineSetup] no HDRP RenderPipelineAsset found — is the Leartes pack imported?"); return; }

        GraphicsSettings.defaultRenderPipeline = rp;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = rp;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[GmPipelineSetup] HDRP assigned: {AssetDatabase.GetAssetPath(rp)} — if Unity offers to create HDRP Global Settings, accept.");
    }
}
