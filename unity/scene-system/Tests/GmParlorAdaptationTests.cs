using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

public sealed class GmParlorAdaptationTests
{
    [Test]
    public void EmptyHistorySelectsFrozenBaselineWithoutConsumingDeckRandomness()
    {
        var profile = new GmParlorProfileView(Array.Empty<GmParlorCompletedRunReceipt>());

        GmParlorAdaptivePackage first = GmParlorAdaptiveDirector.Select(
            profile, seed: 4815, GmParlorAdaptiveMode.Ordinary, catalogVersion: 1);
        GmParlorAdaptivePackage second = GmParlorAdaptiveDirector.Select(
            profile, seed: 4815, GmParlorAdaptiveMode.Ordinary, catalogVersion: 1);

        Assert.That(first.Mode, Is.EqualTo(GmParlorAdaptiveMode.Ordinary));
        Assert.That(first.PrimaryCounterPlanId, Is.EqualTo(GmParlorCounterPlanId.None));
        Assert.That(first.HonestStrategyId,
            Is.EqualTo(GmParlorHonestStrategyId.MeasuredCourtesy));
        Assert.That(first.ToCanonicalBytes(), Is.EqualTo(second.ToCanonicalBytes()));

        var baseline = new GmParlorMatch(4815, 3, 0, true);
        var configured = new GmParlorMatch(4815, 3, 0, true,
            adaptivePackage: first);
        Assert.That(baseline.Start(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(configured.Start(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(configured.RandomState, Is.EqualTo(baseline.RandomState));
        Assert.That(configured.PlayerHand, Is.EqualTo(baseline.PlayerHand));
        Assert.That(configured.AldricHand, Is.EqualTo(baseline.AldricHand));

        Assert.Throws<ArgumentNullException>(() => GmParlorAdaptiveDirector.Select(
            null, 4815, GmParlorAdaptiveMode.Mirror, catalogVersion: 1),
            "a failed profile load must not impersonate a legitimate empty profile");
    }

    [Test]
    public void BehaviorAccumulatorRecordsOnlyCommittedCanonicalPublicDecisions()
    {
        var accumulator = new GmParlorBehaviorAccumulator(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary));
        accumulator.RecordPlayerLead(new GmCard(GmSuit.Eyes, 7),
            roundTrickOrdinal: 2, roundTrickCapacity: 7);
        accumulator.RecordPlayerFollow(new GmCard(GmSuit.Bones, 3));
        accumulator.RecordRead(GmTellObservation.Suspicious,
            GmParlorOutcomeKind.CheatCaught,
            judgementOrdinal: 1);

        GmParlorCompletedMatchSummary summary = accumulator.SealCompletedMatch(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary),
            matchOrdinal: 1, completedRematches: 0);

        Assert.That(summary.JudgementOpportunities, Is.EqualTo(1));
        Assert.That(summary.ReadAttempts, Is.EqualTo(1));
        Assert.That(summary.CorrectReads, Is.EqualTo(1));
        Assert.That(summary.FalseReads, Is.Zero);
        Assert.That(summary.PressureLeads, Is.EqualTo(1));
        Assert.That(summary.EarlyHighRankSpends, Is.EqualTo(1));
        Assert.That(summary.PlayerLeadCountBySuit[(int)GmSuit.Eyes], Is.EqualTo(1));
        Assert.That(summary.PlayerPlayedCountBySuit[(int)GmSuit.Eyes], Is.EqualTo(1));
        Assert.That(summary.PlayerPlayedCountBySuit[(int)GmSuit.Bones], Is.EqualTo(1));
        Assert.That(summary.CommittedEvidenceClaimsByFamily, Is.All.Zero,
            "automatic focus/evidence presentation is not a committed evidence claim");
        Assert.That(accumulator.SealCompletedMatch(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary),
            1, 0).ToCanonicalBytes(),
            Is.EqualTo(summary.ToCanonicalBytes()), "sealing is idempotent");
    }

    [Test]
    public void RejectedBehaviorRecordsAreFailureAtomicAndSealedBehaviorIsImmutable()
    {
        GmParlorAdaptivePackage baseline = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        var accumulator = new GmParlorBehaviorAccumulator(baseline);

        byte[] before = accumulator.ToCanonicalBytes();
        Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.RecordRead(
            GmTellObservation.Suspicious, GmParlorOutcomeKind.CheatCaught,
            judgementOrdinal: 2));
        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before),
            "an invalid explicit judgement ordinal must not partially record an observation");

        Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.RecordPlayerLead(
            new GmCard(GmSuit.Flames, 0), 1, 7));
        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before));
        Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.RecordPlayerFollow(
            new GmCard((GmSuit)99, 1)));
        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            accumulator.RecordAccept((GmTellObservation)99));
        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before));
        Assert.Throws<ArgumentException>(() => accumulator.RecordRead(
            GmTellObservation.Calm, GmParlorOutcomeKind.HonestAccepted));
        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            accumulator.RecordCommittedEvidenceClaim((GmParlorEvidenceFamilyId)99));
        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before));

        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.SealCompletedMatch(baseline, 1, 0);
        byte[] sealedBytes = accumulator.ToCanonicalBytes();
        TestDelegate[] rejectedAfterSeal =
        {
            () => accumulator.RecordPlayerLead(new GmCard(GmSuit.Flames, 1), 1, 7),
            () => accumulator.RecordPlayerFollow(new GmCard(GmSuit.Eyes, 1)),
            () => accumulator.RecordAccept(GmTellObservation.Calm),
            () => accumulator.RecordRead(GmTellObservation.Calm,
                GmParlorOutcomeKind.FalseReadPenalty),
            () => accumulator.RecordCommittedEvidenceClaim(
                GmParlorEvidenceFamilyId.Gesture),
        };
        foreach (TestDelegate rejected in rejectedAfterSeal)
        {
            Assert.Throws<InvalidOperationException>(rejected);
            Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(sealedBytes),
                "a sealed accumulator must be byte-identical after every rejected record API");
        }
    }

    [Test]
    public void CompletedSummaryRejectsPhysicallyImpossibleReadTiming()
    {
        GmParlorCompletedMatchSummary valid =
            GmParlorAdaptationFixtures.FrozenReceipts()[0].Summary;

        GmParlorCompletedMatchSummary impossibleCapacity = valid.DeepCopy();
        impossibleCapacity.readFirstThird = 7;
        impossibleCapacity.readMiddleThird = 1;
        impossibleCapacity.readFinalThird = 1;
        Assert.That(impossibleCapacity.TryValidate(out _), Is.False,
            "ten opportunities have only three first-third ordinals");

        GmParlorCompletedMatchSummary forgedCapacities = valid.DeepCopy();
        forgedCapacities.readFirstThird = 4;
        forgedCapacities.readMiddleThird = 3;
        forgedCapacities.readFinalThird = 2;
        forgedCapacities.firstThirdOpportunityCapacity = 4;
        forgedCapacities.middleThirdOpportunityCapacity = 3;
        forgedCapacities.finalThirdOpportunityCapacity = 3;
        Assert.That(forgedCapacities.TryValidate(out _), Is.False,
            "caller-supplied capacities cannot redefine a ten-opportunity match's thirds");

        GmParlorCompletedMatchSummary contradictoryFirst =
            GmParlorAdaptationFixtures.FrozenReceipts()[1].Summary.DeepCopy();
        contradictoryFirst.firstReadOpportunityOrdinal = 1;
        Assert.That(contradictoryFirst.TryValidate(out _), Is.False,
            "a first-third ordinal cannot claim that all Reads occurred in the final third");

        GmParlorCompletedMatchSummary impossibleAfterFirst = valid.DeepCopy();
        impossibleAfterFirst.firstReadOpportunityOrdinal = 3;
        impossibleAfterFirst.firstReadMatchReadFirstThird = 1;
        Assert.That(impossibleAfterFirst.TryValidate(out _), Is.False,
            "one match cannot place two more first-third Reads before a first Read at ordinal three");
    }

    [Test]
    public void CombinedRematchesRetainTheFirstReadsOriginalMatchTimingFrame()
    {
        GmParlorAdaptivePackage baseline = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        var accumulator = new GmParlorBehaviorAccumulator(baseline);
        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.RecordRead(GmTellObservation.Calm,
            GmParlorOutcomeKind.FalseReadPenalty, 3);
        accumulator.SealCompletedMatch(baseline, 1, 0);
        accumulator.BeginRematch();
        accumulator.RecordRead(GmTellObservation.Suspicious,
            GmParlorOutcomeKind.CheatCaught, 1);
        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.SealCompletedMatch(baseline, 2, 0);

        GmParlorCompletedMatchSummary combined =
            accumulator.CreateCompletedRunSummary();

        Assert.That(combined.TryValidate(out string error), Is.True, error);
        Assert.That(combined.completedRematches, Is.EqualTo(1));
        Assert.That(combined.firstReadOpportunityOrdinal, Is.EqualTo(3));
        Assert.That(combined.firstReadMatchStartOrdinal, Is.Zero);
        Assert.That(combined.firstReadMatchJudgementOpportunities, Is.EqualTo(3));
        Assert.That(combined.readFirstThird, Is.EqualTo(1),
            "the later rematch contributes its own early-third Read");
        Assert.That(combined.readFinalThird, Is.EqualTo(1),
            "the chronologically first Read remains final-third in match one");
        Assert.That(combined.firstThirdOpportunityCapacity, Is.EqualTo(2));
        Assert.That(combined.finalThirdOpportunityCapacity, Is.EqualTo(2));
        Assert.That(combined.judgementOpportunitiesByMatch, Is.EqualTo(new[] { 3, 3 }));

        GmParlorCompletedMatchSummary forged = combined.DeepCopy();
        forged.firstThirdOpportunityCapacity = 3;
        forged.middleThirdOpportunityCapacity = 1;
        forged.finalThirdOpportunityCapacity = 2;
        Assert.That(forged.TryValidate(out _), Is.False,
            "two fixed three-opportunity matches can only realize capacities 2/2/2");
    }

    [Test]
    public void ExtremePublicTrickOrdinalsCannotOverflowIntoAnEarlySpend()
    {
        var accumulator = new GmParlorBehaviorAccumulator(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary));

        accumulator.RecordPlayerLead(new GmCard(GmSuit.Flames, 7),
            int.MaxValue, int.MaxValue);

        Assert.That(accumulator.pressureLeads, Is.EqualTo(1));
        Assert.That(accumulator.earlyHighRankSpends, Is.Zero,
            "the final ordinal in an extreme public round is not in its first half");
    }

    [Test]
    public void BeginRematchRejectsAnExhaustedMatchOrdinalWithoutMutation()
    {
        GmParlorAdaptivePackage baseline = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        var accumulator = new GmParlorBehaviorAccumulator(baseline);
        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.SealCompletedMatch(baseline, 1, 0);
        accumulator.matchOrdinal = GmParlorCompletedMatchSummary.MaxFeatureCount;
        accumulator.sealedSummary.matchOrdinal = accumulator.matchOrdinal;
        accumulator.completedMatchSummaries[0].matchOrdinal = accumulator.matchOrdinal;
        Assert.That(accumulator.TryValidate(out string error), Is.True, error);
        byte[] before = accumulator.ToCanonicalBytes();

        Assert.Throws<InvalidOperationException>(() => accumulator.BeginRematch());

        Assert.That(accumulator.ToCanonicalBytes(), Is.EqualTo(before));
        Assert.That(accumulator.TryValidate(out error), Is.True, error);
    }

    [Test]
    public void PressureFeatureIsDerivedOnlyFromThePublicLeadCard()
    {
        var accumulator = new GmParlorBehaviorAccumulator(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary));

        accumulator.RecordPlayerLead(new GmCard(GmSuit.Flames, 7), 1, 7);
        accumulator.RecordPlayerLead(new GmCard(GmSuit.Eyes, 4), 2, 7);

        Assert.That(accumulator.pressureLeads, Is.EqualTo(1),
            "a public high-rank forcing lead is pressure regardless of Aldric's hidden hand");
        Assert.That(typeof(GmParlorBehaviorAccumulator)
            .GetMethod(nameof(GmParlorBehaviorAccumulator.RecordPlayerLead))
            .GetParameters().Any(parameter => parameter.ParameterType == typeof(bool)),
            Is.False,
            "hidden-hand-derived pressure must not be injectable into durable behavior");
    }

    [Test]
    public void AcceptTelemetryCannotReceiveOrRetainHiddenCheatTruth()
    {
        GmParlorAdaptivePackage baseline = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        var first = new GmParlorBehaviorAccumulator(baseline);
        var second = new GmParlorBehaviorAccumulator(baseline);

        first.RecordAccept(GmTellObservation.Suspicious);
        second.RecordAccept(GmTellObservation.Suspicious);

        GmParlorCompletedMatchSummary firstSummary = first.SealCompletedMatch(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary), 1, 0);
        GmParlorCompletedMatchSummary secondSummary = second.SealCompletedMatch(
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary), 1, 0);
        Assert.That(firstSummary.AcceptedSuspicious, Is.EqualTo(1));
        Assert.That(firstSummary.ToCanonicalBytes(),
            Is.EqualTo(secondSummary.ToCanonicalBytes()));
        Assert.That(typeof(GmParlorBehaviorAccumulator).GetField("missedCheats"), Is.Null,
            "an accepted play has no public honest-versus-cheat result to retain");
        Assert.That(typeof(GmParlorCompletedMatchSummary).GetField("missedCheats"), Is.Null);
        Assert.That(typeof(GmParlorBehaviorAccumulator)
            .GetMethod(nameof(GmParlorBehaviorAccumulator.RecordAccept))
            .GetParameters().Select(parameter => parameter.ParameterType),
            Is.EqualTo(new[] { typeof(GmTellObservation) }),
            "the accept boundary must not admit a concealed outcome");
    }

    [Test]
    public void AdaptivePackageIsFrozenAcrossRematchesAndExactRestore()
    {
        GmParlorAdaptivePackage selected = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.FalseAccuser), 9164,
            GmParlorAdaptiveMode.Mirror, 1);
        var match = new GmParlorMatch(9164, 4, 7, true,
            adaptivePackage: selected);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        GmParlorAdaptivePackage before = match.AdaptivePackage.DeepCopy();

        DriveToMatchResult(match, alwaysRead: false);
        Assert.That(match.StartRematch(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.AdaptivePackage.ToCanonicalBytes(),
            Is.EqualTo(before.ToCanonicalBytes()));

        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
        Assert.That(GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored,
            out string error), Is.True, error);
        Assert.That(restored.AdaptivePackage.ToCanonicalBytes(),
            Is.EqualTo(before.ToCanonicalBytes()));
        Assert.That(restored.PublicStateBytes, Is.EqualTo(match.PublicStateBytes));
    }

    [Test]
    public void CanonicalHashesCoverTheCompletePackageAccumulatorAndActionReplay()
    {
        GmParlorAdaptivePackage package = GmParlorAdaptiveDirector.Select(
            new GmParlorProfileView(GmParlorAdaptationFixtures.FrozenReceipts()), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        Assert.That(package.CanonicalHash, Has.Length.EqualTo(32));
        Assert.That(package.CanonicalHash, Is.EqualTo(
            GmParlorAdaptiveDirector.Select(
                new GmParlorProfileView(GmParlorAdaptationFixtures.FrozenReceipts()), 117,
                GmParlorAdaptiveMode.Mirror, 1).CanonicalHash));

        var first = new GmParlorMatch(117, 3, 0, true, package);
        var second = new GmParlorMatch(117, 3, 0, true, package);
        Assert.That(first.Start(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(second.Start(), Is.EqualTo(GmParlorActionError.None));
        int guard = 320;
        while (first.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            Assert.That(first.ReplayHash, Is.EqualTo(second.ReplayHash));
            StepSamePublicAction(first, second);
        }
        Assert.That(guard, Is.GreaterThan(0));
        Assert.That(first.ReplayHash, Is.EqualTo(second.ReplayHash));

        GmParlorMatchSnapshot snapshot = first.ExportSnapshot();
        byte[] original = snapshot.ReplayHash;
        GmParlorMatchSnapshot changedPackage = snapshot.DeepCopy();
        changedPackage.adaptivePackage.fallbackReason += "-changed";
        Assert.That(changedPackage.ReplayHash, Is.Not.EqualTo(original));
        GmParlorMatchSnapshot changedBehavior = snapshot.DeepCopy();
        changedBehavior.behaviorAccumulator.eventSequence++;
        Assert.That(changedBehavior.ReplayHash, Is.Not.EqualTo(original));
        GmParlorMatchSnapshot changedAction = snapshot.DeepCopy();
        changedAction.sanity--;
        Assert.That(changedAction.ReplayHash, Is.Not.EqualTo(original));
    }

    [Test]
    public void DifferentQualifyingHistoriesWithSameSeedSelectMeasurablyDifferentStrategies()
    {
        const int seed = 23019;
        GmParlorAdaptivePackage accuser = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.FalseAccuser), seed,
            GmParlorAdaptiveMode.Mirror, 1);
        GmParlorAdaptivePackage specialist = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.SuitSpecialistFlames), seed,
            GmParlorAdaptiveMode.Mirror, 1);

        Assert.That(accuser.PrimaryCounterPlanId,
            Is.EqualTo(GmParlorCounterPlanId.CourtesyFeint));
        Assert.That(specialist.PrimaryCounterPlanId,
            Is.EqualTo(GmParlorCounterPlanId.EmberLock));
        Assert.That(accuser.HonestStrategyId, Is.Not.EqualTo(specialist.HonestStrategyId));

        var witness = new List<GmCard>
        {
            new GmCard(GmSuit.Flames, 3),
            new GmCard(GmSuit.Eyes, 1),
            new GmCard(GmSuit.Eyes, 7),
            new GmCard(GmSuit.Bones, 4),
        };
        int baselineLead = GmParlorCore.ChooseAldricLead(witness,
            GmParlorHonestStrategyId.MeasuredCourtesy);
        int specialistLead = GmParlorCore.ChooseAldricLead(witness,
            specialist.HonestStrategyId);
        Assert.That(witness[baselineLead], Is.Not.EqualTo(witness[specialistLead]));
    }

    [Test]
    public void SuitSpecialistCatalogMapsBonesAndTeethByNamedSuitNotEnumArithmetic()
    {
        var cases = new[]
        {
            new { Suit = GmSuit.Bones, Tendency = GmParlorTendency.SuitSpecialistBones,
                Strategy = GmParlorHonestStrategyId.PreferredSuitReserveBones },
            new { Suit = GmSuit.Teeth, Tendency = GmParlorTendency.SuitSpecialistTeeth,
                Strategy = GmParlorHonestStrategyId.PreferredSuitReserveTeeth },
        };
        foreach (var item in cases)
        {
            GmParlorCompletedRunReceipt receipt =
                GmParlorAdaptationFixtures.CreateReceipt(item.Tendency, 1);
            receipt.summary.playerLeadCountBySuit = new[] { 1, 1, 1, 1 };
            receipt.summary.playerLeadCountBySuit[(int)item.Suit] = 6;
            receipt.summary.playerPlayedCountBySuit =
                (int[])receipt.summary.playerLeadCountBySuit.Clone();
            GmParlorAdaptivePackage package = GmParlorAdaptiveDirector.Select(
                new GmParlorProfileView(new[] { receipt }), 117,
                GmParlorAdaptiveMode.Mirror, 1);
            Assert.That(package.TargetTendency, Is.EqualTo(item.Tendency), item.Suit.ToString());
            Assert.That(package.HonestStrategyId, Is.EqualTo(item.Strategy), item.Suit.ToString());
        }


        Assert.That(GmParlorCore.ChooseAldricLead(new[]
        {
            new GmCard(GmSuit.Bones, 1), new GmCard(GmSuit.Teeth, 7),
        }, GmParlorHonestStrategyId.PreferredSuitReserveBones), Is.EqualTo(1));
        Assert.That(GmParlorCore.ChooseAldricLead(new[]
        {
            new GmCard(GmSuit.Teeth, 1), new GmCard(GmSuit.Bones, 7),
        }, GmParlorHonestStrategyId.PreferredSuitReserveTeeth), Is.EqualTo(1));
    }

    [Test]
    public void ReceiptInsertionOrderCannotChangeSelectionOrMaterializedDigest()
    {
        List<GmParlorCompletedRunReceipt> receipts = FixtureReceipts(
            GmParlorTendency.ReadHungry);
        var forward = new GmParlorProfileView(receipts);
        receipts.Reverse();
        var reverse = new GmParlorProfileView(receipts);

        GmParlorAdaptivePackage first = GmParlorAdaptiveDirector.Select(
            forward, 77, GmParlorAdaptiveMode.Mirror, 1);
        GmParlorAdaptivePackage second = GmParlorAdaptiveDirector.Select(
            reverse, 77, GmParlorAdaptiveMode.Mirror, 1);

        Assert.That(forward.HistoryDigest, Is.EqualTo(reverse.HistoryDigest));
        Assert.That(first.ToCanonicalBytes(), Is.EqualTo(second.ToCanonicalBytes()));
    }

    [Test]
    public void LegacySnapshotMigratesToNamedBaselineWithoutConsultingProfile()
    {
        var original = new GmParlorMatch(91, 2, 0, true);
        original.Start();
        GmParlorMatchSnapshot legacy = original.ExportSnapshot();
        legacy.version = 2;
        legacy.adaptivePackage = null;
        legacy.behaviorAccumulator = null;

        Assert.That(GmParlorMatch.TryRestore(legacy, out GmParlorMatch restored,
            out string error), Is.True, error);
        Assert.That(restored.AdaptivePackage.PrimaryCounterPlanId,
            Is.EqualTo(GmParlorCounterPlanId.LegacyBaseline));
        Assert.That(restored.AdaptivePackage.FallbackReason,
            Is.EqualTo("snapshot-v2-no-adaptation"));
    }

    [Test]
    public void UnknownAdaptationSchemaAndContradictorySummaryFailClosed()
    {
        var match = new GmParlorMatch(114, 3, 0, true);
        match.Start();
        GmParlorMatchSnapshot newer = match.ExportSnapshot();
        newer.adaptivePackage.schemaVersion = GmParlorAdaptivePackage.CurrentSchemaVersion + 1;
        Assert.That(GmParlorMatch.TryRestore(newer, out _, out string schemaError), Is.False);
        Assert.That(schemaError, Does.Contain("adaptive package schema"));

        GmParlorMatchSnapshot contradictory = match.ExportSnapshot();
        contradictory.behaviorAccumulator.readAttempts = 1;
        contradictory.behaviorAccumulator.correctReads = 1;
        contradictory.behaviorAccumulator.judgementOpportunities = 0;
        Assert.That(GmParlorMatch.TryRestore(contradictory, out _, out string historyError), Is.False);
        Assert.That(historyError, Does.Contain("behavior accumulator"));
    }

    [Test]
    public void SnapshotPhaseAndBehaviorSealStateAreCrossBound()
    {
        var midMatch = new GmParlorMatch(401, 2, 0, true);
        midMatch.Start();
        var terminal = new GmParlorMatch(401, 2, 0, true);
        terminal.Start();
        DriveToMatchResult(terminal, alwaysRead: false);

        GmParlorMatchSnapshot sealedMidMatch = midMatch.ExportSnapshot();
        sealedMidMatch.behaviorAccumulator = terminal.BehaviorAccumulator;
        Assert.That(GmParlorMatch.TryRestore(sealedMidMatch, out _, out string sealedError),
            Is.False);
        Assert.That(sealedError, Does.Contain("seal state"));

        GmParlorMatchSnapshot unsealedTerminal = terminal.ExportSnapshot();
        unsealedTerminal.behaviorAccumulator = new GmParlorBehaviorAccumulator(
            terminal.AdaptivePackage);
        Assert.That(GmParlorMatch.TryRestore(unsealedTerminal, out _,
            out string terminalError), Is.False);
        Assert.That(terminalError, Does.Contain("seal state"));
    }

    [Test]
    public void BehaviorCursorAndFirstReadOrdinalRejectOverflowOrInternalDrift()
    {
        var package = GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary);
        var behavior = new GmParlorBehaviorAccumulator(package);
        behavior.RecordRead(GmTellObservation.Calm,
            GmParlorOutcomeKind.FalseReadPenalty, judgementOrdinal: 1);
        behavior.firstReadOpportunityOrdinal = 2;
        Assert.That(behavior.TryValidate(out string ordinalError), Is.False);
        Assert.That(ordinalError, Does.Contain("first Read"));

        var match = new GmParlorMatch(402, 2, 0, true);
        GmParlorMatchSnapshot overflow = match.ExportSnapshot();
        overflow.behaviorAccumulator.eventSequence = ulong.MaxValue;
        Assert.That(GmParlorMatch.TryRestore(overflow, out _, out string cursorError),
            Is.False);
        Assert.That(cursorError, Does.Contain("event sequence"));

        var exhausted = new GmParlorBehaviorAccumulator(package)
        {
            eventSequence = GmParlorBehaviorAccumulator.MaxEventSequence,
        };
        byte[] before = exhausted.ToCanonicalBytes();
        Assert.Throws<InvalidOperationException>(() =>
            exhausted.RecordPlayerFollow(new GmCard(GmSuit.Flames, 1)));
        Assert.That(exhausted.ToCanonicalBytes(), Is.EqualTo(before),
            "an exhausted durable cursor must reject before mutating behavior");
    }

    [Test]
    public void PublicProofStateIsTheCompleteCanonicalReplayPayload()
    {
        var match = new GmParlorMatch(403, 2, 0, true);
        match.Start();
        Assert.That(Convert.FromBase64String(match.PublicStateBytes),
            Is.EqualTo(match.ExportSnapshot().ToCanonicalBytes()));
    }

    [Test]
    public void RestoreRejectsAValidPackageSplicedOntoAnotherPackagesSealedBehavior()
    {
        GmParlorAdaptivePackage readPackage = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.ReadHungry), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        GmParlorAdaptivePackage suitPackage = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.SuitSpecialistFlames), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        var match = new GmParlorMatch(117, 3, 0, true, readPackage);
        match.Start();
        DriveToMatchResult(match, alwaysRead: false);

        GmParlorMatchSnapshot splice = match.ExportSnapshot();
        splice.adaptivePackage = suitPackage.DeepCopy();
        Assert.That(splice.adaptivePackage.TryValidate(out string packageError),
            Is.True, packageError);
        Assert.That(splice.behaviorAccumulator.TryValidate(out string behaviorError),
            Is.True, behaviorError);
        Assert.That(GmParlorMatch.TryRestore(splice, out _, out string error), Is.False);
        Assert.That(error, Does.Contain("adaptive package binding"));
    }

    [Test]
    public void PackageBindingExistsBeforeTheFirstActionAndRejectsEveryPreSealSplice()
    {
        GmParlorAdaptivePackage readPackage = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.ReadHungry), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        GmParlorAdaptivePackage suitPackage = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.SuitSpecialistFlames), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        var match = new GmParlorMatch(117, 3, 0, true, readPackage);

        GmParlorMatchSnapshot notStarted = match.ExportSnapshot();
        Assert.That(notStarted.behaviorAccumulator.boundPackageHash,
            Is.EqualTo(readPackage.CanonicalHash));
        notStarted.adaptivePackage = suitPackage.DeepCopy();
        Assert.That(GmParlorMatch.TryRestore(notStarted, out _, out string beforeError),
            Is.False);
        Assert.That(beforeError, Does.Contain("adaptive package binding"));

        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot midMatch = match.ExportSnapshot();
        Assert.That(midMatch.behaviorAccumulator.currentMatchSealed, Is.False);
        midMatch.adaptivePackage = suitPackage.DeepCopy();
        Assert.That(GmParlorMatch.TryRestore(midMatch, out _, out string midError), Is.False);
        Assert.That(midError, Does.Contain("adaptive package binding"));
    }

    [Test]
    public void PackageCatalogRejectsSemanticallyImpossibleStrategyPlanTargetAndTellSplices()
    {
        GmParlorAdaptivePackage valid = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.ReadHungry), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        Assert.That(valid.TryValidate(out string validError), Is.True, validError);

        GmParlorAdaptivePackage wrongTarget = valid.DeepCopy();
        wrongTarget.targetTendency = GmParlorTendency.SuitSpecialistFlames;
        Assert.That(wrongTarget.TryValidate(out _), Is.False);
        GmParlorAdaptivePackage wrongStrategy = valid.DeepCopy();
        wrongStrategy.honestStrategyId = GmParlorHonestStrategyId.MeasuredCourtesy;
        Assert.That(wrongStrategy.TryValidate(out _), Is.False);
        GmParlorAdaptivePackage wrongTell = valid.DeepCopy();
        wrongTell.tellFamilyId = GmParlorTellFamilyId.StillHands;
        Assert.That(wrongTell.TryValidate(out _), Is.False);
        GmParlorAdaptivePackage orphanSecondary = valid.DeepCopy();
        orphanSecondary.secondaryPresentationPlanId =
            GmParlorPresentationPlanId.StillHandsGesture;
        Assert.That(orphanSecondary.TryValidate(out _), Is.False);
    }

    [Test]
    public void ModeAndProvenanceCatalogRejectsEveryHistoryAndModePermutation()
    {
        GmParlorAdaptivePackage ordinary = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        Assert.That(ordinary.provenance,
            Is.EqualTo(GmParlorPackageProvenance.OrdinaryBaseline));
        Assert.That(ordinary.TryValidate(out string ordinaryError), Is.True, ordinaryError);

        GmParlorAdaptivePackage ordinaryHistory = ordinary.DeepCopy();
        ordinaryHistory.historyDigest[0] = 1;
        Assert.That(ordinaryHistory.TryValidate(out _), Is.False);
        GmParlorAdaptivePackage ordinaryTier = ordinary.DeepCopy();
        ordinaryTier.attentionTier = 1;
        Assert.That(ordinaryTier.TryValidate(out _), Is.False);
        GmParlorAdaptivePackage ordinaryFallback = ordinary.DeepCopy();
        ordinaryFallback.fallbackReason = "not-clean";
        Assert.That(ordinaryFallback.TryValidate(out _), Is.False);

        GmParlorAdaptivePackage learned = GmParlorAdaptiveDirector.Select(
            FixtureProfile(GmParlorTendency.ReadHungry), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        Assert.That(learned.provenance,
            Is.EqualTo(GmParlorPackageProvenance.MirrorDirector));
        GmParlorAdaptivePackage modeFlip = learned.DeepCopy();
        modeFlip.mode = GmParlorAdaptiveMode.Ordinary;
        Assert.That(modeFlip.TryValidate(out _), Is.False);
        modeFlip.mode = GmParlorAdaptiveMode.Recollection;
        Assert.That(modeFlip.TryValidate(out _), Is.False);

        GmParlorAdaptivePackage mirrorWithoutReceipt =
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Mirror);
        mirrorWithoutReceipt.historyDigest[0] = 1;
        Assert.That(mirrorWithoutReceipt.TryValidate(out _), Is.False);

        GmParlorAdaptivePackage recollection =
            GmParlorAdaptiveDirector.SelectKnownForRecollection(learned);
        Assert.That(recollection.provenance,
            Is.EqualTo(GmParlorPackageProvenance.RecollectionKnown));
        Assert.That(recollection.TryValidate(out string recollectionError),
            Is.True, recollectionError);
        Assert.That(recollection.CanTeachProfile, Is.False);
        Assert.Throws<ArgumentException>(() =>
            GmParlorAdaptiveDirector.SelectKnownForRecollection(ordinary));

        GmParlorAdaptivePackage legacy = GmParlorAdaptivePackage.LegacyBaseline();
        Assert.That(legacy.provenance,
            Is.EqualTo(GmParlorPackageProvenance.LegacyMigration));
        Assert.That(legacy.TryValidate(out string legacyError), Is.True, legacyError);
        GmParlorAdaptivePackage corruptLegacy = legacy.DeepCopy();
        corruptLegacy.historyDigest[0] = 1;
        Assert.That(corruptLegacy.TryValidate(out _), Is.False);
        corruptLegacy = legacy.DeepCopy();
        corruptLegacy.fallbackReason = string.Empty;
        Assert.That(corruptLegacy.TryValidate(out _), Is.False);
    }

    [Test]
    public void ExistingCheatAndTellProbabilityContractsRemainFrozenAcrossEveryStrategy()
    {
        foreach (GmParlorHonestStrategyId strategy in Enum.GetValues(typeof(GmParlorHonestStrategyId)))
        {
            Assert.That(GmParlorMatch.PreReadCheatProbability, Is.EqualTo(0.55d), strategy.ToString());
            Assert.That(GmParlorMatch.GetPostReadCheatProbability(1), Is.EqualTo(0.28d));
            Assert.That(GmParlorMatch.GetPostReadCheatProbability(2), Is.EqualTo(0.50d));
            Assert.That(GmParlorMatch.GetPostReadCheatProbability(3), Is.EqualTo(0.68d));
            Assert.That(GmParlorMatch.GetPostReadCheatProbability(4), Is.EqualTo(0.82d));
            Assert.That(GmParlorMatch.GetTellReliability(1), Is.EqualTo(0.58d));
            Assert.That(GmParlorMatch.GetTellReliability(2), Is.EqualTo(0.70d));
            Assert.That(GmParlorMatch.GetTellReliability(3), Is.EqualTo(0.84d));
            Assert.That(GmParlorMatch.GetTellReliability(4), Is.EqualTo(0.95d));
        }
    }

    [Test]
    public void FrozenFiveRunMatrixSelectsExactAttentionStrategiesAndPresentation()
    {
        List<GmParlorCompletedRunReceipt> receipts = GmParlorAdaptationFixtures.FrozenReceipts();
        var expected = new[]
        {
            new { Tier = 0, Plan = GmParlorCounterPlanId.None,
                Target = GmParlorTendency.None,
                Strategy = GmParlorHonestStrategyId.MeasuredCourtesy,
                Tell = GmParlorTellFamilyId.TraditionalCuff },
            new { Tier = 1, Plan = GmParlorCounterPlanId.CourtesyFeint,
                Target = GmParlorTendency.ReadHungry,
                Strategy = GmParlorHonestStrategyId.TrumpReserve,
                Tell = GmParlorTellFamilyId.PolitePause },
            new { Tier = 2, Plan = GmParlorCounterPlanId.StillHands,
                Target = GmParlorTendency.SuspiciousAcceptor,
                Strategy = GmParlorHonestStrategyId.MeasuredCourtesy,
                Tell = GmParlorTellFamilyId.StillHands },
            new { Tier = 2, Plan = GmParlorCounterPlanId.EmberLock,
                Target = GmParlorTendency.SuitSpecialistFlames,
                Strategy = GmParlorHonestStrategyId.PreferredSuitReserveFlames,
                Tell = GmParlorTellFamilyId.TraditionalCuff },
            new { Tier = 3, Plan = GmParlorCounterPlanId.ConservationLedger,
                Target = GmParlorTendency.PressurePlayer,
                Strategy = GmParlorHonestStrategyId.CounterConservation,
                Tell = GmParlorTellFamilyId.GazeAndCardContact },
        };

        for (int count = 0; count <= 4; count++)
        {
            var profile = new GmParlorProfileView(receipts.Take(count));
            GmParlorAdaptivePackage package = GmParlorAdaptiveDirector.Select(
                profile, 117, GmParlorAdaptiveMode.Mirror, 1);
            Assert.That(package.AttentionTier, Is.EqualTo(expected[count].Tier), $"H{count}");
            Assert.That(package.PrimaryCounterPlanId, Is.EqualTo(expected[count].Plan), $"H{count}");
            Assert.That(package.TargetTendency, Is.EqualTo(expected[count].Target), $"H{count}");
            Assert.That(package.HonestStrategyId, Is.EqualTo(expected[count].Strategy), $"H{count}");
            Assert.That(package.TellFamilyId, Is.EqualTo(expected[count].Tell), $"H{count}");
            for (int repeat = 0; repeat < 100; repeat++)
                Assert.That(GmParlorAdaptiveDirector.Select(profile, 117,
                    GmParlorAdaptiveMode.Mirror, 1).ToCanonicalBytes(),
                    Is.EqualTo(package.ToCanonicalBytes()), $"H{count} repeat {repeat}");
        }

        GmParlorAdaptivePackage h4 = GmParlorAdaptiveDirector.Select(
            new GmParlorProfileView(receipts.Take(4)), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        Assert.That(h4.SecondaryPresentationPlanId,
            Is.EqualTo(GmParlorPresentationPlanId.StillHandsGesture));
        Assert.That(h4.SecondaryTendency, Is.EqualTo(GmParlorTendency.GestureSpecialist));
        Assert.That(h4.MemoryTokenId, Is.EqualTo(GmParlorMemoryTokenId.TheReturnedCard));
    }

    [Test]
    public void SeedCannotSelectTargetAndThresholdMutationsBreakTheirClaimedCounter()
    {
        List<GmParlorCompletedRunReceipt> receipts = GmParlorAdaptationFixtures.FrozenReceipts();
        var h4 = new GmParlorProfileView(receipts.Take(4));
        GmParlorAdaptivePackage reference = GmParlorAdaptiveDirector.Select(
            h4, 117, GmParlorAdaptiveMode.Mirror, 1);
        for (int seed = -20; seed <= 20; seed++)
        {
            GmParlorAdaptivePackage candidate = GmParlorAdaptiveDirector.Select(
                h4, seed, GmParlorAdaptiveMode.Mirror, 1);
            Assert.That(candidate.TargetTendency, Is.EqualTo(reference.TargetTendency));
            Assert.That(candidate.PrimaryCounterPlanId,
                Is.EqualTo(reference.PrimaryCounterPlanId));
        }

        List<GmParlorCompletedRunReceipt> weakenedRead =
            GmParlorAdaptationFixtures.FrozenReceipts();
        GmParlorCompletedMatchSummary r1 = weakenedRead[0].summary;
        r1.readAttempts = 5;
        r1.correctReads = 4;
        r1.falseReads = 1;
        r1.acceptedCalm = 5;
        r1.readFirstThird = 3;
        r1.readMiddleThird = 1;
        r1.readFinalThird = 1;
        r1.firstReadMatchReadFirstThird = 3;
        r1.firstReadMatchReadMiddleThird = 1;
        r1.firstReadMatchReadFinalThird = 1;
        Assert.That(GmParlorAdaptiveDirector.Select(new GmParlorProfileView(weakenedRead.Take(1)),
            117, GmParlorAdaptiveMode.Mirror, 1).PrimaryCounterPlanId,
            Is.Not.EqualTo(GmParlorCounterPlanId.CourtesyFeint));

        List<GmParlorCompletedRunReceipt> weakenedPressure =
            GmParlorAdaptationFixtures.FrozenReceipts();
        weakenedPressure[3].summary.pressureLeads = 3;
        weakenedPressure[3].summary.earlyHighRankSpends = 3;
        Assert.That(GmParlorAdaptiveDirector.Select(new GmParlorProfileView(
            weakenedPressure.Take(4)), 117, GmParlorAdaptiveMode.Mirror, 1)
            .PrimaryCounterPlanId,
            Is.Not.EqualTo(GmParlorCounterPlanId.ConservationLedger));
    }

    [Test]
    public void ProfileRejectsDuplicateOrMalformedReceiptsAndOwnsMutableInputs()
    {
        GmParlorCompletedRunReceipt source =
            GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.ReadHungry, 1);
        var profile = new GmParlorProfileView(new[] { source });
        byte[] before = profile.HistoryDigest;
        source.summary.readAttempts = 0;
        source.summary.correctReads = 0;
        source.summary.falseReads = 0;
        Assert.That(profile.HistoryDigest, Is.EqualTo(before));

        IReadOnlyList<GmParlorCompletedRunReceipt> exposed = profile.Receipts;
        exposed[0].summary.readAttempts = 0;
        Assert.That(profile.Receipts[0].summary.readAttempts, Is.GreaterThan(0));

        GmParlorCompletedRunReceipt first =
            GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.ReadHungry, 2);
        GmParlorCompletedRunReceipt duplicate =
            GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.ReadHungry, 2);
        duplicate.receiptId = first.receiptId;
        Assert.Throws<ArgumentException>(() =>
            new GmParlorProfileView(new[] { first, duplicate }));

        GmParlorCompletedRunReceipt malformed =
            GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.ReadHungry, 3);
        malformed.summary.falseReads++;
        Assert.Throws<ArgumentException>(() =>
            new GmParlorProfileView(new[] { malformed }));
    }

    [Test]
    public void ReceiptValidationRejectsEverySteeringRelevantCorruptionClass()
    {
        AssertReceiptRejected(summary => summary.correctReads++);
        AssertReceiptRejected(summary => summary.acceptedSuspicious =
            summary.suspiciousObservations + 1);
        AssertReceiptRejected(summary => summary.acceptedCalm =
            summary.judgementOpportunities - summary.suspiciousObservations + 1);
        AssertReceiptRejected(summary =>
            summary.suspiciousObservations = summary.judgementOpportunities + 1);
        AssertReceiptRejected(summary =>
            summary.playerPlayedCountBySuit[0] = summary.playerLeadCountBySuit[0] - 1);
        AssertReceiptRejected(summary => summary.pressureLeads =
            summary.playerLeadCountBySuit.Sum() + 1);
        AssertReceiptRejected(summary => summary.earlyHighRankSpends =
            summary.pressureLeads + 1);
        AssertReceiptRejected(summary =>
        {
            summary.completedRematches = GmParlorCompletedMatchSummary.MaxCompletedRematches + 1;
            summary.matchOrdinal = summary.completedRematches + 1;
        });
        AssertReceiptRejected(summary => summary.completedRematches = 1);
        AssertReceiptRejected(summary => summary.matchOrdinal = 0);
        AssertReceiptRejected(summary => summary.packageHash = null);
        AssertReceiptRejected(summary => summary.packageHash = new byte[31]);
        AssertReceiptRejected(summary => summary.packageHash = new byte[32]);
        AssertReceiptRejected(summary => summary.packageId = (GmParlorCounterPlanId)999);
        AssertReceiptRejected(summary =>
        {
            summary.packageId = GmParlorCounterPlanId.EmberLock;
            summary.packageTargetTendency = GmParlorTendency.ReadHungry;
        });
    }

    [Test]
    public void FrozenPackageOwnsItsDigestAndCooldownCannotTargetSameTendencyThreeTimes()
    {
        List<GmParlorCompletedRunReceipt> receipts = new List<GmParlorCompletedRunReceipt>();
        for (int ordinal = 1; ordinal <= 3; ordinal++)
        {
            GmParlorCompletedRunReceipt receipt =
                GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.ReadHungry, ordinal);
            if (ordinal >= 2)
                GmParlorAdaptationFixtures.StampPackageTarget(
                    receipt.summary, GmParlorTendency.ReadHungry);
            receipts.Add(receipt);
        }
        GmParlorAdaptivePackage selected = GmParlorAdaptiveDirector.Select(
            new GmParlorProfileView(receipts), 912, GmParlorAdaptiveMode.Mirror, 1);
        Assert.That(selected.TargetTendency, Is.Not.EqualTo(GmParlorTendency.ReadHungry));

        byte[] callerDigest = selected.historyDigest;
        var match = new GmParlorMatch(912, 2, 0, true, adaptivePackage: selected);
        callerDigest[0] ^= 0xff;
        selected.honestStrategyId = GmParlorHonestStrategyId.SecondDealHigh;
        Assert.That(match.AdaptivePackage.historyDigest[0], Is.Not.EqualTo(callerDigest[0]));
        Assert.That(match.AdaptivePackage.HonestStrategyId,
            Is.Not.EqualTo(GmParlorHonestStrategyId.SecondDealHigh));
    }

    [Test]
    public void PresentationOnlyCounterChangesTellTraceWithoutChangingRulesOrTruth()
    {
        List<GmParlorCompletedRunReceipt> receipts = GmParlorAdaptationFixtures.FrozenReceipts();
        GmParlorAdaptivePackage stillHands = GmParlorAdaptiveDirector.Select(
            new GmParlorProfileView(receipts.Take(2)), 117,
            GmParlorAdaptiveMode.Mirror, 1);
        GmParlorAdaptivePackage baseline = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        var ordinary = new GmParlorMatch(117, 3, 0, true, adaptivePackage: baseline);
        var adapted = new GmParlorMatch(117, 3, 0, true, adaptivePackage: stillHands);
        ordinary.Start();
        adapted.Start();
        Assert.That(adapted.PlayerHand, Is.EqualTo(ordinary.PlayerHand));
        Assert.That(adapted.AldricHand, Is.EqualTo(ordinary.AldricHand));
        Assert.That(adapted.PlayPlayerCard(0), Is.EqualTo(ordinary.PlayPlayerCard(0)));
        Assert.That(adapted.RandomState, Is.EqualTo(ordinary.RandomState));
        Assert.That(adapted.CurrentFollowCard, Is.EqualTo(ordinary.CurrentFollowCard));
        Assert.That(adapted.AldricCheated, Is.EqualTo(ordinary.AldricCheated));
        Assert.That(adapted.TellObservation, Is.EqualTo(ordinary.TellObservation));
        Assert.That(adapted.AdaptivePackage.TellFamilyId,
            Is.Not.EqualTo(ordinary.AdaptivePackage.TellFamilyId));
    }

    [Test]
    public void EveryAdaptiveStrategyKeepsHonestMovesLegalAndCheatsOnlyWithoutAWinner()
    {
        foreach (GmParlorHonestStrategyId strategy in Enum.GetValues(
            typeof(GmParlorHonestStrategyId)))
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                GmParlorAdaptivePackage package = PackageForStrategy(strategy);
                var match = new GmParlorMatch(seed, 4, 0, true,
                    adaptivePackage: package);
                match.Start();
                for (int leadIndex = 0; leadIndex < match.PlayerHand.Count; leadIndex++)
                {
                    GmCard lead = match.PlayerHand[leadIndex];
                    List<GmCard> before = match.AldricHand.ToList();
                    bool hadWinner = before.Any(card => GmParlorCore.IsLegal(before, lead, card) &&
                        !GmParlorCore.LeadWins(lead, card));
                    if (match.GetPlayerCardError(leadIndex) != GmParlorActionError.None) continue;
                    match.PlayPlayerCard(leadIndex);
                    if (match.AldricCheated)
                        Assert.That(hadWinner, Is.False, $"{strategy}, seed {seed}");
                    else
                    {
                        Assert.That(before.Contains(match.CurrentFollowCard.Value), Is.True);
                        Assert.That(GmParlorCore.IsLegal(before, lead,
                            match.CurrentFollowCard.Value), Is.True, $"{strategy}, seed {seed}");
                    }
                    break;
                }
            }
        }
    }

    [Test]
    public void BehaviorCursorAndSealedSummarySurviveExactSnapshotRestore()
    {
        var match = new GmParlorMatch(334, 2, 0, true);
        match.Start();
        DriveToMatchResult(match, alwaysRead: false);
        Assert.That(match.BehaviorAccumulator.currentMatchSealed, Is.True);
        Assert.That(match.BehaviorAccumulator.sealedSummary.TryValidate(out string summaryError),
            Is.True, summaryError);
        ulong cursor = match.BehaviorAccumulator.EventSequence;

        Assert.That(GmParlorMatch.TryRestore(match.ExportSnapshot(),
            out GmParlorMatch restored, out string error), Is.True, error);
        Assert.That(restored.BehaviorAccumulator.EventSequence, Is.EqualTo(cursor));
        Assert.That(restored.BehaviorAccumulator.sealedSummary.ToCanonicalBytes(),
            Is.EqualTo(match.BehaviorAccumulator.sealedSummary.ToCanonicalBytes()));
        Assert.That(restored.PublicStateBytes, Is.EqualTo(match.PublicStateBytes));
    }

    [Test]
    public void RematchesRetainEverySealedMatchAndProduceOneBoundedRunSummary()
    {
        var match = new GmParlorMatch(4401, 3, 0, true);
        match.Start();
        DriveToMatchResult(match, alwaysRead: false);
        GmParlorCompletedMatchSummary first =
            match.BehaviorAccumulator.sealedSummary.DeepCopy();
        Assert.That(match.StartRematch(), Is.EqualTo(GmParlorActionError.None));
        DriveToMatchResult(match, alwaysRead: false);

        GmParlorBehaviorAccumulator history = match.BehaviorAccumulator;
        Assert.That(history.completedMatchSummaries.Count, Is.EqualTo(2));
        GmParlorCompletedMatchSummary run = history.CreateCompletedRunSummary();
        Assert.That(run.judgementOpportunities, Is.EqualTo(
            history.completedMatchSummaries.Sum(item => item.judgementOpportunities)));
        Assert.That(run.readAttempts, Is.EqualTo(
            history.completedMatchSummaries.Sum(item => item.readAttempts)));
        Assert.That(run.completedRematches, Is.EqualTo(1));
        Assert.That(run.packageId, Is.EqualTo(first.packageId));
        Assert.That(run.TryValidate(out string runError), Is.True, runError);

        Assert.That(GmParlorMatch.TryRestore(match.ExportSnapshot(),
            out GmParlorMatch restored, out string restoreError), Is.True, restoreError);
        Assert.That(restored.BehaviorAccumulator.CreateCompletedRunSummary().ToCanonicalBytes(),
            Is.EqualTo(run.ToCanonicalBytes()));
    }

    [Test]
    public void MutableRunSaveDoesNotPretendToBeTheHouseProfileAuthority()
    {
        string repository = Directory.GetParent(UnityEngine.Application.dataPath).Parent.FullName;
        string store = File.ReadAllText(Path.Combine(repository, "unity", "scene-system",
            "Runtime", "GmRunStore.cs"));
        StringAssert.DoesNotContain("GmParlorCompletedRunReceipt", store);
        StringAssert.DoesNotContain("GmParlorProfileView", store);
        StringAssert.DoesNotContain("houseProfile", store);
    }

    [Test]
    public void FivePlayerArchetypesDivergeAtTheSameSeedWithoutFakeEvidenceTelemetry()
    {
        const int seed = 117;
        var profiles = new[]
        {
            new GmParlorProfileView(Array.Empty<GmParlorCompletedRunReceipt>()),
            new GmParlorProfileView(new[] {
                GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.FalseAccuser, 1) }),
            new GmParlorProfileView(new[] {
                GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.AccuratePatient, 1) }),
            new GmParlorProfileView(new[] {
                GmParlorAdaptationFixtures.CreateReceipt(
                    GmParlorTendency.SuitSpecialistFlames, 1) }),
            new GmParlorProfileView(new[] {
                GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.Rematcher, 1) }),
        };
        GmParlorAdaptivePackage[] packages = profiles.Select(profile =>
            GmParlorAdaptiveDirector.Select(profile, seed,
                GmParlorAdaptiveMode.Mirror, 1)).ToArray();

        Assert.That(packages.Select(item => item.PrimaryCounterPlanId).Distinct().Count(),
            Is.EqualTo(5));
        Assert.That(packages[1].TargetTendency, Is.EqualTo(GmParlorTendency.FalseAccuser));
        Assert.That(packages[2].TargetTendency, Is.EqualTo(GmParlorTendency.AccuratePatient));
        Assert.That(packages[3].TargetTendency,
            Is.EqualTo(GmParlorTendency.SuitSpecialistFlames));
        Assert.That(packages[4].TargetTendency, Is.EqualTo(GmParlorTendency.Rematcher));
        Assert.That(profiles.SelectMany(profile => profile.Receipts)
            .SelectMany(receipt => receipt.summary.committedEvidenceClaimsByFamily), Is.All.Zero,
            "binary Read has no explicit evidence-selection action yet");
    }

    [Test]
    public void OrdinaryIgnoresHistoryAndRecollectionRequiresAnExplicitKnownPackage()
    {
        var profile = new GmParlorProfileView(GmParlorAdaptationFixtures.FrozenReceipts());
        GmParlorAdaptivePackage ordinary = GmParlorAdaptiveDirector.Select(
            profile, 117, GmParlorAdaptiveMode.Ordinary, 1);
        Assert.That(ordinary.PrimaryCounterPlanId, Is.EqualTo(GmParlorCounterPlanId.None));
        Assert.That(ordinary.CanTeachProfile, Is.False);

        Assert.Throws<InvalidOperationException>(() =>
            GmParlorAdaptiveDirector.Select(profile, 117,
                GmParlorAdaptiveMode.Recollection, 1));

        GmParlorAdaptivePackage known = GmParlorAdaptiveDirector.Select(
            profile, 117, GmParlorAdaptiveMode.Mirror, 1);
        GmParlorAdaptivePackage recollection =
            GmParlorAdaptiveDirector.SelectKnownForRecollection(known);
        Assert.That(recollection.Mode, Is.EqualTo(GmParlorAdaptiveMode.Recollection));
        Assert.That(recollection.PrimaryCounterPlanId,
            Is.EqualTo(known.PrimaryCounterPlanId));
        Assert.That(recollection.HonestStrategyId, Is.EqualTo(known.HonestStrategyId));
        Assert.That(recollection.CanTeachProfile, Is.False);
        Assert.That(known.CanTeachProfile, Is.True);
    }

    static GmParlorProfileView FixtureProfile(GmParlorTendency tendency) =>
        new GmParlorProfileView(FixtureReceipts(tendency));

    static List<GmParlorCompletedRunReceipt> FixtureReceipts(GmParlorTendency tendency)
    {
        var result = new List<GmParlorCompletedRunReceipt>();
        for (int ordinal = 1; ordinal <= 5; ordinal++)
            result.Add(GmParlorAdaptationFixtures.CreateReceipt(tendency, ordinal));
        return result;
    }

    static void DriveToMatchResult(GmParlorMatch match, bool alwaysRead)
    {
        int guard = 320;
        while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            if (match.Phase == GmParlorMatchPhase.PlayerLeads ||
                match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
            {
                int index = Enumerable.Range(0, match.PlayerHand.Count)
                    .First(i => match.GetPlayerCardError(i) == GmParlorActionError.None);
                Assert.That(match.PlayPlayerCard(index), Is.EqualTo(GmParlorActionError.None));
            }
            else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            {
                GmParlorActionError action = alwaysRead ? match.Read() : match.ContinueJudgement();
                Assert.That(action, Is.EqualTo(GmParlorActionError.None));
            }
            else
            {
                Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
            }
            if (match.TryPeekOutcome(out _, out ulong sequence))
            {
                match.MarkOutcomeDurable(sequence);
                match.AcknowledgeOutcome(sequence);
            }
        }
        Assert.That(guard, Is.GreaterThan(0));
    }

    static void StepSamePublicAction(GmParlorMatch first, GmParlorMatch second)
    {
        Assert.That(second.Phase, Is.EqualTo(first.Phase));
        if (first.Phase == GmParlorMatchPhase.PlayerLeads ||
            first.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
        {
            int index = Enumerable.Range(0, first.PlayerHand.Count)
                .First(i => first.GetPlayerCardError(i) == GmParlorActionError.None);
            Assert.That(second.GetPlayerCardError(index), Is.EqualTo(GmParlorActionError.None));
            Assert.That(first.PlayPlayerCard(index), Is.EqualTo(second.PlayPlayerCard(index)));
        }
        else if (first.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            Assert.That(first.ContinueJudgement(), Is.EqualTo(second.ContinueJudgement()));
        else
            Assert.That(first.Continue(), Is.EqualTo(second.Continue()));

        if (first.TryPeekOutcome(out _, out ulong firstSequence))
        {
            Assert.That(second.TryPeekOutcome(out _, out ulong secondSequence), Is.True);
            Assert.That(secondSequence, Is.EqualTo(firstSequence));
            Assert.That(first.MarkOutcomeDurable(firstSequence),
                Is.EqualTo(second.MarkOutcomeDurable(secondSequence)));
            Assert.That(first.AcknowledgeOutcome(firstSequence),
                Is.EqualTo(second.AcknowledgeOutcome(secondSequence)));
        }
    }

    static void AssertReceiptRejected(Action<GmParlorCompletedMatchSummary> mutate)
    {
        GmParlorCompletedRunReceipt receipt =
            GmParlorAdaptationFixtures.CreateReceipt(GmParlorTendency.ReadHungry, 40);
        mutate(receipt.summary);
        Assert.Throws<ArgumentException>(() =>
            new GmParlorProfileView(new[] { receipt }));
    }

    static GmParlorAdaptivePackage PackageForStrategy(GmParlorHonestStrategyId strategy)
    {
        var package = GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Mirror);
        if (strategy == GmParlorHonestStrategyId.MeasuredCourtesy) return package;
        package.attentionTier = 1;
        package.selectedFromReceiptOrdinal = 1;
        package.historyDigest[0] = 1;
        package.honestStrategyId = strategy;
        if (strategy == GmParlorHonestStrategyId.TrumpReserve)
        {
            package.primaryCounterPlanId = GmParlorCounterPlanId.CourtesyFeint;
            package.targetTendency = GmParlorTendency.ReadHungry;
            package.tellFamilyId = GmParlorTellFamilyId.PolitePause;
        }
        else if (strategy >= GmParlorHonestStrategyId.PreferredSuitReserveFlames &&
            strategy <= GmParlorHonestStrategyId.PreferredSuitReserveBones)
        {
            package.primaryCounterPlanId = GmParlorCounterPlanId.EmberLock;
            package.targetTendency = strategy ==
                GmParlorHonestStrategyId.PreferredSuitReserveFlames
                    ? GmParlorTendency.SuitSpecialistFlames
                    : strategy == GmParlorHonestStrategyId.PreferredSuitReserveEyes
                        ? GmParlorTendency.SuitSpecialistEyes
                        : strategy == GmParlorHonestStrategyId.PreferredSuitReserveBones
                            ? GmParlorTendency.SuitSpecialistBones
                            : GmParlorTendency.SuitSpecialistTeeth;
        }
        else if (strategy == GmParlorHonestStrategyId.CounterConservation)
        {
            package.primaryCounterPlanId = GmParlorCounterPlanId.ConservationLedger;
            package.targetTendency = GmParlorTendency.PressurePlayer;
            package.tellFamilyId = GmParlorTellFamilyId.GazeAndCardContact;
        }
        else
        {
            package.primaryCounterPlanId = GmParlorCounterPlanId.SecondDeal;
            package.targetTendency = GmParlorTendency.Rematcher;
        }
        Assert.That(package.TryValidate(out string error), Is.True, error);
        return package;
    }

    static string FirstAldricLeadCode(GmParlorMatch match)
    {
        int guard = 80;
        while (match.Phase != GmParlorMatchPhase.PlayerFollowsAldricLead && guard-- > 0)
        {
            if (match.Phase == GmParlorMatchPhase.PlayerLeads)
            {
                int index = Enumerable.Range(0, match.PlayerHand.Count)
                    .First(i => match.GetPlayerCardError(i) == GmParlorActionError.None);
                match.PlayPlayerCard(index);
            }
            else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
                match.ContinueJudgement();
            else match.Continue();
            if (match.TryPeekOutcome(out _, out ulong sequence))
            {
                match.MarkOutcomeDurable(sequence);
                match.AcknowledgeOutcome(sequence);
            }
        }
        Assert.That(guard, Is.GreaterThan(0));
        return match.CurrentLeadCard.Value.ShortName;
    }
}

static class GmParlorAdaptationFixtures
{
    public static void StampPackageTarget(GmParlorCompletedMatchSummary summary,
        GmParlorTendency tendency)
    {
        if (tendency != GmParlorTendency.ReadHungry)
            throw new ArgumentOutOfRangeException(nameof(tendency));
        var package = GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Mirror);
        package.attentionTier = 1;
        package.selectedFromReceiptOrdinal = 1;
        package.historyDigest[0] = 1;
        package.primaryCounterPlanId = GmParlorCounterPlanId.CourtesyFeint;
        package.targetTendency = tendency;
        package.honestStrategyId = GmParlorHonestStrategyId.TrumpReserve;
        package.tellFamilyId = GmParlorTellFamilyId.PolitePause;
        Assert.That(package.TryValidate(out string error), Is.True, error);
        summary.packageId = package.primaryCounterPlanId;
        summary.honestStrategyId = package.honestStrategyId;
        summary.packageTargetTendency = package.targetTendency;
        summary.packageHash = package.CanonicalHash;
    }

    public static List<GmParlorCompletedRunReceipt> FrozenReceipts()
    {
        return new List<GmParlorCompletedRunReceipt>
        {
            Receipt(1, 10, 9, 7, 2, 5, 0, 1, 3, 3, 3,
                new[] { 3, 1, 1, 1 }, 3, 3, 0, new[] { 0, 0, 0 }),
            Receipt(2, 10, 2, 2, 0, 4, 4, 4, 0, 0, 2,
                new[] { 5, 1, 1, 1 }, 5, 2, 0, new[] { 0, 0, 0 }),
            Receipt(3, 10, 3, 3, 0, 4, 1, 6, 2, 1, 0,
                new[] { 7, 1, 0, 0 }, 3, 3, 0, new[] { 1, 1, 1 }),
            Receipt(4, 10, 4, 3, 1, 5, 1, 5, 1, 1, 2,
                new[] { 2, 2, 2, 2 }, 7, 5, 2, new[] { 4, 0, 0 }),
        };
    }

    public static GmParlorCompletedRunReceipt CreateReceipt(GmParlorTendency tendency,
        int ordinal)
    {
        if (tendency == GmParlorTendency.FalseAccuser)
            return Receipt(ordinal, 10, 5, 3, 2, 4, 0, 5, 3, 1, 1,
                new[] { 2, 2, 2, 2 }, 2, 1, 0, new[] { 0, 0, 0 });
        if (tendency == GmParlorTendency.AccuratePatient)
            return Receipt(ordinal, 10, 3, 3, 0, 0, 0, 7, 0, 0, 3,
                new[] { 2, 2, 2, 2 }, 1, 1, 0, new[] { 0, 0, 0 });
        if (tendency == GmParlorTendency.Rematcher)
            return Receipt(ordinal, 10, 2, 2, 0, 0, 0, 8, 1, 0, 1,
                new[] { 2, 2, 2, 2 }, 1, 1, 2, new[] { 0, 0, 0 });
        if (tendency >= GmParlorTendency.SuitSpecialistFlames &&
            tendency <= GmParlorTendency.SuitSpecialistBones)
        {
            int[] suits = { 0, 0, 0, 0 };
            GmSuit suit = tendency == GmParlorTendency.SuitSpecialistFlames ? GmSuit.Flames :
                tendency == GmParlorTendency.SuitSpecialistEyes ? GmSuit.Eyes :
                tendency == GmParlorTendency.SuitSpecialistBones ? GmSuit.Bones : GmSuit.Teeth;
            suits[(int)suit] = 5;
            suits[((int)suit + 1) % 4] = 1;
            return Receipt(ordinal, 10, 2, 2, 0, 2, 1, 7, 0, 1, 1,
                suits, 1, 1, 0, new[] { 0, 0, 0 });
        }
        return Receipt(ordinal, 10, 8, 7, 1, 4, 1, 1, 3, 3, 2,
            new[] { 2, 2, 2, 2 }, 2, 2, 0, new[] { 0, 0, 0 });
    }

    static GmParlorCompletedRunReceipt Receipt(int ordinal, int judgements, int reads,
        int correct, int falseReads, int suspicious, int acceptSuspicious, int acceptCalm,
        int readFirst, int readMiddle, int readFinal, int[] leads, int pressure,
        int earlyHigh, int rematches, int[] claims)
    {
        GmParlorAdaptivePackage package = GmParlorAdaptivePackage.Baseline(
            GmParlorAdaptiveMode.Ordinary);
        var accumulator = new GmParlorBehaviorAccumulator(package);

        var leadCards = new List<GmCard>();
        for (int suit = 0; suit < leads.Length; suit++)
            for (int count = 0; count < leads[suit]; count++)
                leadCards.Add(new GmCard((GmSuit)suit,
                    leadCards.Count < pressure ? 7 : 1));
        for (int index = 0; index < leadCards.Count; index++)
            accumulator.RecordPlayerLead(leadCards[index],
                index < earlyHigh ? 1 : 7, roundTrickCapacity: 7);

        int matchCount = rematches + 1;
        int[] opportunitiesByMatch = new int[matchCount];
        for (int match = 0; match < matchCount; match++)
            opportunitiesByMatch[match] = judgements / matchCount +
                (match < judgements % matchCount ? 1 : 0);
        int remainingFirst = readFirst;
        int remainingMiddle = readMiddle;
        int remainingFinal = readFinal;
        int remainingCorrect = correct;
        int remainingSuspiciousReads = suspicious - acceptSuspicious;
        int remainingSuspiciousAccepts = acceptSuspicious;
        int[] remainingClaims = (int[])claims.Clone();

        for (int match = 0; match < matchCount; match++)
        {
            int opportunities = opportunitiesByMatch[match];
            var readOrdinals = new HashSet<int>();
            int firstCapacity = opportunities / 3;
            int finalStart = opportunities * 2 / 3 + 1;
            int takeFirst = Math.Min(remainingFirst, firstCapacity);
            for (int item = 0; item < takeFirst; item++) readOrdinals.Add(item + 1);
            remainingFirst -= takeFirst;
            int middleCapacity = opportunities - firstCapacity -
                (opportunities - opportunities * 2 / 3);
            int takeMiddle = Math.Min(remainingMiddle, middleCapacity);
            for (int item = 0; item < takeMiddle; item++)
                readOrdinals.Add(firstCapacity + item + 1);
            remainingMiddle -= takeMiddle;
            int finalCapacity = opportunities - opportunities * 2 / 3;
            int takeFinal = Math.Min(remainingFinal, finalCapacity);
            for (int item = 0; item < takeFinal; item++)
                readOrdinals.Add(finalStart + item);
            remainingFinal -= takeFinal;

            int readsThisMatch = 0;
            for (int judgement = 1; judgement <= opportunities; judgement++)
            {
                if (readOrdinals.Contains(judgement))
                {
                    GmTellObservation observation = remainingSuspiciousReads-- > 0
                        ? GmTellObservation.Suspicious : GmTellObservation.Calm;
                    GmParlorOutcomeKind outcome = remainingCorrect-- > 0
                        ? GmParlorOutcomeKind.CheatCaught
                        : GmParlorOutcomeKind.FalseReadPenalty;
                    accumulator.RecordRead(observation, outcome, judgement);
                    readsThisMatch++;
                }
                else
                {
                    GmTellObservation observation = remainingSuspiciousAccepts-- > 0
                        ? GmTellObservation.Suspicious : GmTellObservation.Calm;
                    accumulator.RecordAccept(observation);
                }
            }
            int claimCapacity = readsThisMatch;
            for (int family = 0; family < remainingClaims.Length; family++)
                while (remainingClaims[family] > 0 && claimCapacity > 0)
                {
                    accumulator.RecordCommittedEvidenceClaim(
                        (GmParlorEvidenceFamilyId)family);
                    remainingClaims[family]--;
                    claimCapacity--;
                }
            accumulator.SealCompletedMatch(package, accumulator.matchOrdinal,
                completedRematches: 0);
            if (match + 1 < matchCount) accumulator.BeginRematch();
        }

        Assert.That(remainingFirst, Is.Zero);
        Assert.That(remainingMiddle, Is.Zero);
        Assert.That(remainingFinal, Is.Zero);
        Assert.That(remainingCorrect, Is.EqualTo(-falseReads));
        Assert.That(remainingSuspiciousReads, Is.LessThanOrEqualTo(0));
        Assert.That(remainingSuspiciousAccepts, Is.LessThanOrEqualTo(0));
        Assert.That(remainingClaims, Is.All.Zero);
        GmParlorCompletedMatchSummary summary = accumulator.CreateCompletedRunSummary();
        Assert.That(summary.TryValidate(out string error), Is.True, error);
        Assert.That(summary.judgementOpportunities, Is.EqualTo(judgements));
        Assert.That(summary.readAttempts, Is.EqualTo(reads));
        Assert.That(summary.correctReads, Is.EqualTo(correct));
        Assert.That(summary.falseReads, Is.EqualTo(falseReads));
        Assert.That(summary.suspiciousObservations, Is.EqualTo(suspicious));
        Assert.That(summary.acceptedSuspicious, Is.EqualTo(acceptSuspicious));
        Assert.That(summary.acceptedCalm, Is.EqualTo(acceptCalm));
        Assert.That(summary.readFirstThird, Is.EqualTo(readFirst));
        Assert.That(summary.readMiddleThird, Is.EqualTo(readMiddle));
        Assert.That(summary.readFinalThird, Is.EqualTo(readFinal));
        Assert.That(summary.playerLeadCountBySuit, Is.EqualTo(leads));
        Assert.That(summary.pressureLeads, Is.EqualTo(pressure));
        Assert.That(summary.earlyHighRankSpends, Is.EqualTo(earlyHigh));
        Assert.That(summary.completedRematches, Is.EqualTo(rematches));
        Assert.That(summary.committedEvidenceClaimsByFamily, Is.EqualTo(claims));
        return new GmParlorCompletedRunReceipt
        {
            runOrdinal = ordinal,
            receiptId = "fixture-" + ordinal,
            summary = summary,
        };
    }
}
