// Renders a contact sheet of night-lighting variants so the recipe is CHOSEN FROM RENDERED FRAMES
// rather than reasoned about. The previous night pass was authored by argument ("-1.1 is brighter
// than -2.55") and shipped a fully black scene, then a flat blue one; both would have been caught
// instantly by looking at a frame. This makes looking at frames the cheap default.
//
// One windowed session sweeps every variant in GmVillageNightRecipe.LabSweep() across three fixed
// vantages and writes 1920x1080 PNGs to Screens/VillageLab. HDRP renders white under -batchmode on
// this project, so unity-cli/the operator must launch this WINDOWED with
// -executeMethod GmVillageLightingLab.Run. The routine is EditorApplication.update-driven so HDRP
// has real frames to converge its exposure and volumetrics, and exits the editor itself when done.
//
// The scene is rebuilt from the purchased source once at the start; each variant then re-lights that
// same built scene in place, so all frames differ ONLY by the recipe under test.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GmVillageLightingLab
{
    const string LogTag = "GmVillageLab";
    const int Width = 1920;
    const int Height = 1080;
    const int SettleFrames = 55;    // real editor frames for HDRP exposure/volumetrics to converge
    const int RenderPasses = 16;    // flush temporal accumulation before readback
    const int WatchdogFrames = 20000;

    struct Shot
    {
        public string label;
        public Vector3 position;
        public Vector3 euler;
        public float fov;
    }

    // Vantage A is the pack's own enabled hero camera (the store-render framing). B looks up the
    // street toward the church. C is the player's real eye height at the spawn, which is the only
    // one that answers "what does someone actually standing here see".
    static readonly Shot[] Shots =
    {
        new Shot { label = "A-hero",   position = new Vector3(112.538f, -148.293f, -104.728f), euler = new Vector3(348.43f, 221.865f, 359.358f), fov = 55f },
        new Shot { label = "B-church", position = new Vector3(117.068f, -148.273f, -123.528f), euler = new Vector3(348f, 226f, 0f), fov = 59f },
        new Shot { label = "C-eye",    position = Vector3.zero, euler = Vector3.zero, fov = 60f },  // filled from the real player at runtime
    };

    static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Screens", "VillageLab");

    static List<GmVillageNightRecipe> sweep;
    static GmVillageBuilder.Vantage vantage;
    static Camera rig;
    static Shot[] shots;
    static int variantIndex;
    static int settle;
    static int written;
    static int totalFrames;

    /// Renders the SHIPPED recipe only, from all three vantages. This is the regression check for
    /// every later milestone: after M2 drops a mansion and gate into this scene, or M3 wires zones
    /// through it, re-running this is how "did the village still look right afterwards" gets
    /// answered with frames instead of with hope.
    [MenuItem("GamesMaster/Village/Verify Shipped Look")]
    public static void VerifyShipped()
    {
        RunSweep(new List<GmVillageNightRecipe> { GmVillageNightRecipe.Base().With("shipped") });
    }

    [MenuItem("GamesMaster/Village/Lighting Lab (contact sheet)")]
    public static void Run()
    {
        RunSweep(GmVillageNightRecipe.LabSweep());
    }

    static void RunSweep(List<GmVillageNightRecipe> variants)
    {
        Directory.CreateDirectory(OutDir);
        sweep = variants;
        Debug.Log($"[{LogTag}] START {sweep.Count} variants x {Shots.Length} vantages -> {OutDir}");

        vantage = GmVillageBuilder.Build(sweep[0]);
        shots = BuildShotList();
        rig = MakeRig();

        variantIndex = 0;
        settle = 0;
        written = 0;
        totalFrames = 0;
        EditorApplication.update += Tick;
    }

    // Vantage C is derived from the player the builder actually placed, so if the spawn ever moves
    // this shot follows it instead of silently rendering a stale hand-typed coordinate.
    static Shot[] BuildShotList()
    {
        var list = new List<Shot>(Shots);
        GameObject player = GameObject.Find("Player");
        Transform eye = player != null ? player.transform.Find("PlayerCamera") : null;
        if (eye != null)
        {
            Shot c = list[2];
            c.position = eye.position;
            c.euler = eye.rotation.eulerAngles;
            list[2] = c;
        }
        else
        {
            Debug.LogWarning($"[{LogTag}] no PlayerCamera found; dropping the eye-level vantage");
            list.RemoveAt(2);
        }

        // M2 vantages, derived from the same measured spine the estate was placed on, so they follow
        // the estate if its placement constants ever change instead of going stale.
        float southYaw = GmVillageEstate.SpineYawDeg + 180f;
        list.Add(SpineShot("D-mansion-approach", -60f, southYaw, 1.7f, 60f));
        list.Add(SpineShot("E-gate", 70f, southYaw, 1.7f, 60f));
        list.Add(SpineShot("F-mansion-close", -66f, southYaw, 1.7f, 55f));

        // Looking UP. The sky is a major surface in this game -- the prologue is entirely outdoors
        // at night and the player repeatedly raises their eyes to a roofline, a church tower and a
        // bell -- and none of the other vantages show enough of it to judge.
        Shot up = SpineShot("H-sky", -40f, southYaw, 1.7f, 70f);
        up.euler = new Vector3(-38f, southYaw, 0f);
        list.Add(up);
        return list.ToArray();
    }

    static Shot SpineShot(string label, float t, float yaw, float eyeHeight, float fov)
    {
        Vector2 xz = GmVillageEstate.SpinePoint(t);
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        float groundY = terrain != null
            ? terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y
            : 0f;
        return new Shot
        {
            label = label,
            position = new Vector3(xz.x, groundY + eyeHeight, xz.y),
            euler = new Vector3(0f, yaw, 0f),
            fov = fov,
        };
    }

    // A dedicated offscreen rig, rather than reusing the player camera, so moving it between shots
    // cannot leave the built scene's player facing somewhere the builder did not intend.
    static Camera MakeRig()
    {
        var go = new GameObject("GmVillageLabRig");
        go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 800f;
        go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        return cam;
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

        GmVillageNightRecipe recipe = sweep[variantIndex];
        try
        {
            foreach (Shot shot in shots) Grab(recipe, shot);
            GrabGateStates(recipe);
        }
        catch (Exception e)
        {
            Debug.LogError($"[{LogTag}] capture failed for {recipe.label}: {e}");
        }

        variantIndex++;
        if (variantIndex >= sweep.Count)
        {
            Finish(0);
            return;
        }

        GmVillageNightRecipe next = sweep[variantIndex];
        GmVillageBuilder.ApplyNight(next);
        int practicals = GmVillageBuilder.BuildPracticals(next, vantage);
        Debug.Log($"[{LogTag}] applied {next.label} EV={next.exposureEV} practicals={practicals}");
        settle = 0;
        Resources.UnloadUnusedAssets();
    }

    /// Renders the gate open and closed from the same vantage. The gate leaves are authored geometry
    /// swung by GmGateLeaves at runtime, so without this the closed state -- the one Threshold
    /// Refusal actually depends on -- would only ever be seen by playing to the lock beat.
    static void GrabGateStates(GmVillageNightRecipe recipe)
    {
        var leaves = UnityEngine.Object.FindAnyObjectByType<GmGateLeaves>();
        if (leaves == null)
        {
            Debug.LogWarning($"[{LogTag}] no GmGateLeaves in scene; skipping gate state shots");
            return;
        }

        // Stand just inside the gate looking back at it, which is the player's actual view on the
        // lock beat: they turn around and the gate is shut.
        Vector2 xz = GmVillageEstate.SpinePoint(46f);
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        float groundY = terrain != null
            ? terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y : 0f;

        // Report anything large sitting on top of this vantage. An earlier framing here was filled by
        // a slab of geometry that had no business being on the drive, and guessing at what it was
        // from a render is exactly the habit that produced the black-screen and zig-zag-road bugs.
        var here = new Vector3(xz.x, groundY + 1.7f, xz.y);
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r.GetComponent<Terrain>() != null) continue;
            if (Vector3.Distance(r.bounds.ClosestPoint(here), here) > 3f) continue;
            Debug.Log($"[{LogTag}] near gate vantage: '{r.name}' path={HierarchyPath(r.transform)} " +
                      $"centre={r.bounds.center} size={r.bounds.size}");
        }
        var shot = new Shot
        {
            label = "G-gate",
            position = new Vector3(xz.x, groundY + 1.7f, xz.y),
            euler = new Vector3(0f, GmVillageEstate.SpineYawDeg, 0f),   // looking back north
            fov = 60f,
        };

        leaves.SetOpenImmediate();
        Grab(recipe, new Shot { label = "G-gate-open", position = shot.position, euler = shot.euler, fov = shot.fov });
        leaves.SetClosedImmediate();
        Grab(recipe, new Shot { label = "G-gate-closed", position = shot.position, euler = shot.euler, fov = shot.fov });
        leaves.SetOpenImmediate();   // leave the scene in its authored start state
    }

    static void Grab(GmVillageNightRecipe recipe, Shot shot)
    {
        rig.transform.position = shot.position;
        rig.transform.rotation = Quaternion.Euler(shot.euler);
        rig.fieldOfView = shot.fov;

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        RenderTexture prevActive = RenderTexture.active;
        rig.targetTexture = rt;
        for (int i = 0; i < RenderPasses; i++) rig.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        rig.targetTexture = null;

        string file = Path.Combine(OutDir, $"{recipe.label}__{shot.label}.png");
        File.WriteAllBytes(file, tex.EncodeToPNG());

        // Mean luminance is logged per frame because "too dark" and "washed out" are the two failure
        // modes this lab exists to catch, and a number makes them comparable across variants without
        // eyeballing every PNG.
        Debug.Log($"[{LogTag}] shot {recipe.label}/{shot.label} luma={MeanLuma(tex):0.000}");

        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        written++;
    }

    // NOT named Path(): this file has `using System.IO`, and a static method called Path shadows the
    // System.IO.Path class that OutDir depends on.
    static string HierarchyPath(Transform t)
    {
        string p = t.name;
        for (Transform c = t.parent; c != null; c = c.parent) p = c.name + "/" + p;
        return p;
    }

    static float MeanLuma(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        double sum = 0;
        // Every 37th pixel: a prime stride samples all rows evenly and keeps this off the critical path.
        int step = 37;
        int n = 0;
        for (int i = 0; i < px.Length; i += step)
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
        Debug.Log($"[{LogTag}] LAB COMPLETE {written} frames -> {OutDir}");
        EditorApplication.Exit(code);
    }
}

