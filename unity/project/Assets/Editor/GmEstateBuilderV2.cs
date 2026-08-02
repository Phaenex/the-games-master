// Legacy Wend Hill snapshot compatibility. The original generator is intentionally retired now that
// wend-hill-prologue is canonical; this facade keeps the preserved scene and its archaeology tests
// readable without pretending the old generator is a release path.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmEstateBuilderV2
{
    public const string SceneId = "wend-hill";
    public const string DisplayName = "Wend Hill";
    public const string ScenePath = "Assets/Scenes/WendHill.unity";

    [MenuItem("GamesMaster/Legacy/Open preserved Wend Hill snapshot")]
    public static void Build()
    {
        if (AssetDatabase.LoadMainAssetAtPath(ScenePath) == null)
            throw new InvalidOperationException($"legacy scene snapshot is missing: {ScenePath}");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[GmV2] LEGACY SNAPSHOT OPENED — no regeneration performed; wend-hill-prologue is canonical");
    }

    internal static GameObject FindAssetPrefab(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (string guid in AssetDatabase.FindAssets($"t:GameObject {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
                continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null) return asset;
        }
        return null;
    }
}
