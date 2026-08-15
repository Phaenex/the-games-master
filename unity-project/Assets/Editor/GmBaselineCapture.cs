// Renders the PURCHASED scenes exactly as they ship. No modifications of any kind.
//
// This is step one of the restart. The previous attempt's root failure was that its very first build
// step already stripped the pack's two directional lights and overrode its ~23 authored sky/fog
// volumes, so every judgement made afterwards was about lighting I had written, not lighting the
// pack shipped. The first render ever taken of the Haunted Village pack looked good; everything that
// followed was a re-authoring of what was already there.
//
// So: open, render, move on. Nothing is stripped, nothing is added, nothing is saved. The scenes are
// opened read-only and the editor is never allowed to write them back -- the capture rig is created
// with HideAndDontSave and destroyed after each scene.
//
// Two kinds of frame per scene:
//   * the pack's OWN enabled camera, which is the art-directed shot they sell it with;
//   * three frames at player eye height, which is the only thing that answers "what does it look
//     like to stand in this" -- the question that actually matters and the one a showcase still
//     never answers.
//
// Windowed only (HDRP renders white under -batchmode on this project) and self-exiting.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmBaselineCapture
{
    const string LogTag = "GmBaseline";
    const int Width = 1920;
    const int Height = 1080;
    const int SettleFrames = 70;   // HDRP exposure/volumetrics need real frames to converge
    const int RenderPasses = 16;
    const float EyeHeight = 1.7f;
    const int WatchdogFrames = 40000;

    struct Job { public string label; public string path; }

    static readonly Job[] Jobs =
    {
        new Job { label = "haunted-village",   path = "Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity" },
        new Job { label = "abandoned-village", path = "Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity" },
        new Job { label = "witch-village",     path = "Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_WitchVillage.unity" },
    };

    static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Screens", "Baseline");

    static int jobIndex;
    static int settle;
    static int written;
    static int totalFrames;
    static Camera rig;

    [MenuItem("GamesMaster/Restart/Capture Purchased Baselines")]
    public static void Run()
    {
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);

        Debug.Log($"[{LogTag}] START {Jobs.Length} purchased scenes, UNMODIFIED -> {OutDir}");
        jobIndex = -1;
        written = 0;
        totalFrames = 0;
        Advance();
        EditorApplication.update += Tick;
    }

    static void Advance()
    {
        if (rig != null) { UnityEngine.Object.DestroyImmediate(rig.gameObject); rig = null; }

        jobIndex++;
        if (jobIndex >= Jobs.Length) { Finish(0); return; }

        Job job = Jobs[jobIndex];
        if (!File.Exists(job.path))
        {
            Debug.LogWarning($"[{LogTag}] missing scene {job.path}");
            Advance();
            return;
        }

        try
        {
            EditorSceneManager.OpenScene(job.path, OpenSceneMode.Single);
            settle = 0;
            Debug.Log($"[{LogTag}] opened {job.label} (unmodified)");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] failed to open {job.label}: {e.Message}");
            Advance();
        }
    }

    static void Tick()
    {
        totalFrames++;
        if (totalFrames > WatchdogFrames) { Debug.LogError($"[{LogTag}] watchdog tripped"); Finish(1); return; }
        if (++settle < SettleFrames) return;

        try { Capture(Jobs[jobIndex].label); }
        catch (Exception e) { Debug.LogError($"[{LogTag}] capture failed for {Jobs[jobIndex].label}: {e}"); }

        Resources.UnloadUnusedAssets();
        GC.Collect();
        Advance();
    }

    static void Capture(string label)
    {
        // 1. The pack's own shot, from whichever camera it ships enabled.
        Camera hero = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)
            .FirstOrDefault(c => c.isActiveAndEnabled && c.targetTexture == null);
        if (hero != null) Grab(hero, $"{label}-00-pack-camera");
        else Debug.LogWarning($"[{LogTag}] {label} ships no enabled camera");

        // 2. Eye-level frames. Positions come from the scene's own dressed content, so this works
        //    without knowing anything about a given pack's layout.
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.gameObject.activeInHierarchy && r.GetComponent<Terrain>() == null)
            .ToArray();
        if (renderers.Length == 0) { Debug.LogWarning($"[{LogTag}] {label} has no renderers"); return; }

        Bounds content = renderers[0].bounds;
        foreach (Renderer r in renderers) content.Encapsulate(r.bounds);

        // Weighted centroid rather than bounding-box centre: a stray prop parked far from the
        // dressing drags the box centre out into empty ground, which is the exact mistake that put
        // the player 100m outside the village last time.
        Vector3 sum = Vector3.zero;
        foreach (Renderer r in renderers) sum += r.bounds.center;
        Vector3 centroid = sum / renderers.Length;

        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        float radius = Mathf.Min(48f, Mathf.Max(content.size.x, content.size.z) * 0.22f);

        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f * Mathf.Deg2Rad;
            var at = new Vector3(centroid.x + Mathf.Cos(angle) * radius, 0f,
                                 centroid.z + Mathf.Sin(angle) * radius);
            float groundY = terrain != null
                ? terrain.SampleHeight(at) + terrain.transform.position.y
                : content.min.y;
            at.y = groundY + EyeHeight;

            Vector3 look = new Vector3(centroid.x, at.y, centroid.z) - at;
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;

            EnsureRig();
            rig.transform.position = at;
            rig.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            rig.fieldOfView = 62f;
            Grab(rig, $"{label}-{i + 1:D2}-eye");
        }
    }

    static void EnsureRig()
    {
        if (rig != null) return;
        // HideAndDontSave so the capture rig can never be written into a purchased scene.
        var go = new GameObject("GmBaselineRig") { hideFlags = HideFlags.HideAndDontSave };
        rig = go.AddComponent<Camera>();
        rig.enabled = false;
        rig.nearClipPlane = 0.05f;
        rig.farClipPlane = 900f;
        go.AddComponent<HDAdditionalCameraData>();
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

        File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
        Debug.Log($"[{LogTag}] shot {name} luma={MeanLuma(tex):0.000}");

        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        written++;
    }

    static float MeanLuma(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        double sum = 0;
        int n = 0;
        for (int i = 0; i < px.Length; i += 37)
        {
            sum += (0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b) / 255.0;
            n++;
        }
        return n == 0 ? 0f : (float)(sum / n);
    }

    static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        if (rig != null) UnityEngine.Object.DestroyImmediate(rig.gameObject);
        Debug.Log($"[{LogTag}] BASELINE COMPLETE {written} frames -> {OutDir}");
        EditorApplication.Exit(code);
    }
}

