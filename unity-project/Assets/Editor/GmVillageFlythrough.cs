// Captures the prologue's actual walk as an image sequence, so the route can be reviewed as motion
// rather than as six stills. Stills answer "does this frame look right"; only a walk answers "does
// the approach read, does the mansion arrive at the right moment, does anything pop in".
//
// The path follows the measured village spine from the arrival car, south through the gate, down the
// village street and on to the mansion -- the same order the prologue walks it. Frames land in
// Screens/VillageWalk and are encoded to mp4 by ffmpeg outside Unity.
//
// Windowed only (HDRP renders white under -batchmode on this project) and self-exiting, matching
// GmVillageLightingLab.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmVillageFlythrough
{
    const string LogTag = "GmVillageWalk";
    const int Width = 1600;
    const int Height = 900;
    const int WarmupFrames = 60;    // let HDRP converge before the first captured frame
    const int SettlePerShot = 2;    // editor frames between captures, so temporal effects advance
    const int RenderPasses = 10;
    const int Frames = 240;         // at 24fps -> 10s of walk

    // Start north of the gate at the car, finish just short of the porch steps.
    const float StartT = 74f;
    const float EndT = -74f;
    const float EyeHeight = 1.7f;

    static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "Screens", "VillageWalk");

    static Camera rig;
    static Terrain terrain;
    static int frame;
    static int warm;
    static int settle;
    static Vector3[] path;
    static float[] cumulative;   // arc length at each path point, so speed stays constant

    [MenuItem("GamesMaster/Village/Walkthrough Capture")]
    public static void Run()
    {
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);

        EditorSceneManager.OpenScene(GmVillageBuilder.VillageScenePath, OpenSceneMode.Single);
        terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) throw new InvalidOperationException("village scene has no Terrain");

        var go = new GameObject("GmVillageWalkRig") { hideFlags = HideFlags.HideAndDontSave };
        rig = go.AddComponent<Camera>();
        rig.enabled = false;
        rig.nearClipPlane = 0.05f;
        rig.farClipPlane = 800f;
        rig.fieldOfView = 62f;
        go.AddComponent<HDAdditionalCameraData>();

        BuildPath();

        frame = 0;
        warm = 0;
        settle = 0;
        Debug.Log($"[{LogTag}] START {Frames} frames over {path.Length} path points, " +
                  $"length {cumulative[cumulative.Length - 1]:0.#}m -> {OutDir}");
        EditorApplication.update += Tick;
    }

    /// The route is the ROAD, not the spine. GmVillageBuilder persists the showcase cameras as an
    /// ordered RoadWaypoints chain precisely because the village road curves; walking the straight
    /// principal axis instead put the camera through the wall of a shed. Beyond the waypoints at
    /// either end the road is open ground, so the straight spine is the correct extension there.
    static void BuildPath()
    {
        var points = new System.Collections.Generic.List<Vector3>();
        points.Add(Ground(GmVillageEstate.SpinePoint(StartT)));

        GameObject wpRoot = GameObject.Find(GmVillageBuilder.RoadWaypointsName);
        if (wpRoot != null)
        {
            // The road is measured over a wider span than the prologue walks, so it is trimmed to
            // the car-to-porch stretch. Without the filter the tail waypoints run PAST the mansion
            // and the capture would double back north to reach the manually appended end point.
            foreach (Transform wp in wpRoot.transform)
            {
                float t = Vector2.Dot(
                    new Vector2(wp.position.x, wp.position.z) - GmVillageEstate.SpineCentroid,
                    GmVillageEstate.SpineAxis);
                if (t <= StartT && t >= EndT) points.Add(wp.position);
            }
        }
        else
        {
            Debug.LogWarning($"[{LogTag}] no {GmVillageBuilder.RoadWaypointsName}; falling back to the " +
                             "straight spine, which is known to clip buildings");
        }

        points.Add(Ground(GmVillageEstate.SpinePoint(EndT)));

        // Chaikin smoothing: the waypoints are camera stances, so the raw chain has corners a walker
        // would not take. Two passes round them into a drivable curve without pulling it off the road.
        for (int pass = 0; pass < 2; pass++)
        {
            var next = new System.Collections.Generic.List<Vector3> { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                next.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                next.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
            }
            next.Add(points[points.Count - 1]);
            points = next;
        }

        path = points.ToArray();
        cumulative = new float[path.Length];
        for (int i = 1; i < path.Length; i++)
            cumulative[i] = cumulative[i - 1] +
                Vector2.Distance(new Vector2(path[i - 1].x, path[i - 1].z), new Vector2(path[i].x, path[i].z));
    }

    static Vector3 Ground(Vector2 xz) =>
        new Vector3(xz.x, terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y, xz.y);

    static Vector3 SamplePath(float u, out Vector3 forward)
    {
        float target = Mathf.Clamp01(u) * cumulative[cumulative.Length - 1];
        int i = 1;
        while (i < cumulative.Length - 1 && cumulative[i] < target) i++;

        float span = Mathf.Max(0.0001f, cumulative[i] - cumulative[i - 1]);
        float f = Mathf.Clamp01((target - cumulative[i - 1]) / span);
        Vector3 p = Vector3.Lerp(path[i - 1], path[i], f);

        // Look further down the path than the next point, so the gaze leads the walk instead of
        // swinging at every segment join.
        int look = Mathf.Min(path.Length - 1, i + 6);
        forward = path[look] - p;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        return p;
    }

    static void Tick()
    {
        if (warm < WarmupFrames) { warm++; PoseAt(0); return; }
        if (++settle < SettlePerShot) return;
        settle = 0;

        if (frame >= Frames) { Finish(); return; }

        PoseAt(frame / (float)(Frames - 1));
        Grab(frame);
        frame++;
    }

    static void PoseAt(float u)
    {
        Vector3 p = SamplePath(u, out Vector3 forward);
        float groundY = terrain.SampleHeight(new Vector3(p.x, 0f, p.z)) + terrain.transform.position.y;

        // A slight head bob and a slow drift of gaze keep this from reading as a dolly on rails. Both
        // are deterministic functions of path position, so the capture is reproducible frame for frame.
        float bob = Mathf.Sin(u * Mathf.PI * 34f) * 0.035f;
        float sway = Mathf.Sin(u * Mathf.PI * 6f) * 2.4f;

        rig.transform.position = new Vector3(p.x, groundY + EyeHeight + bob, p.z);
        rig.transform.rotation =
            Quaternion.Euler(Mathf.Sin(u * Mathf.PI * 4f) * 1.1f, sway, 0f) *
            Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    static void Grab(int index)
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

        File.WriteAllBytes(Path.Combine(OutDir, $"frame_{index:D4}.png"), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);

        if (index % 40 == 0) Debug.Log($"[{LogTag}] frame {index}/{Frames}");
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (rig != null) UnityEngine.Object.DestroyImmediate(rig.gameObject);
        Debug.Log($"[{LogTag}] WALK COMPLETE {frame} frames -> {OutDir}");
        EditorApplication.Exit(0);
    }
}

