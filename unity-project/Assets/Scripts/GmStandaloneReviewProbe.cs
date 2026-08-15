// Opt-in player-build visual smoke probe. It captures the actual backbuffer including runtime UI,
// skips the intro, proves the spawn view and look path, then exits. It never runs in normal play.
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public sealed class GmStandaloneReviewProbe : MonoBehaviour
{
    string outputDirectory;
    bool autoExit;
    bool runtimeFailure;
    string firstFailure;
    InputSettings.BackgroundBehavior savedBackgroundBehavior;

    static bool s_loadRenderFailure;
    static string s_loadRenderFailureMessage;

    // Tree/terrain instancing render-integrity warnings (e.g. "couldn't be instanced ... no valid
    // mesh renderer") are emitted DURING scene load, before an AfterSceneLoad probe's Awake can
    // subscribe to logMessageReceived. That gap is exactly how four non-rendering reed prototypes
    // slipped past a green "zero runtime errors" proof. Subscribe before the scene loads so those
    // load-time warnings are captured. Guarded on the review flag so normal launches stay inert.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallLoadTimeRenderFailureWatch()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-gmReviewCaptureDir") < 0) return;
        s_loadRenderFailure = false;
        s_loadRenderFailureMessage = null;
        Application.logMessageReceived += ObserveLoadLog;
    }

    static void ObserveLoadLog(string condition, string stackTrace, LogType type)
    {
        if (!GmRuntimeIntegrityPolicy.IsRenderFailure(condition)) return;
        s_loadRenderFailure = true;
        s_loadRenderFailureMessage ??= condition;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallWhenRequested()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int directoryFlag = System.Array.IndexOf(args, "-gmReviewCaptureDir");
        if (directoryFlag < 0 || directoryFlag + 1 >= args.Length) return;
        var host = new GameObject("GmStandaloneReviewProbe");
        DontDestroyOnLoad(host);
        var probe = host.AddComponent<GmStandaloneReviewProbe>();
        probe.outputDirectory = args[directoryFlag + 1];
        probe.autoExit = System.Array.IndexOf(args, "-gmReviewAutoExit") >= 0;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        // A launched macOS player is not guaranteed to become the frontmost application. By default
        // the Input System disables non-background devices on focus loss, including the virtual pad
        // this opt-in proof creates. Keep the probe deterministic without changing normal gameplay.
        savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-gmReviewNoVSync") >= 0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            Debug.Log("[GmStandaloneProbe] vSync OFF, 120 fps cap for frame-pacing measurement");
        }
        Directory.CreateDirectory(outputDirectory);
        yield return ValidateNinthBellAudio();
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Capture("01-cold-open-ui.png");

        var cold = FindAnyObjectByType<GmColdOpen>();
        var player = FindAnyObjectByType<GmPlayer>();
        if (player == null)
        {
            Debug.LogError("[GmStandaloneProbe] FAILED: no GmPlayer");
            if (autoExit) Application.Quit(2);
            yield break;
        }
        yield return ValidateControllerInput(player, cold);
        if (cold != null && cold.IsRunning) cold.SkipIntroForReview();
        yield return new WaitForSecondsRealtime(1.0f);
        HideTransientReviewUi();
        // The review is supposed to compare authored poses, not the operator's live mouse. A locked
        // macOS cursor can still report hardware deltas between SetReviewPose and the next frame;
        // block normal input for the capture sequence while continuing to exercise ApplyLookDelta
        // explicitly below. Normal launches never install this opt-in probe.
        player.SetControlBlocked(true);
        Debug.Log($"[GmStandaloneProbe] spawn={player.transform.position} forward={player.transform.forward} " +
                  $"blocked={player.ControlBlocked} pointer={player.HasPointerCapture}");
        var route = FindAnyObjectByType<GmRouteSpline>();
        Vector3 expectedForward = route != null ? route.TangentAt(0f) : player.transform.forward;
        if (Vector3.Dot(player.transform.forward, expectedForward) < 0.98f)
        {
            runtimeFailure = true;
            firstFailure ??= $"standalone spawn rotated away from route: forward={player.transform.forward} expected={expectedForward}";
        }
        yield return Capture("02-spawn-facing-mansion.png");

        player.ApplyLookDelta(new Vector2(180f, -30f));
        yield return new WaitForSecondsRealtime(0.5f);
        HideTransientReviewUi();
        yield return Capture("03-look-path.png");

        SetReviewPoseAt(player, "beat-03", "chapel", 2f);
        yield return new WaitForSecondsRealtime(0.45f);
        HideTransientReviewUi();
        yield return Capture("04-middrive-depth.png");

        SetReviewPoseAt(player, "branch-03", "chapel", 2f, 5f);
        yield return new WaitForSecondsRealtime(0.45f);
        HideTransientReviewUi();
        yield return Capture("05-chapel-composition.png");

        SetReviewPoseAt(player, "branch-05", "garden-shed", 6f);
        yield return new WaitForSecondsRealtime(0.45f);
        HideTransientReviewUi();
        yield return Capture("06-garden-composition.png");

        SetReviewPoseAt(player, "beat-05", "manor-porch", 4f, 7f);
        yield return new WaitForSecondsRealtime(0.45f);
        HideTransientReviewUi();
        yield return Capture("07-porch-composition.png");

        yield return MeasureFramePacing();

        if (s_loadRenderFailure)
        {
            runtimeFailure = true;
            firstFailure ??= s_loadRenderFailureMessage;
        }
        if (runtimeFailure)
        {
            Debug.LogError($"[GmStandaloneProbe] FAILED: runtime error during visual capture: {firstFailure}");
            if (autoExit) Application.Quit(3);
            yield break;
        }
        Debug.Log("[GmStandaloneProbe] PASS: 7/7 player-backbuffer frames, performance sampled, zero runtime errors or render-integrity warnings");
        if (autoExit) Application.Quit(0);
    }

    IEnumerator ValidateControllerInput(GmPlayer player, GmColdOpen cold)
    {
        var ambience = FindAnyObjectByType<GmAmbience>();
        var runtime = FindAnyObjectByType<GmDesignRuntime>();
        var camera = player.GetComponentInChildren<Camera>();
        Vector3 savedPosition = player.transform.position;
        Quaternion savedRotation = player.transform.rotation;
        Quaternion savedCameraRotation = camera != null ? camera.transform.localRotation : Quaternion.identity;
        GmWindReviewProfile savedWind = ambience != null ? ambience.reviewProfile : GmWindReviewProfile.HybridBreathing;
        Gamepad gamepad = null;
        GameObject target = null;
        float moved = 0f;
        float dpadMoved = 0f;
        float turned = 0f;
        bool interacted = false;
        bool windCycled = false;
        bool pauseRoundTrip = false;
        bool brightnessAdjusted = false;

        try
        {
            gamepad = InputSystem.AddDevice<Gamepad>();
            gamepad.MakeCurrent();
            // Device registration is not instant, and a single yield was the only wait before the
            // first state event was queued. Wait for the device to actually be added instead of
            // assuming one frame is enough on a machine under load.
            yield return WaitUntil(() => gamepad.added && InputSystem.devices.Contains(gamepad), 60);

            int firstCard = cold != null ? cold.CardNumber : -1;
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.South), 4);
            yield return SendGamepad(gamepad, new GamepadState(), 2);
            if (cold != null && cold.CardNumber <= firstCard)
            {
                yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.South), 4);
                yield return SendGamepad(gamepad, new GamepadState(), 2);
            }
            if (!player.UsingGamepad || cold == null || cold.CardNumber <= firstCard)
                ControllerFailure("A/Cross did not advance the cold open or select controller prompts");

            var hud = FindAnyObjectByType<GmPrologueHud>();
            var document = hud?.GetComponent<UIDocument>();
            var prompt = document?.rootVisualElement.Q<Label>("Prompt");
            // Assert the state, not the wording. Matching prompt prose made every copy improvement a
            // proof failure, which is how a passing gate ends up defending bad UI.
            if (prompt == null || string.IsNullOrWhiteSpace(prompt.text) ||
                hud == null || !hud.PromptUsesControllerLabels)
                ControllerFailure("built-player cold-open prompt did not switch to controller labels");
            yield return Capture("controller-01-cold-open.png");

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return SendGamepad(gamepad, new GamepadState());
            if (cold == null || cold.IsRunning || player.ControlBlocked)
                ControllerFailure("B/Circle did not skip the cold open and restore control");

            Vector3 beforeMove = player.transform.position;
            float beforeYaw = player.transform.eulerAngles.y;
            yield return HoldGamepad(gamepad, new GamepadState
            {
                leftStick = new Vector2(0f, 0.78f),
            }, 0.20f);
            yield return SendGamepad(gamepad, new GamepadState());
            moved = Vector3.Distance(beforeMove, player.transform.position);
            yield return HoldGamepad(gamepad, new GamepadState
            {
                rightStick = new Vector2(0.72f, 0.18f)
            }, 0.16f);
            yield return SendGamepad(gamepad, new GamepadState());
            turned = Mathf.Abs(Mathf.DeltaAngle(beforeYaw, player.transform.eulerAngles.y));
            if (moved <= 0.08f) ControllerFailure($"left stick moved only {moved:F3}m");
            if (turned <= 0.5f) ControllerFailure($"right stick turned only {turned:F3} degrees");

            Vector3 beforeDpad = player.transform.position;
            yield return HoldGamepad(gamepad,
                new GamepadState().WithButton(GamepadButton.DpadUp), 0.16f);
            yield return SendGamepad(gamepad, new GamepadState());
            dpadMoved = Vector3.Distance(beforeDpad, player.transform.position);
            if (dpadMoved <= 0.03f) ControllerFailure($"D-pad moved only {dpadMoved:F3}m");

            if (camera != null && runtime != null)
            {
                target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "StandaloneControllerInteractionProof";
                target.transform.position = camera.transform.position + camera.transform.forward * 1.45f;
                target.transform.localScale = Vector3.one * 0.28f;
                // A runtime primitive gets the built-in default material, which HDRP cannot render,
                // so Unity substitutes Hidden/InternalErrorShader — magenta, 1.4m from the camera,
                // in the middle of the evidence frames. This proof needs the collider to test a
                // focused interaction; it never needed to draw. Instrumentation must not appear in
                // the composition it is verifying.
                var proofRenderer = target.GetComponent<Renderer>();
                if (proofRenderer != null) proofRenderer.enabled = false;
                var interactable = target.AddComponent<GmInteractable>();
                interactable.Configure("standalone-controller-proof", "Examine", 3f, 7f);
                interactable.BindContent("The built player received the controller interaction.",
                    "The built player received it again.");
                Physics.SyncTransforms();
                player.InteractionScanner.Scan();
                yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.South));
                yield return SendGamepad(gamepad, new GamepadState());
                interacted = interactable.Uses == 1 && runtime.lastExamine != null &&
                    runtime.lastExamine.Contains("controller interaction");
            }
            if (!interacted) ControllerFailure("A/Cross did not execute a focused interaction in the built player");

            if (ambience != null)
            {
                string original = ambience.ReviewProfileId;
                yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                yield return SendGamepad(gamepad, new GamepadState());
                windCycled = ambience.ReviewProfileId != original;
            }
            if (!windCycled) ControllerFailure("RB/R1 did not cycle the built-player wind profile");

            // Same pose, one frame earlier, unpaused. The pause frame carries a magenta region that
            // no other frame shows; this isolates whether it belongs to the world or to the pause
            // state, which no amount of edit-time testing can answer.
            yield return Capture("controller-03-prepause-same-pose.png");
            LogWhatOccupies(new Vector2(962f, 547f));

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.Start));
            yield return SendGamepad(gamepad, new GamepadState());
            bool pausedCorrectly = player.IsPaused && Time.timeScale == 0f && AudioListener.pause;
            yield return Capture("controller-02-pause-menu.png");
            GmDisplayCalibration display = player.DisplayCalibration;
            int beforeBrightness = display != null ? display.Level : int.MinValue;
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight));
            yield return SendGamepad(gamepad, new GamepadState());
            brightnessAdjusted = display != null && display.Level ==
                GmDisplayCalibration.ClampLevel(beforeBrightness + 1);
            // The claim is "the pause UI shows the calibration and D-pad moved it", so assert the
            // level changed and the brightness control is actually on screen — not that a particular
            // label happens to contain the word BRIGHTNESS.
            var brightnessValue = document?.rootVisualElement.Q<Label>("BrightnessValue");
            if (!brightnessAdjusted || brightnessValue == null ||
                string.IsNullOrWhiteSpace(brightnessValue.text) ||
                hud == null || !hud.BrightnessUiVisible)
                ControllerFailure("D-pad Right did not adjust the display calibration in the pause UI");
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.DpadLeft));
            yield return SendGamepad(gamepad, new GamepadState());
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return SendGamepad(gamepad, new GamepadState());
            pauseRoundTrip = pausedCorrectly && !player.IsPaused && Time.timeScale > 0f && !AudioListener.pause;
            if (!pauseRoundTrip) ControllerFailure("Menu/Options pause and A/Cross resume did not round-trip");

            var controls = document?.rootVisualElement.Q<Label>("Controls");
            if (controls == null || string.IsNullOrWhiteSpace(controls.text) ||
                hud == null || !hud.ControlsUseControllerLabels)
                ControllerFailure("built-player control legend did not expose the complete gamepad layout");

            if (!runtimeFailure)
                Debug.Log($"[GmStandaloneProbe] CONTROLLER PASS: cold-open, move={moved:F3}m, " +
                    $"dpad={dpadMoved:F3}m, look={turned:F2}deg, interact, wind, brightness, " +
                    $"pause/resume, controller UI");
        }
        finally
        {
            if (player.IsPaused) player.SetPaused(false);
            if (ambience != null) ambience.RestoreReviewProfileForProof(savedWind, true);
            if (target != null) Destroy(target);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            player.transform.position = savedPosition;
            player.transform.rotation = savedRotation;
            if (camera != null) camera.transform.localRotation = savedCameraRotation;
            Physics.SyncTransforms();
        }
    }

    /// Waits for a condition instead of a frame count. Fixed-frame waits encode an assumption about
    /// how fast the machine is, which is why these assertions failed under load and passed on retry.
    /// Bounded, so a genuinely broken path still fails rather than hanging.
    static IEnumerator WaitUntil(System.Func<bool> condition, int maxFrames)
    {
        for (int i = 0; i < maxFrames && !condition(); i++) yield return null;
    }

    static IEnumerator SendGamepad(Gamepad gamepad, GamepadState state, int frames = 2)
    {
        InputSystem.QueueStateEvent(gamepad, state);
        // DO NOT call InputSystem.Update() here. Tried 2026-08-03 to make the edge deterministic for
        // the flaky cold-open assertion; it made things strictly worse. A manual flush consumes the
        // event inside this call, so wasPressedThisFrame is already false by the time any consumer's
        // Update runs that frame — every assertion then failed, including left stick 0.000m and
        // right stick 0.000 degrees. The natural flush is what game code is synchronised against.
        for (int i = 0; i < frames; i++) yield return null;
    }

    static IEnumerator HoldGamepad(Gamepad gamepad, GamepadState state, float seconds)
    {
        InputSystem.QueueStateEvent(gamepad, state);
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    void ControllerFailure(string message)
    {
        runtimeFailure = true;
        firstFailure ??= $"controller: {message}";
        Debug.LogError($"[GmStandaloneProbe] CONTROLLER FAILED: {message}");
    }

    IEnumerator ValidateNinthBellAudio()
    {
        string[] names = { "chapel_bell", "clock_chime", "heartbeat", "ear_whine", "whisper_bed" };
        float[] minimum = { 6.5f, 5.9f, 7.1f, 7.9f, 8.8f };
        float[] maximum = { 7.5f, 6.1f, 7.3f, 8.1f, 9.1f };
        int valid = 0;
        for (int i = 0; i < names.Length; i++)
        {
            var clip = Resources.Load<AudioClip>($"Sfx/{names[i]}");
            if (clip == null)
            {
                runtimeFailure = true;
                firstFailure ??= $"built player is missing Ninth Bell clip {names[i]}";
                Debug.LogError($"[GmStandaloneProbe] AUDIO FAILED: missing {names[i]}");
                continue;
            }
            if (!clip.LoadAudioData())
            {
                runtimeFailure = true;
                firstFailure ??= $"built player could not start decoding Ninth Bell clip {names[i]}";
                Debug.LogError($"[GmStandaloneProbe] AUDIO FAILED: decode start {names[i]}");
                continue;
            }
            float deadline = Time.realtimeSinceStartup + 3f;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (clip.loadState == AudioDataLoadState.Failed || clip.length < minimum[i] ||
                clip.length > maximum[i] || clip.channels != 2)
            {
                runtimeFailure = true;
                firstFailure ??= $"built Ninth Bell clip invalid: {names[i]} state={clip.loadState} " +
                    $"seconds={clip.length:F3} channels={clip.channels}";
                Debug.LogError($"[GmStandaloneProbe] AUDIO FAILED: {names[i]} state={clip.loadState} " +
                    $"seconds={clip.length:F3} channels={clip.channels}");
                continue;
            }
            valid++;
            Debug.Log($"[GmStandaloneProbe] AUDIO {names[i]} seconds={clip.length:F3} " +
                $"channels={clip.channels} rate={clip.frequency} state={clip.loadState}");
        }
        if (valid == names.Length)
            Debug.Log("[GmStandaloneProbe] AUDIO PASS: 5/5 final Ninth Bell clips loaded and decoded");
    }

    [System.Serializable]
    sealed class FramePacingDocument
    {
        public int schemaVersion = 2;
        public int width;
        public int height;
        public float internalRenderScale;
        public int internalWidth;
        public int internalHeight;
        public string upscaleFilter;
        public float lodBias;
        public bool lodCrossFade;
        public int maxQueuedFrames;
        public int sampleFrames;
        public float meanMilliseconds;
        public float p50Milliseconds;
        public float p95Milliseconds;
        public float p99Milliseconds;
        public float maximumMilliseconds;
        public int vSyncCount;
        public int targetFrameRate;
        // Conditions the number was taken under. Without these a p95 is unfalsifiable: on 2026-08-15
        // gate 8 read 19.25ms and the honest answer to "is that a regression?" required digging a
        // historical range out of a tracker doc. A measurement that does not carry its own conditions
        // invites the reader to supply a cause, and the supplied cause is usually wrong.
        public int processorCount;
        public int systemMemoryMegabytes;
        public string graphicsDevice;
        public bool batchMode;
        // How well this run agrees with itself. See MeasureFramePacing for why a tail percentile
        // needs this and a median does not.
        public float p95FirstHalfMilliseconds;
        public float p95SecondHalfMilliseconds;
        public float p95HalfSpreadPercent;
        // Why the tail looks the way it does. See MeasureFramePacing.
        public int gcCollections;
        public long managedGrowthKB;
        public int spikeFrames;
        public float spikeFloorMilliseconds;
        public int firstSpikeFrame;
        public int longestQuietRunFrames;
    }

    /// Percentile over a half-open slice, without disturbing the caller's ordering.
    static float PercentileOf(float[] values, int from, int to, float percentile)
    {
        int count = to - from;
        var slice = new float[count];
        System.Array.Copy(values, from, slice, 0, count);
        System.Array.Sort(slice);
        return slice[Mathf.Clamp(Mathf.CeilToInt((count - 1) * percentile), 0, count - 1)];
    }

    /// Measure the actual built player's steady-state backbuffer cadence after screenshots finish.
    /// Screenshot readback and file IO are deliberately outside the sample. The JSON is evidence,
    /// not a claim that this one Mac represents every Steam target; the Node proof applies only a
    /// conservative sustained-sub-30-fps failure floor.
    IEnumerator MeasureFramePacing()
    {
        float warmupSeconds = 10f;
        string[] args = System.Environment.GetCommandLineArgs();
        int flag = System.Array.IndexOf(args, "-gmReviewWarmupSeconds");
        if (flag >= 0 && flag + 1 < args.Length && float.TryParse(args[flag + 1],
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out float configured)) warmupSeconds = Mathf.Max(0f, configured);
        // 240 frames is roughly 2.5 seconds, which puts p95 at the twelfth-worst frame. Hitches are
        // rare and clustered, so the twelfth-worst is dominated by whether a cluster happened to land
        // inside the window. Measured 2026-08-15: three back-to-back runs of the SAME build on the
        // same idle machine produced p95 of 18.39, 21.17 and 24.32ms -- a 34% spread. The four values
        // this project had recorded as a performance TREND (19.25, 19.72, 20.83, 21.71) all sit
        // inside that one build's noise. "Best on record" and "it regressed" were both unfalsifiable.
        //
        // p50 over the same runs was 8.44, 8.45, 8.48 -- so the median is stable and pinned to the
        // 120fps limiter. Only the tail is noisy, and only the tail is what the budget tests.
        const int sampleFrames = 1800;
        float warmUntil = Time.realtimeSinceStartup + warmupSeconds;
        while (Time.realtimeSinceStartup < warmUntil) yield return new WaitForEndOfFrame();

        var milliseconds = new float[sampleFrames];
        double previous = Time.realtimeSinceStartupAsDouble;
        double total = 0d;
        // Attribution, not just a number. p50 sits on the 120fps limiter and p95 is 2.5x it, so the
        // budget is missed by a small number of hitches rather than by the scene being heavy. That
        // shape has a short list of usual causes and managed GC is top of it, so count collections
        // and bytes across the same window that produces the percentile. A spike count with nothing
        // beside it invites the reader to supply a cause, which is how the mansion collision got
        // blamed for something it did not do.
        int gc0Before = System.GC.CollectionCount(0);
        int gc1Before = System.GC.CollectionCount(1);
        long managedBefore = System.GC.GetTotalMemory(false);
        for (int i = 0; i < sampleFrames; i++)
        {
            yield return new WaitForEndOfFrame();
            double now = Time.realtimeSinceStartupAsDouble;
            float elapsed = (float)((now - previous) * 1000d);
            previous = now;
            milliseconds[i] = elapsed;
            total += elapsed;
        }
        int gcCollections = (System.GC.CollectionCount(0) - gc0Before) + (System.GC.CollectionCount(1) - gc1Before);
        long managedGrowthKB = (System.GC.GetTotalMemory(false) - managedBefore) / 1024;

        // Where the spikes are, not just how many. Evenly spread says "something every N frames";
        // clustered says "one event", and those want completely different investigations.
        float spikeFloor = 2f * (float)(total / sampleFrames);
        int spikes = 0, longestQuietRun = 0, currentQuiet = 0, firstSpike = -1;
        for (int i = 0; i < sampleFrames; i++)
        {
            if (milliseconds[i] > spikeFloor)
            {
                spikes++;
                if (firstSpike < 0) firstSpike = i;
                if (currentQuiet > longestQuietRun) longestQuietRun = currentQuiet;
                currentQuiet = 0;
            }
            else currentQuiet++;
        }
        if (currentQuiet > longestQuietRun) longestQuietRun = currentQuiet;

        // Split-half agreement, computed BEFORE the sort destroys the ordering. Each half is an
        // independent estimate of the same quantity, so the gap between them is this measurement's
        // own reproducibility, measured in the run that produced it rather than assumed from a past
        // one. A number that cannot say how repeatable it is has no business failing a build.
        float p95First = PercentileOf(milliseconds, 0, sampleFrames / 2, 0.95f);
        float p95Second = PercentileOf(milliseconds, sampleFrames / 2, sampleFrames, 0.95f);

        System.Array.Sort(milliseconds);
        float Percentile(float percentile)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt((milliseconds.Length - 1) * percentile),
                0, milliseconds.Length - 1);
            return milliseconds[index];
        }

        var document = new FramePacingDocument {
            width = Screen.width,
            height = Screen.height,
            internalRenderScale = GmWendRenderBudget.LastResolvedScale,
            internalWidth = Mathf.CeilToInt(Screen.width * GmWendRenderBudget.LastResolvedScale),
            internalHeight = Mathf.CeilToInt(Screen.height * GmWendRenderBudget.LastResolvedScale),
            upscaleFilter = GmWendRenderBudget.LastResolvedFilter.ToString(),
            lodBias = QualitySettings.lodBias,
            lodCrossFade = QualitySettings.enableLODCrossFade,
            maxQueuedFrames = QualitySettings.maxQueuedFrames,
            sampleFrames = sampleFrames,
            meanMilliseconds = (float)(total / sampleFrames),
            p50Milliseconds = Percentile(0.50f),
            p95Milliseconds = Percentile(0.95f),
            p99Milliseconds = Percentile(0.99f),
            maximumMilliseconds = milliseconds[milliseconds.Length - 1],
            vSyncCount = QualitySettings.vSyncCount,
            targetFrameRate = Application.targetFrameRate,
            processorCount = SystemInfo.processorCount,
            systemMemoryMegabytes = SystemInfo.systemMemorySize,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            batchMode = Application.isBatchMode,
            p95FirstHalfMilliseconds = p95First,
            p95SecondHalfMilliseconds = p95Second,
            p95HalfSpreadPercent = Mathf.Approximately(Mathf.Min(p95First, p95Second), 0f) ? 0f :
                100f * Mathf.Abs(p95First - p95Second) / Mathf.Min(p95First, p95Second),
            gcCollections = gcCollections,
            managedGrowthKB = managedGrowthKB,
            spikeFrames = spikes,
            spikeFloorMilliseconds = spikeFloor,
            firstSpikeFrame = firstSpike,
            longestQuietRunFrames = longestQuietRun,
        };
        string path = Path.Combine(outputDirectory, "performance.json");
        File.WriteAllText(path, JsonUtility.ToJson(document, true));
        Debug.Log($"[GmStandaloneProbe] PERF: output={document.width}x{document.height} " +
            $"internal={document.internalWidth}x{document.internalHeight} " +
            $"scale={document.internalRenderScale:P0} filter={document.upscaleFilter} frames={sampleFrames} " +
            $"mean={document.meanMilliseconds:F2}ms p50={document.p50Milliseconds:F2}ms " +
            $"p95={document.p95Milliseconds:F2}ms p99={document.p99Milliseconds:F2}ms " +
            $"max={document.maximumMilliseconds:F2}ms " +
            $"p95halves={document.p95FirstHalfMilliseconds:F2}/{document.p95SecondHalfMilliseconds:F2}ms " +
            $"(spread {document.p95HalfSpreadPercent:F0}%) " +
            $"| spikes={document.spikeFrames}/{sampleFrames} over {document.spikeFloorMilliseconds:F1}ms " +
            $"first@{document.firstSpikeFrame} longestQuiet={document.longestQuietRunFrames} " +
            $"gc={document.gcCollections} managed+{document.managedGrowthKB}KB " +
            $"cores={document.processorCount} ram={document.systemMemoryMegabytes}MB " +
            $"gpu='{document.graphicsDevice}' batch={document.batchMode}");
    }

    static void SetReviewPose(GmPlayer player, Vector3 position, float yaw, float pitch)
    {
        player.transform.position = position;
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var camera = player.GetComponentInChildren<Camera>();
        if (camera != null) camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// Names whatever occupies a screen point. Five hypotheses about one magenta region each cost a
    /// full rebuild-and-measure cycle to disprove; asking the running player directly costs one.
    static void LogWhatOccupies(Vector2 screenPoint)
    {
        Camera camera = Camera.main;
        if (camera == null) { Debug.Log("[GmProbePoint] no main camera"); return; }
        Debug.Log($"[GmProbePoint] probing screen {screenPoint} of {Screen.width}x{Screen.height}");

        Ray ray = camera.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            Debug.Log($"[GmProbePoint] raycast hit '{HierarchyPath(hit.collider.transform)}' at {hit.distance:0.0}m");
        else Debug.Log("[GmProbePoint] raycast hit nothing (object may have no collider)");

        int reported = 0;
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (!renderer.isVisible || !renderer.enabled) continue;
            Bounds bounds = renderer.bounds;
            Vector3 view = camera.WorldToScreenPoint(bounds.center);
            if (view.z <= 0f) continue;
            // Screen-space radius of the bounding sphere, so a billboard or quad is caught even
            // when it has no collider for the raycast to find.
            Vector3 edge = camera.WorldToScreenPoint(bounds.center + camera.transform.right * bounds.extents.magnitude);
            float radius = Mathf.Abs(edge.x - view.x);
            if (Vector2.Distance(new Vector2(view.x, view.y), screenPoint) > Mathf.Max(radius, 8f)) continue;
            var material = renderer.sharedMaterial;
            Debug.Log($"[GmProbePoint] covers point: '{HierarchyPath(renderer.transform)}' " +
                      $"dist={view.z:0.0}m shader='{(material != null && material.shader != null ? material.shader.name : "NULL")}' " +
                      $"material='{(material != null ? material.name : "NULL")}'");
            if (++reported >= 12) break;
        }
        if (reported == 0) Debug.Log("[GmProbePoint] no visible Renderer covers that point — " +
            "it is drawn by Terrain, a particle system, or post-processing");
    }

    static string HierarchyPath(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (Transform c = t; c != null; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static void SetReviewPoseAt(GmPlayer player, string atId, string lookId, float pitch, float back = 0f)
    {
        GmWorldAnchor at = GmWorldAnchor.Find(atId);
        GmWorldAnchor look = GmWorldAnchor.Find(lookId);
        if (at == null || look == null)
        {
            Debug.LogError($"[GmStandaloneProbe] FAILED: missing review anchor {atId}->{lookId}");
            return;
        }
        Vector3 direction = look.transform.position - at.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        Vector3 position = at.transform.position - direction.normalized * back + Vector3.up * 0.1f;
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        SetReviewPose(player, position, yaw, pitch);
    }

    static void HideTransientReviewUi()
    {
        FindAnyObjectByType<GmPrologueHud>()?.HideTransientForReview();
    }

    IEnumerator Capture(string fileName)
    {
        string path = Path.Combine(outputDirectory, fileName);
        ScreenCapture.CaptureScreenshot(path, 1);
        float deadline = Time.realtimeSinceStartup + 12f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!File.Exists(path))
        {
            runtimeFailure = true;
            firstFailure ??= $"screenshot missing {path}";
            Debug.LogError($"[GmStandaloneProbe] FAILED: screenshot missing {path}");
            yield break;
        }
        byte[] encoded = File.ReadAllBytes(path);
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!texture.LoadImage(encoded, false))
        {
            runtimeFailure = true;
            firstFailure ??= $"screenshot could not be decoded {path}";
            Destroy(texture);
            yield break;
        }
        Color32[] pixels = texture.GetPixels32();
        var luminance = new byte[pixels.Length];
        int black = 0, white = 0;
        long sum = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = (byte)((pixels[i].r + pixels[i].g + pixels[i].b) / 3);
            luminance[i] = value;
            sum += value;
            if (value <= 2) black++;
            if (value >= 253) white++;
        }
        System.Array.Sort(luminance);
        int p05 = luminance[Mathf.FloorToInt((luminance.Length - 1) * 0.05f)];
        int p95 = luminance[Mathf.CeilToInt((luminance.Length - 1) * 0.95f)];
        float blackFraction = black / (float)pixels.Length;
        float whiteFraction = white / (float)pixels.Length;
        Destroy(texture);
        if (encoded.Length < 10000 || p95 - p05 < 6 || blackFraction > 0.97f || whiteFraction > 0.25f)
        {
            runtimeFailure = true;
            firstFailure ??= $"invalid screenshot {fileName}: p05={p05} p95={p95} black={blackFraction:P1} white={whiteFraction:P1}";
        }
        Debug.Log($"[GmStandaloneProbe] captured {fileName} bytes={encoded.Length} " +
                  $"meanLum={sum / pixels.Length} p05={p05} p95={p95} " +
                  $"black={blackFraction:P1} white={whiteFraction:P1}");
    }

    void Awake() => Application.logMessageReceived += ObserveLog;

    void ObserveLog(string condition, string stackTrace, LogType type)
    {
        bool brokenUi = condition.Contains("No Theme Style Sheet") || condition.Contains("ICU Data not available");
        bool renderIntegrity = GmRuntimeIntegrityPolicy.IsRenderFailure(condition);
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert &&
            !condition.Contains(" FAILED") && !brokenUi && !renderIntegrity) return;
        runtimeFailure = true;
        firstFailure ??= condition;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= ObserveLog;
        if (InputSystem.settings != null)
            InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
    }
}
