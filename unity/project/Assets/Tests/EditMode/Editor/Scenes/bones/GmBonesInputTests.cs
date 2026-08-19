using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmBonesInputTests
{
    string directory;
    GameObject graph;
    GmBonesController controller;
    GmBonesInput input;
    bool paused;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-bones-input-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"));
        GmRunSeed.ForceForReview(7711);
        GmRunStore.BeginNewRun();
        controller = new GmBonesController();
        controller.InitializeOrRestore();
        graph = new GameObject("BonesInputTest");
        graph.SetActive(false);
        input = graph.AddComponent<GmBonesInput>();
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
        Assert.That(input.ResolveFrameIntents(true, true, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.None));
        paused = false;
        Assert.That(input.ResolveFrameIntents(true, true, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Pause));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions));
        Assert.That(input.ResolveFrameIntents(false, true, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Cancel));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions));
        Assert.That(input.ResolveFrameIntents(false, false, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Challenge));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions), "illegal challenge fell through to confirm");
        Assert.That(input.ResolveFrameIntents(false, false, false, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Confirm));
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
        GmBonesFrameIntentResult result = input.ResolveFrameIntents(false, false, false, true);
        Assert.That(result.Consumed, Is.EqualTo(GmBonesFrameIntent.None));
        Assert.That(controller.PlayerDecisionCount, Is.Zero);
        Assert.That(probes, Is.EqualTo(2));
    }

    [TestCase(false, GmBonesFrameIntent.Confirm)]
    [TestCase(true, GmBonesFrameIntent.Challenge)]
    public void InterventionIntentsResolveThroughDurableController(bool callTell,
        GmBonesFrameIntent expected)
    {
        GmBonesMatch pending = PendingLoadedSix();
        GmRunStore.LoadFromSaveData(new GmSaveData { bonesMatch = pending.ExportSnapshot() });
        controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.Restored));
        input.ConfigureForTests(controller, () => false);
        GmBonesFrameIntentResult result = input.ResolveFrameIntents(false, false, callTell, !callTell);
        Assert.That(result.Consumed, Is.EqualTo(expected));
        Assert.That(result.ActionError, Is.EqualTo(GmBonesActionError.None));
        Assert.That(controller.Phase, Is.EqualTo(GmBonesMatchPhase.Complete));
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(File.ReadAllText(
            Path.Combine(directory, "save.json"))));
        Assert.That(GmRunStore.GetBonesMatchSnapshot().phase, Is.EqualTo(GmBonesMatchPhase.Complete));
    }

    [Test]
    public void SourceUsesSharedActionsWithoutKeyboardPolling()
    {
        string source = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../unity/project/Assets/Scripts/Scenes/bones/GmBonesInput.cs")));
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
        controller = new GmBonesController();
        controller.InitializeOrRestore();
        input.ConfigureForTests(controller, () => false);
        string fingerprint = controller.Snapshot.stateFingerprint;
        string disk = backend.LastJson;
        int feedbackEvents = 0;
        input.OnFeedbackChanged += () => feedbackEvents++;

        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected input failure"));
        GmBonesFrameIntentResult failed = input.ResolveFrameIntents(false, false, false, true);
        Assert.That(failed.ActionError, Is.EqualTo(GmBonesActionError.PersistenceFailed));
        Assert.That(input.LastActionError, Is.EqualTo(GmBonesActionError.PersistenceFailed));
        Assert.That(input.FeedbackMessage, Does.Contain("Retry"));
        Assert.That(controller.Snapshot.stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
        Assert.That(backend.LastJson, Is.EqualTo(disk));

        backend.Fail = false;
        Assert.That(input.ResolveFrameIntents(false, false, false, true).ActionError,
            Is.EqualTo(GmBonesActionError.None));
        Assert.That(input.FeedbackMessage, Is.Empty);
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(1));
        Assert.That(feedbackEvents, Is.EqualTo(2));
    }

    [Test]
    public void WrongPhaseFeedbackIsDistinctFromPersistenceFailure()
    {
        GmBonesFrameIntentResult result = input.ResolveFrameIntents(false, false, true, false);
        Assert.That(result.ActionError, Is.EqualTo(GmBonesActionError.WrongPhase));
        Assert.That(input.LastActionError, Is.EqualTo(GmBonesActionError.WrongPhase));
        Assert.That(input.FeedbackMessage, Does.Contain("not available"));
        Assert.That(input.FeedbackMessage, Does.Not.Contain("Retry"));
    }

    static GmBonesMatch PendingLoadedSix()
    {
        int[] dice = { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2 };
        var match = new GmBonesMatch(3UL, dice);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        return match;
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
