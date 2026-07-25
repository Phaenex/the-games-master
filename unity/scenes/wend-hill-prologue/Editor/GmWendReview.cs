// Renders the prologue scene after every build step, so no change ever lands unseen.
//
// This is the harness the previous attempt lacked. It changed six lighting values at once and then
// argued about the result; when a frame finally got looked at it was black, and the next four
// rounds were spent bisecting by hand. One render per step makes the cause of any change obvious,
// because only one thing moved.
//
// Frames land in Screens/WendHill_Prologue, which is the scene name rather than a nickname because
// unity-cli derives a tour's screenshot directory from registry sceneName. Naming it anything else
// means the CLI polls an empty folder and times out. Windowed only (HDRP renders white under
// -batchmode here) and self-exiting.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendReview
{
    const string LogTag = "GmWendReview";
    const int Width = 1600;
    const int Height = 900;
    const int SettleFrames = 70;
    const int RenderPasses = 16;

    /// KNOWN, UNEXPLAINED, DO NOT TRUST THE FIRST FRAME ON ITS OWN.
    ///
    /// The first shot of a run reads mean luma 0.026 on the player's forward look, where GmWendLadder's
    /// rung at the same committed EV, from the same position and rotation in the same scene, reads
    /// 0.059. The rear look agrees between the two harnesses to within about 1% on per-band means. One
    /// view off by 2.3x while another agrees is not settling noise.
    ///
    /// Hypothesis tried and DISPROVEN: that HDRP volumetric fog accumulates temporally per render, so a
    /// hand-rendered disabled camera starves its first shot of history. Raising the first shot from 16
    /// render passes to 80 changed the value not at all, 0.026 both times, so accumulated render history
    /// is not the cause. The 0.026 is reproducible in magnitude across runs while the pixels differ
    /// slightly, which is consistent with S_Wind animating the grass on shader time.
    ///
    /// What this does and does not mean. It is a property of THIS RIG, not of the scene: the saved scene
    /// passes all six contract checks after a real reload, and its other seven vantages land where the
    /// bracket says they should. Which of 0.026 and 0.059 is the truthful forward reading is not known,
    /// because the ladder's own forward frame came seven rungs into a warm session.
    ///
    /// So: judge the forward look from a shot that is not the first in its run, and do not quote
    /// eye-000 as a number on its own until someone finds the cause.
    ///
    /// Not chased further because it does not affect the shipped scene and the next step needs a real
    /// diagnostic rather than another guess. The rig's own file header is right that a review frame
    /// which looks like evidence is worse than none, which is exactly why this is written down instead
    /// of quietly averaged away.
    const int WatchdogFrames = 20000;
    const float EyeHeight = 1.7f;

    struct Shot { public string label; public Vector3 position; public Quaternion rotation; }

    static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Screens", "WendHill_Prologue");

    static Camera rig;
    static Shot[] shots;
    static int index;
    static int settle;
    static int totalFrames;

    [MenuItem("GamesMaster/Wend/Review current scene")]
    public static void Run()
    {
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);

        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        shots = BuildShots();
        if (shots.Length == 0) { Debug.LogError($"[{LogTag}] no shots to render"); EditorApplication.Exit(1); return; }

        var go = new GameObject("GmWendReviewRig") { hideFlags = HideFlags.HideAndDontSave };
        rig = go.AddComponent<Camera>();
        rig.enabled = false;
        rig.nearClipPlane = 0.05f;
        rig.farClipPlane = 900f;
        rig.fieldOfView = 62f;
        go.AddComponent<HDAdditionalCameraData>();

        index = 0;
        settle = 0;
        totalFrames = 0;
        Debug.Log($"[{LogTag}] START {shots.Length} shots -> {OutDir}");
        EditorApplication.update += Tick;
    }

    /// Vantages are derived from the scene, not hardcoded: the player's own eye, then a few points
    /// spread along the road network. Hardcoded coordinates go stale the moment the spawn moves,
    /// and a stale review frame is worse than none because it looks like evidence.
    static Shot[] BuildShots()
    {
        var list = new List<Shot>();

        GameObject player = GameObject.Find(GmWendBuilder.PlayerName);
        Transform eye = player != null ? player.transform.Find("PlayerCamera") : null;
        if (eye != null)
        {
            // Four cardinal looks from where the player actually stands. A single forward shot hides
            // whatever is behind them, and "behind them" is where the empty field was last time.
            for (int i = 0; i < 4; i++)
                list.Add(new Shot
                {
                    label = $"eye-{i * 90:D3}",
                    position = eye.position,
                    rotation = Quaternion.Euler(0f, i * 90f, 0f),
                });
        }
        else Debug.LogWarning($"[{LogTag}] no PlayerCamera found");

        // A few road vantages, spread so they are not all the same corner of a 1.5km map.
        Renderer[] roads = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.gameObject.activeInHierarchy)
            .Where(r => System.Text.RegularExpressions.Regex.IsMatch(
                r.name, @"Road|Path|Street|Track|Lane|Cobble", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .ToArray();

        if (roads.Length > 0 && player != null)
        {
            Vector3 from = player.transform.position;
            // Prefer big, flat road pieces: a large thin mesh is an actual road surface, whereas a
            // tall one matching the name pattern is usually a wall or a frame.
            Renderer[] picked = roads
                .Where(r => (r.bounds.center - from).magnitude > 25f)
                .Where(r => r.bounds.size.y < 3f && Mathf.Max(r.bounds.size.x, r.bounds.size.z) > 6f)
                .OrderBy(r => (r.bounds.center - from).sqrMagnitude)
                .Take(4)
                .ToArray();

            // No axis-aligned containment test here. It was tried and rejected EVERY vantage: an
            // AABB around a building swallows a lot of open ground, so "inside some renderer's
            // bounds" is not the same as "inside geometry". The flat-and-large filter above already
            // excludes the wall-shaped meshes that caused the original buried shots.
            var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            int made = 0;
            foreach (Renderer road in picked)
            {
                // Stand on TOP of the road mesh, not at its centre.
                float surfaceY = road.bounds.max.y;
                if (terrain != null)
                    surfaceY = Mathf.Max(surfaceY,
                        terrain.SampleHeight(road.bounds.center) + terrain.transform.position.y);

                var pos = new Vector3(road.bounds.center.x, surfaceY + EyeHeight, road.bounds.center.z);
                Vector3 look = from - pos;
                look.y = 0f;
                if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
                list.Add(new Shot
                {
                    label = $"road-{++made:D2}",
                    position = pos,
                    rotation = Quaternion.LookRotation(look.normalized, Vector3.up),
                });
            }
        }
        return list.ToArray();
    }

    static void Tick()
    {
        totalFrames++;
        if (totalFrames > WatchdogFrames) { Debug.LogError($"[{LogTag}] watchdog"); Finish(1); return; }
        if (++settle < SettleFrames) return;
        settle = SettleFrames - 6;   // shorter settle between shots in the same scene

        if (index >= shots.Length) { Finish(0); return; }

        Shot s = shots[index];
        rig.transform.SetPositionAndRotation(s.position, s.rotation);

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

        File.WriteAllBytes(Path.Combine(OutDir, $"{s.label}.png"), tex.EncodeToPNG());
        Debug.Log($"[{LogTag}] shot {s.label} luma={MeanLuma(tex):0.000}");
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);

        index++;
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
        Debug.Log($"[{LogTag}] REVIEW COMPLETE {index} frames -> {OutDir}");
        EditorApplication.Exit(code);
    }
}
