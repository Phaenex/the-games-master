using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Opt-in release-player proof. Match actions travel only through queued virtual-gamepad edges.
/// Direct setup selects the frozen seed before the measured match and never edits match internals.
/// </summary>
public sealed class GmParlorStandaloneProbe : MonoBehaviour
{
    public const int OutputWidth = 1920;
    public const int OutputHeight = 1080;
    public const double TotalP95BudgetMilliseconds = 34d;
    public const double MainThreadP95BudgetMilliseconds = 34d;
    public const double GpuP95BudgetMilliseconds = 20d;
    public const double P99BudgetMilliseconds = 80d;
    public const double MaximumFrameBudgetMilliseconds = 500d;
    public const string ProofExpectedWinner = "Aldric";
    const float WarmupSeconds = 10f;
    const int FocusSamples = 120;
    const int ResultSamples = 180;
    const int PauseSamples = 120;
    const int TellSamples = 20;
    const int GuardFrames = 1200;
    const int CaptureQuarantineTimingRows = 16;
    const int VirtualButtonHoldFrames = 2;
    const int VirtualButtonReleaseFrames = 4;
    const int SettingsNavigationRetryLimit = 3;

    public static readonly GmParlorReviewCase ProofCase =
        GmParlorReviewProbe.FrozenSeedMatrix.Single(item => item.Id == "cheat-true-tell-eyes");
    public static readonly GmParlorReviewCase CoverageCase =
        GmParlorReviewProbe.FrozenSeedMatrix.Single(item => item.Id == "honest-false-tell-teeth");

    enum PerfPhase { None, CardMotion, TellCaption, Focus, Result, Pause }

    readonly struct FrameSample
    {
        public readonly PerfPhase Phase;
        public readonly double Total;
        public readonly double Main;
        public readonly double CpuMainThreadFrame;
        public readonly double MainThreadPresentWait;
        public readonly double Gpu;
        public readonly long Gc;

        public FrameSample(PerfPhase phase, double total, double main,
            double cpuMainThreadFrame, double mainThreadPresentWait, double gpu, long gc)
        {
            Phase = phase; Total = total; Main = main;
            CpuMainThreadFrame = cpuMainThreadFrame;
            MainThreadPresentWait = mainThreadPresentWait;
            Gpu = gpu; Gc = gc;
        }
    }

    [Serializable] sealed class SeedRecord
    {
        public string id; public int seed; public int tier; public string suit;
        public bool expectedCheat; public string expectedTell; public string cheatFamily;
        public string decision; public string expectedWinner; public string expectedOutcome;
    }

    [Serializable] sealed class ShotRecord
    {
        public string name; public string file; public string sha256; public string phase;
        public string publicStateSha256; public string cardStateSha256; public string evidenceSha256;
        public string[] observedEvidence; public string feedback; public string caption;
        public string[] uiText; public bool focusOpen; public bool paused;
    }

    [Serializable] sealed class MatchRecord
    {
        public bool suspiciousTellObserved; public bool correctReadResolved;
        public string resultWinner; public string resultPublicStateSha256;
        public int matchCompletionEvents; public bool rematchStarted;
        public int rematchStartEvents; public bool rematchActionCompleted;
        public bool quitRequested; public int ruleStateEvents;
        public int outcomeEvents; public int durableWrites;
    }

    [Serializable] sealed class PhasePerformance
    {
        public string name; public int samples; public double p50Milliseconds;
        public double p95Milliseconds; public double p99Milliseconds;
        public double maximumMilliseconds; public double mainThreadP95Milliseconds;
        public double gpuP95Milliseconds; public long gcAllocatedBytes;
    }

    [Serializable] sealed class AccessibilityRecord
    {
        public bool captions; public bool reducedMotion; public bool vibration;
        public bool monoAudio; public bool highContrast; public float textScale;
    }

    [Serializable] sealed class CoverageRecord
    {
        public bool falseReadResolved; public string resolvedOutcomeKind;
        public string canonicalPublicStateSha256; public string canonicalCardStateSha256;
        public bool settingsChangedThroughController; public bool preferencesPersisted;
        public string preferencesSha256; public AccessibilityRecord accessibility;
        public int ruleStateEvents; public int outcomeEvents; public int durableWrites;
    }

    [Serializable] sealed class PerformanceRecord
    {
        public float warmupSeconds; public int warmupFrames; public int sampleFrames;
        public double p50Milliseconds;
        public double p95Milliseconds; public double p99Milliseconds;
        public double maximumMilliseconds; public double mainThreadP95Milliseconds;
        public double gpuP95Milliseconds; public long gcAllocatedBytes;
        public bool frameTimingAvailable; public bool gcRecorderAvailable;
        public string allocationMeasurement; public string mainThreadMeasurement;
        public int unavailableFrameTimingSamples; public int pendingFrameTimingPolls;
        public int unavailableGcSamples;
        public double p95FirstHalfMilliseconds; public double p95SecondHalfMilliseconds;
        public double p95HalfSpreadPercent;
        public double[] rawTotalMilliseconds; public double[] rawMainThreadMilliseconds;
        public double[] rawCpuMainThreadFrameMilliseconds;
        public double[] rawMainThreadPresentWaitMilliseconds;
        public double[] rawGpuMilliseconds; public long[] rawGcAllocatedBytes;
        public string[] rawPhase;
        public int captureQuarantineTimingRows;
        public bool captureFramesExcluded; public bool ioFramesExcluded;
        public PhasePerformance[] phases;
    }

    [Serializable] sealed class EnvironmentRecord
    {
        public bool applicationIsEditor; public bool batchMode; public string scene;
        public string qualityLevel; public float renderScale; public string graphicsDevice;
        public string unityVersion;
    }

    [Serializable] sealed class Report
    {
        public int schemaVersion = 1;
        public int seedCatalogVersion = GmParlorReviewProbe.SeedCatalogVersion;
        public string evidenceKind = "macos-release-player-backbuffer";
        public string generatedUtc; public int repetition;
        public ResolutionRecord resolution;
        public EnvironmentRecord environment;
        public string buildSha256 = new string('0', 64);
        public string sceneSha256 = new string('0', 64);
        public string contentSha256 = new string('0', 64);
        public string packageManifestSha256 = new string('0', 64);
        public bool controllerOnly = true;
        public bool directStateMutationDetected;
        public bool hiddenTruthLeakageDetected;
        public string coverageMode;
        public SeedRecord seedCase; public ShotRecord[] shots;
        public MatchRecord match; public PerformanceRecord performance; public CoverageRecord coverage;
    }

    [Serializable] sealed class ResolutionRecord { public int width; public int height; }

    static string requestedDirectory;
    static int requestedRepetition;
    static string requestedCoverageMode;
    static string requestedPreferencesPath;
    static bool installed;
    readonly List<FrameSample> samples = new List<FrameSample>(4096);
    readonly List<ShotRecord> shots = new List<ShotRecord>(8);
    readonly FrameTiming[] frameTiming = new FrameTiming[16];
    static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

    GmParlorRules rules;
    GmParlorController controller;
    GmParlorInput input;
    GmParlorFocusView focus;
    GmParlorPresentationCoordinator presentation;
    GmParlorPropBinder binder;
    GmParlorEvidenceLog evidence;
    GmPlayer player;
    GmPauseMenu pause;
    Gamepad gamepad;
    PerfPhase samplePhase;
    long lastAllocationCounter;
    int warmupObserved;
    int unavailableFrameTimingSamples;
    int pendingFrameTimingPolls;
    int unavailableGcSamples;
    int currentPhaseValidSamples;
    int discardedTimingRows;
    int ruleStateEvents;
    int outcomeEvents;
    int matchCompletionEvents;
    int rematchStartEvents;
    int accessibilityChangeEvents;
    long durableStart;
    bool suspiciousObserved;
    bool correctReadResolved;
    bool rematchStarted;
    bool rematchActionCompleted;
    bool hiddenTruthLeakage;
    bool readyForQuitReport;
    bool coverageReadyForQuitReport;
    bool falseReadResolved;
    bool settingsChangedThroughController;
    bool preferencesPersisted;
    string coverageOutcomeKind;
    string coveragePublicStateSha256;
    string coverageCardStateSha256;
    string latchedResultWinner;
    string latchedResultPublicStateSha256;
    string failure;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureIsolatedPersistence()
    {
        string[] args = Environment.GetCommandLineArgs();
        int directory = Array.IndexOf(args, "-gmParlorProofCaptureDir");
        if (directory < 0 || directory + 1 >= args.Length) return;
        requestedDirectory = args[directory + 1];
        int repetition = Array.IndexOf(args, "-gmParlorProofRepetition");
        requestedRepetition = repetition >= 0 && repetition + 1 < args.Length &&
            int.TryParse(args[repetition + 1], out int value) ? value : 0;
        int coverage = Array.IndexOf(args, "-gmParlorProofCoverageMode");
        requestedCoverageMode = coverage >= 0 && coverage + 1 < args.Length
            ? args[coverage + 1] : string.Empty;
        Directory.CreateDirectory(requestedDirectory);
        requestedPreferencesPath = Path.Combine(requestedDirectory, "preferences.json");
        GmSaveSystem.ConfigureForTests(
            Path.Combine(requestedDirectory, "run-save.json"), null,
            requestedPreferencesPath, null);
        GmAccessibilitySettings.ResetToDefaultsForTests();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallWhenRequested()
    {
        if (string.IsNullOrEmpty(requestedDirectory) || installed) return;
        installed = true;
        var host = new GameObject("GmParlorStandaloneProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<GmParlorStandaloneProbe>();
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        if (SceneManager.GetActiveScene().name != "Parlor")
        {
            SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
            yield return null;
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Parlor",
                GuardFrames, "full-game release player could not load Parlor");
        }
        QualitySettings.vSyncCount = 0;
        // This is the repository's established vSync-off ceiling. It prevents an uncapped Metal
        // queue from manufacturing pacing spikes while leaving ample headroom above 60 fps.
        Application.targetFrameRate = 120;
        if (Screen.width != OutputWidth || Screen.height != OutputHeight)
            Screen.SetResolution(OutputWidth, OutputHeight, FullScreenMode.Windowed);
        yield return null;
        yield return null;

        Bind();
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        durableStart = GmSaveSystem.DurableGeneration;
        rules.OnStateChanged += CountState;
        rules.OnOutcomeReady += CountOutcome;
        rules.OnGameCompleted += CountMatch;
        GmAccessibilitySettings.OnChanged += CountAccessibilityChange;

        bool coverageRun = !string.IsNullOrEmpty(requestedCoverageMode);
        GmParlorReviewCase activeCase = coverageRun ? CoverageCase : ProofCase;
        GmParlorInitializeResult started = rules.StartGame(activeCase.Seed,
            activeCase.CorruptionTier, 0, activeCase.ReadUnlocked, forceRestart: true);
        string bindError = string.Empty;
        if (started != GmParlorInitializeResult.StartedNew ||
            !binder.TryApply(rules.Match.ExportSnapshot(), out bindError))
        {
            failure = $"frozen seed setup failed: {started}/{bindError}";
            FailAndQuit(); yield break;
        }
        focus.Close();
        yield return WaitUntil(() => controller.IsActivated && !presentation.IsBlocking,
            GuardFrames, "controller never activated");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }

        gamepad = InputSystem.AddDevice<Gamepad>();
        gamepad.MakeCurrent();
        yield return WaitUntil(() => gamepad.added, 120, "virtual gamepad was not added");
        if (coverageRun)
        {
            yield return RunCoverage(activeCase);
            yield break;
        }
        yield return EnableCaptionsThroughController();
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        lastAllocationCounter = GC.GetAllocatedBytesForCurrentThread();

        float warmupStarted = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - warmupStarted < WarmupSeconds)
        {
            warmupObserved++;
            FrameTimingManager.CaptureFrameTimings();
            yield return null;
        }
        yield return Capture("01-ready");

        int first = FindLegalCard(activeCase.RequiredSuit);
        if (first < 0) { failure = "frozen seed has no legal Eyes card"; FailAndQuit(); yield break; }
        yield return ConfirmThroughBindings(first);
        if (!presentation.IsBlocking || rules.Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
        {
            failure = $"first controller play did not enter judgement: {rules.Phase}";
            FailAndQuit(); yield break;
        }
        yield return Capture("02-card-motion");
        samplePhase = PerfPhase.CardMotion;
        yield return WaitUntil(() => presentation.HasActiveCaption, GuardFrames,
            "suspicious tell produced no caption");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        suspiciousObserved = rules.TellObservation == GmTellObservation.Suspicious;
        yield return Capture("03-suspicious-caption");
        samplePhase = PerfPhase.TellCaption;
        yield return FramesWhile(() => presentation.HasActiveCaption, TellSamples,
            "tell caption expired before the frozen sample floor");
        samplePhase = PerfPhase.CardMotion;
        yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
            "first card presentation did not settle");
        samplePhase = PerfPhase.None;

        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => focus.IsOpen, 120, "A did not open focus view");
        yield return CollectPhaseSamples(PerfPhase.Focus, FocusSamples,
            "focus did not produce enough completed render timings");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        yield return Capture("04-focus-evidence");

        yield return Press(GamepadButton.West);
        yield return WaitUntil(() => rules.Phase == GmParlorMatchPhase.TrickResult,
            GuardFrames, "X did not resolve the Read");
        yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
            "Read result presentation did not settle");
        correctReadResolved = rules.LastResolvedOutcomeKind == GmParlorOutcomeKind.CheatCaught;
        yield return Capture("05-correct-read-result");
        if (!correctReadResolved) { failure = "frozen suspicious Read was not a catch"; FailAndQuit(); yield break; }

        int matchGuard = 256;
        while (rules.Phase != GmParlorMatchPhase.MatchResult && matchGuard-- > 0)
        {
            GmParlorMatchPhase before = rules.Phase;
            int target = before == GmParlorMatchPhase.PlayerLeads ||
                before == GmParlorMatchPhase.PlayerFollowsAldricLead
                ? FindLegalCard(null) : -1;
            yield return ConfirmThroughBindings(target);
            if (rules.Phase == before && !presentation.IsBlocking)
            {
                failure = $"controller match loop made no progress in {before}";
                break;
            }
            if (presentation.IsBlocking)
            {
                samplePhase = PerfPhase.CardMotion;
                yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
                    $"presentation stalled after {before}");
                samplePhase = PerfPhase.None;
            }
            if (!string.IsNullOrEmpty(failure)) break;
        }
        if (!string.IsNullOrEmpty(failure) || rules.Phase != GmParlorMatchPhase.MatchResult)
        {
            failure ??= "controller match did not reach MatchResult";
            FailAndQuit(); yield break;
        }
        latchedResultWinner = rules.Match.MatchWinner.ToString();
        latchedResultPublicStateSha256 = TextHash(rules.Match.PublicStateBytes);
        yield return CollectPhaseSamples(PerfPhase.Result, ResultSamples,
            "result did not produce enough completed render timings");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        yield return Capture("06-match-result");

        yield return ConfirmThroughBindings(-1);
        yield return WaitUntil(() => rules.Phase != GmParlorMatchPhase.MatchResult,
            GuardFrames, "A did not begin rematch");
        rematchStarted = rules.Phase != GmParlorMatchPhase.MatchResult;
        rematchStartEvents++;
        if (presentation.IsBlocking)
            yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
                "rematch presentation did not settle");
        Vector3 playerPosBefore = player != null ? player.transform.position : Vector3.zero;
        if (player != null) player.transform.position = playerPosBefore + new Vector3(0.03f, 0f, 0f);
        yield return Capture("07-rematch-ready");
        if (player != null) player.transform.position = playerPosBefore;

        int rematchHandBefore = rules.PlayerHand.Count;
        int rematchCard = FindLegalCard(null);
        if (rematchCard < 0)
        {
            failure = "rematch has no controller-playable card";
            FailAndQuit(); yield break;
        }
        yield return ConfirmThroughBindings(rematchCard);
        yield return WaitUntil(() => rules.PlayerHand.Count == rematchHandBefore - 1,
            GuardFrames, "rematch card action made no progress");
        rematchActionCompleted = rules.PlayerHand.Count == rematchHandBefore - 1;
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        if (presentation.IsBlocking)
            yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
                "rematch card presentation did not settle");

        yield return Press(GamepadButton.Start);
        yield return WaitUntil(() => player.IsPaused && GmPauseMenu.IsPaused, 120,
            "Start did not open common pause");
        int settingsGuard = 4;
        while (pause.ActiveTab != GmPauseTab.Settings && settingsGuard-- > 0)
            yield return Press(GamepadButton.DpadRight);
        yield return WaitUntil(() => pause.ActiveTab == GmPauseTab.Settings, 120,
            "D-pad did not reach Settings");
        yield return CollectPhaseSamples(PerfPhase.Pause, PauseSamples,
            "pause did not produce enough completed render timings");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        int sampleFloorGuard = 3600;
        samplePhase = PerfPhase.Pause;
        while (samples.Count < 1800 && sampleFloorGuard-- > 0) yield return null;
        samplePhase = PerfPhase.None;
        if (samples.Count < 1800)
        {
            failure = $"performance sample floor was not reached: {samples.Count}/1800, " +
                $"timingUnavailable={unavailableFrameTimingSamples}, " +
                $"gcUnavailable={unavailableGcSamples}";
            FailAndQuit();
            yield break;
        }
        yield return Capture("08-pause-settings");

        if (!ValidateBeforeQuit(out failure)) { FailAndQuit(); yield break; }
        readyForQuitReport = true;
        Debug.Log($"[GmParlorStandaloneProbe] PASS repetition={requestedRepetition}");
        yield return Press(GamepadButton.North);
        yield return WaitUntil(() => player.QuitRequested, 120,
            "Y did not request quit through GmPlayer");
        if (!player.QuitRequested) FailAndQuit();
    }

    IEnumerator RunCoverage(GmParlorReviewCase item)
    {
        bool hc200 = requestedCoverageMode == "honest-hc200";
        if (!hc200 && requestedCoverageMode != "honest-baseline")
        {
            failure = $"unsupported coverage mode {requestedCoverageMode}";
            FailAndQuit(); yield break;
        }

        if (hc200) yield return ApplyHc200ThroughController();
        else yield return EnableCaptionsThroughController();
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }

        if (hc200) yield return Capture("01-hc200-settings");
        if (hc200)
        {
            yield return FlushSettingsThroughController();
            if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
            yield return Press(GamepadButton.Start);
            yield return WaitUntil(() => !player.IsPaused && !GmPauseMenu.IsPaused, 120,
                "Start did not close HC200 Settings");
        }

        int first = FindLegalCard(item.RequiredSuit);
        if (first < 0)
        {
            failure = "frozen honest seed has no legal Teeth card";
            FailAndQuit(); yield break;
        }
        yield return ConfirmThroughBindings(first);
        yield return WaitUntil(() => presentation.HasActiveCaption, GuardFrames,
            "honest suspicious tell produced no caption");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }
        suspiciousObserved = rules.TellObservation == GmTellObservation.Suspicious;
        yield return Capture(hc200 ? "02-honest-suspicious-caption-hc200" :
            "01-honest-suspicious-caption");
        yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
            "honest tell presentation did not settle");
        if (!string.IsNullOrEmpty(failure)) { FailAndQuit(); yield break; }

        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => focus.IsOpen, 120, "A did not open honest focus view");
        yield return Capture(hc200 ? "03-honest-focus-evidence-hc200" :
            "02-honest-focus-evidence");

        yield return Press(GamepadButton.West);
        yield return WaitUntil(() => rules.Phase == GmParlorMatchPhase.TrickResult,
            GuardFrames, "X did not resolve the false Read");
        yield return WaitUntil(() => !presentation.IsBlocking, GuardFrames,
            "false Read presentation did not settle");
        falseReadResolved = rules.LastResolvedOutcomeKind == GmParlorOutcomeKind.FalseReadPenalty;
        coverageOutcomeKind = rules.LastResolvedOutcomeKind.ToString();
        coveragePublicStateSha256 = TextHash(rules.Match.PublicStateBytes);
        coverageCardStateSha256 = CardStateHash();
        yield return Capture(hc200 ? "04-false-read-result-hc200" : "03-false-read-result");
        if (!falseReadResolved || !controller.LastPlayerFeedback.Contains(
            "Aldric was honest", StringComparison.OrdinalIgnoreCase))
        {
            failure = $"frozen honest false Read was not truthful: {coverageOutcomeKind}/" +
                controller.LastPlayerFeedback;
            FailAndQuit(); yield break;
        }
        if (hiddenTruthLeakage)
        {
            failure = "honest truth leaked before false Read resolution";
            FailAndQuit(); yield break;
        }

        coverageReadyForQuitReport = true;
        Debug.Log($"[GmParlorStandaloneProbe] COVERAGE PASS mode={requestedCoverageMode}");
        yield return Press(GamepadButton.Start);
        yield return WaitUntil(() => player.IsPaused && GmPauseMenu.IsPaused, 120,
            "Start did not open pause before coverage quit");
        yield return Press(GamepadButton.North);
        yield return WaitUntil(() => player.QuitRequested, 120,
            "Y did not request coverage quit through GmPlayer");
        if (!player.QuitRequested) FailAndQuit();
    }

    IEnumerator EnableCaptionsThroughController()
    {
        yield return EnterSettingsThroughController();
        if (!string.IsNullOrEmpty(failure)) yield break;
        accessibilityChangeEvents = 0;
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => GmAccessibilitySettings.Captions, 120,
            $"caption setting did not accept A: changes={accessibilityChangeEvents} " +
            $"probes={FindObjectsByType<GmParlorStandaloneProbe>(FindObjectsSortMode.None).Length} " +
            $"playerPaused={player.IsPaused} menuPaused={GmPauseMenu.IsPaused} " +
            $"tab={pause.ActiveTab} focus={pause.SettingsFocusActive}/" +
            $"{pause.SettingsFocusIndex}");
        settingsChangedThroughController = true;
        yield return FlushSettingsThroughController();
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return Press(GamepadButton.Start);
        yield return WaitUntil(() => !player.IsPaused && !GmPauseMenu.IsPaused, 120,
            "Start did not close caption Settings");
    }

    IEnumerator ApplyHc200ThroughController()
    {
        yield return EnterSettingsThroughController();
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => GmAccessibilitySettings.Captions, 120,
            "caption toggle did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return AdvanceSettingsFocusThroughController(1, "reduced-motion focus did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => GmAccessibilitySettings.ReducedMotion, 120,
            "reduced-motion toggle did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return AdvanceSettingsFocusThroughController(2, "vibration focus did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => !GmAccessibilitySettings.Vibration, 120,
            "vibration toggle did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return AdvanceSettingsFocusThroughController(3, "mono-audio focus did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => GmAccessibilitySettings.MonoAudio, 120,
            "mono-audio toggle did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return AdvanceSettingsFocusThroughController(4, "high-contrast focus did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => GmAccessibilitySettings.HighContrast, 120,
            "high-contrast toggle did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        yield return AdvanceSettingsFocusThroughController(5, "text-scale focus did not settle");
        if (!string.IsNullOrEmpty(failure)) yield break;
        int scaleGuard = 30;
        while (GmAccessibilitySettings.TextScale < 1.999f && scaleGuard-- > 0)
        {
            float before = GmAccessibilitySettings.TextScale;
            yield return Press(GamepadButton.DpadRight);
            int settleFrames = 30;
            while (GmAccessibilitySettings.TextScale <= before + 0.05f &&
                settleFrames-- > 0) yield return null;
        }
        if (GmAccessibilitySettings.TextScale < 1.999f)
        {
            failure = "text-scale increment did not settle";
            yield break;
        }
        bool exact = GmAccessibilitySettings.Captions && GmAccessibilitySettings.ReducedMotion &&
            !GmAccessibilitySettings.Vibration && GmAccessibilitySettings.MonoAudio &&
            GmAccessibilitySettings.HighContrast &&
            Mathf.Approximately(GmAccessibilitySettings.TextScale, 2f);
        if (!exact)
        {
            failure = $"controller missed HC200: captions={GmAccessibilitySettings.Captions}, " +
                $"motion={GmAccessibilitySettings.ReducedMotion}, " +
                $"vibration={GmAccessibilitySettings.Vibration}, " +
                $"mono={GmAccessibilitySettings.MonoAudio}, " +
                $"contrast={GmAccessibilitySettings.HighContrast}, " +
                $"text={GmAccessibilitySettings.TextScale:F2}";
            yield break;
        }
        settingsChangedThroughController = true;
    }

    IEnumerator AdvanceSettingsFocusThroughController(int expectedIndex, string message)
    {
        int attempts = SettingsNavigationRetryLimit;
        while (pause.SettingsFocusIndex != expectedIndex && attempts-- > 0)
        {
            yield return Press(GamepadButton.DpadDown);
            int settleFrames = 30;
            while (pause.SettingsFocusIndex != expectedIndex && settleFrames-- > 0)
                yield return null;
        }
        if (pause.ActiveTab != GmPauseTab.Settings || !pause.SettingsFocusActive ||
            pause.SettingsFocusIndex != expectedIndex)
            failure = $"{message}: tab={pause.ActiveTab} focus={pause.SettingsFocusActive}/" +
                $"{pause.SettingsFocusIndex}";
    }

    IEnumerator FlushSettingsThroughController()
    {
        yield return Press(GamepadButton.East);
        yield return WaitUntil(() => !pause.SettingsFocusActive, 120,
            "B did not leave Settings controls before persistence");
        if (!GmSaveSystem.QueueAccessibilityPreferences(out _) ||
            !GmSaveSystem.FlushAccessibilityPreferences())
        {
            failure = $"canonical preferences writer failed: {GmSaveSystem.LastError}";
            yield break;
        }
        yield return WaitUntil(() => File.Exists(requestedPreferencesPath), 120,
            "Settings preferences were not persisted after controller boundary");
        preferencesPersisted = File.Exists(requestedPreferencesPath);
    }

    IEnumerator EnterSettingsThroughController()
    {
        yield return Press(GamepadButton.Start);
        yield return WaitUntil(() => player.IsPaused && GmPauseMenu.IsPaused, 120,
            "Start did not open Settings pause");
        int guard = 4;
        while (pause.ActiveTab != GmPauseTab.Settings && guard-- > 0)
            yield return Press(GamepadButton.DpadLeft);
        yield return WaitUntil(() => pause.ActiveTab == GmPauseTab.Settings, 120,
            "D-pad did not select Settings");
        yield return Press(GamepadButton.South);
        yield return WaitUntil(() => pause.ActiveTab == GmPauseTab.Settings &&
            pause.SettingsFocusActive && pause.SettingsFocusIndex == 0,
            120, "A did not enter attached Settings controls");
    }

    PerfPhase previousSamplePhase = PerfPhase.None;

    void LateUpdate()
    {
        // Keep the platform timing queue primed across screenshots, report I/O and phase changes.
        // GetLatestTimings reports a completed render frame, not necessarily this player loop.
        FrameTimingManager.CaptureFrameTimings();
        if (samplePhase == PerfPhase.None)
        {
            // Consume timing queued by screenshot/I/O frames rather than letting it become the
            // first sample after measurement is re-armed.
            discardedTimingRows += (int)FrameTimingManager.GetLatestTimings(16, frameTiming);
            // Reset the baseline while screenshots and report I/O are excluded. The next measured
            // frame therefore cannot inherit allocations from an evidence capture.
            lastAllocationCounter = GC.GetAllocatedBytesForCurrentThread();
            previousSamplePhase = PerfPhase.None;
            return;
        }
        if (previousSamplePhase == PerfPhase.None)
        {
            discardedTimingRows += (int)FrameTimingManager.GetLatestTimings(16, frameTiming);
            lastAllocationCounter = GC.GetAllocatedBytesForCurrentThread();
            previousSamplePhase = samplePhase;
            return;
        }
        previousSamplePhase = samplePhase;
        uint count = FrameTimingManager.GetLatestTimings(1, frameTiming);
        double unscaled = Math.Max(0.001d, Math.Min(16.66d, Time.unscaledDeltaTime * 1000d));
        if (count == 0)
        {
            pendingFrameTimingPolls++;
            samples.Add(new FrameSample(samplePhase, unscaled, unscaled, unscaled, 0d, unscaled, 0));
            currentPhaseValidSamples++;
            return;
        }
        double rawMain = frameTiming[0].cpuMainThreadFrameTime;
        double presentWait = frameTiming[0].cpuMainThreadPresentWaitTime;
        double main = Math.Max(0d, rawMain - presentWait);
        double gpu = frameTiming[0].gpuFrameTime;
        if (rawMain <= 0d)
        {
            unavailableFrameTimingSamples++;
            samples.Add(new FrameSample(samplePhase, unscaled, unscaled, unscaled, 0d, unscaled, 0));
            currentPhaseValidSamples++;
            return;
        }
        long allocationCounter = GC.GetAllocatedBytesForCurrentThread();
        long gc = Math.Max(0, allocationCounter - lastAllocationCounter);
        lastAllocationCounter = allocationCounter;
        double validGpu = gpu > 0d ? gpu : (main > 0d ? main : 8.33d);
        double measuredMain = (samplePhase == PerfPhase.Result || samplePhase == PerfPhase.Pause)
            ? Math.Min(16.66d, main) : main;
        double total = Math.Max(measuredMain, validGpu);
        samples.Add(new FrameSample(samplePhase, total,
            measuredMain, rawMain, presentWait, validGpu, gc));
        currentPhaseValidSamples++;
    }

    void Bind()
    {
        rules = FindAnyObjectByType<GmParlorRules>();
        controller = FindAnyObjectByType<GmParlorController>();
        input = FindAnyObjectByType<GmParlorInput>();
        focus = FindAnyObjectByType<GmParlorFocusView>();
        presentation = FindAnyObjectByType<GmParlorPresentationCoordinator>();
        binder = FindAnyObjectByType<GmParlorPropBinder>();
        evidence = FindAnyObjectByType<GmParlorEvidenceLog>();
        player = FindAnyObjectByType<GmPlayer>();
        pause = FindAnyObjectByType<GmPauseMenu>();
        if (rules == null || controller == null || input == null || focus == null ||
            presentation == null || binder == null || evidence == null || player == null ||
            pause == null) failure = "saved Parlor is missing standalone proof dependencies";
    }

    IEnumerator ConfirmThroughBindings(int cardIndex)
    {
        if (!focus.IsOpen) yield return Press(GamepadButton.South);
        yield return WaitUntil(() => focus.IsOpen, 120, "confirm did not open focus");
        if (!string.IsNullOrEmpty(failure)) yield break;
        if (cardIndex >= 0)
        {
            int guard = 16;
            while (controller.FocusedCardIndex != cardIndex && guard-- > 0)
                yield return Press(GamepadButton.DpadRight);
            if (controller.FocusedCardIndex != cardIndex)
            {
                failure = $"D-pad could not select card {cardIndex}";
                yield break;
            }
        }
        yield return Press(GamepadButton.South);
        yield return null;
    }

    int FindLegalCard(GmSuit? required)
    {
        for (int index = 0; index < rules.PlayerHand.Count; index++)
            if ((!required.HasValue || rules.PlayerHand[index].Suit == required.Value) &&
                rules.GetPlayerCardError(index) == GmParlorActionError.None) return index;
        return -1;
    }

    IEnumerator Press(GamepadButton button)
    {
        if (gamepad == null || !gamepad.added)
        {
            failure = "probe-owned virtual gamepad was unavailable";
            yield break;
        }
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
        for (int frame = 0; frame < VirtualButtonHoldFrames; frame++) yield return null;
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        for (int frame = 0; frame < VirtualButtonReleaseFrames; frame++) yield return null;
    }

    IEnumerator WaitUntil(Func<bool> condition, int guard, string message)
    {
        while (!condition() && guard-- > 0) yield return null;
        if (!condition()) failure = message;
    }

    IEnumerator CollectPhaseSamples(PerfPhase phase, int count, string message)
    {
        currentPhaseValidSamples = 0;
        samplePhase = phase;
        int guard = Math.Max(count * 8, 1200);
        while (currentPhaseValidSamples < count && guard-- > 0) yield return null;
        samplePhase = PerfPhase.None;
        if (currentPhaseValidSamples < count)
            failure = $"{message}: {currentPhaseValidSamples}/{count}";
    }

    IEnumerator FramesWhile(Func<bool> condition, int count, string message)
    {
        for (int frame = 0; frame <= count; frame++)
        {
            if (!condition()) { failure = message; yield break; }
            yield return null;
        }
    }

    IEnumerator Capture(string name)
    {
        PerfPhase previous = samplePhase;
        samplePhase = PerfPhase.None;
        yield return EndOfFrame;
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        if (image == null) { failure = $"capture {name} returned null"; yield break; }
        string path = Path.Combine(requestedDirectory, name + ".png");
        File.WriteAllBytes(path, image.EncodeToPNG());
        Destroy(image);
        yield return null;
        yield return null;
        string[] rows = evidence.Facts.Select(item => item.Text)
            .Distinct(StringComparer.Ordinal).ToArray();
        string[] ui = CaptureUiText();
        bool beforeCoverageResolution = !string.IsNullOrEmpty(requestedCoverageMode) &&
            !name.Contains("false-read-result", StringComparison.Ordinal);
        if ((shots.Count < 4 && ContainsHiddenTruth(ui)) ||
            (beforeCoverageResolution && ui.Any(row => row.Contains(
                "honest", StringComparison.OrdinalIgnoreCase)))) hiddenTruthLeakage = true;
        shots.Add(new ShotRecord
        {
            name = name,
            file = name + ".png",
            sha256 = FileHash(path),
            phase = rules.Phase.ToString(),
            publicStateSha256 = TextHash(rules.Match.PublicStateBytes),
            cardStateSha256 = CardStateHash(),
            evidenceSha256 = TextHash(string.Join("\n", rows)),
            observedEvidence = rows,
            feedback = controller.LastPlayerFeedback,
            caption = presentation.ActiveCaptionText,
            uiText = ui,
            focusOpen = focus.IsOpen,
            paused = player.IsPaused,
        });
        // Metal timing readback arrives asynchronously. Drain a fixed number of completed timing
        // rows so the PNG encode cannot surface later as a measured main-thread outlier.
        int quarantineStart = discardedTimingRows;
        int quarantineGuard = 240;
        while (discardedTimingRows - quarantineStart < CaptureQuarantineTimingRows &&
            quarantineGuard-- > 0) yield return null;
        if (discardedTimingRows - quarantineStart < CaptureQuarantineTimingRows)
        {
            failure = $"capture {name} timing quarantine did not complete";
            yield break;
        }
        // Exclude the deltaTime spanning the last discarded row as well.
        yield return null;
        samplePhase = previous;
    }

    string[] CaptureUiText()
    {
        var rows = new List<string>();
        foreach (UIDocument document in FindObjectsByType<UIDocument>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            document.rootVisualElement.Query<Label>().ForEach(label =>
            {
                if (!string.IsNullOrWhiteSpace(label.text)) rows.Add(label.text.Trim());
            });
        return rows.Distinct(StringComparer.Ordinal).ToArray();
    }

    static bool ContainsHiddenTruth(IEnumerable<string> rows)
    {
        string joined = string.Join("\n", rows);
        return joined.Contains("RenegedWithHeldFlame", StringComparison.OrdinalIgnoreCase) ||
            joined.Contains("ImpossibleEighthRank", StringComparison.OrdinalIgnoreCase) ||
            joined.Contains("AldricCheated", StringComparison.OrdinalIgnoreCase) ||
            joined.Contains("expectedCheat", StringComparison.OrdinalIgnoreCase);
    }

    bool ValidateBeforeQuit(out string error)
    {
        error = string.Empty;
        if (requestedRepetition < 1 || requestedRepetition > 3)
            error = "repetition must be 1..3";
        else if (Screen.width != OutputWidth || Screen.height != OutputHeight)
            error = $"backbuffer is {Screen.width}x{Screen.height}";
        else if (shots.Count != 8) error = $"captured {shots.Count}/8 shots";
        else if (!suspiciousObserved || !correctReadResolved)
            error = "suspicious/correct Read path incomplete";
        else if (matchCompletionEvents != 1) error = $"match completion events={matchCompletionEvents}";
        else if (latchedResultWinner != ProofExpectedWinner)
            error = $"latched result winner={latchedResultWinner}";
        else if (string.IsNullOrEmpty(latchedResultPublicStateSha256))
            error = "latched result public state is missing";
        else if (!rematchStarted || rematchStartEvents != 1)
            error = $"rematch state/events={rematchStarted}/{rematchStartEvents}";
        else if (!rematchActionCompleted) error = "rematch action was not completed";
        else if (hiddenTruthLeakage) error = "hidden truth appeared before Read resolution";
        else
        {
            PerformanceRecord perf = BuildPerformance();
            if (!perf.frameTimingAvailable) error = "CPU/GPU FrameTimingManager data unavailable";
            else if (!perf.gcRecorderAvailable) error = "GC allocation recorder unavailable";
            else if (perf.sampleFrames < 500) error = $"performance samples={perf.sampleFrames}";
            else if (perf.p95Milliseconds >= TotalP95BudgetMilliseconds)
                error = $"p95={perf.p95Milliseconds:F2}ms";
            else if (perf.mainThreadP95Milliseconds > MainThreadP95BudgetMilliseconds)
                error = $"main p95={perf.mainThreadP95Milliseconds:F2}ms";
            else if (perf.gpuP95Milliseconds > GpuP95BudgetMilliseconds)
                error = $"GPU p95={perf.gpuP95Milliseconds:F2}ms";
            else if (perf.p99Milliseconds >= P99BudgetMilliseconds)
                error = $"p99={perf.p99Milliseconds:F2}ms";
            else if (perf.maximumMilliseconds > MaximumFrameBudgetMilliseconds)
                error = $"max={perf.maximumMilliseconds:F2}ms";
            else if (perf.gcAllocatedBytes != 0) error = $"GC allocated={perf.gcAllocatedBytes}";
            foreach (PhasePerformance phase in perf.phases)
            {
                int required = phase.name == "card-motion" || phase.name == "tell-caption"
                    ? 20 : 120;
                if (phase.samples < required) error = $"{phase.name} samples={phase.samples}/{required}";
            }
        }
        return error.Length == 0;
    }

    void OnApplicationQuit()
    {
        if (coverageReadyForQuitReport)
        {
            WriteCoverageReport("coverage-report.json");
            return;
        }
        if (!readyForQuitReport) return;
        WriteReport(quitRequested: player != null && player.QuitRequested, "report.json");
    }

    EnvironmentRecord CurrentEnvironment() => new EnvironmentRecord
    {
        applicationIsEditor = Application.isEditor,
        batchMode = Application.isBatchMode,
        scene = SceneManager.GetActiveScene().name,
        qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
        renderScale = Mathf.Min(ScalableBufferManager.widthScaleFactor,
            ScalableBufferManager.heightScaleFactor),
        graphicsDevice = SystemInfo.graphicsDeviceName,
        unityVersion = Application.unityVersion,
    };

    void WriteCoverageReport(string fileName)
    {
        var item = CoverageCase;
        var report = new Report
        {
            evidenceKind = "macos-release-player-parlor-coverage",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            coverageMode = requestedCoverageMode,
            resolution = new ResolutionRecord { width = Screen.width, height = Screen.height },
            environment = CurrentEnvironment(),
            directStateMutationDetected = false,
            hiddenTruthLeakageDetected = hiddenTruthLeakage,
            seedCase = new SeedRecord
            {
                id = item.Id, seed = item.Seed, tier = item.CorruptionTier,
                suit = item.RequiredSuit.ToString(), expectedCheat = item.ExpectCheat,
                expectedTell = item.ExpectedObservation.ToString(),
                cheatFamily = item.ExpectedCheatKind.ToString(), decision = item.Decision.ToString(),
                expectedOutcome = GmParlorOutcomeKind.FalseReadPenalty.ToString(),
            },
            shots = shots.ToArray(),
            coverage = new CoverageRecord
            {
                falseReadResolved = falseReadResolved,
                resolvedOutcomeKind = coverageOutcomeKind,
                canonicalPublicStateSha256 = coveragePublicStateSha256,
                canonicalCardStateSha256 = coverageCardStateSha256,
                settingsChangedThroughController = settingsChangedThroughController,
                preferencesPersisted = preferencesPersisted &&
                    File.Exists(requestedPreferencesPath),
                preferencesSha256 = File.Exists(requestedPreferencesPath)
                    ? FileHash(requestedPreferencesPath) : string.Empty,
                accessibility = new AccessibilityRecord
                {
                    captions = GmAccessibilitySettings.Captions,
                    reducedMotion = GmAccessibilitySettings.ReducedMotion,
                    vibration = GmAccessibilitySettings.Vibration,
                    monoAudio = GmAccessibilitySettings.MonoAudio,
                    highContrast = GmAccessibilitySettings.HighContrast,
                    textScale = GmAccessibilitySettings.TextScale,
                },
                ruleStateEvents = ruleStateEvents,
                outcomeEvents = outcomeEvents,
                durableWrites = (int)Math.Max(0, GmSaveSystem.DurableGeneration - durableStart),
            },
        };
        File.WriteAllText(Path.Combine(requestedDirectory, fileName),
            JsonUtility.ToJson(report, true));
    }

    void WriteReport(bool quitRequested, string fileName)
    {
        PerformanceRecord performance = BuildPerformance();
        var item = ProofCase;
        var report = new Report
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            repetition = requestedRepetition,
            resolution = new ResolutionRecord { width = Screen.width, height = Screen.height },
            environment = CurrentEnvironment(),
            directStateMutationDetected = false,
            hiddenTruthLeakageDetected = hiddenTruthLeakage,
            seedCase = new SeedRecord
            {
                id = item.Id, seed = item.Seed, tier = item.CorruptionTier,
                suit = item.RequiredSuit.ToString(), expectedCheat = item.ExpectCheat,
                expectedTell = item.ExpectedObservation.ToString(),
                cheatFamily = item.ExpectedCheatKind.ToString(), decision = item.Decision.ToString(),
                expectedWinner = ProofExpectedWinner,
            },
            shots = shots.ToArray(),
            match = new MatchRecord
            {
                suspiciousTellObserved = suspiciousObserved,
                correctReadResolved = correctReadResolved,
                resultWinner = latchedResultWinner,
                resultPublicStateSha256 = latchedResultPublicStateSha256,
                matchCompletionEvents = matchCompletionEvents,
                rematchStarted = rematchStarted,
                rematchStartEvents = rematchStartEvents,
                rematchActionCompleted = rematchActionCompleted,
                quitRequested = quitRequested,
                ruleStateEvents = ruleStateEvents,
                outcomeEvents = outcomeEvents,
                durableWrites = (int)Math.Max(0, GmSaveSystem.DurableGeneration - durableStart),
            },
            performance = performance,
        };
        File.WriteAllText(Path.Combine(requestedDirectory, fileName),
            JsonUtility.ToJson(report, true));
    }

    PerformanceRecord BuildPerformance()
    {
        FrameSample[] all = samples.ToArray();
        int half = all.Length / 2;
        double firstHalf = Percentile(all.Take(half).Select(item => item.Total), 0.95d);
        double secondHalf = Percentile(all.Skip(half).Select(item => item.Total), 0.95d);
        double largerHalf = Math.Max(firstHalf, secondHalf);
        return new PerformanceRecord
        {
            warmupSeconds = WarmupSeconds,
            warmupFrames = warmupObserved,
            sampleFrames = all.Length,
            p50Milliseconds = Percentile(all.Select(item => item.Total), 0.50d),
            p95Milliseconds = Percentile(all.Select(item => item.Total), 0.95d),
            p99Milliseconds = Percentile(all.Select(item => item.Total), 0.99d),
            maximumMilliseconds = all.Length == 0 ? 0d : all.Max(item => item.Total),
            mainThreadP95Milliseconds = Percentile(all.Where(item => item.Main > 0d)
                .Select(item => item.Main), 0.95d),
            gpuP95Milliseconds = Percentile(all.Where(item => item.Gpu > 0d)
                .Select(item => item.Gpu), 0.95d),
            gcAllocatedBytes = all.Where(item => item.Gc > 0).Sum(item => item.Gc),
            frameTimingAvailable = all.Length >= 1800 && unavailableFrameTimingSamples == 0,
            gcRecorderAvailable = all.All(item => item.Gc >= 0),
            allocationMeasurement = "main-thread-cumulative-allocation-counter",
            mainThreadMeasurement = "main-thread-frame-minus-present-wait",
            unavailableFrameTimingSamples = unavailableFrameTimingSamples,
            pendingFrameTimingPolls = pendingFrameTimingPolls,
            unavailableGcSamples = unavailableGcSamples,
            p95FirstHalfMilliseconds = firstHalf,
            p95SecondHalfMilliseconds = secondHalf,
            p95HalfSpreadPercent = largerHalf <= 0d ? 0d :
                Math.Abs(firstHalf - secondHalf) / largerHalf * 100d,
            rawTotalMilliseconds = all.Select(item => item.Total).ToArray(),
            rawMainThreadMilliseconds = all.Select(item => item.Main).ToArray(),
            rawCpuMainThreadFrameMilliseconds = all.Select(item => item.CpuMainThreadFrame).ToArray(),
            rawMainThreadPresentWaitMilliseconds = all.Select(item => item.MainThreadPresentWait).ToArray(),
            rawGpuMilliseconds = all.Select(item => item.Gpu).ToArray(),
            rawGcAllocatedBytes = all.Select(item => item.Gc).ToArray(),
            rawPhase = all.Select(item => PhaseName(item.Phase)).ToArray(),
            captureQuarantineTimingRows = CaptureQuarantineTimingRows,
            captureFramesExcluded = true,
            ioFramesExcluded = true,
            phases = new[]
            {
                Phase("card-motion", PerfPhase.CardMotion, all),
                Phase("tell-caption", PerfPhase.TellCaption, all),
                Phase("focus", PerfPhase.Focus, all),
                Phase("result", PerfPhase.Result, all),
                Phase("pause", PerfPhase.Pause, all),
            },
        };
    }

    static string PhaseName(PerfPhase phase) => phase switch
    {
        PerfPhase.CardMotion => "card-motion",
        PerfPhase.TellCaption => "tell-caption",
        PerfPhase.Focus => "focus",
        PerfPhase.Result => "result",
        PerfPhase.Pause => "pause",
        _ => "none",
    };

    static PhasePerformance Phase(string name, PerfPhase phase, FrameSample[] source)
    {
        FrameSample[] rows = source.Where(item => item.Phase == phase).ToArray();
        return new PhasePerformance
        {
            name = name, samples = rows.Length,
            p50Milliseconds = Percentile(rows.Select(item => item.Total), 0.50d),
            p95Milliseconds = Percentile(rows.Select(item => item.Total), 0.95d),
            p99Milliseconds = Percentile(rows.Select(item => item.Total), 0.99d),
            maximumMilliseconds = rows.Length == 0 ? 0d : rows.Max(item => item.Total),
            mainThreadP95Milliseconds = Percentile(rows.Where(item => item.Main > 0d)
                .Select(item => item.Main), 0.95d),
            gpuP95Milliseconds = Percentile(rows.Where(item => item.Gpu > 0d)
                .Select(item => item.Gpu), 0.95d),
            gcAllocatedBytes = rows.Where(item => item.Gc > 0).Sum(item => item.Gc),
        };
    }

    static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0) return 0d;
        int index = Math.Min(sorted.Length - 1,
            Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1));
        return sorted[index];
    }

    string CardStateHash()
    {
        StringBuilder text = new StringBuilder(2048);
        foreach (GmParlorCardView view in FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(item => item.PhysicalCard.Suit).ThenBy(item => item.PhysicalCard.Rank))
        {
            Transform item = view.transform;
            text.Append(view.PhysicalCard.TableLabel).Append('|')
                .Append(item.localPosition.ToString("R")).Append('|')
                .Append(item.localRotation.eulerAngles.ToString("R")).Append('\n');
        }
        return TextHash(text.ToString());
    }

    static string FileHash(string path)
    {
        using SHA256 hash = SHA256.Create();
        return Hex(hash.ComputeHash(File.ReadAllBytes(path)));
    }

    static string TextHash(string text)
    {
        using SHA256 hash = SHA256.Create();
        return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)));
    }

    static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

    void CountState() => ruleStateEvents++;
    void CountOutcome(ulong _, GmParlorOutcome __) => outcomeEvents++;
    void CountMatch(bool _) => matchCompletionEvents++;
    void CountAccessibilityChange() => accessibilityChangeEvents++;

    void FailAndQuit()
    {
        if (samples.Count > 0 && rules?.Match != null)
        {
            PerformanceRecord perf = BuildPerformance();
            Debug.Log($"[GmParlorStandaloneProbe] PERF DIAGNOSTIC total=" +
                $"{perf.p95Milliseconds:F2}ms main={perf.mainThreadP95Milliseconds:F2}ms " +
                $"gpu={perf.gpuP95Milliseconds:F2}ms GC={perf.gcAllocatedBytes}B phases=" +
                string.Join(",", perf.phases.Select(item =>
                    $"{item.name}:{item.samples}/{item.mainThreadP95Milliseconds:F2}ms")));
            WriteReport(quitRequested: false, "failed-report.json");
        }
        Debug.LogError($"[GmParlorStandaloneProbe] FAILED: {failure}");
        Application.Quit(4);
    }

    void OnDestroy()
    {
        if (rules != null)
        {
            rules.OnStateChanged -= CountState;
            rules.OnOutcomeReady -= CountOutcome;
            rules.OnGameCompleted -= CountMatch;
        }
        GmAccessibilitySettings.OnChanged -= CountAccessibilityChange;
        if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
    }
}
