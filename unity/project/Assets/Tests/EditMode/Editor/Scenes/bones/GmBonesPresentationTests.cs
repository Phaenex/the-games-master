using System;
using System.IO;
using NUnit.Framework;

public sealed class GmBonesPresentationTests
{
    string directory;
    string path;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-bones-present-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        GmSaveSystem.ConfigureForTests(path);
        GmRunSeed.ForceForReview(7711);
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.ResetTestConfiguration();
        GmRunSeed.ResetForTests();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void PlayerChoiceProjectionIsCloneOwnedAndContainsNoEngineSecrets()
    {
        var controller = new GmBonesController();
        controller.InitializeOrRestore();
        GmBonesPresentationState state = GmBonesPresentationModel.Project(controller);
        Assert.That(state.Phase, Is.EqualTo(GmBonesPresentationPhase.PlayerChoice));
        Assert.That(state.FocusIndex, Is.Zero);
        Assert.That(state.Actions.Length, Is.EqualTo(4));
        Assert.That(state.Actions[0].Label, Does.Contain("Bank"));
        Assert.That(state.Actions[0].Selected, Is.True);
        Assert.That(state.Actions[1].Label, Does.Contain("Press"));
        Assert.That(state.ActionLog, Is.Empty);
        int originalDie = state.Dice[0];
        state.Dice[0] = originalDie == 6 ? 1 : 6;
        state.ActionLog = new[] { "forged" };
        Assert.That(GmBonesPresentationModel.Project(controller).Dice[0], Is.EqualTo(originalDie));
        Assert.That(GmBonesPresentationModel.Project(controller).ActionLog, Is.Empty);
        foreach (string forbidden in new[] { "Fingerprint", "Random", "Replay", "Seed", "Session" })
            Assert.That(typeof(GmBonesPresentationState).GetProperty(forbidden), Is.Null);
    }

    [Test]
    public void ActionLogUsesPlayerFacingChoices()
    {
        var controller = new GmBonesController();
        controller.InitializeOrRestore();
        controller.MoveFocus(2);
        controller.ConfirmFocusedAction();
        GmBonesPresentationState state = GmBonesPresentationModel.Project(controller);
        Assert.That(state.ActionLog, Is.EqualTo(new[] { "Round 1: Press, locked die 2" }));
        Assert.That(state.ActionLog[0], Does.Not.Contain("round-1:press:1"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LoadedSixTruthIsGatedAndNeutralMarkerSurvivesResolution(bool challenge)
    {
        GmBonesMatch pending = PendingLoadedSix();
        GmRunStore.LoadFromSaveData(new GmSaveData { bonesMatch = pending.ExportSnapshot() });
        var controller = new GmBonesController();
        controller.InitializeOrRestore();
        GmBonesPresentationState waiting = GmBonesPresentationModel.Project(controller);
        Assert.That(waiting.Phase, Is.EqualTo(GmBonesPresentationPhase.Intervention));
        Assert.That(waiting.CanChallenge, Is.True);
        Assert.That(waiting.CanProceed, Is.True);
        Assert.That(waiting.HasLoadedSixMarker, Is.True);
        Assert.That(waiting.Dice, Is.EqualTo(new[] { 6, 6, 2 }),
            "pending table must show Aldric's altered throw");
        Assert.That(waiting.DisplayedReroll, Is.EqualTo(new[] { 6, 2 }));
        Assert.That(waiting.ChangedDieSlot, Is.EqualTo(1));
        Assert.That(waiting.ObservedHonestReroll, Is.Empty, "unobserved honest dice leaked before challenge");

        if (challenge) controller.CallTell(); else controller.ConfirmFocusedAction();
        GmBonesPresentationState complete = GmBonesPresentationModel.Project(controller);
        Assert.That(complete.Phase, Is.EqualTo(GmBonesPresentationPhase.Complete));
        Assert.That(complete.HasLoadedSixMarker, Is.True);
        Assert.That(complete.ObservedHonestReroll.Length, Is.EqualTo(challenge ? 2 : 0));
        Assert.That(complete.DisplayedReroll, Is.EqualTo(new[] { 6, 2 }),
            "historical table evidence vanished after resolution");
        Assert.That(complete.Dice, Is.EqualTo(challenge
            ? new[] { 6, 1, 2 }
            : new[] { 6, 6, 2 }));
        Assert.That(complete.AldricTotal, Is.EqualTo(challenge
            ? pending.InterventionReceipt.honestAldricTotal
            : pending.InterventionReceipt.alteredAldricTotal));
        Assert.That(complete.CorrectedByChallenge, Is.EqualTo(challenge));
        Assert.That(complete.ChangedDieSlot, Is.EqualTo(1));
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(File.ReadAllText(path)));
        var restored = new GmBonesController();
        restored.InitializeOrRestore();
        GmBonesPresentationState reloaded = GmBonesPresentationModel.Project(restored);
        Assert.That(reloaded.HasLoadedSixMarker, Is.True);
        Assert.That(reloaded.ChangedDieSlot, Is.EqualTo(1));
        Assert.That(reloaded.DisplayedReroll, Is.EqualTo(new[] { 6, 2 }));
        Assert.That(reloaded.Dice, Is.EqualTo(complete.Dice));
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
