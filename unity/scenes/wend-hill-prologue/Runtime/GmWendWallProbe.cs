// Walks the real player into each of the four boundary walls and the estate gate's fence wings,
// including offsets beyond the old 45m cutoff, and checks they stop.
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
    const float EvidenceRetreat = 4f;

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

            // The verdict above belongs to the contact position. Photographing from that same point
            // put the near plane inside terrain or hard against an invisible boundary collider, so
            // the evidence showed a wall of clipped pixels instead of the place that was tested.
            // Retreat only after measurement, back toward the playable side, and keep looking at the
            // attacked plane. This changes no collision result; it makes the screenshot judgeable.
            MoveForEvidence(cc, ended - outward * EvidenceRetreat, outward, terrain);
            yield return new WaitForSecondsRealtime(0.25f);

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

        GameObject gate = GameObject.Find("EstateGate");
        if (gate == null) { Finish("no EstateGate; the gate perimeter cannot be attacked", 1); yield break; }
        GmGateLeaves leaves = gate.GetComponent<GmGateLeaves>();
        if (leaves == null) { Finish("EstateGate has no GmGateLeaves", 1); yield break; }
        if (gate.transform.Find("PerimeterWingLeft") == null ||
            gate.transform.Find("PerimeterWingRight") == null)
        {
            Finish("EstateGate is missing a perimeter wing", 1);
            yield break;
        }

        leaves.SetClosedImmediate();
        Physics.SyncTransforms();
        int gateHeld = 0, gateBreached = 0;
        foreach (float offset in new[] { -55f, 55f })
        {
            Vector3 start = gate.transform.position + gate.transform.right * offset - gate.transform.forward * 2f;
            float ground = terrain != null
                ? terrain.SampleHeight(start) + terrain.transform.position.y
                : gate.transform.position.y;
            start.y = ground + 1.0f;
            playerGo.transform.rotation = Quaternion.LookRotation(gate.transform.forward, Vector3.up);

            GmPhysicalIntegrityProbe.BypassAttemptResult attempt =
                GmPhysicalIntegrityProbe.AttemptBypass(
                    cc, start, gate.transform.forward, 6f,
                    gate.transform.position, gate.transform.forward);
            bool heldByFence = attempt.blocked && !attempt.penetratedBarrier;
            if (heldByFence) gateHeld++; else gateBreached++;

            MoveForEvidence(cc, attempt.finalPosition - gate.transform.forward * EvidenceRetreat,
                gate.transform.forward, terrain);
            yield return new WaitForSecondsRealtime(0.25f);

            string side = offset < 0f ? "Left" : "Right";
            yield return Shot($"EstateGate-{side}-{Mathf.Abs(offset):0}m-{(heldByFence ? "held" : "BREACHED")}");
            if (heldByFence)
                Debug.Log($"[GmWendWallProbe] EstateGate {side} wing held beyond the former 45m cutoff: " +
                          GmPhysicalIntegrityProbe.Describe(attempt));
            else
                Debug.LogError($"[GmWendWallProbe] EstateGate {side} wing BREACHED beyond the former " +
                               "45m cutoff: " + GmPhysicalIntegrityProbe.Describe(attempt));
        }

        Finish($"{held} wall(s) held, {breached} wall(s) breached; {gateHeld} gate wing(s) held, " +
               $"{gateBreached} gate wing(s) breached; {missingScreenshots} missing screenshot(s)",
            breached == 0 && gateBreached == 0 && missingScreenshots == 0 ? 0 : 1);
    }

    static void MoveForEvidence(CharacterController controller, Vector3 position, Vector3 look,
        Terrain terrain)
    {
        float ground = terrain != null
            ? terrain.SampleHeight(position) + terrain.transform.position.y
            : position.y - 1f;
        position.y = ground + 1f;
        controller.enabled = false;
        controller.transform.SetPositionAndRotation(position,
            Quaternion.LookRotation(look, Vector3.up));
        controller.enabled = true;
        Physics.SyncTransforms();
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
