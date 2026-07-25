// Standalone macOS build of the prologue scene, scoped to ONLY WendHill_Prologue.unity.
//
// Output goes to its own path so it can never collide with or overwrite the real game at
// Builds/macOS/The Games Master.app, or the retired village walk at Builds/macOS-Village.
//
// The guard runs BEFORE the build, not after, because the failure being prevented is a five-minute
// build of the wrong thing. GmWendSceneContract audits the night by value rather than by root objects,
// for the reason written up in that file: a crashed builder leaves a scene that has every root the
// purchased pack ships and is broad daylight.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmWendStandaloneBuild
{
    const string LogTag = "GmWendBuild";
    public const string OutputPath = "Builds/macOS-Wend/Wend Hill Prologue.app";

    [MenuItem("GamesMaster/Wend/4. Build the prologue app")]
    public static void BuildPrologueApp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            throw new InvalidOperationException(
                $"active target is {EditorUserBuildSettings.activeBuildTarget}; switch to macOS before building");
        if (AssetDatabase.LoadMainAssetAtPath(GmWendBuilder.ScenePath) == null)
            throw new InvalidOperationException(
                $"{GmWendBuilder.ScenePath} does not exist. Run GamesMaster/Wend/3 first.");

        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        GmWendSceneContract.AssertBuilt();

        // Windowed and non-native by choice. PlayerSettings are project-wide rather than per-build, so
        // whatever the last build left behind is what this one inherits: an earlier village build came
        // out FullScreenWindow at native resolution, and macOS puts a true fullscreen app in its own
        // Space, so the window never appeared in desktop screenshots at all. This is a review build.
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();

        string output = Path.GetFullPath(OutputPath);
        string directory = Path.GetDirectoryName(output);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("build output has no parent directory");
        Directory.CreateDirectory(directory);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { GmWendBuilder.ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.StrictMode,
        });

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
            throw new InvalidOperationException(
                $"prologue build failed: result={summary.result}, errors={summary.totalErrors}, " +
                $"warnings={summary.totalWarnings}");
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException(
                $"Unity reported success but the app bundle is missing: {output}");

        Debug.Log($"[{LogTag}] PASS: {output} bytes={summary.totalSize} warnings={summary.totalWarnings}");
        EditorApplication.Exit(0);
    }
}
