// RETIRED 2026-07-25. Superseded by GmWendStandaloneBuild, which builds WendHill_Prologue.unity.
//
// Left in place rather than deleted because GmVillageBuilder and GmVillageEstate are still referenced
// elsewhere, but do not run this expecting the current game. It builds WendHillVillage.unity, which was
// authored on Haunted Village, a pack that ships zero practical lights. Its night was invented rather
// than converted, and it is not the scene anything else now targets. See docs/WEND-NIGHT-LADDER.md.
//
// This matters more than an unused menu item usually would: the whole point of the guard at the bottom
// of this file is that an app built from the wrong scene still opens, still runs, and is silently the
// wrong game. An out-of-date build entry point is that same hazard one level up.
//
// M1 walk build: a standalone macOS app scoped to ONLY WendHillVillage.unity, output to a distinct
// path from the real game (Builds/macOS/The Games Master.app) so this experimental environment pass
// can never collide with or overwrite the known-good Wend Hill build. Mirrors GmStandaloneBuild's
// safety checks (play mode guard, active build target, output verification).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmVillageStandaloneBuild
{
    public const string OutputPath = "Builds/macOS-Village/Wend Hill Village Walk.app";

    [MenuItem("GamesMaster/Village/Build Night Walk App")]
    public static void BuildVillageWalkApp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            throw new InvalidOperationException(
                $"active target is {EditorUserBuildSettings.activeBuildTarget}; switch to macOS before building");
        if (AssetDatabase.LoadMainAssetAtPath(GmVillageBuilder.VillageScenePath) == null)
            throw new InvalidOperationException(
                $"{GmVillageBuilder.VillageScenePath} does not exist -- run GmVillageBuilder.BuildNightWalk first");

        AssertSceneIsBuilt();

        // The first village app build silently inherited FullScreenWindow + native-resolution from
        // whatever GmStandaloneBuild last left in ProjectSettings (project-wide, not per-build) --
        // macOS puts a true fullscreen app in its own dedicated Space, so the window never showed up
        // in screenshots of the desktop Space. This is an iterative review build; force a normal,
        // resizable windowed mode so it behaves predictably and Nick can freely alt-tab out of it.
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();

        string output = Path.GetFullPath(OutputPath);
        string directory = Path.GetDirectoryName(output);
        if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("build output has no parent directory");
        Directory.CreateDirectory(directory);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { GmVillageBuilder.VillageScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.StrictMode,
        });
        var summary = report.summary;
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
            throw new InvalidOperationException(
                $"village walk build failed: result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException($"Unity reported success but app bundle is missing: {output}");

        Debug.Log($"[GmVillageStandaloneBuild] PASS: {output} bytes={summary.totalSize} warnings={summary.totalWarnings}");
    }

    /// Refuses to build an app from a half-built scene.
    ///
    /// GmVillageBuilder starts by DELETING the village scene and copying the purchased Showcase
    /// scene over it, then layers everything on top and saves. If it dies in between -- a Burst
    /// compiler crash did exactly this once -- what is left on disk is a pristine copy of the
    /// purchased showcase: no night lighting, no estate, no systems, no player. It still opens, it
    /// still builds, and the resulting app is silently the wrong game. The mtime does not even give
    /// it away, because CopyAsset inherits the source file's timestamp.
    ///
    /// Checking for the roots the builder is contractually required to leave behind turns a
    /// five-minute build of the wrong thing into an immediate, specific error.
    static void AssertSceneIsBuilt()
    {
        Scene previous = SceneManager.GetActiveScene();
        string previousPath = previous.path;

        Scene scene = EditorSceneManager.OpenScene(GmVillageBuilder.VillageScenePath, OpenSceneMode.Single);
        var present = new HashSet<string>();
        foreach (GameObject root in scene.GetRootGameObjects()) present.Add(root.name);

        string[] required = { "GmSystems", "Player", GmVillageEstate.RootName, "VillagePois", "WendHillVillageNightVolume" };
        string[] missing = required.Where(r => !present.Contains(r)).ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"{GmVillageBuilder.VillageScenePath} is not fully built -- missing root object(s): " +
                $"{string.Join(", ", missing)}. This is what a crashed GmVillageBuilder run leaves behind " +
                "(a raw copy of the purchased Showcase scene). Re-run GmVillageBuilder.BuildNightWalk.");

        Debug.Log($"[GmVillageStandaloneBuild] scene verified: {required.Length}/{required.Length} required roots present");
        if (!string.IsNullOrEmpty(previousPath) && previousPath != GmVillageBuilder.VillageScenePath)
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
    }
}

