// Renders the night conversion as a cumulative ladder: one frame set per individual change.
//
//   step0  the pack's shipped daylight, untouched (the control)
//   step1  + the existing sun re-aimed and dimmed to a moon
//   step2  + the existing sky and fog retinted to night
//   step3  + the pack's ungated foliage emission turned off
//   step4+ + a fixed exposure added and dropped, one EV per rung
//
// This is the harness the previous attempt never had. It moved six lighting values at once, could
// not attribute any result to any cause, and burned four rounds bisecting by hand. With a ladder the
// culprit for any change is whatever moved on that rung.
//
// Each rung REBUILDS from the purchased source first, so the rungs are genuinely cumulative from a
// clean state rather than accumulating whatever the previous rung left behind.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendLadder
{
    const string LogTag = "GmWendLadder";
    const int Width = 1600;
    const int Height = 900;
    const int SettleFrames = 80;   // HDRP exposure + volumetrics need real frames after a relight
    const int RenderPasses = 16;
    const int Steps = 10;   // 5 structural rungs + a 5-value exposure bracket
    const float EyeHeight = 1.7f;

    // Deadlines are WALL CLOCK, not frames. A frame counter only advances when Tick runs, so it can
    // only catch a ladder that is still alive and slow. It cannot catch the failure that actually
    // happened: a domain reload unsubscribed Tick, the run died, nothing incremented the counter, and
    // the watchdog sat at zero for 48 minutes while the CLI polled an output directory that would
    // never fill. Frames also are not time here, since a rung rebuilds 4997 renderers and the frame
    // rate across a run spans more than an order of magnitude.
    //
    // Two deadlines rather than one: the per-rung deadline names WHICH rung wedged, which is the
    // question anyone reading the log actually has. Both are generous on purpose, and every rung now
    // logs its own elapsed time so these can be tightened against measured numbers instead of guesses.
    const double RungDeadlineSeconds = 480;
    const double RunDeadlineSeconds = 5400;

    // Survives a domain reload; cleared when the editor restarts. This is the only part of the run
    // that outlives the reload that kills everything else, so it is what turns a silent death into an
    // error message.
    const string InFlightKey = "GmWendLadder.InFlight";

    static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Screens", "WendLadder");

    static Camera rig;
    static int step;
    static int settle;
    static bool applied;
    static double runStarted;
    static double rungStarted;

    /// A ladder run cannot survive a domain reload: the reload resets every static above and drops the
    /// Tick subscription, so the run is already over by the time this executes. It cannot be resumed,
    /// only reported, and reporting it is the whole point. Silence here is what cost the 48 minutes.
    [InitializeOnLoadMethod]
    static void ReportInterruptedRun()
    {
        if (!SessionState.GetBool(InFlightKey, false)) return;
        SessionState.SetBool(InFlightKey, false);
        Debug.LogError($"[{LogTag}] ABORTED: a domain reload interrupted the ladder mid-run. " +
                       "The run is dead, not slow; its statics and its update subscription are gone. " +
                       "A script compile or an AssetDatabase refresh during a rung is the usual cause.");
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }

    [MenuItem("GamesMaster/Wend/2. Night ladder (render every step)")]
    public static void Run()
    {
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);

        step = -1;
        settle = 0;
        applied = false;
        runStarted = EditorApplication.timeSinceStartup;
        SessionState.SetBool(InFlightKey, true);

        var go = new GameObject("GmWendLadderRig") { hideFlags = HideFlags.HideAndDontSave };
        rig = go.AddComponent<Camera>();
        rig.enabled = false;
        rig.nearClipPlane = 0.05f;
        rig.farClipPlane = 900f;
        rig.fieldOfView = 62f;
        go.AddComponent<HDAdditionalCameraData>();

        Debug.Log($"[{LogTag}] START {Steps} rungs -> {OutDir}");
        Advance();
        EditorApplication.update += Tick;
    }

    static void Advance()
    {
        if (step >= 0)
            Debug.Log($"[{LogTag}] rung {step} took {EditorApplication.timeSinceStartup - rungStarted:0.0}s");

        step++;
        applied = false;
        settle = 0;
        rungStarted = EditorApplication.timeSinceStartup;
        if (step >= Steps) { Finish(0); return; }

        try
        {
            // Rebuild from the purchased source every rung, so step N is exactly "the shipped scene
            // plus the first N changes" and not "whatever rung N-1 happened to leave behind".
            GmWendBuilder.BuildBase();
            GmWendNight.ApplyStep(step);
            applied = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[{LogTag}] rung {step} failed: {e.Message}");
            Advance();
        }
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - rungStarted > RungDeadlineSeconds)
        {
            Debug.LogError($"[{LogTag}] watchdog: rung {step} exceeded {RungDeadlineSeconds:0}s " +
                           $"(settled {settle} of {SettleFrames} frames, applied={applied})");
            Finish(1);
            return;
        }
        if (now - runStarted > RunDeadlineSeconds)
        {
            Debug.LogError($"[{LogTag}] watchdog: the run exceeded {RunDeadlineSeconds:0}s at rung {step}");
            Finish(1);
            return;
        }
        if (!applied) return;
        if (++settle < SettleFrames) return;

        try { Capture(step); }
        catch (Exception e) { Debug.LogError($"[{LogTag}] capture rung {step} failed: {e}"); }

        Resources.UnloadUnusedAssets();
        Advance();
    }

    static void Capture(int n)
    {
        GameObject player = GameObject.Find(GmWendBuilder.PlayerName);
        Transform eye = player != null ? player.transform.Find("PlayerCamera") : null;
        if (eye == null) { Debug.LogWarning($"[{LogTag}] no PlayerCamera on rung {n}"); return; }

        // Two looks, 180 apart. One direction can flatter or damn a lighting change by accident;
        // the opposite view is the cheapest possible guard against that.
        for (int i = 0; i < 2; i++)
        {
            rig.transform.SetPositionAndRotation(eye.position, Quaternion.Euler(0f, i * 180f, 0f));
            Grab($"step{n}-{i * 180:D3}");
        }
    }

    static void Grab(string label)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        RenderTexture prev = RenderTexture.active;
        rig.targetTexture = rt;
        for (int i = 0; i < RenderPasses; i++) rig.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        rig.targetTexture = null;

        File.WriteAllBytes(Path.Combine(OutDir, label + ".png"), tex.EncodeToPNG());
        Debug.Log($"[{LogTag}] shot {label} luma={MeanLuma(tex):0.000}");

        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
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
        SessionState.SetBool(InFlightKey, false);
        if (rig != null) UnityEngine.Object.DestroyImmediate(rig.gameObject);

        // Say which ending this was. "LADDER COMPLETE" printed on the watchdog path too, so a timed-out
        // run and a finished run produced the same last line in the log.
        double elapsed = EditorApplication.timeSinceStartup - runStarted;
        if (code == 0) Debug.Log($"[{LogTag}] LADDER COMPLETE in {elapsed:0.0}s -> {OutDir}");
        else Debug.LogError($"[{LogTag}] LADDER FAILED after {elapsed:0.0}s at rung {step} -> {OutDir}");
        EditorApplication.Exit(code);
    }
}
