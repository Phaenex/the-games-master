using NUnit.Framework;
using Nyx.GameCraft;

public sealed class GmStudyMatchTests
{
    [Test]
    public void MatchStartsAtFirstAuthoredPositionWithNoRng()
    {
        var first = new GmStudyMatch(91UL);
        var replay = new GmStudyMatch(91UL);
        Assert.That(first.PositionIndex, Is.Zero);
        Assert.That(first.CurrentFen, Is.EqualTo(GmStudyRules.GetPosition(0).originalFen));
        Assert.That(first.CurrentMoveCards, Has.Length.EqualTo(3));
        Assert.That(first.StateFingerprint, Is.EqualTo(replay.StateFingerprint));
        Assert.That(first.ExportSnapshot().session.gameId, Is.EqualTo("seven-debts.study"));
        Assert.That(first.ExportSnapshot().session.runSeed, Is.EqualTo(91UL));
    }

    [Test]
    public void LegalAlternativesScoreZeroAdvanceAndEndInLoss()
    {
        var match = new GmStudyMatch(1UL);
        Choose(match, "captured-record:c8-a6");
        Assert.That(match.PositionIndex, Is.EqualTo(1));
        Choose(match, "closing-file:b1-a1");
        Assert.That(match.PositionIndex, Is.EqualTo(2));
        Choose(match, "quiet-rank:f7-a2");
        Assert.That(match.Phase, Is.EqualTo(GmStudyMatchPhase.Complete));
        Assert.That(match.CorrectCount, Is.Zero);
        Assert.That(match.Result, Is.EqualTo(GmStudyMatchResult.AldricWin));
        Assert.That(match.PlayerDecisionCount, Is.EqualTo(3));
    }

    [Test]
    public void FirstCorrectThatWouldAwardPointTwoOpensExactOverride()
    {
        GmStudyMatch match = PendingOnClosingFile();
        Assert.That(match.Phase, Is.EqualTo(GmStudyMatchPhase.AwaitingIntervention));
        Assert.That(match.CorrectCount, Is.EqualTo(1));
        Assert.That(match.CurrentBoardVariant, Is.EqualTo(GmStudyBoardVariant.Altered));
        Assert.That(match.CurrentFen, Is.EqualTo(GmStudyRules.GetPosition(1).alteredFen));
        GmStudyInterventionReceipt receipt = match.InterventionReceipt;
        Assert.That(receipt.interventionId, Is.EqualTo("aldric-arbiter-override"));
        Assert.That(receipt.positionId, Is.EqualTo("closing-file"));
        Assert.That(receipt.actionId, Is.EqualTo("closing-file:b6-a6"));
        Assert.That(receipt.overrideFromSquare, Is.EqualTo("b4"));
        Assert.That(receipt.overrideToSquare, Is.EqualTo("b3"));
        Assert.That(receipt.evidenceIconId, Is.EqualTo("arbiter-override"));
        Assert.That(receipt.honestFingerprint, Is.Not.EqualTo(receipt.alteredFingerprint));
        Assert.That(match.ExportSnapshot().session.intervention.interventionId,
            Is.EqualTo("aldric-arbiter-override"));
    }

    [Test]
    public void ChallengeRestoresHonestPointAndWinsImmediately()
    {
        GmStudyMatch match = PendingOnClosingFile();
        Resolve(match, true);
        Assert.That(match.Phase, Is.EqualTo(GmStudyMatchPhase.Complete));
        Assert.That(match.CorrectCount, Is.EqualTo(2));
        Assert.That(match.CurrentBoardVariant, Is.EqualTo(GmStudyBoardVariant.Original));
        Assert.That(match.CurrentFen, Is.EqualTo(GmStudyRules.GetPosition(1).originalFen));
        Assert.That(match.Result, Is.EqualTo(GmStudyMatchResult.PlayerWin));
        Assert.That(match.InterventionReceipt.response, Is.EqualTo("challenge"));
        Assert.That(match.ExportSnapshot().session.terminalResult, Is.EqualTo(GameTerminalResult.Win));
    }

    [Test]
    public void ProceedWithholdsPointAdvancesAndLaterCorrectCanWin()
    {
        GmStudyMatch match = PendingOnClosingFile();
        Resolve(match, false);
        Assert.That(match.Phase, Is.EqualTo(GmStudyMatchPhase.AwaitingMove));
        Assert.That(match.PositionIndex, Is.EqualTo(2));
        Assert.That(match.CorrectCount, Is.EqualTo(1));
        Assert.That(match.CurrentFen, Is.EqualTo(GmStudyRules.GetPosition(2).originalFen));
        Assert.That(match.InterventionReceipt.response, Is.EqualTo("proceed"));
        Choose(match, "quiet-rank:f7-f8");
        Assert.That(match.Result, Is.EqualTo(GmStudyMatchResult.PlayerWin));
        Assert.That(match.CorrectCount, Is.EqualTo(2));
        Assert.That(match.InterventionUsed, Is.True);
    }

    [Test]
    public void FinalPositionOverrideProceedEndsInLossWithoutTie()
    {
        var match = new GmStudyMatch(3UL);
        Choose(match, "captured-record:c8-a6");
        Choose(match, "closing-file:b6-a6");
        Choose(match, "quiet-rank:f7-f8");
        Assert.That(match.Phase, Is.EqualTo(GmStudyMatchPhase.AwaitingIntervention));
        Resolve(match, false);
        Assert.That(match.Result, Is.EqualTo(GmStudyMatchResult.AldricWin));
        Assert.That(match.CorrectCount, Is.EqualTo(1));
    }

    [Test]
    public void IllegalOrOutOfPhaseMovesNeverMutate()
    {
        var match = new GmStudyMatch(4UL);
        string before = match.StateFingerprint;
        Assert.That(match.TryChoose("closing-file:b6-a6", out _), Is.False);
        Assert.That(match.TryChoose("missing", out _), Is.False);
        Assert.That(match.StateFingerprint, Is.EqualTo(before));
        Assert.That(match.PlayerDecisionCount, Is.Zero);
        Assert.That(match.TryResolveIntervention(true, out _), Is.False);
    }

    [Test]
    public void SnapshotsRestoreAtMovePendingInterventionAndBothTerminalBranches()
    {
        var move = new GmStudyMatch(5UL);
        Choose(move, "captured-record:c8-c1");
        AssertRestores(move);
        GmStudyMatch pending = PendingOnClosingFile(5UL);
        AssertRestores(pending);
        GmStudyMatch challenge = Restore(pending.ExportSnapshot());
        Resolve(challenge, true);
        AssertRestores(challenge);
        GmStudyMatch proceed = Restore(pending.ExportSnapshot());
        Resolve(proceed, false);
        Choose(proceed, "quiet-rank:f7-f8");
        AssertRestores(proceed);
    }

    [Test]
    public void RestoreRejectsForgedFenScoreIndexActionReceiptInterventionFingerprintPhaseAndResult()
    {
        GmStudyMatchSnapshot pending = PendingOnClosingFile(6UL).ExportSnapshot();
        Reject(pending, copy => copy.currentFen = "forged");
        Reject(pending, copy => copy.correctCount++);
        Reject(pending, copy => copy.positionIndex = 2);
        Reject(pending, copy => copy.actionJournal[0] = "forged");
        Reject(pending, copy => copy.interventionReceipt.overrideToSquare = "a1");
        Reject(pending, copy => copy.interventionReceipt.evidenceFacts = null);
        Reject(pending, copy => copy.session.intervention.interventionId = "forged");
        Reject(pending, copy => copy.stateFingerprint = "forged");
        Reject(pending, copy => copy.phase = GmStudyMatchPhase.AwaitingMove);
        GmStudyMatch completed = Restore(pending);
        Resolve(completed, true);
        GmStudyMatchSnapshot terminal = completed.ExportSnapshot();
        Reject(terminal, copy => copy.result = GmStudyMatchResult.AldricWin);
        Reject(terminal, copy => copy.hasResult = false);
    }

    [Test]
    public void DefinitionsReceiptsAndSnapshotsAreCloneSafe()
    {
        GmStudyMatch match = PendingOnClosingFile(7UL);
        GmStudyInterventionReceipt receipt = match.InterventionReceipt;
        receipt.evidenceFacts[0] = "forged";
        Assert.That(match.InterventionReceipt.evidenceFacts[0], Is.Not.EqualTo("forged"));
        GmStudyMatchSnapshot snapshot = match.ExportSnapshot();
        GmStudyMatchSnapshot copy = snapshot.DeepCopy();
        copy.actionJournal[0] = "forged";
        copy.interventionReceipt.evidenceFacts[0] = "forged";
        copy.session.actions[0].actionId = "forged";
        Assert.That(snapshot.actionJournal[0], Is.Not.EqualTo("forged"));
        Assert.That(snapshot.interventionReceipt.evidenceFacts[0], Is.Not.EqualTo("forged"));
        Assert.That(snapshot.session.actions[0].actionId, Is.Not.EqualTo("forged"));
    }

    static GmStudyMatch PendingOnClosingFile(ulong seed = 2UL)
    {
        var match = new GmStudyMatch(seed);
        Choose(match, "captured-record:c8-c1");
        Choose(match, "closing-file:b6-a6");
        return match;
    }

    static void Choose(GmStudyMatch match, string actionId) =>
        Assert.That(match.TryChoose(actionId, out string error), Is.True, error);

    static void Resolve(GmStudyMatch match, bool challenge) =>
        Assert.That(match.TryResolveIntervention(challenge, out string error), Is.True, error);

    static void AssertRestores(GmStudyMatch source)
    {
        GmStudyMatch restored = Restore(source.ExportSnapshot());
        Assert.That(restored.StateFingerprint, Is.EqualTo(source.StateFingerprint));
        Assert.That(restored.ActionJournal, Is.EqualTo(source.ActionJournal));
        Assert.That(restored.CurrentFen, Is.EqualTo(source.CurrentFen));
        Assert.That(restored.Phase, Is.EqualTo(source.Phase));
    }

    static GmStudyMatch Restore(GmStudyMatchSnapshot snapshot)
    {
        Assert.That(GmStudyMatch.TryRestore(snapshot, out GmStudyMatch restored, out string error),
            Is.True, error);
        return restored;
    }

    static void Reject(GmStudyMatchSnapshot source, System.Action<GmStudyMatchSnapshot> mutation)
    {
        GmStudyMatchSnapshot corrupt = source.DeepCopy();
        mutation(corrupt);
        Assert.That(GmStudyMatch.TryRestore(corrupt, out _, out _), Is.False);
    }
}
