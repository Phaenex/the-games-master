using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Release, scene-scoped Parlor proof app. It never overwrites the full game build.</summary>
public static class GmParlorStandaloneBuild
{
    const string LogTag = "GmParlorBuild";
    public const string OutputPath =
        "Builds/macOS-Parlor/The Games Master Parlor Proof.app";
    public const BuildOptions ReleaseBuildOptions = BuildOptions.StrictMode;
    public const bool EnableFrameTimingStatsForBuild = true;

    [MenuItem("GamesMaster/Scenes/Parlor/Build macOS release proof")]
    public static void BuildReleaseProof()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building Parlor proof");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            throw new InvalidOperationException(
                $"active target is {EditorUserBuildSettings.activeBuildTarget}; switch to macOS");
        if (AssetDatabase.LoadMainAssetAtPath(GmParlorBuilder.ScenePath) == null)
            throw new FileNotFoundException("rebuild Parlor before building", GmParlorBuilder.ScenePath);

        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = GmParlorStandaloneProbe.OutputWidth;
        PlayerSettings.defaultScreenHeight = GmParlorStandaloneProbe.OutputHeight;
        PlayerSettings.resizableWindow = false;
        bool oldFrameTiming = PlayerSettings.enableFrameTimingStats;
        PlayerSettings.enableFrameTimingStats = EnableFrameTimingStatsForBuild;
        AssetDatabase.SaveAssets();

        string output = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output) ??
            throw new InvalidOperationException("Parlor build output has no parent"));
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GmParlorBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = ReleaseBuildOptions,
            });
        }
        finally
        {
            PlayerSettings.enableFrameTimingStats = oldFrameTiming;
            AssetDatabase.SaveAssets();
        }

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
            throw new InvalidOperationException(
                $"Parlor release build failed: {summary.result}, errors={summary.totalErrors}, " +
                $"warnings={summary.totalWarnings}");
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException($"Parlor app is missing: {output}");
        Debug.Log($"[{LogTag}] BUILD PASS: {output} bytes={summary.totalSize} " +
            $"warnings={summary.totalWarnings} frameTiming={EnableFrameTimingStatsForBuild}");
        EditorApplication.Exit(0);
    }
}
