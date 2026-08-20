using System;
using System.IO;
using NUnit.Framework;

public sealed class GmStudyPresentationTests
{
    string directory;
    string path;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-study-present-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        GmSaveSystem.ConfigureForTests(path);
        GmRunSeed.ForceForReview(4409);
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
    public void MoveProjectionIsCloneOwnedAndContainsNoEngineSecrets()
    {
        var controller = new GmStudyController();
        controller.InitializeOrRestore();
        GmStudyPresentationState state = GmStudyPresentationModel.Project(controller);
        Assert.That(state.Phase, Is.EqualTo(GmStudyPresentationPhase.Move));
        Assert.That(state.FocusIndex, Is.Zero);
        Assert.That(state.PositionIndex, Is.Zero);
        Assert.That(state.PositionTitle, Is.EqualTo(GmStudyRules.GetPosition(0).title));
        Assert.That(state.Actions.Length, Is.EqualTo(3));
        Assert.That(state.Actions[0].Label, Does.Contain(GmStudyRules.GetPosition(0).cards[0].notation));
        Assert.That(state.Actions[0].Selected, Is.True);
        Assert.That(state.ActionLog, Is.Empty);
        Assert.That(state.Actions, Is.Not.SameAs(GmStudyPresentationModel.Project(controller).Actions));

        state.ActionLog = new[] { "forged" };
        Assert.That(GmStudyPresentationModel.Project(controller).ActionLog, Is.Empty);
        foreach (string forbidden in new[] { "Fingerprint", "Seed", "Session", "Random", "Replay" })
            Assert.That(typeof(GmStudyPresentationState).GetProperty(forbidden), Is.Null);
    }

    [Test]
    public void ActionLogUsesNotationNotRawActionIds()
    {
        var controller = new GmStudyController();
        controller.InitializeOrRestore();
        controller.MoveFocus(1);
        controller.ConfirmFocusedAction();
        GmStudyPresentationState state = GmStudyPresentationModel.Project(controller);
        string wrongNotation = GmStudyRules.GetPosition(0).cards[1].notation;
        Assert.That(state.ActionLog, Is.EqualTo(new[] { $"Position 1: {wrongNotation}" }));
        Assert.That(state.ActionLog[0], Does.Not.Contain("captured-record:"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ArbiterOverrideTruthIsGatedAndNeutralMarkerSurvivesResolution(bool challenge)
    {
        // Intervention must be forced at the FINAL position: only there does Proceed also
        // terminate the match (AldricWin), matching Challenge's unconditional PlayerWin
        // completion. Forcing it earlier leaves Proceed mid-match, not Complete.
        GmStudyMatch pending = PendingInterventionAtFinalPosition();
        GmRunStore.LoadFromSaveData(new GmSaveData { studyMatch = pending.ExportSnapshot() });
        var controller = new GmStudyController();
        controller.InitializeOrRestore();
        GmStudyPresentationState waiting = GmStudyPresentationModel.Project(controller);
        Assert.That(waiting.Phase, Is.EqualTo(GmStudyPresentationPhase.Intervention));
        Assert.That(waiting.CanChallenge, Is.True);
        Assert.That(waiting.CanProceed, Is.True);
        Assert.That(waiting.HasArbiterMarker, Is.True);
        GmStudyPosition finalPosition = GmStudyRules.GetPosition(2);
        Assert.That(waiting.ShownFen, Is.EqualTo(finalPosition.alteredFen),
            "pending table must show Aldric's altered board");
        Assert.That(waiting.AlteredFen, Is.EqualTo(finalPosition.alteredFen));
        Assert.That(waiting.OverrideFromSquare, Is.EqualTo(finalPosition.overrideFromSquare));
        Assert.That(waiting.OverrideToSquare, Is.EqualTo(finalPosition.overrideToSquare));
        Assert.That(waiting.ObservedOriginalFen, Is.Empty, "unobserved honest position leaked before challenge");

        if (challenge) controller.Challenge(); else controller.ConfirmFocusedAction();
        GmStudyPresentationState complete = GmStudyPresentationModel.Project(controller);
        Assert.That(complete.Phase, Is.EqualTo(GmStudyPresentationPhase.Complete));
        Assert.That(complete.HasArbiterMarker, Is.True);
        Assert.That(complete.ObservedOriginalFen,
            challenge ? Is.EqualTo(finalPosition.originalFen) : Is.Empty);
        Assert.That(complete.AlteredFen, Is.EqualTo(finalPosition.alteredFen),
            "historical table evidence vanished after resolution");
        Assert.That(complete.ShownFen, Is.EqualTo(challenge ? finalPosition.originalFen : finalPosition.alteredFen));
        Assert.That(complete.CorrectedByChallenge, Is.EqualTo(challenge));
        Assert.That(complete.Result, Is.EqualTo(challenge
            ? GmStudyMatchResult.PlayerWin
            : GmStudyMatchResult.AldricWin));

        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(File.ReadAllText(path)));
        var restored = new GmStudyController();
        restored.InitializeOrRestore();
        GmStudyPresentationState reloaded = GmStudyPresentationModel.Project(restored);
        Assert.That(reloaded.HasArbiterMarker, Is.True);
        Assert.That(reloaded.AlteredFen, Is.EqualTo(finalPosition.alteredFen));
        Assert.That(reloaded.ShownFen, Is.EqualTo(complete.ShownFen));
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
}
