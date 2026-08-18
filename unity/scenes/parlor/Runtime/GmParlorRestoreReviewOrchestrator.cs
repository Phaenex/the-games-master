using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Persistent editor-runtime evidence owner for the Parlor's disk restore contract. It survives
/// unloading the authored scene, then observes the same public lifecycle a returning player does.
/// </summary>
public sealed class GmParlorRestoreReviewOrchestrator : MonoBehaviour
{
    const string SceneName = "Parlor";
    const string CaseId = "aldric-win-restore";

    sealed class ObservedState
    {
        public string PublicHash;
        public string[] CardRows;
        public string CardHash;
        public string[] EvidenceRows;
        public string EvidenceHash;
        public bool FocusOpen;
        public string RunHash;
        public string Phase;
    }

    sealed class EventCounts
    {
        public int Cue;
        public int Evidence;
        public int Outcome;
        public int Trick;
        public int Round;
        public int Match;
        public int Total => Cue + Evidence + Outcome + Trick + Round + Match;
    }

    readonly List<GmParlorShotTour.ReviewShotRecord> records =
        new List<GmParlorShotTour.ReviewShotRecord>(24);
    string directory;
    Camera currentCamera;
    GmParlorReviewProbe probe;
    GmParlorRules rules;
    GmParlorController controller;
    GmParlorPresentationCoordinator presentation;
    GmParlorFocusView focus;
    GmParlorEvidenceLog evidence;
    GmParlorAldricPresenter aldricPresenter;
    GmPlayer player;
    GmParlorInput input;
    EventCounts activeEvents;
    bool loadedCallbackObserved;
    bool originalHighContrast;
    bool originalCaptions;
    float originalTextScale;
    int originalCaptureFramerate;
    int written;
    int invalidVisualEvidence;
    int minimumLuminanceRange;
    float maximumNearBlackFraction;
    float maximumNearWhiteFraction;
    float maximumSaturatedMagentaFraction;
    GmParlorShotTour.RestoreVerificationRecord verification;

    internal static void Begin(Camera camera, string outputDirectory,
        IReadOnlyList<GmParlorShotTour.ReviewShotRecord> directRecords,
        bool highContrast, bool captions, float textScale, int captureFramerate, int directWritten,
        int directInvalidVisualEvidence, int minimumRange, float maximumBlack,
        float maximumWhite, float maximumMagenta)
    {
        var owner = new GameObject("GmParlorRestoreReviewOrchestrator");
        DontDestroyOnLoad(owner);
        var runner = owner.AddComponent<GmParlorRestoreReviewOrchestrator>();
        runner.currentCamera = camera;
        runner.directory = outputDirectory;
        runner.records.AddRange(directRecords);
        runner.originalHighContrast = highContrast;
        runner.originalCaptions = captions;
        runner.originalTextScale = textScale;
        runner.originalCaptureFramerate = captureFramerate;
        runner.written = directWritten;
        runner.invalidVisualEvidence = directInvalidVisualEvidence;
        runner.minimumLuminanceRange = minimumRange;
        runner.maximumNearBlackFraction = maximumBlack;
        runner.maximumNearWhiteFraction = maximumWhite;
        runner.maximumSaturatedMagentaFraction = maximumMagenta;
        runner.StartCoroutine(runner.Run());
    }

    IEnumerator Run()
    {
        IEnumerator proof = RunProof();
        while (true)
        {
            object current = null;
            bool moved = false;
            try { moved = proof.MoveNext(); if (moved) current = proof.Current; }
            catch (Exception exception)
            {
                invalidVisualEvidence++;
                Debug.LogError($"[GmParlorReviewProbe] RESTORE FAILED: {exception.Message}\n" +
                    exception.StackTrace);
            }
            if (!moved) break;
            yield return current;
        }

        ResetAccessibility();
        Time.captureFramerate = originalCaptureFramerate;
        GmSaveSystem.ResetTestConfiguration();
        if (verification != null) GmParlorShotTour.WriteReport(records, verification);
        if (invalidVisualEvidence > 0 || verification == null || records.Count != 24)
        {
            Debug.LogError($"[GmSceneReviewTour] FAILED: {invalidVisualEvidence} invalid capture/restore " +
                $"result(s), report frames {records.Count}/24");
            EndEditor(1);
        }
        else
        {
            Debug.Log($"[GmSceneReviewTour] TOUR COMPLETE {written}/24 -> {directory}");
            EndEditor(0);
        }
    }

    IEnumerator RunProof()
    {
        ResetAccessibility();
        if (!BindScene()) throw new InvalidOperationException("saved Parlor dependencies are missing");
        string project = Directory.GetCurrentDirectory();
        string isolatedRoot = Path.GetFullPath(Path.Combine(project, "Library",
            "GmSceneIntelligence", "parlor-review-tour")) + Path.DirectorySeparatorChar;
        string actualSave = Path.GetFullPath(GmSaveSystem.SavePath);
        if (!actualSave.StartsWith(isolatedRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"restore probe is not isolated: {actualSave}");
        if (player.IsPaused) player.SetPaused(false);
        focus.Close();
        evidence.Clear();
        presentation.ResetTransientState();

        GmParlorReviewCase item = GmParlorReviewProbe.FrozenSeedMatrix.Single(c => c.Id == CaseId);
        if (!probe.StageCaseJudgement(item, out string stageError))
            throw new InvalidOperationException($"could not stage observed restore judgement: {stageError}");

        int motionGuard = 600;
        while (presentation.IsBlocking && motionGuard-- > 0) yield return null;
        if (presentation.IsBlocking)
            throw new InvalidOperationException("observed judgement presentation did not settle");
        if (evidence.Facts.Count == 0)
            throw new InvalidOperationException("observed judgement produced no public evidence");
        if (!GmSaveSystem.Flush())
            throw new InvalidOperationException($"isolated restore save did not flush: {GmSaveSystem.LastError}");

        ObservedState baseline = Observe();
        if (baseline.CardRows.Length != GmParlorCore.TotalCards)
            throw new InvalidOperationException($"baseline has {baseline.CardRows.Length}/" +
                $"{GmParlorCore.TotalCards} physical card views");
        yield return Capture("22-restore-before", baseline);

        EventCounts firstEvents = new EventCounts();
        yield return ReloadSavedScene(firstEvents);
        ObservedState restored = Observe();
        int firstDeltas = Compare(baseline, restored, requireClosedFocus: true);
        yield return Capture("23-restore-after", restored);

        yield return OpenFocusThroughGamepad();
        ObservedState focused = Observe();
        if (!focused.FocusOpen)
            throw new InvalidOperationException("real Interact input did not open restored focus");
        if (!baseline.EvidenceRows.SequenceEqual(focused.EvidenceRows))
            throw new InvalidOperationException("opening restored focus changed observed evidence");
        yield return Capture("24-restore-focus", focused);

        EventCounts secondEvents = new EventCounts();
        yield return ReloadSavedScene(secondEvents);
        ObservedState second = Observe();
        int secondDeltas = Compare(baseline, second, requireClosedFocus: true);

        int runDeltas = CountDifferent(baseline.RunHash, restored.RunHash) +
            CountDifferent(baseline.RunHash, second.RunHash);
        verification = new GmParlorShotTour.RestoreVerificationRecord
        {
            cardsCompared = baseline.CardRows.Length,
            firstReloadDeltaCount = firstDeltas,
            secondReloadDeltaCount = secondDeltas,
            cueEvents = firstEvents.Cue + secondEvents.Cue,
            evidenceEvents = firstEvents.Evidence + secondEvents.Evidence,
            outcomeEvents = firstEvents.Outcome + secondEvents.Outcome,
            trickEvents = firstEvents.Trick + secondEvents.Trick,
            roundEvents = firstEvents.Round + secondEvents.Round,
            matchEvents = firstEvents.Match + secondEvents.Match,
            runDeltaCount = runDeltas,
            isolatedSavePath = Path.GetRelativePath(Directory.GetCurrentDirectory(),
                GmSaveSystem.SavePath).Replace('\\', '/'),
            baselinePublicStateSha256 = baseline.PublicHash,
            restoredPublicStateSha256 = restored.PublicHash,
            secondReloadPublicStateSha256 = second.PublicHash,
            baselineCardStateSha256 = baseline.CardHash,
            restoredCardStateSha256 = restored.CardHash,
            secondReloadCardStateSha256 = second.CardHash,
            baselineEvidenceSha256 = baseline.EvidenceHash,
            restoredEvidenceSha256 = restored.EvidenceHash,
            secondReloadEvidenceSha256 = second.EvidenceHash,
            baselineRunStateSha256 = baseline.RunHash,
            restoredRunStateSha256 = restored.RunHash,
            secondReloadRunStateSha256 = second.RunHash,
        };
        if (firstDeltas != 0 || secondDeltas != 0 || runDeltas != 0 ||
            firstEvents.Total != 0 || secondEvents.Total != 0)
            throw new InvalidOperationException(
                $"restore deltas first={firstDeltas} second={secondDeltas} run={runDeltas} " +
                $"events={firstEvents.Total + secondEvents.Total}");
        Debug.Log("[GmParlorReviewProbe] RESTORE PASS cards=28 firstDelta=0 " +
            "secondDelta=0 cue=0 evidence=0 outcome=0 run=0");
    }

    IEnumerator ReloadSavedScene(EventCounts counts)
    {
        Scene parlor = SceneManager.GetSceneByName(SceneName);
        if (!parlor.IsValid() || !parlor.isLoaded)
            throw new InvalidOperationException("Parlor was not loaded before restore cycle");
        if (!SceneManager.GetSceneByName("GmParlorReviewBridge").IsValid())
            SceneManager.CreateScene("GmParlorReviewBridge");
        Scene bridge = SceneManager.GetSceneByName("GmParlorReviewBridge");
        SceneManager.SetActiveScene(bridge);

        AsyncOperation unload = SceneManager.UnloadSceneAsync(parlor);
        if (unload == null) throw new InvalidOperationException("Parlor unload did not start");
        while (!unload.isDone) yield return null;
        yield return null;
        if (FindAnyObjectByType<GmParlorRules>() != null)
            throw new InvalidOperationException("Parlor rules survived full scene unload");

        GmRunStore.BeginNewRun();
        GmRunStore.ClearParlorMatch();
        if (!GmSaveSystem.Load())
            throw new InvalidOperationException($"isolated disk load failed: {GmSaveSystem.LastError}");

        activeEvents = counts;
        loadedCallbackObserved = false;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
        if (load == null) throw new InvalidOperationException("Parlor reload did not start");
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (!loadedCallbackObserved)
            throw new InvalidOperationException("Parlor sceneLoaded subscriber window was missed");
        Scene loaded = SceneManager.GetSceneByName(SceneName);
        SceneManager.SetActiveScene(loaded);

        int activationGuard = 60;
        while ((controller == null || !controller.IsActivated) && activationGuard-- > 0)
            yield return null;
        if (controller == null || !controller.IsActivated)
            throw new InvalidOperationException("shipping delayed restore activation did not complete");
        yield return null;
        currentCamera = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        if (currentCamera == null) throw new InvalidOperationException("reloaded Parlor has no camera");
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName) return;
        loadedCallbackObserved = BindScene();
        if (!loadedCallbackObserved) return;
        rules.OnOutcomeReady += (_, _) => activeEvents.Outcome++;
        rules.OnTrickCompleted += _ => activeEvents.Trick++;
        rules.OnRoundCompleted += _ => activeEvents.Round++;
        rules.OnGameCompleted += _ => activeEvents.Match++;
        evidence.OnChanged += () => activeEvents.Evidence++;
        aldricPresenter.OnCuePresented += _ => activeEvents.Cue++;
    }

    bool BindScene()
    {
        probe = FindAnyObjectByType<GmParlorReviewProbe>();
        rules = FindAnyObjectByType<GmParlorRules>();
        controller = FindAnyObjectByType<GmParlorController>();
        presentation = FindAnyObjectByType<GmParlorPresentationCoordinator>();
        focus = FindAnyObjectByType<GmParlorFocusView>();
        evidence = FindAnyObjectByType<GmParlorEvidenceLog>();
        aldricPresenter = FindAnyObjectByType<GmParlorAldricPresenter>();
        player = FindAnyObjectByType<GmPlayer>();
        input = FindAnyObjectByType<GmParlorInput>();
        return probe != null && rules != null && controller != null && presentation != null &&
            focus != null && evidence != null && aldricPresenter != null && player != null &&
            input != null;
    }

    ObservedState Observe()
    {
        GmParlorCardView[] views = FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        string[] cards = CardStateRows(views);
        string[] facts = evidence.Facts.Select(fact =>
            $"{fact.CommandId}|{fact.Fact}|{fact.Text}").ToArray();
        string run = string.Join("|", new[]
        {
            GmRunStore.CorruptionTier.ToString(CultureInfo.InvariantCulture),
            GmRunStore.Sanity.ToString("R", CultureInfo.InvariantCulture),
            GmRunStore.DefianceCount.ToString(CultureInfo.InvariantCulture),
            GmRunStore.ComplianceCount.ToString(CultureInfo.InvariantCulture),
            GmRunStore.TableGameIndex.ToString(CultureInfo.InvariantCulture),
            GmRunStore.ParlorOutcomeNamespace.ToString(CultureInfo.InvariantCulture),
            GmRunStore.ParlorAppliedOutcomeSequence.ToString(CultureInfo.InvariantCulture),
            string.Join(",", GmRunStore.CheatsCaught.OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(",", GmRunStore.CompletedRooms.OrderBy(value => value, StringComparer.Ordinal)),
        });
        return new ObservedState
        {
            PublicHash = GmParlorShotTour.TextSha256(rules.Match.PublicStateBytes),
            CardRows = cards,
            CardHash = GmParlorShotTour.TextSha256(string.Join("\n", cards)),
            EvidenceRows = facts,
            EvidenceHash = GmParlorShotTour.TextSha256(string.Join("\n", facts)),
            FocusOpen = focus.IsOpen,
            RunHash = GmParlorShotTour.TextSha256(run),
            Phase = rules.Phase.ToString(),
        };
    }

    static int Compare(ObservedState expected, ObservedState actual, bool requireClosedFocus)
    {
        int deltas = 0;
        deltas += CountDifferent(expected.PublicHash, actual.PublicHash);
        deltas += CountDifferent(expected.CardHash, actual.CardHash);
        deltas += CountDifferent(expected.EvidenceHash, actual.EvidenceHash);
        deltas += CountDifferent(expected.RunHash, actual.RunHash);
        deltas += expected.Phase == actual.Phase ? 0 : 1;
        deltas += expected.CardRows.SequenceEqual(actual.CardRows) ? 0 : 1;
        deltas += expected.EvidenceRows.SequenceEqual(actual.EvidenceRows) ? 0 : 1;
        deltas += !requireClosedFocus || !actual.FocusOpen ? 0 : 1;
        return deltas;
    }

    static int CountDifferent(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal) ? 0 : 1;

    IEnumerator OpenFocusThroughGamepad()
    {
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            InputSystem.QueueStateEvent(pad,
                new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }
        finally
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        }
    }

    IEnumerator Capture(string shotName, ObservedState state)
    {
        PositionCamera(GmParlorShotTour.ShotNamed(shotName));
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        Texture2D captured = ScreenCapture.CaptureScreenshotAsTexture();
        if (captured == null) throw new InvalidOperationException($"{shotName} backbuffer was null");
        string file = Path.Combine(directory, $"tour-{shotName}.png");
        File.WriteAllBytes(file, captured.EncodeToPNG());
        Color32[] pixels = captured.GetPixels32();
        int[] luminance = new int[pixels.Length];
        long sum = 0;
        int black = 0;
        int white = 0;
        int magenta = 0;
        for (int index = 0; index < pixels.Length; index++)
        {
            Color32 pixel = pixels[index];
            int value = (pixel.r + pixel.g + pixel.b) / 3;
            luminance[index] = value;
            sum += value;
            if (value <= 2) black++;
            if (value >= 253) white++;
            if (pixel.r >= 180 && pixel.b >= 180 && pixel.g <= 70) magenta++;
        }
        Array.Sort(luminance);
        int p05 = luminance[Mathf.Clamp(Mathf.FloorToInt(luminance.Length * 0.05f), 0,
            luminance.Length - 1)];
        int p95 = luminance[Mathf.Clamp(Mathf.FloorToInt(luminance.Length * 0.95f), 0,
            luminance.Length - 1)];
        float blackFraction = black / (float)pixels.Length;
        float whiteFraction = white / (float)pixels.Length;
        float magentaFraction = magenta / (float)pixels.Length;
        if (p95 - p05 < minimumLuminanceRange ||
            blackFraction > maximumNearBlackFraction ||
            whiteFraction > maximumNearWhiteFraction ||
            magentaFraction > maximumSaturatedMagentaFraction)
        {
            invalidVisualEvidence++;
            Debug.LogError($"[GmSceneReviewTour] FAILED VISUAL EVIDENCE {shotName}: " +
                $"p05={p05} p95={p95} black={blackFraction:P1} white={whiteFraction:P1} " +
                $"magenta={magentaFraction:P3}");
        }
        records.Add(new GmParlorShotTour.ReviewShotRecord
        {
            name = shotName,
            file = Path.GetRelativePath(Directory.GetCurrentDirectory(), file),
            sha256 = GmParlorShotTour.FileSha256(file),
            caseId = CaseId,
            phase = state.Phase,
            publicStateSha256 = state.PublicHash,
            cardStateSha256 = state.CardHash,
            observedTell = rules.TellObservation.ToString(),
            feedback = controller.LastPlayerFeedback,
            activeCaption = presentation.ActiveCaptionText,
            focusOpen = state.FocusOpen,
            observedEvidenceCount = focus.Model.Evidence.Length,
            observedEvidence = focus.Model.Evidence.ToArray(),
            captions = GmPauseMenu.Captions,
            highContrast = GmPauseMenu.HighContrast,
            textScale = GmPauseMenu.TextScale,
        });
        written++;
        Debug.Log($"[GmSceneReviewTour] {shotName} meanLum={sum / pixels.Length} p05={p05} " +
            $"p95={p95} black={blackFraction:P1} white={whiteFraction:P1} -> {file}");
        Destroy(captured);
    }

    void PositionCamera(GmReviewShot shot)
    {
        if (currentCamera == null) throw new InvalidOperationException("restore capture has no camera");
        if (player != null && currentCamera.transform.IsChildOf(player.transform))
        {
            float eyeHeight = currentCamera.transform.localPosition.y;
            player.transform.position = shot.Position - Vector3.up * eyeHeight;
            player.transform.rotation = Quaternion.Euler(0f, shot.Yaw, 0f);
            currentCamera.transform.localRotation = Quaternion.Euler(shot.Pitch, 0f, 0f);
        }
        else
        {
            currentCamera.transform.position = shot.Position;
            currentCamera.transform.rotation = Quaternion.Euler(shot.Pitch, shot.Yaw, 0f);
        }
        Physics.SyncTransforms();
    }

    internal static string CardStateSha256(IReadOnlyList<GmParlorCardView> views) =>
        GmParlorShotTour.TextSha256(string.Join("\n", CardStateRows(views)));

    static string[] CardStateRows(IReadOnlyList<GmParlorCardView> views) => views
        .Where(view => view != null)
        .OrderBy(view => (int)view.PhysicalCard.Suit)
        .ThenBy(view => view.PhysicalCard.Rank)
        .Select(view =>
        {
            GmParlorCardBinding binding = view.Binding;
            Vector3 position = view.transform.localPosition;
            Quaternion rotation = view.transform.localRotation;
            return string.Join("|", new[]
            {
                $"{view.PhysicalCard.Suit}:{view.PhysicalCard.Rank}",
                $"{view.DisplayCard.Suit}:{view.DisplayCard.Rank}",
                binding.Zone.ToString(), binding.Slot.ToString(CultureInfo.InvariantCulture),
                binding.Facing.ToString(), view.IsFaceUp.ToString(),
                view.IsFaceVisualVisible.ToString(), view.IsBackVisualVisible.ToString(),
                position.x.ToString("R", CultureInfo.InvariantCulture),
                position.y.ToString("R", CultureInfo.InvariantCulture),
                position.z.ToString("R", CultureInfo.InvariantCulture),
                rotation.x.ToString("R", CultureInfo.InvariantCulture),
                rotation.y.ToString("R", CultureInfo.InvariantCulture),
                rotation.z.ToString("R", CultureInfo.InvariantCulture),
                rotation.w.ToString("R", CultureInfo.InvariantCulture),
            });
        }).ToArray();

    void ResetAccessibility()
    {
        if (player != null && player.IsPaused) player.SetPaused(false);
        GmPauseMenu.SetHighContrast(originalHighContrast);
        GmPauseMenu.SetCaptions(originalCaptions);
        GmPauseMenu.SetTextScale(originalTextScale);
    }

    static void EndEditor(int code)
    {
#if UNITY_EDITOR
        EditorApplication.Exit(code);
#endif
    }
}
