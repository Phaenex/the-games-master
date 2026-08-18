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
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendStandaloneBuild
{
    const string LogTag = "GmWendBuild";
    public const string OutputPath = "Builds/macOS-Wend/Wend Hill Prologue.app";
    public const string ProfileOutputPath =
        "Builds/macOS-Wend-Profile/Wend Hill Prologue Profile.app";
    public const BuildOptions ReleaseBuildOptions = BuildOptions.StrictMode;
    public const BuildOptions ProfileBuildOptions =
        BuildOptions.StrictMode | BuildOptions.Development;

    [MenuItem("GamesMaster/Wend/4. Build the prologue app")]
    public static void BuildPrologueApp() => Build(OutputPath, ReleaseBuildOptions);

    [MenuItem("GamesMaster/Wend/Diagnostics/Build profiler-enabled prologue app")]
    public static void BuildPrologueProfileApp() =>
        Build(ProfileOutputPath, ProfileBuildOptions);

    static void Build(string outputPath, BuildOptions options)
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

        string output = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(output);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("build output has no parent directory");
        Directory.CreateDirectory(directory);

        // The HDRP instance is created before scene Awake methods run, so dynamic-resolution support
        // must be present in the pipeline serialized into the player. Configure the active asset only
        // in memory for the duration of this build and restore it afterward; the purchased asset on
        // disk and every other build profile remain untouched.
        HDRenderPipelineAsset pipeline = QualitySettings.renderPipeline as HDRenderPipelineAsset ??
            GraphicsSettings.defaultRenderPipeline as HDRenderPipelineAsset;
        if (pipeline == null) throw new InvalidOperationException("prologue build requires an active HDRP asset");
        RenderPipelineSettings originalSettings = pipeline.currentPlatformRenderPipelineSettings;
        pipeline.currentPlatformRenderPipelineSettings = GmWendRenderBudget.ConfigureSettings(originalSettings);
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GmWendBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = options,
            });
        }
        finally
        {
            pipeline.currentPlatformRenderPipelineSettings = originalSettings;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssetIfDirty(pipeline);
        }

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
            throw new InvalidOperationException(
                $"prologue build failed: result={summary.result}, errors={summary.totalErrors}, " +
                $"warnings={summary.totalWarnings}");
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException(
                $"Unity reported success but the app bundle is missing: {output}");

        string verdict = (options & BuildOptions.Development) != 0 ? "PROFILE PASS:" : "PASS:";
        Debug.Log($"[{LogTag}] {verdict} {output} bytes={summary.totalSize} " +
                  $"warnings={summary.totalWarnings}");
        EditorApplication.Exit(0);
    }
}
