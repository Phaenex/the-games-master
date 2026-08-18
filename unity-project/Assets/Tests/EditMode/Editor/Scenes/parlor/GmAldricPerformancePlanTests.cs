using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class GmAldricPerformancePlanTests
{
    [TestCase(GmTellObservation.Calm, false)]
    [TestCase(GmTellObservation.Calm, true)]
    [TestCase(GmTellObservation.Suspicious, false)]
    [TestCase(GmTellObservation.Suspicious, true)]
    public void HonestCheatedByCalmSuspiciousMatrixHasOnlyTwoPublicPreJudgementTraces(
        GmTellObservation observation, bool hiddenScenarioWasCheated)
    {
        // The scenario label exists only in this adversarial test. There is deliberately nowhere to
        // put it in GmAldricPerformanceRequest, so a public twin must produce the identical trace.
        _ = hiddenScenarioWasCheated;
        GmAldricPerformanceRequest scenario = Request(observation, 0);
        GmAldricPerformanceRequest publicTwin = Request(observation, 0);

        GmAldricPerformancePlan candidate = GmAldricPerformancePlanner.Build(scenario);
        GmAldricPerformancePlan twin = GmAldricPerformancePlanner.Build(publicTwin);

        Assert.That(candidate.PreJudgementTrace, Is.EqualTo(twin.PreJudgementTrace));
        Assert.That(candidate.Reaction, Is.EqualTo(GmAldricPublicReaction.None));
        Assert.That(twin.Reaction, Is.EqualTo(GmAldricPublicReaction.None));
    }

    [Test]
    public void PlannerContractCannotReceiveHiddenTruth()
    {
        string[] forbidden = { "cheat", "legality", "pending", "winner", "future" };
        string[] requestSurface = typeof(GmAldricPerformanceRequest)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.Name.ToLowerInvariant())
            .Concat(typeof(GmAldricPerformanceRequest).GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => (parameter.Name + ":" + parameter.ParameterType.Name)
                    .ToLowerInvariant()))
            .ToArray();

        foreach (string word in forbidden)
            Assert.That(requestSurface, Has.None.Contains(word),
                $"performance request leaks hidden input '{word}'");
    }

    [Test]
    public void SuspiciousVariantsAreFrozenByStablePublicInputs()
    {
        GmAldricPerformancePlan first = GmAldricPerformancePlanner.Build(
            Request(GmTellObservation.Suspicious, 0));
        GmAldricPerformancePlan second = GmAldricPerformancePlanner.Build(
            Request(GmTellObservation.Suspicious, 0));

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.PreJudgementTrace.HoverMilliseconds, Is.InRange(180, 260));
        Assert.That(first.PreJudgementTrace.MinimumEvidenceMilliseconds, Is.EqualTo(750));
        Assert.That(first.PreJudgementTrace.Foley, Is.EqualTo(GmAldricAudioIntent.Unavailable));
        Assert.That(first.PreJudgementTrace.Voice, Is.EqualTo(GmAldricAudioIntent.Unavailable));
        Assert.That(first.PreJudgementTrace.Camera, Is.EqualTo(GmAldricCameraIntent.None));
    }

    [TestCase(GmAldricPublicOutcome.CorrectRead, GmAldricPublicReaction.Caught)]
    [TestCase(GmAldricPublicOutcome.FalseAccusation, GmAldricPublicReaction.FalselyAccused)]
    [TestCase(GmAldricPublicOutcome.RoundWon, GmAldricPublicReaction.RoundWin)]
    [TestCase(GmAldricPublicOutcome.RoundLost, GmAldricPublicReaction.RoundLoss)]
    [TestCase(GmAldricPublicOutcome.MatchWon, GmAldricPublicReaction.MatchWin)]
    [TestCase(GmAldricPublicOutcome.MatchLost, GmAldricPublicReaction.MatchLoss)]
    [TestCase(GmAldricPublicOutcome.Rematch, GmAldricPublicReaction.Rematch)]
    public void ReactionAppearsOnlyAfterPublicResolution(
        GmAldricPublicOutcome outcome, GmAldricPublicReaction expected)
    {
        GmAldricPerformancePlan before = GmAldricPerformancePlanner.Build(
            Request(GmTellObservation.Suspicious, GmAldricPublicOutcome.None));
        GmAldricPerformancePlan after = GmAldricPerformancePlanner.Build(
            Request(GmTellObservation.Suspicious, outcome));

        Assert.That(before.Reaction, Is.EqualTo(GmAldricPublicReaction.None));
        Assert.That(after.Reaction, Is.EqualTo(expected));
        Assert.That(after.ReactionStartsAfterPublicOutcome, Is.True);
        Assert.That(after.PreJudgementTrace, Is.EqualTo(before.PreJudgementTrace));
    }

    [Test]
    public void StableRestorePoseIsDeterministicFromPublicInputs()
    {
        GmAldricPerformancePlan one = GmAldricPerformancePlanner.Build(
            new GmAldricPerformanceRequest(GmTellObservation.Calm,
                GmAldricActionRole.Follow, 2, 991UL, GmAldricPublicOutcome.None));
        GmAldricPerformancePlan two = GmAldricPerformancePlanner.Build(
            new GmAldricPerformanceRequest(GmTellObservation.Calm,
                GmAldricActionRole.Follow, 2, 991UL, GmAldricPublicOutcome.None));

        Assert.That(one.PublicRestorePoseId, Is.EqualTo(two.PublicRestorePoseId));
        Assert.That(one.PublicRestorePoseId, Is.Not.Zero);
    }

    [Test]
    public void InvalidPublicEnumsFailClosedAtTheRequestBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GmAldricPerformanceRequest(
            (GmTellObservation)99, GmAldricActionRole.Lead, 0, 1, GmAldricPublicOutcome.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GmAldricPerformanceRequest(
            GmTellObservation.Calm, (GmAldricActionRole)99, 0, 1, GmAldricPublicOutcome.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GmAldricPerformanceRequest(
            GmTellObservation.Calm, GmAldricActionRole.Lead, 0, 1,
            (GmAldricPublicOutcome)99));
    }

    [Test]
    public void DefaultRequestCannotBypassThePlannerBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GmAldricPerformancePlanner.Build(default));
    }

    [Test]
    public void ConstructingAndBuildingPlansAllocatesNoManagedMemoryAfterWarmup()
    {
        int checksum = 0;
        for (int i = 0; i < 32; i++)
        {
            var warm = new GmAldricPerformanceRequest(GmTellObservation.Suspicious,
                GmAldricActionRole.Follow, i, (ulong)(i + 1), GmAldricPublicOutcome.None);
            checksum += GmAldricPerformancePlanner.Build(warm).PublicRestorePoseId;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            var request = new GmAldricPerformanceRequest(GmTellObservation.Suspicious,
                GmAldricActionRole.Follow, i, (ulong)(i + 1), GmAldricPublicOutcome.None);
            checksum += GmAldricPerformancePlanner.Build(request).PublicRestorePoseId;
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(checksum, Is.GreaterThan(0));
        Assert.That(after - before, Is.Zero);
    }

    [TestCase(GmAldricActionRole.Lead, GmAldricIkIntent.RightHandToLeadCard)]
    [TestCase(GmAldricActionRole.Follow, GmAldricIkIntent.RightHandToFollowCard)]
    [TestCase(GmAldricActionRole.Judgement, GmAldricIkIntent.RightHandToJudgementCard)]
    public void EveryPublicRoleMapsToItsOwnIkTarget(GmAldricActionRole role,
        GmAldricIkIntent expected)
    {
        GmAldricPerformancePlan plan = GmAldricPerformancePlanner.Build(
            new GmAldricPerformanceRequest(GmTellObservation.Calm, role, 0, 41,
                GmAldricPublicOutcome.None));
        Assert.That(plan.PreJudgementTrace.Ik, Is.EqualTo(expected));
    }

    [Test]
    public void AllThreeSuspiciousVariantsAreReachableAndFrozen()
    {
        var variants = new System.Collections.Generic.HashSet<int>();
        for (ulong commandId = 1; commandId <= 128; commandId++)
        {
            GmAldricPerformancePlan first = GmAldricPerformancePlanner.Build(
                new GmAldricPerformanceRequest(GmTellObservation.Suspicious,
                    GmAldricActionRole.Follow, 2, commandId, GmAldricPublicOutcome.None));
            GmAldricPerformancePlan second = GmAldricPerformancePlanner.Build(
                new GmAldricPerformanceRequest(GmTellObservation.Suspicious,
                    GmAldricActionRole.Follow, 2, commandId, GmAldricPublicOutcome.None));
            Assert.That(first.PreJudgementTrace.Variant,
                Is.EqualTo(second.PreJudgementTrace.Variant));
            variants.Add(first.PreJudgementTrace.Variant);
        }
        Assert.That(variants, Is.EquivalentTo(new[] { 0, 1, 2 }));
    }

    static GmAldricPerformanceRequest Request(GmTellObservation observation,
        GmAldricPublicOutcome outcome)
    {
        return new GmAldricPerformanceRequest(observation, GmAldricActionRole.Follow,
            trickOrdinal: 3, stableCommandId: 0xA11D_71C0UL, outcome);
    }
}
