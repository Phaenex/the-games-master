// Walks the REAL player down the REAL road in a BUILT PLAYER, capturing the backbuffer as it goes.
//
// This exists because of two lessons that each cost a round of wrong conclusions:
//
//   1. Editor frames do not prove what the player renders. Every review rig in this project renders a
//      camera it creates and aims itself, so none of them went through a scene camera. The first
//      prologue app shipped rendering the pack's showcase camera instead of the player's and nothing
//      in the editor could have shown it.
//   2. macOS `screencapture` cannot capture this app's window content in the automation environment.
//      ScreenCapture.CaptureScreenshot reads the app's own backbuffer and needs no screen-recording
//      permission, which is the same reason GmVillageSelfProbe exists.
//
// It drives the actual CharacterController rather than teleporting a free camera, so the frames prove
// the route is WALKABLE and not merely photogenic. A free camera passes straight through the mansion,
// the gate and 189 scattered props, and every one of those frames looks correct.
//
// Opt-in via -gmWendWalk <outputDirectory>; inert on a normal launch, including Nick's.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

public sealed class GmWendWalkProbe : MonoBehaviour
{
    const float Speed = 3.4f;
    const float Gravity = -18f;
    const float TurnDegreesPerSecond = 220f;
    const float ArriveRadius = 3.0f;
    const float CaptureEveryMeters = 15f;
    const float SettleSeconds = 4f;     // HDRP volumetrics and the sky need real frames before shot 1

    // 15m spacing over a 580m route needs 39 shots. The old 32 silently truncated at 465m, which is
    // the kind of cap that reads as "the walk covered everything" in a directory listing. It was not
    // the binding constraint while stalls capped the walk at 188m, and it becomes the binding one the
    // moment the route is actually walkable, so it is raised now and reported when it is reached.
    const int MaxCaptures = 48;
    const float MaxSeconds = 480f;      // sidesteps cost time; the old 300s cut the walk short
    const float StallSeconds = 5f;
    const float SidestepSeconds = 1.6f;
    const int MaxSidesteps = 2;
    const float StallDistance = 0.75f;
    const float FellOutOfWorld = 25f;   // metres below the spawn means the floor stopped existing

    // Road matching, waypoint spacing and the max-hop limit used to be duplicated here to feed a second
    // route builder. GmWendRoute owns all of that now, and it is the only place that should.

    /// How far off a waypoint may be from the baked mesh and still be reachable. Route waypoints are
    /// road-mesh bounding-box centres, so they sit near the surface rather than exactly on it.
    const float NavSampleRadius = 6f;

    string outputDirectory;
    int captures;
    int stalls;
    int pathed;         // waypoints reached via a real NavMesh path
    int straightLined;  // waypoints the mesh could not path to, walked as a straight line instead

    // Frame pacing, sampled WHILE WALKING.
    //
    // GmStandaloneReviewProbe already measures steady-state pacing from a fixed pose, which answers a
    // different question. This scene carries 4997 renderers and nothing had ever measured it in
    // motion, where streaming, culling and the volumetric fog's temporal accumulation actually cost
    // something. Traversal is the case the prologue is made of.
    readonly List<float> frameMilliseconds = new List<float>();
    double lastFrameAt;
    bool discardNextInterval;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int flag = System.Array.IndexOf(args, "-gmWendWalk");
        if (flag < 0 || flag + 1 >= args.Length) return;

        var host = new GameObject("GmWendWalkProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<GmWendWalkProbe>().outputDirectory = args[flag + 1];
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

        // Drive the controller directly rather than fighting the input handler.
        var human = playerGo.GetComponent<GmPlayer>();
        if (human != null) human.enabled = false;

        // The same route the player was spawned at the start of. Deriving it separately here is exactly
        // how the spawn and the walk came to describe two different places.
        List<Vector3> route = GmWendRoute.Build(out string routeReport);
        if (route.Count == 0) { Finish("no road meshes to walk", 1); yield break; }
        Debug.Log($"[GmWendWalkProbe] route\n{routeReport}");
        Debug.Log($"[GmWendWalkProbe] START at {playerGo.transform.position} over {route.Count} waypoint(s)");

        yield return new WaitForSecondsRealtime(SettleSeconds);
        yield return Capture(playerGo.transform.position, 0f);

        float began = Time.realtimeSinceStartup;
        lastFrameAt = Time.realtimeSinceStartupAsDouble;
        float nextCaptureAt = CaptureEveryMeters;
        float vertical = 0f;
        float walked = 0f;
        float spawnY = playerGo.transform.position.y;

        for (int i = 0; i < route.Count && captures < MaxCaptures; i++)
        {
            // Route to the waypoint through the NavMesh, so a building in the way becomes a path
            // around it instead of five seconds spent walking into it. With no baked mesh this
            // returns the waypoint itself and the walk behaves exactly as it did before.
            List<Vector3> corners = StepsToward(playerGo.transform.position, route[i]);
            bool abandonWaypoint = false;

            foreach (Vector3 target in corners)
            {
                if (abandonWaypoint || captures >= MaxCaptures) break;

                float stallTimer = 0f;
                float bestDistance = float.MaxValue;
                int sidesteps = 0;

                while (Time.realtimeSinceStartup - began < MaxSeconds && captures < MaxCaptures)
                {
                    SampleFrameInterval();

                    Vector3 here = playerGo.transform.position;

                    // Falling out of the world is a real defect, and it silently produced two black
                    // frames that a luma check alone would have read as "the night got darker". Stop
                    // and say so. GmWendCatchPlane now recovers the player in a normal run; this stays
                    // fatal here because a probe exists to report the hole, not to survive it.
                    if (here.y < spawnY - FellOutOfWorld)
                    {
                        Debug.LogError($"[GmWendWalkProbe] FELL OUT OF THE WORLD at {here}, " +
                                       $"{spawnY - here.y:0}m below the spawn.");
                        yield return Capture(here, walked, "fell");
                        Finish($"walked {walked:0}m before falling out of the world", 1);
                        yield break;
                    }

                    Vector3 flat = new Vector3(target.x - here.x, 0f, target.z - here.z);
                    float distance = flat.magnitude;
                    if (distance <= ArriveRadius) break;

                    // Face where we are going, so the frames look down the road rather than sideways.
                    Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    playerGo.transform.rotation = Quaternion.RotateTowards(
                        playerGo.transform.rotation, want, TurnDegreesPerSecond * Time.deltaTime);

                    vertical = cc.isGrounded ? -1f : vertical + Gravity * Time.deltaTime;
                    Vector3 step = flat.normalized * Speed * Time.deltaTime;
                    step.y = vertical * Time.deltaTime;
                    cc.Move(step);

                    float moved = Vector3.Distance(new Vector3(here.x, 0f, here.z),
                        new Vector3(playerGo.transform.position.x, 0f, playerGo.transform.position.z));
                    walked += moved;

                    // Fire on fixed distance MILESTONES, and name the frame after the milestone rather
                    // than after the distance actually reached. An accumulator that resets to zero
                    // drifts by whatever fraction of a frame overshot each threshold, so two runs would
                    // land on 45.2m and 45.9m and produce filenames that do not line up. Milestones
                    // make walk-0045m mean the same place in every run, which is the point.
                    if (walked >= nextCaptureAt)
                    {
                        yield return Capture(playerGo.transform.position, nextCaptureAt);
                        nextCaptureAt += CaptureEveryMeters;
                    }

                    // Stuck detection. A wall, a prop or a ledge the controller cannot climb is a real
                    // finding about the route, not a reason to abandon the rest of the walk, so it is
                    // recorded and the walk moves on.
                    if (distance < bestDistance - StallDistance) { bestDistance = distance; stallTimer = 0f; }
                    else stallTimer += Time.deltaTime;

                    if (stallTimer >= StallSeconds)
                    {
                        // Sidesteps predate the NavMesh and are kept deliberately. The mesh routes
                        // around what it knows about at bake time; it does not know about anything the
                        // pack placed without a collider, or about the controller's own skin width. A
                        // stall AFTER a path was returned is the interesting case, because it means the
                        // mesh and the body that walks it disagree.
                        if (sidesteps < MaxSidesteps)
                        {
                            sidesteps++;
                            Debug.Log($"[GmWendWalkProbe] blocked {distance:0.0}m short of waypoint {i}, " +
                                      $"sidestep {sidesteps} of {MaxSidesteps}");
                            yield return Sidestep(cc, playerGo, flat, sidesteps % 2 == 1 ? 1f : -1f);
                            stallTimer = 0f;
                            bestDistance = float.MaxValue;
                            continue;
                        }

                        stalls++;
                        Debug.LogWarning($"[GmWendWalkProbe] STALLED {stalls} at {playerGo.transform.position} " +
                                         $"{distance:0.0}m short of waypoint {i} after {MaxSidesteps} sidestep(s); " +
                                         "skipping to the next");
                        yield return Capture(playerGo.transform.position, walked, "stall");

                        // Abandon the whole waypoint, not just this corner. The remaining corners were
                        // computed from a position the player never reached, so steering at them walks
                        // a path that no longer starts where it was planned from.
                        abandonWaypoint = true;
                        break;
                    }

                    yield return null;
                }
            }
        }

        // Name the reason the walk ended. Hitting a cap and finishing the route produce the same
        // cheerful summary otherwise, and "walked 465m, 48 frames" reads as coverage rather than as
        // truncation. Whatever bounded the walk gets said out loud.
        string why = "";
        if (captures >= MaxCaptures)
            why = $"; STOPPED AT THE {MaxCaptures}-FRAME CAP, the route past {walked:0}m is unmeasured";
        else if (Time.realtimeSinceStartup - began >= MaxSeconds)
            why = $"; STOPPED AT THE {MaxSeconds:0}s LIMIT, the route past {walked:0}m is unmeasured";

        // Say whether the NavMesh was actually doing anything. Without this a run with no baked mesh
        // and a run where the mesh pathed every waypoint produce identical summaries.
        string nav = pathed + straightLined == 0
            ? ""
            : $", {pathed} waypoint(s) pathed on the NavMesh and {straightLined} walked straight" +
              (pathed == 0 ? " (NO NAVMESH REACHED THE ROUTE: this walk had no pathfinding at all)" : "");

        WritePacing(walked);
        Finish($"walked {walked:0}m, {captures} frame(s), {stalls} stall(s){nav}{why}", 0);
    }

    // A private BuildRoute lived here: an unreferenced second route builder using a global principal
    // axis. GmWendRoute's own header records that exact approach being tried and rejected, because a
    // global axis through scattered clusters describes none of them and left 177m to 944m gaps between
    // consecutive waypoints. Keeping a superseded copy of the route logic inside the walker is the
    // setup for the bug this file's header already warns about, the spawn and the walk describing two
    // different places, so it is deleted rather than left for someone to call by accident.

    /// Records the time since the previous sampled frame.
    ///
    /// Intervals spanning a screenshot or a sidestep are DISCARDED, because both are instrumentation:
    /// ScreenCapture stalls on readback and file IO, and a sidestep yields across many frames inside
    /// its own coroutine. Measuring those would report the harness rather than the game.
    ///
    /// Genuine hitches are deliberately NOT filtered out by magnitude. A 400ms frame caused by the
    /// scene streaming something is exactly the finding a perf pass is for, and dropping outliers to
    /// make a mean look better is how a scene ships stuttering with a clean report.
    void SampleFrameInterval()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float elapsed = (float)((now - lastFrameAt) * 1000d);
        lastFrameAt = now;

        if (discardNextInterval) { discardNextInterval = false; return; }
        if (elapsed > 0f) frameMilliseconds.Add(elapsed);
    }

    [System.Serializable]
    struct WalkPacingDocument
    {
        public int width;
        public int height;
        public float walkedMetres;
        public int sampleFrames;
        public float meanMilliseconds;
        public float p50Milliseconds;
        public float p95Milliseconds;
        public float p99Milliseconds;
        public float maximumMilliseconds;
        public int vSyncCount;
        public int targetFrameRate;
    }

    /// Writes the traversal pacing next to the frames. Evidence, not a verdict: this is one Mac, and
    /// the numbers say what this machine did on this route, not what a Steam target will do.
    void WritePacing(float walked)
    {
        if (frameMilliseconds.Count == 0)
        {
            Debug.LogWarning("[GmWendWalkProbe] no frame intervals sampled; perf not measured");
            return;
        }

        float[] sorted = frameMilliseconds.ToArray();
        System.Array.Sort(sorted);

        float Percentile(float p)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt((sorted.Length - 1) * p), 0, sorted.Length - 1);
            return sorted[index];
        }

        double total = 0d;
        foreach (float ms in sorted) total += ms;

        var document = new WalkPacingDocument
        {
            width = Screen.width,
            height = Screen.height,
            walkedMetres = walked,
            sampleFrames = sorted.Length,
            meanMilliseconds = (float)(total / sorted.Length),
            p50Milliseconds = Percentile(0.50f),
            p95Milliseconds = Percentile(0.95f),
            p99Milliseconds = Percentile(0.99f),
            maximumMilliseconds = sorted[sorted.Length - 1],
            vSyncCount = QualitySettings.vSyncCount,
            targetFrameRate = Application.targetFrameRate,
        };

        File.WriteAllText(Path.Combine(outputDirectory, "walk-performance.json"),
            JsonUtility.ToJson(document, true));

        Debug.Log($"[GmWendWalkProbe] PERF over {walked:0}m: {document.width}x{document.height} " +
                  $"frames={document.sampleFrames} mean={document.meanMilliseconds:F2}ms " +
                  $"p50={document.p50Milliseconds:F2}ms p95={document.p95Milliseconds:F2}ms " +
                  $"p99={document.p99Milliseconds:F2}ms max={document.maximumMilliseconds:F2}ms");
    }

    /// The corners to steer through to reach `to`, from the baked NavMesh when there is one.
    ///
    /// Falls back to the destination itself, which reproduces the old straight-line behaviour exactly.
    /// The fallback is COUNTED and reported rather than taken quietly: a walk that silently reverted to
    /// straight lines would report the same stall count as before while looking like the NavMesh had
    /// been tried, and "the fix did nothing" and "the fix was never running" are different findings.
    ///
    /// PathPartial is kept rather than discarded. A partial path still crosses most of the gap, and
    /// walking it reaches somewhere nearer the waypoint than refusing to move at all does. It is
    /// counted as a straight line because it did not fully connect.
    List<Vector3> StepsToward(Vector3 from, Vector3 to)
    {
        var corners = new List<Vector3>();

        if (NavMesh.SamplePosition(from, out NavMeshHit fromHit, NavSampleRadius, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(to, out NavMeshHit toHit, NavSampleRadius, NavMesh.AllAreas))
        {
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path) &&
                path.status != NavMeshPathStatus.PathInvalid &&
                path.corners.Length > 1)
            {
                // corners[0] is where the player already stands, so steering at it does nothing.
                for (int i = 1; i < path.corners.Length; i++) corners.Add(path.corners[i]);

                if (path.status == NavMeshPathStatus.PathComplete) pathed++;
                else straightLined++;
                return corners;
            }
        }

        straightLined++;
        corners.Add(to);
        return corners;
    }

    /// Strafes perpendicular to the blocked direction, to get out from behind a prop.
    IEnumerator Sidestep(CharacterController cc, GameObject go, Vector3 forward, float sign)
    {
        Vector3 side = Vector3.Cross(Vector3.up, forward.normalized).normalized * sign;
        float until = Time.realtimeSinceStartup + SidestepSeconds;
        while (Time.realtimeSinceStartup < until)
        {
            Vector3 step = side * Speed * Time.deltaTime;
            step.y = -1f * Time.deltaTime;   // stay pinned to the ground while strafing
            cc.Move(step);
            yield return null;
        }

        discardNextInterval = true;
        lastFrameAt = Time.realtimeSinceStartupAsDouble;
    }

    /// Frames are named by DISTANCE, not by capture index, so that walk-0045m is the same place on the
    /// road in every run. Index naming made two walks of different length impossible to compare
    /// frame-to-frame: shot 05 of a stalling run and shot 05 of a clean run are hundreds of metres
    /// apart, and a table built by pairing them off had to be retracted.
    ///
    /// Only "walk" frames are milestone-aligned, so only they get bare distance names. A stall or a
    /// fall happens wherever it happens, there can be several, and they are not comparable across runs
    /// anyway, so those keep an index to stay unique.
    /// The naming contract, public and static so it can be tested without walking anything. The defect
    /// it replaced was invisible in any single run and only showed up when two runs were compared,
    /// which is the kind of thing a test should catch rather than a person reading a contact sheet.
    public static string FrameName(string tag, float metreMark, int index)
    {
        int metres = Mathf.RoundToInt(metreMark);
        return tag == "walk" ? $"walk-{metres:D4}m.png" : $"{tag}-{metres:D4}m-{index:D2}.png";
    }

    IEnumerator Capture(Vector3 at, float metreMark, string tag = "walk")
    {
        string path = Path.Combine(outputDirectory, FrameName(tag, metreMark, captures));
        ScreenCapture.CaptureScreenshot(path, 1);

        float deadline = Time.realtimeSinceStartup + 10f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;

        if (File.Exists(path))
        {
            captures++;
            Debug.Log($"[GmWendWalkProbe] shot {Path.GetFileName(path)} at {at} after {metreMark:0}m " +
                      $"bytes={new FileInfo(path).Length}");
        }
        else Debug.LogError($"[GmWendWalkProbe] screenshot never appeared at {path}");

        // A screenshot costs a readback and a file write. Charging that to the scene would make the
        // walk look like it stutters every 15m when what stutters is the camera taking the picture.
        discardNextInterval = true;
        lastFrameAt = Time.realtimeSinceStartupAsDouble;
    }

    void Finish(string summary, int code)
    {
        Debug.Log($"[GmWendWalkProbe] WALK COMPLETE: {summary}");
        Application.Quit(code);
    }
}
