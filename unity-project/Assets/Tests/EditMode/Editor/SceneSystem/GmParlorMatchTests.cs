using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class GmParlorMatchTests
{
    [Test]
    public void HandAndRevealViewsCannotAliasMutableBackingLists()
    {
        var match = NewMatch(17, readUnlocked: true);
        Assert.That(match.PlayerHand, Is.Not.InstanceOf<List<GmCard>>());
        Assert.That(match.AldricHand, Is.Not.InstanceOf<List<GmCard>>());
        Assert.That(match.RevealedAldricCards, Is.Not.InstanceOf<List<GmCard>>());

        var playerList = (IList<GmCard>)match.PlayerHand;
        var aldricList = (IList<GmCard>)match.AldricHand;
        var revealList = (IList<GmCard>)match.RevealedAldricCards;
        Assert.That(playerList.IsReadOnly, Is.True);
        Assert.That(aldricList.IsReadOnly, Is.True);
        Assert.That(revealList.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => playerList.Add(new GmCard(GmSuit.Eyes, 1)));
        Assert.Throws<NotSupportedException>(() => aldricList.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => revealList.Clear());
    }

    [Test]
    public void BasisPointThresholdBoundariesAreExact()
    {
        Assert.That(GmParlorMatch.PassesBasisPointThreshold(0, 0), Is.False);
        Assert.That(GmParlorMatch.PassesBasisPointThreshold(0, 1), Is.True);
        Assert.That(GmParlorMatch.PassesBasisPointThreshold(5499, 5500), Is.True);
        Assert.That(GmParlorMatch.PassesBasisPointThreshold(5500, 5500), Is.False);
        Assert.That(GmParlorMatch.PassesBasisPointThreshold(9999, 10000), Is.True);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GmParlorMatch.PassesBasisPointThreshold(-1, 5500));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GmParlorMatch.PassesBasisPointThreshold(1, 10001));
    }

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    public void NonZeroPrngDomainMapsPowerOfTwoBoundsWithoutModuloBias(int bound)
    {
        int[] lowCycle = new int[bound];
        for (uint value = 1; value <= (uint)(bound * 1024); value++)
        {
            Assert.That(GmParlorMatch.TryMapNonZeroRandomToIndex(value, bound, out int index), Is.True);
            lowCycle[index]++;
        }
        Assert.That(lowCycle, Is.All.EqualTo(1024));

        ulong domainSize = uint.MaxValue;
        ulong acceptedCount = domainSize - domainSize % (uint)bound;
        uint lastAcceptedValue = (uint)acceptedCount;
        Assert.That(GmParlorMatch.TryMapNonZeroRandomToIndex(
            lastAcceptedValue, bound, out int lastIndex), Is.True);
        Assert.That(lastIndex, Is.EqualTo((int)((acceptedCount - 1) % (uint)bound)));
        Assert.That(GmParlorMatch.TryMapNonZeroRandomToIndex(
            lastAcceptedValue + 1, bound, out _), Is.False);
        Assert.That(GmParlorMatch.TryMapNonZeroRandomToIndex(uint.MaxValue, bound, out _), Is.False);
        Assert.That(acceptedCount / (uint)bound * (uint)bound, Is.EqualTo(acceptedCount));
    }

    [Test]
    public void CanonicalCheatAndTellProbabilityTablesAreExact()
    {
        Assert.That(GmParlorMatch.PreReadCheatProbability, Is.EqualTo(0.55d));
        Assert.That(GmParlorMatch.GetPostReadCheatProbability(1), Is.EqualTo(0.28d));
        Assert.That(GmParlorMatch.GetPostReadCheatProbability(2), Is.EqualTo(0.50d));
        Assert.That(GmParlorMatch.GetPostReadCheatProbability(3), Is.EqualTo(0.68d));
        Assert.That(GmParlorMatch.GetPostReadCheatProbability(4), Is.EqualTo(0.82d));
        Assert.That(GmParlorMatch.GetTellReliability(1), Is.EqualTo(0.58d));
        Assert.That(GmParlorMatch.GetTellReliability(2), Is.EqualTo(0.70d));
        Assert.That(GmParlorMatch.GetTellReliability(3), Is.EqualTo(0.84d));
        Assert.That(GmParlorMatch.GetTellReliability(4), Is.EqualTo(0.95d));
    }

    [Test]
    public void DeterministicSeedSweepTracksEveryCanonicalCheatRate()
    {
        AssertCheatRate(readUnlocked: false, tier: 1,
            expected: GmParlorMatch.PreReadCheatProbability);
        for (int tier = 1; tier <= 4; tier++)
            AssertCheatRate(readUnlocked: true, tier: tier,
                expected: GmParlorMatch.GetPostReadCheatProbability(tier));
    }

    [Test]
    public void DeterministicSeedSweepTracksEveryCanonicalTellRate()
    {
        const int samples = 5000;
        const double tolerance = 0.025d;
        for (int tier = 1; tier <= 4; tier++)
        {
            int correct = 0;
            for (int seed = 1; seed <= samples; seed++)
            {
                var match = NewMatch(seed, readUnlocked: true, corruption: tier);
                Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
                if (match.TellShown == match.AldricCheated) correct++;
            }

            double actual = correct / (double)samples;
            TestContext.WriteLine($"tell tier={tier}: {correct}/{samples} = {actual:F4}");
            Assert.That(actual, Is.EqualTo(GmParlorMatch.GetTellReliability(tier))
                .Within(tolerance),
                $"tier {tier}: n=5000 has worst-case SE 0.0071; 0.025 is >3.5 SE");
        }
    }

    [Test]
    public void MatchIsBestOfThreeAndEveryRoundStartsWithPlayerLead()
    {
        var match = NewMatch(73, readUnlocked: true);
        int completedRounds = 0;
        int guard = 300;

        while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            if (match.Phase == GmParlorMatchPhase.RoundResult)
            {
                completedRounds++;
                Assert.That(match.PlayerTricks >= 4 || match.AldricTricks >= 4 ||
                    match.PlayerHand.Count == 0 || match.AldricHand.Count == 0, Is.True);
                Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
                if (match.Phase != GmParlorMatchPhase.MatchResult)
                    Assert.That(match.Phase, Is.EqualTo(GmParlorMatchPhase.PlayerLeads));
                continue;
            }

            StepAutomatically(match);
        }

        Assert.That(guard, Is.GreaterThan(0));
        Assert.That(completedRounds, Is.InRange(2, 3));
        Assert.That(match.PlayerRounds == 2 || match.AldricRounds == 2, Is.True);
    }

    [Test]
    public void AldricNeverCheatsOverALegalWinningResponse()
    {
        bool exercised = false;
        for (int seed = 1; seed <= 400 && !exercised; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true, corruption: 4);
            for (int i = 0; i < match.PlayerHand.Count; i++)
            {
                GmCard lead = match.PlayerHand[i];
                if (!HasLegalAldricWinner(match.AldricHand, lead)) continue;
                Assert.That(match.PlayPlayerCard(i), Is.EqualTo(GmParlorActionError.None));
                Assert.That(match.AldricCheated, Is.False);
                exercised = true;
                break;
            }
        }
        Assert.That(exercised, Is.True, "seed sweep must exercise a legal Aldric winner");
    }

    [Test]
    public void CorruptionTiersMonotonicallyIncreaseCheatFrequencyForTheSameSeeds()
    {
        int[] cheats = new int[4];
        for (int seed = 1; seed <= 1200; seed++)
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                var match = NewMatch(seed, readUnlocked: true, corruption: tier);
                int lead = FindLeadWithNoLegalAldricWinner(match);
                if (lead < 0) continue;
                Assert.That(match.PlayPlayerCard(lead), Is.EqualTo(GmParlorActionError.None));
                if (match.AldricCheated) cheats[tier - 1]++;
            }
        }

        Assert.That(cheats[0], Is.LessThan(cheats[1]));
        Assert.That(cheats[1], Is.LessThan(cheats[2]));
        Assert.That(cheats[2], Is.LessThan(cheats[3]));
        Assert.That(cheats[3] - cheats[0], Is.GreaterThan(150));
    }

    [Test]
    public void TellReliabilityRisesMonotonicallyAndScriptedTestIsAccurate()
    {
        int[] correct = new int[4];
        for (int seed = 1; seed <= 1200; seed++)
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                var match = NewMatch(seed, readUnlocked: true, corruption: tier);
                Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
                if (match.TellShown == match.AldricCheated) correct[tier - 1]++;
            }
        }

        Assert.That(correct[0], Is.LessThan(correct[1]));
        Assert.That(correct[1], Is.LessThan(correct[2]));
        Assert.That(correct[2], Is.LessThan(correct[3]));
        Assert.That(correct[3] - correct[0], Is.GreaterThan(300));

        GmParlorMatch taught = FindActiveReadTest();
        Assert.That(taught.AldricCheated, Is.True);
        Assert.That(taught.TellShown, Is.True);
    }

    [Test]
    public void TellObservationCoversTruthFalsePositiveAndFalseNegativeWithoutTextLeakage()
    {
        bool[,] seen = new bool[2, 2];
        for (int seed = 1; seed <= 20000; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true, corruption: 1);
            int lead = FindLeadWithNoLegalAldricWinner(match);
            if (lead < 0) continue;
            match.PlayPlayerCard(lead);
            int truth = match.AldricCheated ? 1 : 0;
            int suspicious = match.TellObservation == GmTellObservation.Suspicious ? 1 : 0;
            seen[truth, suspicious] = true;
            Assert.That(string.IsNullOrEmpty(match.AuthoritativeCheatTell),
                Is.EqualTo(!match.AldricCheated));
            if (seen[0, 0] && seen[0, 1] && seen[1, 0] && seen[1, 1]) break;
        }

        Assert.That(seen[0, 0], Is.True, "honest and calm");
        Assert.That(seen[0, 1], Is.True, "honest false-positive observation");
        Assert.That(seen[1, 0], Is.True, "cheat hidden by calm observation");
        Assert.That(seen[1, 1], Is.True, "cheat with suspicious observation");
    }

    [Test]
    public void OutcomeIsSequencedAndConsumableExactlyOnce()
    {
        GmParlorMatch match = FindHonestAwaitingRead();
        Assert.That(match.OutcomeSequence, Is.EqualTo(0));
        match.ContinueJudgement();
        Assert.That(match.OutcomeSequence, Is.EqualTo(1));
        Assert.That(match.LastOutcome.Kind, Is.Not.EqualTo(GmParlorOutcomeKind.None));
        Assert.That(match.LastOutcome.Kind, Is.EqualTo(match.LastOutcome.Kind),
            "repeated state reads do not consume the outcome");

        Assert.That(match.TryConsumeOutcome(out GmParlorOutcome first, out ulong firstSequence),
            Is.True);
        Assert.That(firstSequence, Is.EqualTo(1));
        Assert.That(first.Kind, Is.Not.EqualTo(GmParlorOutcomeKind.None));
        Assert.That(match.LastOutcome.Kind, Is.EqualTo(GmParlorOutcomeKind.None));
        Assert.That(match.TryConsumeOutcome(out _, out _), Is.False);

        match.Continue();
        while (match.Phase != GmParlorMatchPhase.TrickResult)
            StepAutomatically(match);
        Assert.That(match.OutcomeSequence, Is.EqualTo(2));
        Assert.That(match.TryConsumeOutcome(out _, out ulong secondSequence), Is.True);
        Assert.That(secondSequence, Is.EqualTo(2));
    }

    [Test]
    public void LockedTeachingNeverTerminatesSilentlyAndRematchCarriesDeferredTest()
    {
        GmParlorMatch deferred = null;
        for (int tier = 1; tier <= 4; tier++)
        {
            for (int seed = 1; seed <= 240; seed++)
            {
                var match = NewMatch(seed, readUnlocked: false, corruption: tier);
                DriveAcceptingToMatchResult(match);
                Assert.That(match.ReadUnlocked, Is.False);
                if (match.Suspicion >= 2)
                {
                    Assert.That(match.ReadTestDeferred, Is.True,
                        $"seed={seed}, tier={tier} ended with taught suspicion");
                    Assert.That(match.RematchRequiredForReadTest, Is.True);
                    if (deferred == null) deferred = match;
                }
            }
        }

        Assert.That(deferred, Is.Not.Null, "sweep must include a terminal pending test");
        int suspicion = deferred.Suspicion;
        Assert.That(deferred.StartRematch(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(deferred.Suspicion, Is.EqualTo(suspicion));
        Assert.That(deferred.ReadUnlocked, Is.False);
        Assert.That(deferred.ReadTestState,
            Is.EqualTo(GmReadTestState.Pending).Or.EqualTo(GmReadTestState.Active));

        int guard = 160;
        while (!(deferred.ReadTestState == GmReadTestState.Active &&
                 deferred.Phase == GmParlorMatchPhase.AwaitingAldricJudgement) && guard-- > 0)
            StepAutomatically(deferred);
        Assert.That(guard, Is.GreaterThan(0));
        Assert.That(deferred.AldricCheated, Is.True);
        Assert.That(deferred.Read(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(deferred.ReadUnlocked, Is.True);
        Assert.That(deferred.ReadTestDeferred, Is.False);
    }

    [Test]
    public void MissingAnActiveTeachingCheatReturnsPendingThenReactivatesOnVulnerableLead()
    {
        GmParlorMatch match = FindActiveReadTest();
        Assert.That(match.ReadTestState, Is.EqualTo(GmReadTestState.Active));
        Assert.That(match.AldricCheated, Is.True);
        Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.ReadTestState, Is.EqualTo(GmReadTestState.Pending));

        int guard = 180;
        while (!(match.ReadTestState == GmReadTestState.Active &&
                 match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement) && guard-- > 0)
        {
            if (match.Phase == GmParlorMatchPhase.MatchResult)
                Assert.That(match.StartRematch(), Is.EqualTo(GmParlorActionError.None));
            else
                StepAutomatically(match);
        }

        Assert.That(guard, Is.GreaterThan(0));
        Assert.That(match.AldricCheated, Is.True);
        Assert.That(match.TellObservation, Is.EqualTo(GmTellObservation.Suspicious));
    }

    [Test]
    public void TwoMissesScheduleTestAndCorrectReadUnlocksPermanently()
    {
        GmParlorMatch match = FindActiveReadTest();
        Assert.That(match.Suspicion, Is.GreaterThanOrEqualTo(2));
        Assert.That(match.ReadTestState, Is.EqualTo(GmReadTestState.Active));
        Assert.That(match.ReadUnlocked, Is.False);
        Assert.That(match.Read(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.ReadUnlocked, Is.True);
        Assert.That(match.ReadTestState, Is.EqualTo(GmReadTestState.Completed));
        Assert.That(match.LastOutcome.Kind, Is.EqualTo(GmParlorOutcomeKind.CheatCaught));
        Assert.That(match.LastOutcome.CatchDelta, Is.EqualTo(1));
        Assert.That(match.LastTrickWinner, Is.EqualTo(GmTrickOwner.Player));
    }

    [Test]
    public void ReadRejectsWhileLockedAndFalseReadLeavesHonestWinnerWithPenalty()
    {
        var locked = NewMatch(22, readUnlocked: false);
        Assert.That(locked.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        string before = locked.PublicStateBytes;
        Assert.That(locked.Read(), Is.EqualTo(GmParlorActionError.ReadLocked));
        Assert.That(locked.PublicStateBytes, Is.EqualTo(before));

        GmParlorMatch honest = FindHonestAwaitingRead();
        GmTrickOwner honestWinner = honest.EffectiveWinner;
        Assert.That(honest.Read(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(honest.LastOutcome.Kind, Is.EqualTo(GmParlorOutcomeKind.FalseReadPenalty));
        Assert.That(honest.LastOutcome.SanityDelta, Is.LessThan(0));
        Assert.That(honest.LastTrickWinner, Is.EqualTo(honestWinner));
    }

    [Test]
    public void MissOutcomeReportsOnlyTheDeltasActuallyApplied()
    {
        for (int seed = 1; seed <= 500; seed++)
        {
            var match = NewMatch(seed, readUnlocked: false, corruption: 4);
            int lead = FindLeadWithNoLegalAldricWinner(match);
            if (lead < 0) continue;
            match.PlayPlayerCard(lead);
            if (!match.AldricCheated) continue;

            Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(match.LastOutcome.Kind, Is.EqualTo(GmParlorOutcomeKind.CheatMissed));
            Assert.That(match.LastOutcome.CorruptionDelta, Is.EqualTo(0),
                "tier four cannot report corruption it did not apply");
            Assert.That(match.LastOutcome.SuspicionDelta, Is.EqualTo(1));
            Assert.That(match.LastOutcome.SanityDelta, Is.EqualTo(-3));
            return;
        }
        Assert.Fail("no tier-four missed-cheat case found");
    }

    [Test]
    public void EyesTeethAndBonesApplyAndResetAtRoundBoundary()
    {
        GmParlorMatch eyes = FindPlayerLeadSuit(GmSuit.Eyes);
        int eyesIndex = FindSuit(eyes.PlayerHand, GmSuit.Eyes);
        Assert.That(eyes.PlayPlayerCard(eyesIndex), Is.EqualTo(GmParlorActionError.None));
        Assert.That(eyes.EyesExposeAldricHand, Is.True);
        Assert.That(eyes.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(eyes.EyesExposeAldricHand, Is.False);

        GmParlorMatch teeth = FindResolvedPlayerWinWithSuit(GmSuit.Teeth);
        Assert.That(teeth.RevealedAldricCards.Count, Is.EqualTo(1));
        GmCard revealed = teeth.RevealedAldricCards[0];
        Assert.That(teeth.AldricHand.Contains(revealed), Is.True);

        GmParlorMatch bones = FindResolvedPlayerLossWithSuit(GmSuit.Bones);
        Assert.That(bones.BonesReturnedThisRound, Is.True);
        Assert.That(bones.PlayerHand.Any(card => card.Suit == GmSuit.Bones), Is.True);

        AdvanceToNextRound(bones);
        Assert.That(bones.BonesReturnedThisRound, Is.False);
        Assert.That(bones.RevealedAldricCards, Is.Empty);
        Assert.That(bones.EyesExposeAldricHand, Is.False);
    }

    [Test]
    public void DealsStayValueUniqueAndTeethRevealNeverOutlivesTheCardInHand()
    {
        for (int seed = 1; seed <= 300; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true);
            Assert.That(match.PlayerHand.Concat(match.AldricHand).Distinct().Count(), Is.EqualTo(14));
        }

        GmParlorMatch teeth = FindResolvedPlayerWinWithSuit(GmSuit.Teeth);
        GmCard revealed = teeth.RevealedAldricCards[0];
        int guard = 120;
        while (guard-- > 0)
        {
            Assert.That(teeth.RevealedAldricCards.All(teeth.AldricHand.Contains), Is.True,
                "every revealed value must still be in Aldric's hand");
            bool heldBefore = teeth.AldricHand.Contains(revealed);
            StepAutomatically(teeth);
            if (heldBefore && !teeth.AldricHand.Contains(revealed))
            {
                Assert.That(teeth.RevealedAldricCards.Contains(revealed), Is.False);
                return;
            }
        }
        Assert.Fail("revealed card never left Aldric's hand");
    }

    [Test]
    public void HostLeadRequiresPlayerFollowSuitAndUsesHonestCard()
    {
        GmParlorMatch match = FindAldricLeadWithRequiredSuit();
        Assert.That(match.Phase, Is.EqualTo(GmParlorMatchPhase.PlayerFollowsAldricLead));
        int illegal = FindDifferentSuit(match.PlayerHand, match.CurrentLeadCard.Value.Suit);
        Assert.That(illegal, Is.GreaterThanOrEqualTo(0));
        string before = match.PublicStateBytes;
        Assert.That(match.PlayPlayerCard(illegal), Is.EqualTo(GmParlorActionError.MustFollowSuit));
        Assert.That(match.PublicStateBytes, Is.EqualTo(before));

        int legal = FindSuit(match.PlayerHand, match.CurrentLeadCard.Value.Suit);
        Assert.That(match.PlayPlayerCard(legal), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.AldricCheated, Is.False);
        Assert.That(match.Phase, Is.EqualTo(GmParlorMatchPhase.TrickResult));
    }

    [Test]
    public void EmptyEitherHandEndsRoundAndAwardsAldricWithoutFourPlayerTricks()
    {
        for (int seed = 1; seed <= 300; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true);
            int guard = 30;
            while (match.Phase != GmParlorMatchPhase.RoundResult && guard-- > 0)
                StepAutomatically(match);
            Assert.That(guard, Is.GreaterThan(0));
            Assert.That(match.PlayerRounds + match.AldricRounds, Is.EqualTo(1));
            if (match.PlayerTricks < 4)
                Assert.That(match.AldricRounds, Is.EqualTo(1));
        }
    }

    [Test]
    public void SameSeedAndActionsProduceByteEquivalentStateSequence()
    {
        var first = NewMatch(90210, readUnlocked: true, corruption: 3);
        var second = NewMatch(90210, readUnlocked: true, corruption: 3);
        var firstSequence = new List<string> { first.PublicStateBytes };
        var secondSequence = new List<string> { second.PublicStateBytes };

        int guard = 120;
        while (first.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            StepAutomatically(first);
            StepAutomatically(second);
            firstSequence.Add(first.PublicStateBytes);
            secondSequence.Add(second.PublicStateBytes);
        }

        CollectionAssert.AreEqual(firstSequence, secondSequence);
        Assert.That(guard, Is.GreaterThan(0));
    }

    [Test]
    public void EveryIllegalActionIsNonMutatingAndReportsWhy()
    {
        var match = NewMatch(61, readUnlocked: false);
        AssertUnchanged(match, () => match.PlayPlayerCard(-1), GmParlorActionError.InvalidCardIndex);
        AssertUnchanged(match, () => match.PlayPlayerCard(99), GmParlorActionError.InvalidCardIndex);
        AssertUnchanged(match, match.Continue, GmParlorActionError.WrongPhase);
        AssertUnchanged(match, match.ContinueJudgement, GmParlorActionError.WrongPhase);
        AssertUnchanged(match, match.Read, GmParlorActionError.WrongPhase);
    }

    [Test]
    public void PlayerCardValidationExposesLegalActionsWithoutMutatingState()
    {
        var match = NewMatch(61, readUnlocked: true);
        string before = match.PublicStateBytes;
        Assert.That(match.GetPlayerCardError(0), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.GetPlayerCardError(-1), Is.EqualTo(GmParlorActionError.InvalidCardIndex));
        Assert.That(match.GetPlayerCardError(99), Is.EqualTo(GmParlorActionError.InvalidCardIndex));
        Assert.That(match.PublicStateBytes, Is.EqualTo(before));
    }

    static GmParlorMatch NewMatch(int seed, bool readUnlocked, int corruption = 1)
    {
        var match = new GmParlorMatch(seed, corruption, priorCatchCount: 0,
            readInitiallyUnlocked: readUnlocked);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static void AssertCheatRate(bool readUnlocked, int tier, double expected)
    {
        const int seeds = 7000;
        const double tolerance = 0.025d;
        int opportunities = 0;
        int cheats = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var match = NewMatch(seed, readUnlocked, tier);
            int lead = FindLeadWithNoLegalAldricWinner(match);
            if (lead < 0) continue;
            Assert.That(match.PlayPlayerCard(lead), Is.EqualTo(GmParlorActionError.None));
            opportunities++;
            if (match.AldricCheated) cheats++;
        }

        Assert.That(opportunities, Is.GreaterThan(5000));
        double actual = cheats / (double)opportunities;
        TestContext.WriteLine(
            $"cheat read={readUnlocked} tier={tier}: {cheats}/{opportunities} = {actual:F4}");
        Assert.That(actual, Is.EqualTo(expected).Within(tolerance),
            $"read={readUnlocked}, tier={tier}: n>5000 has worst-case SE <0.0071; " +
            "0.025 is >3.5 SE");
    }

    static void StepAutomatically(GmParlorMatch match)
    {
        switch (match.Phase)
        {
            case GmParlorMatchPhase.PlayerLeads:
            case GmParlorMatchPhase.PlayerFollowsAldricLead:
                Assert.That(match.PlayPlayerCard(FirstLegal(match)), Is.EqualTo(GmParlorActionError.None));
                break;
            case GmParlorMatchPhase.AwaitingAldricJudgement:
                Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
                break;
            case GmParlorMatchPhase.TrickResult:
            case GmParlorMatchPhase.RoundResult:
                Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
                break;
            default:
                Assert.Fail("automatic driver stalled in " + match.Phase);
                break;
        }
    }

    static void DriveAcceptingToMatchResult(GmParlorMatch match)
    {
        int guard = 180;
        while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
            StepAutomatically(match);
        Assert.That(guard, Is.GreaterThan(0), "accepting driver must terminate");
    }

    static int FirstLegal(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
            if (match.GetPlayerCardError(i) == GmParlorActionError.None) return i;
        return -1;
    }

    static bool HasLegalAldricWinner(IReadOnlyList<GmCard> hand, GmCard lead)
    {
        for (int i = 0; i < hand.Count; i++)
            if (GmParlorCore.IsLegal(hand, i, lead) && !GmParlorCore.LeadWins(lead, hand[i])) return true;
        return false;
    }

    static int FindLeadWithNoLegalAldricWinner(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
            if (!HasLegalAldricWinner(match.AldricHand, match.PlayerHand[i])) return i;
        return -1;
    }

    static GmParlorMatch FindHonestAwaitingRead()
    {
        for (int seed = 1; seed <= 500; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true, corruption: 1);
            for (int i = 0; i < match.PlayerHand.Count; i++)
            {
                var probe = NewMatch(seed, readUnlocked: true, corruption: 1);
                probe.PlayPlayerCard(i);
                if (!probe.AldricCheated) return probe;
            }
        }
        Assert.Fail("no honest awaiting-read state found");
        return null;
    }

    static GmParlorMatch FindActiveReadTest()
    {
        for (int seed = 1; seed <= 600; seed++)
        {
            var match = NewMatch(seed, readUnlocked: false, corruption: 1);
            int guard = 100;
            while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
            {
                if (match.ReadTestState == GmReadTestState.Active &&
                    match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement) return match;
                StepAutomatically(match);
            }
        }
        Assert.Fail("no deterministic suspicion-to-test path found");
        return null;
    }

    static GmParlorMatch FindPlayerLeadSuit(GmSuit suit)
    {
        for (int seed = 1; seed <= 200; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true);
            if (FindSuit(match.PlayerHand, suit) >= 0) return match;
        }
        Assert.Fail("no player lead suit found");
        return null;
    }

    static GmParlorMatch FindResolvedPlayerWinWithSuit(GmSuit suit)
    {
        for (int seed = 1; seed <= 900; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true);
            int index = FindSuit(match.PlayerHand, suit);
            if (index < 0) continue;
            match.PlayPlayerCard(index);
            if (match.AldricCheated) match.Read(); else match.ContinueJudgement();
            if (match.LastTrickWinner == GmTrickOwner.Player && match.AldricHand.Count > 0) return match;
        }
        Assert.Fail("no resolved player-win suit case found");
        return null;
    }

    static GmParlorMatch FindResolvedPlayerLossWithSuit(GmSuit suit)
    {
        for (int seed = 1; seed <= 900; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true);
            int index = FindSuit(match.PlayerHand, suit);
            if (index < 0) continue;
            match.PlayPlayerCard(index);
            match.ContinueJudgement();
            if (match.LastTrickWinner == GmTrickOwner.Aldric) return match;
        }
        Assert.Fail("no resolved player-loss suit case found");
        return null;
    }

    static GmParlorMatch FindAldricLeadWithRequiredSuit()
    {
        for (int seed = 1; seed <= 500; seed++)
        {
            var match = NewMatch(seed, readUnlocked: true);
            int guard = 80;
            while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
            {
                if (match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead &&
                    match.CurrentLeadCard.HasValue &&
                    FindSuit(match.PlayerHand, match.CurrentLeadCard.Value.Suit) >= 0 &&
                    FindDifferentSuit(match.PlayerHand, match.CurrentLeadCard.Value.Suit) >= 0)
                    return match;
                StepAutomatically(match);
            }
        }
        Assert.Fail("no Aldric-led follow-suit case found");
        return null;
    }

    static void AdvanceToNextRound(GmParlorMatch match)
    {
        int rounds = match.PlayerRounds + match.AldricRounds;
        int guard = 100;
        while (match.PlayerRounds + match.AldricRounds == rounds && guard-- > 0)
            StepAutomatically(match);
        Assert.That(guard, Is.GreaterThan(0));
        if (match.Phase == GmParlorMatchPhase.RoundResult) match.Continue();
    }

    static int FindSuit(IReadOnlyList<GmCard> hand, GmSuit suit)
    {
        for (int i = 0; i < hand.Count; i++) if (hand[i].Suit == suit) return i;
        return -1;
    }

    static int FindDifferentSuit(IReadOnlyList<GmCard> hand, GmSuit suit)
    {
        for (int i = 0; i < hand.Count; i++) if (hand[i].Suit != suit) return i;
        return -1;
    }

    static void AssertUnchanged(GmParlorMatch match,
        System.Func<GmParlorActionError> action, GmParlorActionError expected)
    {
        string before = match.PublicStateBytes;
        Assert.That(action(), Is.EqualTo(expected));
        Assert.That(match.PublicStateBytes, Is.EqualTo(before));
    }
}
