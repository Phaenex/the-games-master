using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

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
        Assert.That(input.ResolveFrameIntents(true, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.None));
        paused = false;
        Assert.That(input.ResolveFrameIntents(true, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Cancel));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions));
        Assert.That(input.ResolveFrameIntents(false, true, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Challenge));
        Assert.That(controller.PlayerDecisionCount, Is.EqualTo(decisions), "illegal challenge fell through to confirm");
        Assert.That(input.ResolveFrameIntents(false, false, true).Consumed, Is.EqualTo(GmBonesFrameIntent.Confirm));
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
        GmBonesFrameIntentResult result = input.ResolveFrameIntents(false, false, true);
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
        GmBonesFrameIntentResult result = input.ResolveFrameIntents(false, callTell, !callTell);
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
        foreach (string action in new[] { "Move", "Interact", "CallTell", "Cancel" })
            Assert.That(source, Does.Contain($"FindAction(\"{action}\""));
        Assert.That(source, Does.Not.Contain("Keyboard.current"));
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
}
