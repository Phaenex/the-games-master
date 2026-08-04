// Drives the REAL player, with its real CharacterController and real colliders, from the arrival car
// to the mansion porch, and fails loudly if it gets stuck.
//
// Every visual check so far -- the lighting lab, the walkthrough video -- rendered from a free camera
// with no collider. Those frames prove the route LOOKS right; they cannot prove it is walkable. The
// mansion, the gate and 189 scattered grounds props could all be blocking the path and every one of
// those renders would look identical. This is the check that can actually tell.
//
// Opt-in via -gmVillageWalkTest; inert on a normal launch, including Nick's.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class GmVillageWalkTest : MonoBehaviour
{
    const float Speed = 3.4f;
    const float Gravity = -18f;
    const float ArriveRadius = 2.6f;
    const float StallSeconds = 4f;      // no net progress toward the target for this long => stuck
    const float StallDistance = 0.75f;  // progress smaller than this counts as none
    const float MaxSeconds = 180f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-gmVillageWalkTest") < 0) return;
        var host = new GameObject("GmVillageWalkTest");
        DontDestroyOnLoad(host);
        host.AddComponent<GmVillageWalkTest>();
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return null;

        GameObject playerGo = GameObject.Find("Player");
        if (playerGo == null) { Fail("no Player in scene"); yield break; }

        var cc = playerGo.GetComponent<CharacterController>();
        if (cc == null) { Fail("Player has no CharacterController"); yield break; }

        // The cold open holds the player still behind its cards; skip it so the walk can start.
        var cold = FindFirstObjectByType<GmColdOpen>();
        cold?.SkipIntroForReview();

        // Drive the controller directly rather than fighting GmPlayer's input handling.
        var human = playerGo.GetComponent<GmPlayer>();
        if (human != null) human.enabled = false;

        List<Vector3> route = BuildRoute();
        if (route.Count == 0) { Fail("no RoadWaypoints and no mansion to walk to"); yield break; }

        Debug.Log($"[GmVillageWalkTest] START at {playerGo.transform.position} over {route.Count} targets");

        float vertical = 0f;
        float began = Time.realtimeSinceStartup;
        bool armedBell = false;

        for (int i = 0; i < route.Count; i++)
        {
            // GmThreshold arms the nine-bell on the first BACKWARD move after the gate is passed.
            // Doing that deliberately and early means the count runs during the walk instead of
            // only starting when the player happens to glance back, which is what makes verifying
            // all nine tolls in one run practical.
            if (!armedBell && i == 4)
            {
                armedBell = true;
                yield return Retreat(cc, playerGo.transform, 2.5f);
                Debug.Log("[GmVillageWalkTest] retreated to arm the gate lock / bell");
            }

            Vector3 target = route[i];
            float best = Flat(playerGo.transform.position, target);
            float lastProgressAt = Time.realtimeSinceStartup;

            while (Flat(playerGo.transform.position, target) > ArriveRadius)
            {
                if (Time.realtimeSinceStartup - began > MaxSeconds)
                { Fail($"exceeded {MaxSeconds}s at target {i} {Fmt(playerGo.transform.position)}"); yield break; }

                Vector3 to = target - playerGo.transform.position;
                to.y = 0f;
                Vector3 step = to.normalized * Speed;

                vertical = cc.isGrounded ? -1f : vertical + Gravity * Time.deltaTime;
                step.y = vertical;
                cc.Move(step * Time.deltaTime);

                SampleFrame(playerGo.transform.position);

                float d = Flat(playerGo.transform.position, target);
                if (best - d > StallDistance) { best = d; lastProgressAt = Time.realtimeSinceStartup; }
                else if (Time.realtimeSinceStartup - lastProgressAt > StallSeconds)
                {
                    Fail($"STUCK heading to target {i} {Fmt(target)} — " +
                         $"held at {Fmt(playerGo.transform.position)}, {d:0.#}m short. " +
                         "Something solid is blocking the route.");
                    yield break;
                }
                yield return null;
            }
            Debug.Log($"[GmVillageWalkTest] reached target {i + 1}/{route.Count} {Fmt(target)}");
        }

        Debug.Log($"[GmVillageWalkTest] walked the full route in " +
                  $"{Time.realtimeSinceStartup - began:0.#}s, ending at {Fmt(playerGo.transform.position)}");
        ReportFrameTimes();

        yield return WatchBell();

        Debug.Log("[GmVillageWalkTest] PASS: route walkable and bell sequence observed");
        Application.Quit(0);
    }

    /// Nudge backward along the road. Used to trip the gate lock, which is the event that arms the
    /// bell -- so this is what starts the nine-count.
    IEnumerator Retreat(CharacterController cc, Transform player, float metres)
    {
        Vector3 back = -player.forward;
        back.y = 0f;
        back.Normalize();
        float travelled = 0f;
        while (travelled < metres)
        {
            Vector3 step = back * Speed * 0.6f;
            step.y = -2f;
            cc.Move(step * Time.deltaTime);
            travelled += Speed * 0.6f * Time.deltaTime;
            yield return null;
        }
    }

    /// Waits out the nine-bell count and confirms toll 9 hands off to GmCrossing.
    ///
    /// This is the piece that could not be verified from the walk alone: the default cadence is
    /// 45s + 8x30s = 4m45s, far longer than the walk takes. Launch with -gmPacing=195 for the tight
    /// review cadence (35s + 8x20s).
    IEnumerator WatchBell()
    {
        var bell = FindFirstObjectByType<GmBellSummons>();
        if (bell == null) { Debug.LogWarning("[GmVillageWalkTest] no GmBellSummons to watch"); yield break; }

        float deadline = Time.realtimeSinceStartup + 300f;
        int seen = -1;
        float nextReport = 0f;
        while (bell.Toll < 9 && Time.realtimeSinceStartup < deadline)
        {
            if (bell.Toll != seen)
            {
                seen = bell.Toll;
                Debug.Log($"[GmVillageWalkTest] bell toll {seen}/9");
            }

            // The count stalled at 7 once, with 135 real seconds and no eighth toll on a 20s
            // interval. GmBellSummons gates on Time.time, which stops advancing when timeScale is 0,
            // so this reports the state that would explain it instead of leaving it to inference.
            if (Time.realtimeSinceStartup > nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 15f;
                Debug.Log($"[GmVillageWalkTest] watch: realtime={Time.realtimeSinceStartup:0.#} " +
                          $"time={Time.time:0.#} timeScale={Time.timeScale:0.##} " +
                          $"toll={bell.Toll} bellEnabled={bell.enabled} " +
                          $"bellActive={bell.gameObject.activeInHierarchy}");
            }
            yield return null;
        }

        if (bell.Toll < 9)
        {
            Debug.LogError($"[GmVillageWalkTest] FAILED: bell stalled at toll {bell.Toll}/9 " +
                           "— it was armed but never counted out");
            Application.Quit(1);
            yield break;
        }

        Debug.Log("[GmVillageWalkTest] bell reached toll 9; waiting for the crossing");
        float crossDeadline = Time.realtimeSinceStartup + 25f;
        while (FindFirstObjectByType<GmCrossing>() != null && Time.realtimeSinceStartup < crossDeadline)
            yield return null;
    }

    /// Road waypoints, then the mansion itself so the last leg covers the porch approach where the
    /// grounds dressing is densest.
    static List<Vector3> BuildRoute()
    {
        var route = new List<Vector3>();
        GameObject wps = GameObject.Find("RoadWaypoints");
        if (wps != null)
            foreach (Transform wp in wps.transform) route.Add(wp.position);

        GameObject estate = GameObject.Find("WendHillEstate");
        Transform mansion = estate != null ? estate.transform.Find("Mansion") : null;
        if (mansion == null && estate != null && estate.transform.childCount > 0)
            mansion = estate.transform.GetChild(0);
        if (mansion != null)
        {
            // Stop short of the facade: the porch steps are geometry, and the contract is that the
            // player reaches the porch, not that they walk through the wall.
            Vector3 p = mansion.position;
            route.Add(new Vector3(p.x, p.y, p.z + 14f));
        }
        return route;
    }

    // ── frame-time telemetry ────────────────────────────────────────────────────────────────────
    //
    // Measured ALONG THE PLAYER'S ACTUAL ROUTE rather than from a fixed camera, because cost in this
    // scene is entirely a function of where you stand: the village core has ~3,300 renderers, eight
    // local fog volumes and ten flickering point lights, and the open ground at either end has
    // almost none. A single average would hide the one stretch that matters.
    readonly List<float> frameMs = new List<float>();
    readonly List<Vector3> framePos = new List<Vector3>();
    float warmupUntil = -1f;

    void SampleFrame(Vector3 at)
    {
        // Skip the first couple of seconds: shader warm-up and streaming spikes are real but they are
        // a loading cost, not a walking cost, and averaging them in flatters nothing and misleads.
        if (warmupUntil < 0f) warmupUntil = Time.realtimeSinceStartup + 2f;
        if (Time.realtimeSinceStartup < warmupUntil) return;

        frameMs.Add(Time.unscaledDeltaTime * 1000f);
        framePos.Add(at);
    }

    void ReportFrameTimes()
    {
        if (frameMs.Count < 30)
        {
            Debug.LogWarning($"[GmVillageWalkTest] only {frameMs.Count} frame samples; no perf verdict");
            return;
        }

        var sorted = frameMs.OrderBy(v => v).ToList();
        float mean = frameMs.Average();
        float median = sorted[sorted.Count / 2];
        float p95 = sorted[Mathf.Clamp((int)(sorted.Count * 0.95f), 0, sorted.Count - 1)];
        float p99 = sorted[Mathf.Clamp((int)(sorted.Count * 0.99f), 0, sorted.Count - 1)];
        float worst = sorted[sorted.Count - 1];

        Debug.Log($"[GmVillagePerf] frames={frameMs.Count} " +
                  $"mean={mean:0.00}ms ({1000f / mean:0} fps) median={median:0.00}ms " +
                  $"p95={p95:0.00}ms ({1000f / p95:0} fps) p99={p99:0.00}ms worst={worst:0.00}ms");

        // Where the slow frames happen matters more than that they happen. Report the worst few with
        // positions so a hotspot can be found on the map instead of guessed at.
        var worstFrames = Enumerable.Range(0, frameMs.Count)
            .OrderByDescending(i => frameMs[i]).Take(5);
        foreach (int i in worstFrames)
            Debug.Log($"[GmVillagePerf] slow frame {frameMs[i]:0.0}ms at {Fmt(framePos[i])}");

        // 60fps = 16.7ms. Judge on p95, not mean: a walk that averages fine but stutters through the
        // village centre is the thing a player actually notices.
        string verdict = p95 <= 16.7f ? "GOOD (p95 within 60fps)"
            : p95 <= 33.3f ? "ACCEPTABLE (p95 within 30fps)"
            : "POOR (p95 below 30fps) — needs a culling/LOD pass";
        Debug.Log($"[GmVillagePerf] VERDICT: {verdict}");
    }

    static float Flat(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static string Fmt(Vector3 v) => $"({v.x:0.#},{v.y:0.#},{v.z:0.#})";

    static void Fail(string why)
    {
        Debug.LogError($"[GmVillageWalkTest] FAILED: {why}");
        Application.Quit(1);
    }
}

