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

    /// Marks a flat prop that lies ON the floor and is meant to be walked over -- a rug, a gravel
    /// path, a painted track -- by taking its collider away.
    ///
    /// GameObject.CreatePrimitive attaches a collider to everything, so a two-centimetre rug ships as
    /// a two-centimetre kerb. The player does not stand on it, they stand IN it: the floor beneath is
    /// still the surface the controller rests on, and the rug's box swallows their ankles for as long
    /// as they are on it. Four of these shipped -- the Entry Hall runner, the Parlor carpet, and the
    /// Labyrinth's gravel path and patrol track -- and the last two were 6cm, which is a real lip a
    /// controller has to step up. The floor underneath already carries collision. A rug is not a step.
    public static void MakeDecorativeOverlay(GameObject overlay)
    {
        if (overlay == null) throw new ArgumentNullException(nameof(overlay));
        var collider = overlay.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    /// Dresses a blockout primitive in the owned Victorian interior kit's real materials.
    ///
    /// The Parlor and the Entry Hall shipped as 21 and 24 CreatePrimitive cubes with FLAT COLOURS and
    /// zero real meshes, while the prologue exterior runs on a purchased Victorian pack. They are the
    /// first two rooms a player reaches after the crossing, and they did not look like they belonged
    /// in the same game -- which is exactly what Nick said when he watched one.
    ///
    /// Tiling is derived from the object's real size rather than passed in, because a hand-tuned
    /// number per surface is a texture stretched on the first wall somebody resizes. Texel density
    /// stays constant across every surface in the house, which is most of what separates "a room"
    /// from "a grey box with a brown box next to it".
    public static void ApplyVictorianSurface(GameObject target, string family,
        float metresPerTile = 2f, Color? tint = null)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;

        // The two largest dimensions are the face the texture is seen on. A wall is thin in one axis
        // and a floor is thin in Y, so this picks the right pair without the caller declaring which.
        Vector3 size = target.transform.lossyScale;
        float a = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z));
        float b = Mathf.Abs(size.y) > Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.z))
            ? Mathf.Max(Mathf.Abs(size.y), Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.z)))
            : Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)) == Mathf.Abs(size.x)
                ? Mathf.Abs(size.z) : Mathf.Abs(size.x);
        var tiling = new Vector2(Mathf.Max(1f, a / metresPerTile), Mathf.Max(1f, b / metresPerTile));

        renderer.sharedMaterial = GmVictorianInteriorKit.Surface(family, $"Gm_{family}_{target.name}", tiling, tint);
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
