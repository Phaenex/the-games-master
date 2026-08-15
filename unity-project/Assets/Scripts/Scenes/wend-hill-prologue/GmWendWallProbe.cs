// Walks the real player into each of the four boundary walls and checks they stop.
//
// This exists because "zero catch-plane fires over a 517m walk" is not evidence the walls work. The
// route never goes near the map edge, so that number says only that the player never tried. A wall
// that was never touched is a wall that was never tested, and the contract can only assert that four
// colliders exist with the right sizes, not that they are solid to a CharacterController.
//
// The failure it is looking for is specific and plausible: a BoxCollider whose size was set but whose
// GameObject scale, layer or collision matrix leaves it non-solid to the player, which looks correct
// in every inspector and in the scene contract.
//
// Opt-in via -gmWendWallTest <outputDirectory>; inert on a normal launch.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class GmWendWallProbe : MonoBehaviour
{
    const float Speed = 3.4f;
    const float Gravity = -18f;
    const float StartInset = 12f;    // metres inside the wall to begin
    const float PushSeconds = 9f;    // at 3.4 m/s that is ~30m of walking into a wall 12m away
    const float SettleSeconds = 3f;
    const float Tolerance = 1.5f;    // controller skin width and collider thickness

    string outputDirectory;
    int missingScreenshots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int flag = System.Array.IndexOf(args, "-gmWendWallTest");
        if (flag < 0 || flag + 1 >= args.Length) return;

        var host = new GameObject("GmWendWallProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<GmWendWallProbe>().outputDirectory = args[flag + 1];
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Directory.CreateDirectory(outputDirectory);
        yield return null;

        GameObject playerGo = GameObject.Find("Player");
        if (playerGo == null) { Finish("no Player in the scene", 1); yield break; }
        var cc = playerGo.GetComponent<CharacterController>();
        if (cc == null) { Finish("Player has no CharacterController", 1); yield break; }

        var human = playerGo.GetComponent<GmPlayer>();
        if (human != null) human.enabled = false;
        // Wall evidence must show the wall. The cold open holds four cards over the whole screen for
        // ~21s from scene start, and the first wall is settled, pushed and photographed inside that
        // window: the held/BREACHED verdict is measured from the transform and stays correct, but the
        // screenshot backing it would be a card. Same fix, same reason, as GmWendWalkProbe.
        GmColdOpen coldOpen = FindAnyObjectByType<GmColdOpen>();
        if (coldOpen != null && coldOpen.IsRunning) coldOpen.SkipIntroForReview();

        GameObject boundsRoot = GameObject.Find("GmWendBounds");
        if (boundsRoot == null) { Finish("no GmWendBounds root; the map edge is open", 1); yield break; }

        var walls = new List<BoxCollider>(boundsRoot.GetComponentsInChildren<BoxCollider>(true));
        if (walls.Count == 0) { Finish("GmWendBounds has no colliders", 1); yield break; }

        // The playable box, recovered from the walls themselves rather than recomputed, so the test
        // measures the geometry that actually shipped in the scene.
        Bounds playable = walls[0].bounds;
        foreach (BoxCollider w in walls) playable.Encapsulate(w.bounds);

        var terrain = Terrain.activeTerrain;
        yield return new WaitForSecondsRealtime(SettleSeconds);

        int held = 0, breached = 0;
        foreach (BoxCollider wall in walls)
        {
            // Outward is whichever axis this wall is thin on, pointing away from the centre.
            Vector3 toWall = wall.bounds.center - playable.center;
            Vector3 outward = Mathf.Abs(toWall.x) > Mathf.Abs(toWall.z)
                ? new Vector3(Mathf.Sign(toWall.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(toWall.z));

            Vector3 start = wall.bounds.center - outward * StartInset;
            float ground = terrain != null
                ? terrain.SampleHeight(start) + terrain.transform.position.y
                : start.y;
            start.y = ground + 1.0f;

            cc.enabled = false;
            playerGo.transform.position = start;
            playerGo.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            cc.enabled = true;
            yield return new WaitForSecondsRealtime(1.0f);

            float vertical = 0f;
            float until = Time.realtimeSinceStartup + PushSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                vertical = cc.isGrounded ? -1f : vertical + Gravity * Time.deltaTime;
                Vector3 step = outward * Speed * Time.deltaTime;
                step.y = vertical * Time.deltaTime;
                cc.Move(step);
                yield return null;
            }

            Vector3 ended = playerGo.transform.position;

            // Did the player get past the wall's own plane, on the wall's thin axis?
            float wallPlane = Vector3.Dot(wall.bounds.center, outward);
            float endedAlong = Vector3.Dot(ended, outward);
            bool past = endedAlong > wallPlane + Tolerance;

            if (past) breached++; else held++;

            string tag = past ? "BREACHED" : "held";
            yield return Shot($"{wall.name}-{tag}");

            if (past)
                Debug.LogError($"[GmWendWallProbe] {wall.name} BREACHED: walked to {ended}, which is " +
                               $"{endedAlong - wallPlane:0.0}m past the wall plane. The collider is not " +
                               "solid to the CharacterController.");
            else
                Debug.Log($"[GmWendWallProbe] {wall.name} held: stopped at {ended}, " +
                          $"{wallPlane - endedAlong:0.0}m short of the wall plane after {PushSeconds}s " +
                          "of walking into it.");
        }

        Finish($"{held} wall(s) held, {breached} breached, {missingScreenshots} missing screenshot(s)",
            breached == 0 && missingScreenshots == 0 ? 0 : 1);
    }

    IEnumerator Shot(string label)
    {
        string path = Path.Combine(outputDirectory, $"wall-{label}.png");
        ScreenCapture.CaptureScreenshot(path, 1);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!File.Exists(path))
        {
            missingScreenshots++;
            Debug.LogError($"[GmWendWallProbe] screenshot never appeared at {path}");
        }
    }

    void Finish(string summary, int code)
    {
        if (code == 0) Debug.Log($"[GmWendWallProbe] WALL TEST PASS: {summary}");
        else Debug.LogError($"[GmWendWallProbe] WALL TEST FAIL: {summary}");
        Application.Quit(code);
    }
}
