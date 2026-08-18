using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmParlorSaveRestoreTests
{
    string savePath;
    string directory;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-parlor-save-" + System.Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(directory, "save.json");
        GmSaveSystem.ConfigureForTests(savePath);
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.Flush();
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void AdapterPersistsEverySuccessfulTransitionButNeverFailedOnes()
    {
        GameObject go = new GameObject("rules");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            rules.StartGame(FindCheatSeed(), 4, 2, true);
            AssertStoredEquals(rules.Match);
            string before = GmRunStore.GetParlorMatchSnapshot().playerHand.Count.ToString();
            Assert.That(rules.PlayPlayerCard(-1), Is.EqualTo(GmParlorActionError.InvalidCardIndex));
            Assert.That(GmRunStore.GetParlorMatchSnapshot().playerHand.Count.ToString(), Is.EqualTo(before));

            int lead = FindVulnerableLead(rules.Match);
            Assert.That(rules.PlayPlayerCard(lead), Is.EqualTo(GmParlorActionError.None));
            Assert.That(rules.Match.AldricCheated, Is.True);
            AssertStoredEquals(rules.Match);
            Assert.That(GmSaveSystem.Flush(), Is.True);
            Assert.That(GmSaveSystem.HasSave(), Is.True);

            Assert.That(rules.ReadAldricPlay(), Is.EqualTo(GmParlorActionError.None));
            AssertStoredEquals(rules.Match);
            Assert.That(rules.ContinueResult(), Is.EqualTo(GmParlorActionError.None));
            AssertStoredEquals(rules.Match);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void CorruptSavedMatchRefusesInitializationWithoutPartialOrFreshState()
    {
        var match = new GmParlorMatch(12, 1, 0, true);
        match.Start();
        GmParlorMatchSnapshot corrupt = match.ExportSnapshot();
        corrupt.randomState = 0;
        GmRunStore.LoadFromSaveData(new GmSaveData { parlorMatch = corrupt });

        GameObject go = new GameObject("rules");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            LogAssert.Expect(LogType.Error,
                "[GmParlorRules] Refusing corrupt saved match: random state cannot be zero");
            Assert.That(rules.InitializeOrRestore(),
                Is.EqualTo(GmParlorInitializeResult.CorruptSavedState));
            Assert.That(rules.Match, Is.Null);
            Assert.That(rules.LastRestoreError, Does.Contain("random"));
            Assert.That(GmRunStore.HasParlorMatch, Is.True,
                "refusal must not erase the evidence and silently start over");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void RestoringConsumedOutcomeAndCompletedMatchDoesNotReplayOrDoubleComplete()
    {
        GameObject firstGo = new GameObject("first");
        var first = firstGo.AddComponent<GmParlorRules>();
        first.StartGame(71, 2, 1, true);
        DriveToMatchResult(first);
        Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        Object.DestroyImmediate(firstGo);

        GameObject restoredGo = new GameObject("restored");
        var restored = restoredGo.AddComponent<GmParlorRules>();
        int outcomes = 0;
        int games = 0;
        restored.OnOutcomeReady += (_, __) => outcomes++;
        restored.OnGameCompleted += _ => games++;
        try
        {
            Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            Assert.That(restored.Match.Phase, Is.EqualTo(GmParlorMatchPhase.MatchResult));
            Assert.That(outcomes, Is.Zero);
            Assert.That(games, Is.Zero);
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(restoredGo); }
    }

    [Test]
    public void RealSaveFileRestoresMidCheatAndContinuesWithExactFutureRandomness()
    {
        int seed = FindCheatSeed();
        GameObject liveGo = new GameObject("live");
        var live = liveGo.AddComponent<GmParlorRules>();
        live.StartGame(seed, 4, 5, true);
        int lead = FindVulnerableLead(live.Match);
        Assert.That(live.PlayPlayerCard(lead), Is.EqualTo(GmParlorActionError.None));
        Assert.That(live.Match.AldricCheated, Is.True);
        GmParlorMatchSnapshot diskPoint = live.Match.ExportSnapshot();
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Assert.That(GmSaveSystem.HasSave(), Is.True);
        Object.DestroyImmediate(liveGo);

        Assert.That(GmParlorMatch.TryRestore(diskPoint, out GmParlorMatch baseline,
            out string baselineError), Is.True, baselineError);
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);

        GameObject restoredGo = new GameObject("restored");
        var restoredRules = restoredGo.AddComponent<GmParlorRules>();
        GmParlorOutcome delivered = default;
        ulong deliveredSequence = 0;
        restoredRules.OnOutcomeReady += (sequence, outcome) =>
        {
            deliveredSequence = sequence;
            delivered = outcome;
        };
        try
        {
            Assert.That(restoredRules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            Assert.That(restoredRules.Match.PublicStateBytes, Is.EqualTo(baseline.PublicStateBytes));
            Assert.That(restoredRules.Match.TellObservation, Is.EqualTo(baseline.TellObservation));

            Assert.That(restoredRules.AcceptAldricPlay(), Is.EqualTo(baseline.ContinueJudgement()));
            Assert.That(baseline.TryConsumeOutcome(out GmParlorOutcome expectedOutcome,
                out ulong expectedSequence), Is.True);
            Assert.That(deliveredSequence, Is.EqualTo(expectedSequence));
            Assert.That(delivered.Kind, Is.EqualTo(expectedOutcome.Kind));
            Assert.That(delivered.CorruptionDelta, Is.EqualTo(expectedOutcome.CorruptionDelta));
            Assert.That(delivered.SanityDelta, Is.EqualTo(expectedOutcome.SanityDelta));
            Assert.That(restoredRules.Match.PublicStateBytes, Is.EqualTo(baseline.PublicStateBytes));
            DriveTogetherToNextRound(baseline, restoredRules);
            Assert.That(restoredRules.Match.PublicStateBytes, Is.EqualTo(baseline.PublicStateBytes),
                "future deal/tells diverged after real JSON save/load");
        }
        finally { Object.DestroyImmediate(restoredGo); }
    }

    [Test]
    public void RealSaveFileRetainsTheFrozenAdaptivePackageAndBehaviorCursorExactly()
    {
        var package = GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Mirror);
        package.attentionTier = 2;
        package.honestStrategyId = GmParlorHonestStrategyId.PreferredSuitReserveFlames;
        package.primaryCounterPlanId = GmParlorCounterPlanId.EmberLock;
        package.targetTendency = GmParlorTendency.SuitSpecialistFlames;
        package.tellFamilyId = GmParlorTellFamilyId.TraditionalCuff;
        package.memoryTokenId = GmParlorMemoryTokenId.AshUnderGlass;
        package.selectedFromReceiptOrdinal = 4;
        for (int index = 0; index < package.historyDigest.Length; index++)
            package.historyDigest[index] = (byte)(index * 7 + 3);
        byte[] expectedPackage = package.ToCanonicalBytes();

        GameObject liveGo = new GameObject("adaptive-live");
        var live = liveGo.AddComponent<GmParlorRules>();
        Assert.That(live.StartAdaptiveGame(package, seed: 117, corruptionTier: 3,
            priorCatchCount: 2, readInitiallyUnlocked: true, forceRestart: true),
            Is.EqualTo(GmParlorInitializeResult.StartedNew));
        Assert.That(live.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        ulong expectedCursor = live.Match.BehaviorAccumulator.EventSequence;
        Assert.That(GmSaveSystem.Flush(), Is.True);
        Object.DestroyImmediate(liveGo);

        package.honestStrategyId = GmParlorHonestStrategyId.SecondDealHigh;
        package.historyDigest[0] ^= 0xff;
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);

        GameObject restoredGo = new GameObject("adaptive-restored");
        var restored = restoredGo.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(restored.InitializeOrRestore(),
                Is.EqualTo(GmParlorInitializeResult.Restored));
            Assert.That(restored.Match.AdaptivePackage.ToCanonicalBytes(),
                Is.EqualTo(expectedPackage));
            Assert.That(restored.Match.BehaviorAccumulator.EventSequence,
                Is.EqualTo(expectedCursor));
        }
        finally { Object.DestroyImmediate(restoredGo); }
    }

    static void AssertStoredEquals(GmParlorMatch match)
    {
        GmParlorMatchSnapshot saved = GmRunStore.GetParlorMatchSnapshot();
        Assert.That(saved, Is.Not.Null);
        Assert.That(GmParlorMatch.TryRestore(saved, out GmParlorMatch copy, out string error), Is.True, error);
        Assert.That(copy.PublicStateBytes, Is.EqualTo(match.PublicStateBytes));
    }

    static int FindCheatSeed()
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            var match = new GmParlorMatch(seed, 4, 0, true);
            match.Start();
            int lead = FindVulnerableLead(match);
            if (lead < 0) continue;
            match.PlayPlayerCard(lead);
            if (match.AldricCheated) return seed;
        }
        Assert.Fail("no cheat seed");
        return -1;
    }

    static int FindVulnerableLead(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
        {
            bool aldricCanWin = false;
            for (int j = 0; j < match.AldricHand.Count; j++)
                if (GmParlorCore.IsLegal(match.AldricHand, j, match.PlayerHand[i]) &&
                    !GmParlorCore.LeadWins(match.PlayerHand[i], match.AldricHand[j])) aldricCanWin = true;
            if (!aldricCanWin) return i;
        }
        return -1;
    }

    static int FirstLegal(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
            if (match.GetPlayerCardError(i) == GmParlorActionError.None) return i;
        return -1;
    }

    static void DriveToMatchResult(GmParlorRules rules)
    {
        int guard = 240;
        while (rules.Match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            if (rules.Match.Phase == GmParlorMatchPhase.PlayerLeads ||
                rules.Match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
                rules.PlayPlayerCard(FirstLegal(rules.Match));
            else if (rules.Match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
                rules.AcceptAldricPlay();
            else rules.ContinueResult();
        }
        Assert.That(guard, Is.GreaterThan(0));
    }

    static void DriveTogetherToNextRound(GmParlorMatch baseline, GmParlorRules restored)
    {
        int startingRound = baseline.RoundNumber;
        int guard = 160;
        while (baseline.RoundNumber == startingRound && guard-- > 0)
        {
            Assert.That(restored.Match.PublicStateBytes, Is.EqualTo(baseline.PublicStateBytes));
            if (baseline.Phase == GmParlorMatchPhase.PlayerLeads ||
                baseline.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
            {
                int index = FirstLegal(baseline);
                Assert.That(restored.PlayPlayerCard(index), Is.EqualTo(baseline.PlayPlayerCard(index)));
            }
            else if (baseline.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
                Assert.That(restored.AcceptAldricPlay(), Is.EqualTo(baseline.ContinueJudgement()));
            else
                Assert.That(restored.ContinueResult(), Is.EqualTo(baseline.Continue()));
            baseline.TryConsumeOutcome(out _, out _);
        }
        Assert.That(guard, Is.GreaterThan(0));
    }
}
