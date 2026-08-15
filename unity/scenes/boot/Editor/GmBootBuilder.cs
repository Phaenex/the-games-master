// Generated scene entry point. Keep construction deterministic and split large passes into
// named methods. Do not hand-edit the generated .unity file as the source of truth.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class GmBootBuilder
{
    public const string SceneId = "boot";
    public const string DisplayName = "Boot";
    public const string ScenePath = "Assets/Scenes/Boot.unity";

    [MenuItem("GamesMaster/Scenes/Rebuild Boot")]
    public static void Build()
    {
        Scene scene = GmSceneBuildUtility.CreateEmptyScene();
        var systems = new GameObject("SceneSystems");
        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);
        new GameObject("Environment");
        new GameObject("Gameplay");
        new GameObject("Lighting");

        var composition = new GameObject("Composition");
        GmBootCompositionPlan.Author(composition);

        var cameraRig = new GameObject("ReviewCamera");
        cameraRig.transform.position = new Vector3(0f, 1.7f, -6f);
        cameraRig.AddComponent<Camera>();
        cameraRig.AddComponent<HDAdditionalCameraData>();
        cameraRig.AddComponent<AudioListener>();
        cameraRig.tag = "MainCamera";
        systems.AddComponent<GmBootShotTour>();

        // The menu, and with it the scene director. This is the object the whole scene exists for:
        // until something instantiated GmSceneDirector, no transition could run and no ending could
        // resolve, which is why six authored endings had never once been reached in a playthrough.
        systems.AddComponent<GmBootMenu>();
        GmSceneBuildUtility.SaveScene(scene, ScenePath);
        Debug.Log("[GmBoot] BUILD PASS: " + ScenePath);
    }
}
