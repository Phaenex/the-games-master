// Shared mechanics for deterministic scene builders. Room builders own their content; this utility
// owns new-scene creation, durable identity, safe save and enabled build-settings registration.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmSceneBuildUtility
{
    const string ReviewSceneArgument = "-gmReviewScene";

    public static Scene CreateEmptyScene()
    {
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    public static GmSceneIdentity MarkScene(GameObject owner, string sceneId, string displayName)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!owner.scene.IsValid()) throw new ArgumentException("identity owner must belong to a scene", nameof(owner));
        var identity = owner.GetComponent<GmSceneIdentity>() ?? owner.AddComponent<GmSceneIdentity>();
        identity.Configure(sceneId, displayName);
        return identity;
    }

    public static void SaveScene(Scene scene, string scenePath)
    {
        if (!scene.IsValid()) throw new ArgumentException("cannot save an invalid scene", nameof(scene));
        RequireSafeScenePath(scenePath, nameof(scenePath));

        string directory = Path.GetDirectoryName(scenePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new InvalidOperationException($"Unity failed to save scene '{scenePath}'");
        EnsureBuildScene(scenePath);
        AssetDatabase.SaveAssets();
    }

    public static void EnsureBuildScene(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int existing = scenes.FindIndex(scene => scene.path == scenePath);
        if (existing < 0) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        else scenes[existing] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Safe human-review entry point. Unlike a screenshot tour, this deliberately clears tour arms,
    // opens the requested authored scene, selects Game view and leaves the editor running in Play
    // mode. The command-line path comes from the validated JavaScript scene registry.
    public static void LaunchPlayReviewFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, ReviewSceneArgument);
        if (flag < 0 || flag + 1 >= args.Length)
            throw new ArgumentException($"missing {ReviewSceneArgument} <Assets/Scenes/name.unity>");
        LaunchPlayReview(args[flag + 1]);
    }

    [MenuItem("GamesMaster/Play Current Scene (No Tour)")]
    public static void LaunchCurrentSceneReview()
    {
        string scenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrWhiteSpace(scenePath))
            throw new InvalidOperationException("save the current scene below Assets/Scenes before review");
        LaunchPlayReview(scenePath);
    }

    static void LaunchPlayReview(string scenePath)
    {
        RequireSafeScenePath(scenePath, nameof(scenePath));
        if (!File.Exists(scenePath)) throw new FileNotFoundException("review scene does not exist", scenePath);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Unity is already entering or running Play mode");

        var active = SceneManager.GetActiveScene();
        if (active.path != scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // A recently completed automated tour persists a short-lived arm across its domain reload.
        // Human review must never inherit that arm or the tour will take over and close the editor.
        SessionState.SetBool(GmSceneReviewTour.ArmKey, false);
        EditorPrefs.DeleteKey(GmSceneReviewTour.ArmKey);
        foreach (var tour in UnityEngine.Object.FindObjectsByType<GmSceneReviewTour>(FindObjectsInactive.Include))
            tour.runOnPlay = false;

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        Debug.Log($"[GmSceneReview] ENTERING PLAY: {scenePath} (tour disabled)");
        EditorApplication.isPlaying = true;
    }

    static void RequireSafeScenePath(string scenePath, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.StartsWith("Assets/Scenes/") ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || scenePath.Contains(".."))
            throw new ArgumentException("scene path must be a safe .unity path below Assets/Scenes", parameterName);
    }
}
