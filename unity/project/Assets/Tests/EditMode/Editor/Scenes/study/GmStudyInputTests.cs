using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmStudyInputTests
{
    string directory;
    GameObject graph;
    GmStudyController controller;
    GmStudyInput input;
    bool paused;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-study-input-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"));
        GmRunSeed.ForceForReview(4409);
        GmRunStore.BeginNewRun();
        controller = new GmStudyController();
        controller.InitializeOrRestore();
        graph = new GameObject("StudyInputTest");
        graph.SetActive(false);
        input = graph.AddComponent<GmStudyInput>();
        input.ConfigureForTests(controller, () => paused);
        graph.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(graph);
        GmSaveSystem.ResetTestConfiguration();
        GmRunSeed.ResetForTests();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void ChordPriorityIsPauseThenCancelThenChallengeThenConfirm()
    {
        int decisions = controller.PlayerDecisionCount;
        paused = true;
        Assert.That(input.ResolveFrameIntents(true, true, true, true).Consumed, Is.EqualTo(GmStudyFrameIntent.None));
        paused = false;
        Assert.That(input.ResolveFrameIntents(true, true, true, true).Consumed, Is.EqualTo(GmStudyFrameIntent.Pause));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions));
        Assert.That(input.ResolveFrameIntents(false, true, true, true).Consumed, Is.EqualTo(GmStudyFrameIntent.Cancel));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions));
        Assert.That(input.ResolveFrameIntents(false, false, true, true).Consumed, Is.EqualTo(GmStudyFrameIntent.Challenge));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions), "illegal challenge fell through to confirm");
        Assert.That(input.ResolveFrameIntents(false, false, false, true).Consumed, Is.EqualTo(GmStudyFrameIntent.Confirm));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions + 1));
    }

    [Test]
    public void NavigationUsesEngageAndReleaseLatch()
    {
        input.HandleNavigationIntent(new Vector2(0.56f, 0));
        Assert.That(controller.FocusIndex, Is.EqualTo(1));
        input.HandleNavigationIntent(Vector2.right);
        Assert.That(controller.FocusIndex, Is.EqualTo(1));
        input.HandleNavigationIntent(new Vector2(0.09f, 0));
        input.HandleNavigationIntent(new Vector2(0.07f, 0));
        input.HandleNavigationIntent(Vector2.right);
        Assert.That(controller.FocusIndex, Is.EqualTo(2));
    }

    [Test]
    public void PauseRecheckCanStopAnActionAtResolutionBoundary()
    {
        int probes = 0;
        input.ConfigureForTests(controller, () => ++probes == 2);
        GmStudyFrameIntentResult result = input.ResolveFrameIntents(false, false, false, true);
        Assert.That(result.Consumed, Is.EqualTo(GmStudyFrameIntent.None));
        Assert.That(controller.PlayerDecisionCount, Is.Zero);
        Assert.That(probes, Is.EqualTo(2));
    }

    [TestCase(false, GmStudyFrameIntent.Confirm)]
    [TestCase(true, GmStudyFrameIntent.Challenge)]
    public void InterventionIntentsResolveThroughDurableController(bool doChallenge,
        GmStudyFrameIntent expected)
    {
        GmStudyMatch pending = PendingInterventionAtFinalPosition();
        GmRunStore.LoadFromSaveData(new GmSaveData { studyMatch = pending.ExportSnapshot() });
        controller = new GmStudyController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmStudyInitializeResult.Restored));
        input.ConfigureForTests(controller, () => false);
        GmStudyFrameIntentResult result = input.ResolveFrameIntents(false, false, doChallenge, !doChallenge);
        Assert.That(result.Consumed, Is.EqualTo(expected));
        Assert.That(result.ActionError, Is.EqualTo(GmStudyActionError.None));
        Assert.That(controller.Phase, Is.EqualTo(GmStudyMatchPhase.Complete));
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(File.ReadAllText(
            Path.Combine(directory, "save.json"))));
        Assert.That(GmRunStore.GetStudyMatchSnapshot().phase, Is.EqualTo(GmStudyMatchPhase.Complete));
    }

    [Test]
    public void SourceUsesSharedActionsWithoutKeyboardPolling()
    {
        string source = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/project/Assets/Scripts/Scenes/study/GmStudyInput.cs")));
        Assert.That(source, Does.Contain("Resources.Load<InputActionAsset>(\"Input/GmControls\")"));
        foreach (string action in new[] { "Move", "Interact", "CallTell", "Cancel", "Pause" })
            Assert.That(source, Does.Contain($"FindAction(\"{action}\""));
        Assert.That(source, Does.Not.Contain("Keyboard.current"));
        Assert.That(source, Does.Not.Contain("SetPaused("));
    }

    [Test]
    public void PersistenceFailurePublishesRetryFeedbackAndSuccessfulRetryClearsIt()
    {
        var backend = new ToggleBackend();
        string savePath = Path.Combine(directory, "feedback-save.json");
        GmSaveSystem.ConfigureForTests(savePath, backend);
        GmRunStore.BeginNewRun();
        controller = new GmStudyController();
        controller.InitializeOrRestore();
        input.ConfigureForTests(controller, () => false);
        string fingerprint = controller.Snapshot.stateFingerprint;
        string disk = backend.LastJson;
        int feedbackEvents = 0;
        input.OnFeedbackChanged += () => feedbackEvents++;

        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected input failure"));
        GmStudyFrameIntentResult failed = input.ResolveFrameIntents(false, false, false, true);
        Assert.That(failed.ActionError, Is.EqualTo(GmStudyActionError.PersistenceFailed));
        Assert.That(input.LastActionError, Is.EqualTo(GmStudyActionError.PersistenceFailed));
        Assert.That(input.FeedbackMessage, Does.Contain("Retry"));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(GmRunStore.GetStudyMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(backend.LastJson, Is.EqualTo(disk));

        backend.Fail = false;
        Assert.That(input.ResolveFrameIntents(false, false, false, true).ActionError,
            Is.EqualTo(GmStudyActionError.None));
        Assert.That(input.FeedbackMessage, Is.Empty);
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(1));
        Assert.That(feedbackEvents, Is.EqualTo(2));
    }

    [Test]
    public void WrongPhaseFeedbackIsDistinctFromPersistenceFailure()
    {
        GmStudyFrameIntentResult result = input.ResolveFrameIntents(false, false, true, false);
        Assert.That(result.ActionError, Is.EqualTo(GmStudyActionError.WrongPhase));
        Assert.That(input.LastActionError, Is.EqualTo(GmStudyActionError.WrongPhase));
        Assert.That(input.FeedbackMessage, Does.Contain("not available"));
        Assert.That(input.FeedbackMessage, Does.Not.Contain("Retry"));
    }

    static GmStudyMatch PendingInterventionAtFinalPosition()
    {
        var match = new GmStudyMatch(11UL);
        match.TryChoose(GmStudyRules.GetPosition(0).cards[0].actionId, out _);
        match.TryChoose(WrongActionId(1), out _);
        match.TryChoose(GmStudyRules.GetPosition(2).cards[0].actionId, out _);
        return match;
    }

    static string WrongActionId(int positionIndex)
    {
        foreach (GmStudyMoveCard card in GmStudyRules.GetPosition(positionIndex).cards)
            if (!card.isCorrect) return card.actionId;
        throw new InvalidOperationException("position " + positionIndex + " has no wrong card");
    }

    sealed class ToggleBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public string LastJson { get; private set; }
        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected input failure");
            LastJson = json;
        }
    }
}
