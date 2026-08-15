// The build that contains the whole game, as opposed to the two review builds that came before it.
//
// There were already two standalone builders and neither ships the game:
//
//   GmWendStandaloneBuild  one scene, WendHill_Prologue, windowed 1600x900. Deliberately scoped --
//                          it is the app Nick walks for the Phase 0 gate, and it must stay that way.
//   GmStandaloneBuild      the legacy estate review app, which refuses to build unless WendHill is
//                          the startup scene.
//
// So Phase A1's boot scene and Phase A2's six player rigs were real, tested, and in no build at all.
// GmBootMenu.EnsureSceneDirector is the only thing anywhere that constructs a GmSceneDirector, so
// with Boot absent the director never existed in a player, ResolveEnding() was never called, and the
// six endings resolved only in the editor. Every one of those parts passed its tests. None of them
// had reached a person.
//
// The manifest is DERIVED from GmSceneDirector.ReachableScenePaths rather than written out again
// here. A scene the director can name is a scene the player can be sent to, and the two lists
// drifting apart is precisely the failure this file exists to end.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GmFullGameBuild
{
    const string LogTag = "GmFullGameBuild";

    /// Its own output path. The prologue review app and the legacy estate app each own theirs, and a
    /// builder that overwrites another builder's output is a five-minute way to review the wrong
    /// thing without knowing it.
    public const string MacOutputPath = "Builds/macOS-Game/The Games Master.app";

    public const string BootScenePath = "Assets/Scenes/Boot.unity";

    /// The shipped scene list, in build order.
    ///
    /// Boot is index 0 because in a built player index 0 IS the startup scene — there is no separate
    /// setting for it. That single fact is the whole reason A1 delivered nothing to a player.
    public static List<string> ScenePaths()
    {
        var paths = new List<string> { BootScenePath };
        foreach (string path in GmSceneDirector.ReachableScenePaths)
            if (!paths.Contains(path)) paths.Add(path);
        return paths;
    }

    [MenuItem("GamesMaster/Build/macOS Full Game")]
    public static void BuildMacGame()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building the game");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            throw new InvalidOperationException(
                $"active target is {EditorUserBuildSettings.activeBuildTarget}; switch to macOS before building");

        List<string> scenes = ScenePaths();
        foreach (string path in scenes)
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new FileNotFoundException(
                    $"{path} is in the game manifest but does not exist — rebuild it before building the game", path);

        PlayerSettings.companyName = "Damatnic";
        PlayerSettings.productName = "The Games Master";
        PlayerSettings.applicationIdentifier = "com.damatnic.thegamesmaster";
        PlayerSettings.bundleVersion = "0.1.0";
        // Windowed, like the prologue review app and for the same reason written up there: macOS puts
        // a true fullscreen app in its own Space, where it never appears in a desktop screenshot.
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();

        string output = Path.GetFullPath(MacOutputPath);
        string directory = Path.GetDirectoryName(output);
        if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("build output has no parent directory");
        Directory.CreateDirectory(directory);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.StrictMode,
        });
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
            throw new InvalidOperationException(
                $"full game build failed: result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException($"Unity reported success but the app bundle is missing: {output}");

        Debug.Log($"[{LogTag}] BUILD PASS: {output} startup='{scenes[0]}' scenes={scenes.Count} " +
                  $"bytes={summary.totalSize} warnings={summary.totalWarnings}");
    }
}
