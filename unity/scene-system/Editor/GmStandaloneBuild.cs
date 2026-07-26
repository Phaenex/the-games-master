// Deterministic local macOS review build. This is the same player path later wrapped by Steam,
// but it deliberately does not claim signing, notarization, achievements or depot readiness.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GmStandaloneBuild
{
    public const string MacOutputPath = "Builds/macOS/The Games Master.app";

    [MenuItem("GamesMaster/Build/macOS Review App")]
    public static void BuildMacReview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building the review app");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            throw new InvalidOperationException(
                $"active target is {EditorUserBuildSettings.activeBuildTarget}; switch to macOS before building");

        var scenes = EnabledScenePaths();
        if (scenes.Count == 0) throw new InvalidOperationException("no enabled scenes are in Build Settings");
        if (scenes[0] != GmSceneCatalog.WendHillPath)
            throw new InvalidOperationException(
                $"first startup scene is '{scenes[0]}', expected '{GmSceneCatalog.WendHillPath}'");

        PlayerSettings.companyName = "Damatnic";
        PlayerSettings.productName = "The Games Master";
        PlayerSettings.applicationIdentifier = "com.damatnic.thegamesmaster";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();

        string output = Path.GetFullPath(MacOutputPath);
        string directory = Path.GetDirectoryName(output);
        if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("build output has no parent directory");
        Directory.CreateDirectory(directory);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.StrictMode,
        });
        var summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
            throw new InvalidOperationException(
                $"macOS build failed: result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException($"Unity reported success but app bundle is missing: {output}");

        Debug.Log($"[GmStandaloneBuild] PASS: {output} scenes={scenes.Count} " +
                  $"bytes={summary.totalSize} warnings={summary.totalWarnings}");
    }

    static List<string> EnabledScenePaths()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            if (string.IsNullOrWhiteSpace(scene.path) || !scene.path.StartsWith("Assets/Scenes/") ||
                !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || scene.path.Contains(".."))
                throw new InvalidOperationException($"unsafe enabled scene path '{scene.path}'");
            if (!File.Exists(scene.path)) throw new FileNotFoundException("enabled scene is missing", scene.path);
            if (!seen.Add(scene.path)) throw new InvalidOperationException($"duplicate enabled scene '{scene.path}'");
            result.Add(scene.path);
        }
        return result;
    }
}
