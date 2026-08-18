using System;
using System.Linq;
using NUnit.Framework;

public sealed class GmParlorPresentationJournalTests
{
    [Test]
    public void PlayerLeadProducesCardFollowAndObservedJudgementCommands()
    {
        GmParlorMatch match = NewStartedMatch(117);
        GmParlorMatchSnapshot before = match.ExportSnapshot();

        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));

        GmParlorPresentationCommand[] commands =
            GmParlorPresentationJournal.Build(before, match.ExportSnapshot());

        Assert.That(commands.Select(command => command.Action), Is.EqualTo(new[]
        {
            GmParlorPresentationAction.PlayerCardToLead,
            GmParlorPresentationAction.AldricCardToFollow,
            GmParlorPresentationAction.OpenJudgement,
        }));
        Assert.That(commands[0].Card, Is.EqualTo(match.CurrentLeadCard));
        Assert.That(commands[1].Card, Is.EqualTo(match.CurrentFollowCard));
        Assert.That(commands[2].Card, Is.Null);
        Assert.That(commands[2].Observation, Is.EqualTo(match.TellObservation));
    }

    [Test]
    public void JudgementResolutionProducesOnlyThePublicTrickResult()
    {
        GmParlorMatch match = NewStartedMatch(210);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot before = match.ExportSnapshot();

        Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        GmParlorPresentationCommand[] commands =
            GmParlorPresentationJournal.Build(before, match.ExportSnapshot());

        Assert.That(commands, Has.Length.EqualTo(1));
        Assert.That(commands[0].Action, Is.EqualTo(GmParlorPresentationAction.ResolveTrick));
        Assert.That(commands[0].Owner, Is.EqualTo(match.LastTrickWinner));
        Assert.That(commands[0].Card, Is.Null);
        Assert.That(commands[0].Observation, Is.EqualTo(GmTellObservation.Calm));
    }

    [Test]
    public void StableCommandIdsSurviveExactSaveRestore()
    {
        GmParlorMatch match = NewStartedMatch(31415);
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();

        Assert.That(GmParlorMatch.TryRestore(before, out GmParlorMatch restoredBefore, out string beforeError),
            Is.True, beforeError);
        Assert.That(GmParlorMatch.TryRestore(after, out GmParlorMatch restoredAfter, out string afterError),
            Is.True, afterError);

        ulong[] liveIds = GmParlorPresentationJournal.Build(before, after)
            .Select(command => command.Id).ToArray();
        ulong[] restoredIds = GmParlorPresentationJournal.Build(
                restoredBefore.ExportSnapshot(), restoredAfter.ExportSnapshot())
            .Select(command => command.Id).ToArray();

        Assert.That(restoredIds, Is.EqualTo(liveIds));
        Assert.That(liveIds, Is.All.Not.EqualTo(0UL));
        Assert.That(liveIds.Distinct().Count(), Is.EqualTo(liveIds.Length));
    }

    [Test]
    public void JournalDoesNotExposeHiddenCheatTruthInTheJudgementCommand()
    {
        GmParlorMatch match = FindCheatedJudgement();
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        GmParlorMatchSnapshot before = after.DeepCopy();
        before.phase = GmParlorMatchPhase.PlayerLeads;
        before.playerHand.Insert(0, after.currentLeadCard);
        before.aldricHand.Insert(after.aldricPaidIndex, after.aldricPaidCard);
        before.hasCurrentLeadCard = false;
        before.hasCurrentFollowCard = false;
        before.hasAldricPaidCard = false;
        before.aldricPaidIndex = -1;
        before.aldricCheated = false;
        before.aldricCheatKind = GmParlorCheatKind.None;
        before.authoritativeCheatTell = string.Empty;
        before.tellObservation = GmTellObservation.Calm;

        GmParlorPresentationCommand judgement = GmParlorPresentationJournal.Build(before, after)[2];

        Assert.That(judgement.Action, Is.EqualTo(GmParlorPresentationAction.OpenJudgement));
        Assert.That(judgement.Observation, Is.EqualTo(after.tellObservation));
        Assert.That(typeof(GmParlorPresentationCommand).GetFields()
            .Select(field => field.Name), Has.None.Contains("Cheated"));
        Assert.That(typeof(GmParlorPresentationCommand).GetFields()
            .Select(field => field.Name), Has.None.Contains("CheatKind"));
    }

    [Test]
    public void ContinueAfterAldricWinClearsTableThenPresentsHisLead()
    {
        GmParlorMatch match = FindTrickResult(GmTrickOwner.Aldric);
        GmParlorMatchSnapshot before = match.ExportSnapshot();

        Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.Phase, Is.EqualTo(GmParlorMatchPhase.PlayerFollowsAldricLead));
        GmParlorPresentationCommand[] commands =
            GmParlorPresentationJournal.Build(before, match.ExportSnapshot());

        Assert.That(commands.Select(command => command.Action), Is.EqualTo(new[]
        {
            GmParlorPresentationAction.ClearTable,
            GmParlorPresentationAction.AldricCardToLead,
        }));
        Assert.That(commands[1].Card, Is.EqualTo(match.CurrentLeadCard));
    }

    [Test]
    public void RoundAndMatchBoundariesAreExplicitCommands()
    {
        GmParlorMatchSnapshot trick = Snapshot(GmParlorMatchPhase.TrickResult);
        trick.playerTricks = GmParlorMatch.TricksToWinRound;
        trick.lastTrickWinner = GmTrickOwner.Player;
        GmParlorMatchSnapshot round = trick.DeepCopy();
        round.phase = GmParlorMatchPhase.RoundResult;
        round.playerRounds = 1;
        round.roundWinner = GmTrickOwner.Player;

        GmParlorMatchSnapshot match = round.DeepCopy();
        match.playerRounds = GmParlorMatch.RoundsToWinMatch;
        match.phase = GmParlorMatchPhase.MatchResult;
        match.matchWinner = GmTrickOwner.Player;

        Assert.That(GmParlorPresentationJournal.Build(trick, round).Single().Action,
            Is.EqualTo(GmParlorPresentationAction.ResolveRound));
        Assert.That(GmParlorPresentationJournal.Build(round, match).Single().Action,
            Is.EqualTo(GmParlorPresentationAction.ResolveMatch));
    }

    [Test]
    public void ImpossibleTransitionFailsClosedWithDiagnostic()
    {
        GmParlorMatchSnapshot before = Snapshot(GmParlorMatchPhase.PlayerLeads);
        GmParlorMatchSnapshot after = before.DeepCopy();
        after.phase = GmParlorMatchPhase.MatchResult;

        Assert.That(GmParlorPresentationJournal.TryBuild(before, after,
            out GmParlorPresentationCommand[] commands, out string error), Is.False);
        Assert.That(commands, Is.Empty);
        StringAssert.Contains("PlayerLeads -> MatchResult", error);
        Assert.Throws<InvalidOperationException>(() => GmParlorPresentationJournal.Build(before, after));
    }

    static GmParlorMatch NewStartedMatch(int seed)
    {
        var match = new GmParlorMatch(seed, 2, 0, true);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static GmParlorMatch FindCheatedJudgement()
    {
        for (int seed = 1; seed < 10000; seed++)
        {
            GmParlorMatch match = NewStartedMatch(seed);
            for (int card = 0; card < match.PlayerHand.Count; card++)
            {
                GmParlorMatch candidate = NewStartedMatch(seed);
                if (candidate.PlayPlayerCard(card) == GmParlorActionError.None && candidate.AldricCheated)
                    return candidate;
            }
        }
        Assert.Fail("No deterministic cheated judgement fixture found");
        return null;
    }

    static GmParlorMatch FindTrickResult(GmTrickOwner winner)
    {
        for (int seed = 1; seed < 10000; seed++)
        {
            GmParlorMatch match = NewStartedMatch(seed);
            if (match.PlayPlayerCard(0) != GmParlorActionError.None) continue;
            if (match.ContinueJudgement() != GmParlorActionError.None) continue;
            if (match.LastTrickWinner == winner && match.PlayerTricks < GmParlorMatch.TricksToWinRound &&
                match.AldricTricks < GmParlorMatch.TricksToWinRound) return match;
        }
        Assert.Fail($"No deterministic {winner} trick fixture found");
        return null;
    }

    static GmParlorMatchSnapshot Snapshot(GmParlorMatchPhase phase)
    {
        return new GmParlorMatchSnapshot
        {
            seed = 9,
            randomState = 1,
            phase = phase,
            corruptionTier = 1,
            sanity = 80,
            roundNumber = 1,
            trickNumber = 1,
        };
    }
}
