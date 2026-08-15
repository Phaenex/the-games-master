// One-off review tool: render the owned Leartes demo scenes so their environment density can be
// judged as evidence for the Wend Hill environment rebuild. HDRP renders white in -batchmode on this
// project, so unity-cli/the operator must launch this in a WINDOWED editor (no -batchmode) with
// -executeMethod GmDemoSceneCapture.CaptureAll. The routine is EditorApplication.update-driven so
// HDRP has real frames to converge, captures each scene's art-directed cameras, writes PNGs to
// Screens/DemoScenes, then exits the editor itself. It saves nothing to any scene or asset.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmDemoSceneCapture
{
    const string LogTag = "GmDemoCapture";
    const int Width = 1920;
    const int Height = 1080;
    const int SettleFrames = 60;   // real editor frames for HDRP exposure/volumetrics to settle
    const int RenderPasses = 16;   // repeated Render() to flush temporal accumulation before readback
    const int MaxCamsPerScene = 2;
    const int WatchdogFrames = 6000;

    struct Job { public string label; public string path; }

    static readonly Job[] Jobs =
    {
        new Job { label = "abandoned-village-main",  path = "Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity" },
        new Job { label = "abandoned-village-view",  path = "Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_ Overview.unity" },
        new Job { label = "haunted-village-showcase", path = "Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity" },
        new Job { label = "haunted-village-view",     path = "Assets/LeartesStudios/HauntedVillage/Scene/Overview.unity" },
        new Job { label = "witch-village-main",      path = "Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_WitchVillage.unity" },
        new Job { label = "witch-village-view",      path = "Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_Overview.unity" },
    };

    static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Screens", "DemoScenes");
    static int jobIndex;
    static int settle;
    static int written;
    static int totalFrames;

    public static void CaptureAll()
    {
        Directory.CreateDirectory(OutDir);
        Debug.Log($"[{LogTag}] START -> {OutDir}");
        jobIndex = -1;
        written = 0;
        totalFrames = 0;
        AdvanceScene();
        EditorApplication.update += Tick;
    }

    static void AdvanceScene()
    {
        jobIndex++;
        if (jobIndex >= Jobs.Length)
        {
            Finish(0);
            return;
        }
        Job job = Jobs[jobIndex];
        if (!File.Exists(job.path))
        {
            Debug.LogWarning($"[{LogTag}] missing scene {job.path}");
            AdvanceScene();
            return;
        }
        try
        {
            EditorSceneManager.OpenScene(job.path, OpenSceneMode.Single);
            settle = 0;
            Debug.Log($"[{LogTag}] opened {job.label}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] failed to open {job.label}: {e.Message}");
            AdvanceScene();
        }
    }

    static void Tick()
    {
        totalFrames++;
        if (totalFrames > WatchdogFrames)
        {
            Debug.LogError($"[{LogTag}] watchdog tripped at frame {totalFrames}");
            Finish(1);
            return;
        }
        settle++;
        if (settle < SettleFrames) return;

        try
        {
            CaptureCurrent(Jobs[jobIndex].label);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] capture failed for {Jobs[jobIndex].label}: {e.Message}");
        }
        Resources.UnloadUnusedAssets();
        GC.Collect();
        AdvanceScene();
    }

    static void CaptureCurrent(string label)
    {
        var cameras = new List<Camera>();
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.InstanceID))
            if (c != null && c.isActiveAndEnabled && c.targetTexture == null)
                cameras.Add(c);

        if (cameras.Count == 0)
        {
            Debug.LogWarning($"[{LogTag}] {label} has no active camera; skipped");
            return;
        }

        int shots = Mathf.Min(MaxCamsPerScene, cameras.Count);
        for (int i = 0; i < shots; i++)
            Grab(cameras[i], shots > 1 ? $"{label}-cam{i + 1}" : label);
    }

    static void Grab(Camera cam, string name)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        for (int i = 0; i < RenderPasses; i++) cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;

        string file = Path.Combine(OutDir, name + ".png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        written++;
        Debug.Log($"[{LogTag}] shot {name}");
    }

    static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        Debug.Log($"[{LogTag}] DEMO CAPTURE COMPLETE {written} shots -> {OutDir}");
        EditorApplication.Exit(code);
    }
}

