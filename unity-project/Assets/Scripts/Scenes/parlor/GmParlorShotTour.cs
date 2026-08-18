using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime/editor evidence tour for the actual Parlor table. Every frame stages a public gameplay
/// state and captures the final backbuffer so UI Toolkit overlays are part of the evidence.
/// </summary>
public sealed class GmParlorShotTour : GmSceneReviewTour
{
    [Serializable]
    internal sealed class ReviewCaseRecord
    {
        public string id;
        public int seed;
        public int tier;
        public string suit;
        public bool expectedCheat;
        public string expectedTell;
        public string cheatFamily;
        public string decision;
    }

    [Serializable]
    internal sealed class ReviewShotRecord
    {
        public string name;
        public string file;
        public string sha256;
        public string caseId;
        public string phase;
        public string publicStateSha256;
        public string cardStateSha256;
        public string observedTell;
        public string feedback;
        public string activeCaption;
        public bool focusOpen;
        public int observedEvidenceCount;
        public string[] observedEvidence;
        public bool captions;
        public bool highContrast;
        public float textScale;
    }

    [Serializable]
    internal sealed class RestoreVerificationRecord
    {
        public int cardsCompared;
        public int firstReloadDeltaCount;
        public int secondReloadDeltaCount;
        public int cueEvents;
        public int evidenceEvents;
        public int outcomeEvents;
        public int trickEvents;
        public int roundEvents;
        public int matchEvents;
        public int runDeltaCount;
        public string isolatedSavePath;
        public string baselinePublicStateSha256;
        public string restoredPublicStateSha256;
        public string secondReloadPublicStateSha256;
        public string baselineCardStateSha256;
        public string restoredCardStateSha256;
        public string secondReloadCardStateSha256;
        public string baselineEvidenceSha256;
        public string restoredEvidenceSha256;
        public string secondReloadEvidenceSha256;
        public string baselineRunStateSha256;
        public string restoredRunStateSha256;
        public string secondReloadRunStateSha256;
    }

    [Serializable]
    internal sealed class ReviewReport
    {
        public int schemaVersion = 1;
        public int seedCatalogVersion = GmParlorReviewProbe.SeedCatalogVersion;
        public string evidenceKind = "editor-runtime-backbuffer-not-built-player";
        public string generatedUtc;
        public string sceneSha256;
        public string contentSha256;
        public string packageManifestSha256;
        public ReviewCaseRecord[] cases;
        public ReviewShotRecord[] shots;
        public RestoreVerificationRecord restore;
    }

    static readonly Vector3 TableView = new Vector3(0f, 1.3f, -2.3f);
    static readonly Vector3 CardView = new Vector3(0f, 1.55f, -1.35f);

    static readonly GmReviewShot[] Shots =
    {
        new GmReviewShot("01-player-hand-ready", CardView, 0f, 36f),
        new GmReviewShot("02-empty-host-chair-framing", new Vector3(0f, 1.3f, -1.6f), 0f, 10f),
        new GmReviewShot("03-player-lead-and-aldric-follow", TableView, 0f, 15f),
        new GmReviewShot("04-aldric-lead-player-follow", new Vector3(0.03f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("05-true-suspicious-contact", TableView, 0f, 15f),
        new GmReviewShot("06-false-suspicious-contact", new Vector3(0.04f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("07-focus-true-observed-facts", TableView, 0f, 15f),
        new GmReviewShot("08-focus-false-observed-facts", new Vector3(0.04f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("09-correct-read-result", TableView, 0f, 15f),
        new GmReviewShot("10-false-read-result", new Vector3(0.04f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("11-missed-cheat-result", new Vector3(-0.04f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("12-locked-read-feedback", TableView, 0f, 15f),
        new GmReviewShot("13-late-read-feedback", new Vector3(0.04f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("14-trick-result", new Vector3(0.05f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("15-round-result", new Vector3(-0.03f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("16-player-match-win", TableView, 0f, 15f),
        new GmReviewShot("17-aldric-match-win", new Vector3(0.04f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("18-rematch-ready", new Vector3(0.04f, 1.55f, -1.35f), 0f, 36f),
        new GmReviewShot("19-pause-journal", TableView, 0f, 15f),
        new GmReviewShot("20-settings-default", TableView, 0f, 15f),
        new GmReviewShot("21-settings-high-contrast-200", TableView, 0f, 15f),
        new GmReviewShot("22-restore-before", new Vector3(-0.02f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("23-restore-after", new Vector3(0.08f, 1.3f, -2.3f), 0f, 15f),
        new GmReviewShot("24-restore-focus", CardView, 0f, 36f),
    };

    GmParlorReviewProbe probe;
    GmParlorRules rules;
    GmParlorController controller;
    GmParlorPresentationCoordinator presentation;
    GmParlorFocusView focus;
    GmParlorEvidenceLog evidence;
    GmPlayer player;
    GmPauseMenu pauseMenu;
    string stageError;
    string currentCaseId = string.Empty;
    readonly List<ReviewShotRecord> shotRecords = new List<ReviewShotRecord>();
    bool originalHighContrast;
    bool originalCaptions;
    float originalTextScale;
    int originalCaptureFramerate;
    bool captureFreezeActive;
    float captureFreezeTimeScale;
    bool captureFreezeAudioPause;

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
    protected override bool CaptureReviewBackbuffer => true;
    protected override int DirectCaptureShotCount => 21;

    protected override void BeforeTour()
    {
        string isolated = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "parlor-review-tour", "run-save.json");
        Directory.CreateDirectory(Path.GetDirectoryName(isolated));
        GmSaveSystem.ConfigureForTests(isolated);
        GmRunStore.BeginNewRun();
        originalHighContrast = GmPauseMenu.HighContrast;
        originalCaptions = GmPauseMenu.Captions;
        originalTextScale = GmPauseMenu.TextScale;
        originalCaptureFramerate = Time.captureFramerate;
        // Fixed capture time keeps a slow HDRP editor frame from consuming an entire 0.75-second
        // caption hold before UI Toolkit has repainted the next backbuffer.
        Time.captureFramerate = 60;
        Bind();
        shotRecords.Clear();
    }

    protected override void BeforeShot(GmReviewShot shot)
    {
        stageError = string.Empty;
        currentCaseId = string.Empty;
        Bind();
        IsolateReviewEvidence();
        GmPauseMenu.SetCaptions(originalCaptions);
        if (player != null && player.IsPaused) player.SetPaused(false);
        pauseMenu?.SetPauseState(false);

        switch (shot.Name)
        {
            case "01-player-hand-ready":
            case "02-empty-host-chair-framing":
                StageReady(Case("honest-calm-flames"));
                break;
            case "03-player-lead-and-aldric-follow":
                StageJudgement(Case("honest-calm-flames"));
                break;
            case "04-aldric-lead-player-follow":
                StageAldricLeadAndPlayerFollow();
                break;
            case "05-true-suspicious-contact":
                StageObservedJudgement(Case("cheat-true-tell-eyes"));
                break;
            case "06-false-suspicious-contact":
                StageObservedJudgement(Case("honest-false-tell-teeth"));
                break;
            case "07-focus-true-observed-facts":
                StageObservedJudgement(Case("cheat-true-tell-eyes"));
                focus?.Open();
                break;
            case "08-focus-false-observed-facts":
                StageObservedJudgement(Case("honest-false-tell-teeth"));
                focus?.Open();
                break;
            case "09-correct-read-result":
                StageResolved(Case("cheat-true-tell-eyes"));
                break;
            case "10-false-read-result":
                StageResolved(Case("honest-false-tell-teeth"));
                break;
            case "11-missed-cheat-result":
                StageResolved(Case("cheat-calm-bones"));
                break;
            case "12-locked-read-feedback":
                StageJudgement(Case("honest-locked-read-eyes"));
                if (string.IsNullOrEmpty(stageError) &&
                    controller.CallRead() != GmParlorActionError.ReadLocked)
                    stageError = "locked Read did not return ReadLocked";
                focus?.Open();
                break;
            case "13-late-read-feedback":
                StageResolved(Case("cheat-late-read-flames"));
                focus?.Open();
                break;
            case "14-trick-result":
                StageResolved(Case("honest-calm-flames"));
                break;
            case "15-round-result":
                StageIntermediate(GmParlorMatchPhase.RoundResult);
                break;
            case "16-player-match-win":
                StageMatch(Case("bones-player-win-rematch"), GmTrickOwner.Player);
                break;
            case "17-aldric-match-win":
                StageMatch(Case("aldric-win-restore"), GmTrickOwner.Aldric);
                break;
            case "18-rematch-ready":
                StageRematch();
                break;
            case "19-pause-journal":
                StagePaused(GmPauseTab.Journal, highContrast: false, textScale: 1f);
                break;
            case "20-settings-default":
                StagePaused(GmPauseTab.Settings, highContrast: false, textScale: 1f);
                break;
            case "21-settings-high-contrast-200":
                StagePaused(GmPauseTab.Settings, highContrast: true, textScale: 2f);
                break;
        }
    }

    protected override IEnumerator BeforeShotSettled(GmReviewShot shot)
    {
        if (shot.Name == "05-true-suspicious-contact" ||
            shot.Name == "06-false-suspicious-contact" ||
            shot.Name == "07-focus-true-observed-facts" ||
            shot.Name == "08-focus-false-observed-facts")
        {
            int guard = 240;
            while (presentation != null && !presentation.HasActiveCaption && guard-- > 0)
                yield return null;
            if (presentation == null || !presentation.HasActiveCaption)
                stageError = "observed judgement never reached its player-facing caption";
            captureFreezeTimeScale = Time.timeScale;
            captureFreezeAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            captureFreezeActive = true;
            for (int frame = 0; frame < 4; frame++) yield return null;
            // ScreenCapture on the windowed macOS editor can return the previous composited UI
            // buffer on the first read after a panel-state change. Prime that backbuffer once;
            // the base captures the next rendered frame and binds only that PNG to the manifest.
            yield return new WaitForEndOfFrame();
            Texture2D warmBackbuffer = ScreenCapture.CaptureScreenshotAsTexture();
            if (warmBackbuffer != null) Destroy(warmBackbuffer);
            yield return null;
        }
        // UI Toolkit lays out on the frame after semantic state changes. Two frames also let the
        // common pause menu mirror GmPlayer without advancing canonical play while paused.
        yield return null;
        yield return null;
    }

    protected override string ValidateCapturedShot(GmReviewShot shot, Color32[] pixels)
    {
        return string.IsNullOrEmpty(stageError) ? null : stageError;
    }

    protected override void AfterShotCaptured(GmReviewShot shot, string file, Texture2D captured)
    {
        shotRecords.Add(new ReviewShotRecord
        {
            name = shot.Name,
            file = Path.GetRelativePath(Directory.GetCurrentDirectory(), file),
            sha256 = FileSha256(file),
            caseId = currentCaseId,
            phase = rules?.Phase.ToString() ?? "Missing",
            publicStateSha256 = rules?.Match == null ? string.Empty :
                TextSha256(rules.Match.PublicStateBytes),
            cardStateSha256 = CardStateSha256(),
            observedTell = rules?.TellObservation.ToString() ?? "Missing",
            feedback = controller?.LastPlayerFeedback ?? string.Empty,
            activeCaption = presentation?.ActiveCaptionText ?? string.Empty,
            focusOpen = focus != null && focus.IsOpen,
            observedEvidenceCount = focus?.Model?.Evidence?.Length ?? 0,
            observedEvidence = focus?.Model?.Evidence?.ToArray() ?? Array.Empty<string>(),
            captions = GmPauseMenu.Captions,
            highContrast = GmPauseMenu.HighContrast,
            textScale = GmPauseMenu.TextScale,
        });
        if (captureFreezeActive)
        {
            Time.timeScale = captureFreezeTimeScale;
            AudioListener.pause = captureFreezeAudioPause;
            captureFreezeActive = false;
        }
    }

    protected override bool TryBeginDeferredCompletion(Camera camera, string directory,
        int written, int invalidVisualEvidence)
    {
        GmParlorRestoreReviewOrchestrator.Begin(camera, directory, shotRecords,
            originalHighContrast, originalCaptions, originalTextScale,
            originalCaptureFramerate,
            written, invalidVisualEvidence, minimumLuminanceRange,
            maximumNearBlackFraction, maximumNearWhiteFraction,
            maximumSaturatedMagentaFraction);
        return true;
    }

    void Bind()
    {
        probe ??= FindAnyObjectByType<GmParlorReviewProbe>();
        rules ??= FindAnyObjectByType<GmParlorRules>();
        controller ??= FindAnyObjectByType<GmParlorController>();
        presentation ??= FindAnyObjectByType<GmParlorPresentationCoordinator>();
        focus ??= FindAnyObjectByType<GmParlorFocusView>();
        evidence ??= FindAnyObjectByType<GmParlorEvidenceLog>();
        player ??= FindAnyObjectByType<GmPlayer>();
        pauseMenu ??= FindAnyObjectByType<GmPauseMenu>();
        if (probe == null || rules == null || controller == null || presentation == null ||
            focus == null || evidence == null || player == null || pauseMenu == null)
            stageError = "saved Parlor is missing a review dependency";
    }

    void IsolateReviewEvidence()
    {
        focus?.Close();
        evidence?.Clear();
        presentation?.ResetTransientState();
    }

    void StageReady(GmParlorReviewCase item)
    {
        currentCaseId = item.Id;
        if (!string.IsNullOrEmpty(stageError)) return;
        GmParlorInitializeResult started = rules.StartGame(item.Seed, item.CorruptionTier, 0,
            item.ReadUnlocked, forceRestart: true);
        if (started != GmParlorInitializeResult.StartedNew)
        {
            stageError = $"{item.Id} ready state failed: {started}";
            return;
        }
        presentation.ResetTransientState();
        GmParlorActionError activated = controller.Activate();
        if (activated != GmParlorActionError.None) stageError = $"activation failed: {activated}";
    }

    void StageJudgement(GmParlorReviewCase item)
    {
        currentCaseId = item.Id;
        if (!probe.StageCaseJudgement(item, out stageError)) return;
        presentation.FastForwardToCanonicalState();
    }

    void StageResolved(GmParlorReviewCase item)
    {
        StageJudgement(item);
        if (string.IsNullOrEmpty(stageError)) probe.ResolveReviewDecision(item, out stageError);
    }

    void StageObservedJudgement(GmParlorReviewCase item)
    {
        currentCaseId = item.Id;
        GmPauseMenu.SetCaptions(true);
        if (!probe.StageCaseJudgement(item, out stageError)) return;
        focus.Refresh();
    }

    void StageAldricLeadAndPlayerFollow()
    {
        StageResolved(Case("honest-calm-flames"));
        if (!string.IsNullOrEmpty(stageError)) return;
        if (rules.Phase == GmParlorMatchPhase.TrickResult &&
            controller.ConfirmFocusedAction() != GmParlorActionError.None)
        {
            stageError = "could not leave first trick result";
            return;
        }
        presentation.FastForwardToCanonicalState();
        probe.DriveToPhase(GmParlorMatchPhase.PlayerFollowsAldricLead,
            GmTrickOwner.Player, out stageError);
    }

    void StageIntermediate(GmParlorMatchPhase phase)
    {
        StageResolved(Case("honest-calm-flames"));
        if (string.IsNullOrEmpty(stageError))
            probe.DriveToPhase(phase, GmTrickOwner.Aldric, out stageError);
    }

    void StageMatch(GmParlorReviewCase item, GmTrickOwner winner)
    {
        StageJudgement(item);
        if (string.IsNullOrEmpty(stageError)) probe.ResolveReviewDecision(item, out stageError);
        if (string.IsNullOrEmpty(stageError)) probe.DriveToMatchResult(winner, out stageError);
    }

    void StageRematch()
    {
        StageMatch(Case("bones-player-win-rematch"), GmTrickOwner.Player);
        if (!string.IsNullOrEmpty(stageError)) return;
        GmParlorActionError result = controller.ConfirmFocusedAction();
        if (result != GmParlorActionError.None) stageError = $"rematch intent failed: {result}";
        presentation.FastForwardToCanonicalState();
    }

    void StagePaused(GmPauseTab tab, bool highContrast, float textScale)
    {
        StageReady(Case("honest-calm-flames"));
        GmPauseMenu.SetHighContrast(highContrast);
        GmPauseMenu.SetTextScale(textScale);
        player.SetPaused(true);
        pauseMenu.SetPauseState(true);
        pauseMenu.SwitchTab(tab);
    }

    static GmParlorReviewCase Case(string id) =>
        GmParlorReviewProbe.FrozenSeedMatrix.Single(item => item.Id == id);

    internal static void WriteReport(IReadOnlyList<ReviewShotRecord> records,
        RestoreVerificationRecord restore)
    {
        string project = Directory.GetCurrentDirectory();
        string scene = Path.Combine(project, "Assets", "Scenes", "Parlor.unity");
        string packages = Path.Combine(project, "Packages", "manifest.json");
        string sourceRoot = Path.Combine(project, "Assets", "Scripts", "Scenes", "parlor");
        var contentFiles = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Concat(new[] { Path.Combine(project, "Assets", "Scripts", "GmParlorMatch.cs") })
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var report = new ReviewReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            sceneSha256 = FileSha256(scene),
            packageManifestSha256 = FileSha256(packages),
            contentSha256 = FilesSha256(contentFiles),
            cases = GmParlorReviewProbe.FrozenSeedMatrix.Select(item => new ReviewCaseRecord
            {
                id = item.Id,
                seed = item.Seed,
                tier = item.CorruptionTier,
                suit = item.RequiredSuit.ToString(),
                expectedCheat = item.ExpectCheat,
                expectedTell = item.ExpectedObservation.ToString(),
                cheatFamily = item.ExpectedCheatKind.ToString(),
                decision = item.Decision.ToString(),
            }).ToArray(),
            shots = records.ToArray(),
            restore = restore,
        };
        string output = Path.Combine(project, "Library", "GmSceneIntelligence",
            "parlor-review", "parlor-review-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log($"[GmParlorReviewProbe] REPORT {records.Count}/{Shots.Length} -> {output}");
    }

    internal static string FileSha256(string file)
    {
        using SHA256 hash = SHA256.Create();
        return Hex(hash.ComputeHash(File.ReadAllBytes(file)));
    }

    static string FilesSha256(IEnumerable<string> files)
    {
        using SHA256 hash = SHA256.Create();
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), file)
                .Replace('\\', '/');
            byte[] path = Encoding.UTF8.GetBytes(relative);
            hash.TransformBlock(path, 0, path.Length, null, 0);
            byte[] bytes = File.ReadAllBytes(file);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(hash.Hash);
    }

    internal static string TextSha256(string value)
    {
        using SHA256 hash = SHA256.Create();
        return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
    }

    static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

    internal static GmReviewShot ShotNamed(string name) =>
        Shots.Single(shot => shot.Name == name);

    string CardStateSha256()
    {
        GmParlorCardView[] views = FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        return GmParlorRestoreReviewOrchestrator.CardStateSha256(views);
    }
}

#if UNITY_EDITOR
public static class GmParlorShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Parlor")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmParlorShotTour>("Assets/Scenes/Parlor.unity");
    }
}
#endif
