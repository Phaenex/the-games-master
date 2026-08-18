using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public sealed class GmParlorPersistenceTests
{
    [Test]
    public void CurrentSnapshotRoundTripsWithoutMigration()
    {
        GmParlorMatch original = FindAwaiting(cheated: true);
        Assert.That(original.Read(), Is.EqualTo(GmParlorActionError.None));
        ConsumeOutcome(original);
        GmParlorMatchSnapshot snapshot = original.ExportSnapshot();
        Assert.That(snapshot.version, Is.EqualTo(GmParlorMatchSnapshot.CurrentVersion));

        Assert.That(GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored,
            out string error), Is.True, error);
        Assert.That(restored.PublicStateBytes, Is.EqualTo(original.PublicStateBytes));
    }

    [Test]
    public void SnapshotRoundTripsEveryShippingPhaseAndContinuesDeterministically()
    {
        foreach (GmParlorMatch original in RequiredStates())
        {
            GmParlorMatchSnapshot snapshot = original.ExportSnapshot();
            Assert.That(GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored,
                out string error), Is.True, $"{original.Phase}: {error}");
            Assert.That(restored.PublicStateBytes, Is.EqualTo(original.PublicStateBytes),
                original.Phase.ToString());

            DriveInLockstep(original, restored, 240);
        }
    }

    [Test]
    public void ExportIsADeepCopyAndImportFailureNeverMutatesTheTarget()
    {
        GmParlorMatch match = NewMatch(611, readUnlocked: true);
        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
        string before = match.PublicStateBytes;

        snapshot.playerHand[0] = new GmCard(GmSuit.Eyes, 99);
        Assert.That(match.PublicStateBytes, Is.EqualTo(before), "export aliased the live hand");
        Assert.That(match.TryImport(snapshot, out string error), Is.False);
        StringAssert.Contains("rank", error.ToLowerInvariant());
        Assert.That(match.PublicStateBytes, Is.EqualTo(before), "failed import partially mutated match");
    }

    [Test]
    public void CorruptSnapshotsAreRejectedExplicitlyAndNonMutating()
    {
        GmParlorMatch source = FindAwaiting(cheated: true);
        GmParlorMatch target = NewMatch(991, readUnlocked: true);
        string targetBefore = target.PublicStateBytes;

        AssertRejected(source, target, s => s.version++, "version");
        AssertRejected(source, target, s => s.randomState = 0, "random");
        AssertRejected(source, target, s => s.phase = (GmParlorMatchPhase)999, "phase");
        AssertRejected(source, target, s => s.playerHand[0] = new GmCard((GmSuit)999, 1), "suit");
        AssertRejected(source, target, s => s.playerHand[0] = new GmCard(GmSuit.Eyes, 0), "rank");
        AssertRejected(source, target, s => s.playerHand[1] = s.playerHand[0], "duplicate");
        AssertRejected(source, target, s => s.hasCurrentFollowCard = false, "table");
        AssertRejected(source, target, s => s.playerTricks = 99, "trick");

        Assert.That(target.PublicStateBytes, Is.EqualTo(targetBefore));
    }

    [Test]
    public void LegacyV0MigratesReconstructablePaidCardsButRejectsImpossibleEight()
    {
        GmParlorMatch honest = FindAwaiting(cheated: false);
        GmParlorMatchSnapshot legacy = honest.ExportSnapshot();
        legacy.version = 0;
        legacy.hasAldricPaidCard = false;
        legacy.aldricPaidCard = default;
        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.AldricPaidCard, Is.EqualTo(migrated.CurrentFollowCard));

        GmParlorMatch impossible = FindImpossibleEight();
        GmParlorMatchSnapshot ambiguous = impossible.ExportSnapshot();
        ambiguous.version = 0;
        ambiguous.hasAldricPaidCard = false;
        ambiguous.aldricPaidCard = default;
        Assert.That(GmParlorMatch.TryRestore(ambiguous, out _, out error), Is.False);
        StringAssert.Contains("legacy", error.ToLowerInvariant());
        StringAssert.Contains("paid", error.ToLowerInvariant());
    }

    [Test]
    public void LegacyV0MigratesConsumedOutcomeSequencesAcrossResultAndLaterPhases()
    {
        GmParlorMatch consumedTrick = FindAwaiting(cheated: false);
        Assert.That(consumedTrick.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        ConsumeOutcome(consumedTrick);
        AssertLegacyConsumedSequenceMigrates(consumedTrick, GmParlorMatchPhase.TrickResult);

        GmParlorMatch laterTrick = RestoreLegacy(consumedTrick);
        Assert.That(laterTrick.Continue(), Is.EqualTo(GmParlorActionError.None));
        AssertLegacyConsumedSequenceMigrates(laterTrick, laterTrick.Phase);

        GmParlorMatch round = NewMatch(712, readUnlocked: true);
        DriveUntil(round, GmParlorMatchPhase.RoundResult);
        AssertLegacyConsumedSequenceMigrates(round, GmParlorMatchPhase.RoundResult);

        GmParlorMatch result = NewMatch(713, readUnlocked: true);
        DriveUntil(result, GmParlorMatchPhase.MatchResult);
        AssertLegacyConsumedSequenceMigrates(result, GmParlorMatchPhase.MatchResult);
    }

    [Test]
    public void LegacyV0MigratesRepresentablePendingOutcomeAsDurableButUnacknowledged()
    {
        GmParlorMatch pending = FindAwaiting(cheated: false);
        Assert.That(pending.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot legacy = pending.ExportSnapshot();
        legacy.version = 0;
        legacy.highestDurableOutcomeSequence = 0;
        legacy.highestAcknowledgedOutcomeSequence = 0;

        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.HighestDurableOutcomeSequence, Is.EqualTo(migrated.OutcomeSequence));
        Assert.That(migrated.HighestAcknowledgedOutcomeSequence,
            Is.LessThan(migrated.OutcomeSequence));
        Assert.That(migrated.TryPeekOutcome(out _, out _), Is.True);
    }

    [Test]
    public void SerializedV1PendingOutcomeMigratesDurableUnacknowledgedAndPreservesEvidence()
    {
        GmParlorMatch pending = FindAwaiting(cheated: true);
        Assert.That(pending.Read(), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot legacy = SerializeAsFaithfulV1(pending, consumed: false);

        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.HighestDurableOutcomeSequence, Is.EqualTo(migrated.OutcomeSequence));
        Assert.That(migrated.HighestAcknowledgedOutcomeSequence,
            Is.EqualTo(migrated.OutcomeSequence - 1));
        Assert.That(migrated.TryPeekOutcome(out GmParlorOutcome outcome, out _), Is.True);
        Assert.That(outcome.Kind, Is.EqualTo(GmParlorOutcomeKind.CheatCaught));
        AssertPaidAndEvidencePreserved(legacy, migrated);
    }

    [Test]
    public void SerializedV1ConsumedOutcomesMigrateAcrossResultAndLaterPhases()
    {
        GmParlorMatch consumedTrick = FindAwaiting(cheated: false);
        Assert.That(consumedTrick.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        ConsumeOutcome(consumedTrick);
        AssertV1ConsumedMigrates(consumedTrick, GmParlorMatchPhase.TrickResult);

        GmParlorMatch later = RestoreSerializedV1(consumedTrick, consumed: true);
        Assert.That(later.Continue(), Is.EqualTo(GmParlorActionError.None));
        AssertV1ConsumedMigrates(later, later.Phase);

        GmParlorMatch round = NewMatch(714, readUnlocked: true);
        DriveUntil(round, GmParlorMatchPhase.RoundResult);
        AssertV1ConsumedMigrates(round, GmParlorMatchPhase.RoundResult);

        GmParlorMatch result = NewMatch(715, readUnlocked: true);
        DriveUntil(result, GmParlorMatchPhase.MatchResult);
        AssertV1ConsumedMigrates(result, GmParlorMatchPhase.MatchResult);
    }

    [Test]
    public void SerializedV1ZeroSequenceStaysZero()
    {
        GmParlorMatch original = NewMatch(716, readUnlocked: true);
        GmParlorMatchSnapshot legacy = SerializeAsFaithfulV1(original, consumed: false);
        Assert.That(legacy.outcomeSequence, Is.Zero);

        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.OutcomeSequence, Is.Zero);
        Assert.That(migrated.HighestDurableOutcomeSequence, Is.Zero);
        Assert.That(migrated.HighestAcknowledgedOutcomeSequence, Is.Zero);
        Assert.That(migrated.RandomState, Is.EqualTo(original.RandomState));
        Assert.That(migrated.PlayerHand, Is.EqualTo(original.PlayerHand));
        Assert.That(migrated.AldricHand, Is.EqualTo(original.AldricHand));
        Assert.That(migrated.AdaptivePackage.PrimaryCounterPlanId,
            Is.EqualTo(GmParlorCounterPlanId.LegacyBaseline));
    }

    [Test]
    public void SerializedV1ImpossibleEightWithPaidCardMigratesWithoutV0Reconstruction()
    {
        GmParlorMatch original = FindImpossibleEight();
        GmParlorMatchSnapshot legacy = SerializeAsFaithfulV1(original, consumed: false);
        Assert.That(legacy.hasAldricPaidCard, Is.True);

        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.AldricCheatKind, Is.EqualTo(GmParlorCheatKind.ImpossibleEighthRank));
        AssertPaidAndEvidencePreserved(legacy, migrated);
    }

    [Test]
    public void DerivedWinnerOutcomeAndEvidenceContradictionsFailClosed()
    {
        GmParlorMatch target = NewMatch(991, readUnlocked: true);
        GmParlorMatch pending = FindAwaiting(cheated: true);
        pending.Read();
        AssertRejected(pending, target, s => s.lastTrickWinner = GmTrickOwner.Aldric, "winner");
        AssertRejected(pending, target, s => s.lastOutcome.CatchDelta = 0, "outcome");
        AssertRejected(pending, target, s => s.aldricPaidCard = s.playerHand[0], "paid");

        GmParlorMatch round = NewMatch(702, readUnlocked: true);
        DriveUntil(round, GmParlorMatchPhase.RoundResult);
        AssertRejected(round, target, s => s.roundWinner = Other(s.roundWinner), "round winner");

        GmParlorMatch result = NewMatch(703, readUnlocked: true);
        DriveUntil(result, GmParlorMatchPhase.MatchResult);
        AssertRejected(result, target, s => s.matchWinner = Other(s.matchWinner), "match winner");
    }

    static void AssertRejected(GmParlorMatch source, GmParlorMatch target,
        Action<GmParlorMatchSnapshot> corrupt, string expectedError)
    {
        GmParlorMatchSnapshot snapshot = source.ExportSnapshot();
        corrupt(snapshot);
        Assert.That(target.TryImport(snapshot, out string error), Is.False);
        StringAssert.Contains(expectedError, error.ToLowerInvariant());
    }

    static void AssertLegacyConsumedSequenceMigrates(GmParlorMatch source,
        GmParlorMatchPhase expectedPhase)
    {
        Assert.That(source.OutcomeSequence, Is.GreaterThan(0));
        Assert.That(source.TryPeekOutcome(out _, out _), Is.False);
        GmParlorMatchSnapshot legacy = FaithfulConsumedLegacySnapshot(source);
        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.Phase, Is.EqualTo(expectedPhase));
        Assert.That(migrated.HighestDurableOutcomeSequence, Is.EqualTo(migrated.OutcomeSequence));
        Assert.That(migrated.HighestAcknowledgedOutcomeSequence, Is.EqualTo(migrated.OutcomeSequence));
        Assert.That(migrated.LastOutcome.Kind, Is.EqualTo(GmParlorOutcomeKind.None));
        Assert.That(migrated.LastOutcome.CatchDelta, Is.Zero);
        Assert.That(migrated.LastOutcome.CorruptionDelta, Is.Zero);
        Assert.That(migrated.LastOutcome.SuspicionDelta, Is.Zero);
        Assert.That(migrated.LastOutcome.SanityDelta, Is.Zero);
        Assert.That(migrated.LastOutcome.DefianceDelta, Is.Zero);
        Assert.That(migrated.LastOutcome.ComplianceDelta, Is.Zero);
        Assert.That(migrated.RoundNumber, Is.EqualTo(legacy.roundNumber));
        Assert.That(migrated.PlayerRounds, Is.EqualTo(legacy.playerRounds));
        Assert.That(migrated.AldricRounds, Is.EqualTo(legacy.aldricRounds));
        Assert.That(migrated.PlayerTricks, Is.EqualTo(legacy.playerTricks));
        Assert.That(migrated.AldricTricks, Is.EqualTo(legacy.aldricTricks));
        Assert.That(migrated.PlayerHand, Is.EqualTo(legacy.playerHand));
        Assert.That(migrated.AldricHand, Is.EqualTo(legacy.aldricHand));
        Assert.That(migrated.CurrentLeadCard,
            Is.EqualTo(legacy.hasCurrentLeadCard ? legacy.currentLeadCard : (GmCard?)null));
        Assert.That(migrated.CurrentFollowCard,
            Is.EqualTo(legacy.hasCurrentFollowCard ? legacy.currentFollowCard : (GmCard?)null));
    }

    static void AssertV1ConsumedMigrates(GmParlorMatch source,
        GmParlorMatchPhase expectedPhase)
    {
        GmParlorMatchSnapshot legacy = SerializeAsFaithfulV1(source, consumed: true);
        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        Assert.That(migrated.Phase, Is.EqualTo(expectedPhase));
        Assert.That(migrated.HighestDurableOutcomeSequence, Is.EqualTo(migrated.OutcomeSequence));
        Assert.That(migrated.HighestAcknowledgedOutcomeSequence, Is.EqualTo(migrated.OutcomeSequence));
        Assert.That(migrated.LastOutcome.Kind, Is.EqualTo(GmParlorOutcomeKind.None));
        Assert.That(migrated.RoundNumber, Is.EqualTo(legacy.roundNumber));
        Assert.That(migrated.PlayerRounds, Is.EqualTo(legacy.playerRounds));
        Assert.That(migrated.AldricRounds, Is.EqualTo(legacy.aldricRounds));
        Assert.That(migrated.PlayerTricks, Is.EqualTo(legacy.playerTricks));
        Assert.That(migrated.AldricTricks, Is.EqualTo(legacy.aldricTricks));
        Assert.That(migrated.PlayerHand, Is.EqualTo(legacy.playerHand));
        Assert.That(migrated.AldricHand, Is.EqualTo(legacy.aldricHand));
        AssertPaidAndEvidencePreserved(legacy, migrated);
    }

    static GmParlorMatch RestoreSerializedV1(GmParlorMatch source, bool consumed)
    {
        GmParlorMatchSnapshot legacy = SerializeAsFaithfulV1(source, consumed);
        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        return migrated;
    }

    static GmParlorMatchSnapshot SerializeAsFaithfulV1(GmParlorMatch source, bool consumed)
    {
        GmParlorMatchSnapshot snapshot = source.ExportSnapshot();
        snapshot.version = 1;
        if (consumed) snapshot.lastOutcome = default;
        string json = JsonUtility.ToJson(snapshot);
        json = Regex.Replace(json, ",\\\"highestDurableOutcomeSequence\\\":\\d+", string.Empty);
        json = Regex.Replace(json, ",\\\"highestAcknowledgedOutcomeSequence\\\":\\d+", string.Empty);
        Assert.That(json, Does.Not.Contain("highestDurableOutcomeSequence"));
        Assert.That(json, Does.Not.Contain("highestAcknowledgedOutcomeSequence"));
        GmParlorMatchSnapshot legacy = JsonUtility.FromJson<GmParlorMatchSnapshot>(json);
        Assert.That(legacy.version, Is.EqualTo(1));
        Assert.That(legacy.highestDurableOutcomeSequence, Is.Zero);
        Assert.That(legacy.highestAcknowledgedOutcomeSequence, Is.Zero);
        return legacy;
    }

    static void AssertPaidAndEvidencePreserved(GmParlorMatchSnapshot legacy,
        GmParlorMatch migrated)
    {
        Assert.That(migrated.AldricPaidCard,
            Is.EqualTo(legacy.hasAldricPaidCard ? legacy.aldricPaidCard : (GmCard?)null));
        Assert.That(migrated.AldricPaidIndex, Is.EqualTo(legacy.aldricPaidIndex));
        Assert.That(migrated.AldricCheated, Is.EqualTo(legacy.aldricCheated));
        Assert.That(migrated.AldricCheatKind, Is.EqualTo(legacy.aldricCheatKind));
        Assert.That(migrated.AuthoritativeCheatTell, Is.EqualTo(legacy.authoritativeCheatTell));
        Assert.That(migrated.CurrentLeadCard,
            Is.EqualTo(legacy.hasCurrentLeadCard ? legacy.currentLeadCard : (GmCard?)null));
        Assert.That(migrated.CurrentFollowCard,
            Is.EqualTo(legacy.hasCurrentFollowCard ? legacy.currentFollowCard : (GmCard?)null));
    }

    static GmParlorMatch RestoreLegacy(GmParlorMatch source)
    {
        GmParlorMatchSnapshot legacy = FaithfulConsumedLegacySnapshot(source);
        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch migrated,
            out string error), Is.True, error);
        return migrated;
    }

    static GmParlorMatchSnapshot FaithfulConsumedLegacySnapshot(GmParlorMatch source)
    {
        GmParlorMatchSnapshot legacy = source.ExportSnapshot();
        legacy.version = 0;
        legacy.highestDurableOutcomeSequence = 0;
        legacy.highestAcknowledgedOutcomeSequence = 0;
        legacy.lastOutcome = default;
        return legacy;
    }

    static void ConsumeOutcome(GmParlorMatch match)
    {
        Assert.That(match.TryPeekOutcome(out _, out ulong sequence), Is.True);
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
    }

    static IEnumerable<GmParlorMatch> RequiredStates()
    {
        yield return NewMatch(101, readUnlocked: true); // PlayerLeads
        yield return FindPlayerFollow();
        yield return FindAwaiting(cheated: false);
        yield return FindAwaiting(cheated: true);

        GmParlorMatch trick = FindAwaiting(cheated: true);
        Assert.That(trick.Read(), Is.EqualTo(GmParlorActionError.None));
        yield return trick;

        GmParlorMatch round = NewMatch(401, readUnlocked: true);
        DriveUntil(round, GmParlorMatchPhase.RoundResult);
        yield return round;

        GmParlorMatch result = NewMatch(402, readUnlocked: true);
        DriveUntil(result, GmParlorMatchPhase.MatchResult);
        yield return result;

        GmParlorMatch deferred = FindDeferredTeachingMatch();
        yield return deferred;
        Assert.That(deferred.StartRematch(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(deferred.ReadTestState, Is.EqualTo(GmReadTestState.Pending).Or.EqualTo(GmReadTestState.Active));
        yield return deferred;
    }

    static GmParlorMatch NewMatch(int seed, bool readUnlocked)
    {
        var match = new GmParlorMatch(seed, 2, 3, readUnlocked);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static GmParlorMatch FindAwaiting(bool cheated)
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            GmParlorMatch match = NewMatch(seed, readUnlocked: true);
            for (int i = 0; i < match.PlayerHand.Count; i++)
            {
                GmParlorMatch probe = NewMatch(seed, readUnlocked: true);
                if (probe.PlayPlayerCard(i) == GmParlorActionError.None &&
                    probe.AldricCheated == cheated) return probe;
            }
        }
        Assert.Fail("could not find required judgement state");
        return null;
    }

    static GmParlorMatch FindImpossibleEight()
    {
        for (int seed = 1; seed <= 2000; seed++)
        {
            GmParlorMatch match = NewMatch(seed, readUnlocked: true);
            int lead = FindLeadWithNoLegalAldricWinner(match);
            if (lead < 0) continue;
            match.PlayPlayerCard(lead);
            if (match.AldricCheatKind == GmParlorCheatKind.ImpossibleEighthRank) return match;
        }
        Assert.Fail("no impossible-eight state");
        return null;
    }

    static GmParlorMatch FindPlayerFollow()
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            GmParlorMatch match = NewMatch(seed, readUnlocked: true);
            int guard = 100;
            while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
            {
                StepAccepting(match);
                if (match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead) return match;
            }
        }
        Assert.Fail("could not find Aldric lead");
        return null;
    }

    static GmParlorMatch FindDeferredTeachingMatch()
    {
        for (int seed = 1; seed <= 500; seed++)
        {
            var match = new GmParlorMatch(seed, 1, 0, false);
            match.Start();
            DriveUntil(match, GmParlorMatchPhase.MatchResult);
            if (match.ReadTestDeferred) return match;
        }
        Assert.Fail("could not find deferred teaching match");
        return null;
    }

    static void DriveUntil(GmParlorMatch match, GmParlorMatchPhase phase)
    {
        int guard = 300;
        while (match.Phase != phase && guard-- > 0) StepAccepting(match);
        Assert.That(guard, Is.GreaterThan(0), $"did not reach {phase}");
    }

    static void DriveInLockstep(GmParlorMatch first, GmParlorMatch second, int guard)
    {
        bool rematched = first.Phase != GmParlorMatchPhase.MatchResult;
        while (guard-- > 0)
        {
            Assert.That(second.PublicStateBytes, Is.EqualTo(first.PublicStateBytes));
            if (first.Phase == GmParlorMatchPhase.MatchResult)
            {
                if (rematched) return;
                Assert.That(first.StartRematch(), Is.EqualTo(second.StartRematch()));
                rematched = true;
                continue;
            }

            GmParlorActionError a;
            GmParlorActionError b;
            if (first.Phase == GmParlorMatchPhase.PlayerLeads ||
                first.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
            {
                int index = FirstLegal(first);
                a = first.PlayPlayerCard(index);
                b = second.PlayPlayerCard(index);
            }
            else if (first.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            {
                a = first.ContinueJudgement();
                b = second.ContinueJudgement();
            }
            else
            {
                a = first.Continue();
                b = second.Continue();
            }
            Assert.That(b, Is.EqualTo(a));
        }
        Assert.Fail("lockstep continuation did not terminate");
    }

    static void StepAccepting(GmParlorMatch match)
    {
        if (match.Phase == GmParlorMatchPhase.PlayerLeads ||
            match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
            Assert.That(match.PlayPlayerCard(FirstLegal(match)), Is.EqualTo(GmParlorActionError.None));
        else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        else
            Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
    }

    static int FirstLegal(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
            if (match.GetPlayerCardError(i) == GmParlorActionError.None) return i;
        Assert.Fail("no legal player card");
        return -1;
    }

    static int FindLeadWithNoLegalAldricWinner(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
        {
            bool winner = false;
            for (int j = 0; j < match.AldricHand.Count; j++)
                if (GmParlorCore.IsLegal(match.AldricHand, j, match.PlayerHand[i]) &&
                    !GmParlorCore.LeadWins(match.PlayerHand[i], match.AldricHand[j])) winner = true;
            if (!winner) return i;
        }
        return -1;
    }

    static GmTrickOwner Other(GmTrickOwner owner) => owner == GmTrickOwner.Player
        ? GmTrickOwner.Aldric : GmTrickOwner.Player;
}
