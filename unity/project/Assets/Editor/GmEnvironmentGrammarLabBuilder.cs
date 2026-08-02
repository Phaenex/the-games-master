// Compatibility surface for retired environment experiments. These commands remain callable for
// old diagnostic scripts but cannot report release success.
using UnityEngine;

public static class GmEnvironmentGrammarLabBuilder
{
    public const string ScenePath = "Assets/Scenes/EnvironmentGrammarLab.unity";
    public const string TreeValidationScenePath = "Assets/Scenes/EnvironmentGrammarTrees.unity";
    public const string SourceValidationScenePath = "Assets/Scenes/EnvironmentGrammarSources.unity";

    public static void PrepareTransferAssets() =>
        Debug.LogWarning("[GmEnvironmentGrammar] retired experiment; no assets changed");
    public static void BuildAndSave() => PrepareTransferAssets();
    public static void BuildTreeValidationAndSave() => PrepareTransferAssets();
    public static void BuildSourceValidationAndSave() => PrepareTransferAssets();
    public static bool ValidateTransferBudgets()
    {
        Debug.LogWarning("[GmEnvironmentGrammar] retired experiment has no active quality gate");
        return false;
    }
    public static GameObject[] LoadTransferTerrainFoliage() => System.Array.Empty<GameObject>();

    public static void PositionCamera(Camera camera, float z, float targetZ, float lateral)
    {
        if (camera == null) return;
        camera.transform.position = new Vector3(lateral, 1.7f, z);
        camera.transform.LookAt(new Vector3(0f, 1.7f, targetZ));
    }

    public static void PositionCameraSide(Camera camera, float z, bool right)
    {
        if (camera == null) return;
        camera.transform.position = new Vector3(right ? 8f : -8f, 1.7f, z);
        camera.transform.LookAt(new Vector3(0f, 1.7f, z - 10f));
    }
}
