using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmParlorDurabilityTests
{
    string directory;
    string path;
    MutableBackend backend;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-parlor-durable-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        backend = new MutableBackend();
        GmSaveSystem.ConfigureForTests(path, backend);
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void DefaultInitializationUsesTheAuthoritativeRunAndOutdoorCatchCarriesRead()
    {
        GmRunStore.RaiseCorruption("one");
        GmRunStore.RaiseCorruption("two");
        GmRunStore.RecordCatch("outbuilding-ledger-catch");
        var go = new GameObject("rules");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.StartedNew));
            Assert.That(rules.Match.CorruptionTier, Is.EqualTo(3));
            Assert.That(rules.Match.PriorCatchCount, Is.EqualTo(1));
            Assert.That(rules.Match.ReadUnlocked, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void ProducedOutcomeSaveFailureRollsBackAndReturnsPersistenceError()
    {
        GmParlorRules rules = NewAwaitingCheat(out GameObject go);
        try
        {
            string before = rules.Match.PublicStateBytes;
            int catches = GmRunStore.CheatsCaughtCount;
            backend.Fail = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected"));
            Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.PersistenceFailed));
            Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
            Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catches));
        }
        finally { backend.Fail = false; UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void ThrowingObserverReportsFailureAfterDurableAckAndNeverReplays()
    {
        GmParlorRules rules = NewAwaitingCheat(out GameObject go);
        rules.OnOutcomeReady += (_, __) => throw new InvalidOperationException("observer exploded");
        try
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("observer exploded"));
            Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.OutcomeHandlerFailed));
            ulong sequence = rules.Match.OutcomeSequence;
            Assert.That(rules.Match.HighestAcknowledgedOutcomeSequence, Is.EqualTo(sequence));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
            int catches = GmRunStore.CheatsCaughtCount;
            UnityEngine.Object.DestroyImmediate(go);

            var restoredGo = new GameObject("restored");
            var restored = restoredGo.AddComponent<GmParlorRules>();
            int replayed = 0;
            restored.OnOutcomeReady += (_, __) => replayed++;
            Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            Assert.That(replayed, Is.Zero);
            Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catches));
            UnityEngine.Object.DestroyImmediate(restoredGo);
        }
        finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void AckSaveFailureReturnsErrorAndRestartReplaysPendingSideEffectOnce()
    {
        GmParlorRules rules = NewAwaitingCheat(out GameObject go);
        int failAt = backend.WriteCount + 2; // produced identity succeeds; acknowledgement fails
        backend.FailAtWrite = failAt;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected"));
        Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.PersistenceFailed));
        Assert.That(rules.Match.HighestAcknowledgedOutcomeSequence,
            Is.LessThan(rules.Match.OutcomeSequence));
        Assert.That(rules.Match.TryPeekOutcome(out _, out _), Is.True,
            "the live adapter must restore the durable-pending outcome for an immediate retry");
        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(rules.Match.OutcomeSequence));

        int delivered = 0;
        rules.OnOutcomeReady += (_, __) => delivered++;
        backend.FailAtWrite = -1;
        Assert.That(rules.DeliverPendingOutcome(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(delivered, Is.EqualTo(1));
        Assert.That(rules.DeliverPendingOutcome(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(delivered, Is.EqualTo(1));

        string lastDurableJson = backend.LastJson;
        Assert.That(GmSaveSystem.QueueSave(out _), Is.True);
        Assert.That(GmSaveSystem.Flush(), Is.True);
        UnityEngine.Object.DestroyImmediate(go);
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(JsonUtility.FromJson<GmSaveData>(lastDurableJson));
        var restoredGo = new GameObject("ack-restart");
        var restored = restoredGo.AddComponent<GmParlorRules>();
        int replayed = 0;
        restored.OnOutcomeReady += (_, __) => replayed++;
        Assert.That(restored.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
        Assert.That(restored.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(replayed, Is.Zero);
        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence,
            Is.EqualTo(restored.Match.OutcomeSequence));
        Assert.That(restored.DeliverPendingOutcome(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(replayed, Is.Zero);
        UnityEngine.Object.DestroyImmediate(restoredGo);
    }

    [Test]
    public void RestoredOutcomeWaitsForExplicitActivationAndDeliversOnce()
    {
        GmParlorMatch match = MatchWithPendingOutcome(GmParlorOutcomeKind.CheatMissed);
        Assert.That(match.MarkOutcomeDurable(match.OutcomeSequence), Is.True);
        Assert.That(GmRunStore.TrySetParlorMatch(match.ExportSnapshot(), out string error),
            Is.True, error);
        var go = new GameObject("restore-order");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            int delivered = 0;
            rules.OnOutcomeReady += (_, __) => delivered++;
            Assert.That(delivered, Is.Zero, "Initialize/Awake must restore only");
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(delivered, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void TrickBoundaryFlushFailureRollsBackStateAndEventThenRetryEmitsOnce()
    {
        GmParlorMatch awaiting = FindAwaiting(cheated: false);
        GmParlorRules rules = RestoreRules(awaiting, out GameObject go);
        try
        {
            AssertBoundaryRollbackAndRetry(rules, () => rules.AcceptAldricPlay(),
                GmParlorMatchPhase.TrickResult, h => rules.OnTrickCompleted += h,
                () => GmRunStore.IsRoomComplete("parlor"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void RoundBoundaryFlushFailureRollsBackStateAndEventThenRetryEmitsOnce()
    {
        GmParlorMatch beforeRound = MatchBeforeRoundResult();
        GmParlorRules rules = RestoreRules(beforeRound, out GameObject go);
        try
        {
            AssertBoundaryRollbackAndRetry(rules, () => rules.ContinueResult(),
                GmParlorMatchPhase.RoundResult, h => rules.OnRoundCompleted += h,
                () => GmRunStore.IsRoomComplete("parlor"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void MatchBoundaryFlushFailureRollsBackCompletionAndEventThenRetryEmitsOnce()
    {
        GmParlorMatch beforeMatch = MatchBeforeMatchResult();
        GmParlorRules rules = RestoreRules(beforeMatch, out GameObject go);
        try
        {
            AssertBoundaryRollbackAndRetry(rules, () => rules.ContinueResult(),
                GmParlorMatchPhase.MatchResult, h => rules.OnGameCompleted += h,
                () => GmRunStore.IsRoomComplete("parlor"));
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void StartGameFailureReturnsExplicitResultAndPreservesForcedRestartState()
    {
        var go = new GameObject("start-transaction");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            rules.InitializeOrRestore(77, 2, 3, true);
            Assert.That(GmSaveSystem.Flush(), Is.True);
            string before = rules.Match.PublicStateBytes;
            string storedBefore = SnapshotBytes(GmRunStore.GetParlorMatchSnapshot());
            MethodInfo startGame = typeof(GmParlorRules).GetMethod(nameof(GmParlorRules.StartGame));
            Assert.That(startGame.ReturnType, Is.EqualTo(typeof(GmParlorInitializeResult)));

            backend.Fail = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected"));
            object result = startGame.Invoke(rules, new object[] { 99, 4, 8, true, true });
            Assert.That(result, Is.EqualTo(GmParlorInitializeResult.PersistenceFailed));
            Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
            Assert.That(SnapshotBytes(GmRunStore.GetParlorMatchSnapshot()), Is.EqualTo(storedBefore));
        }
        finally { backend.Fail = false; UnityEngine.Object.DestroyImmediate(go); }
    }

    [TestCase(GmParlorOutcomeKind.CheatCaught)]
    [TestCase(GmParlorOutcomeKind.FalseReadPenalty)]
    [TestCase(GmParlorOutcomeKind.CheatMissed)]
    public void DurablePendingOutcomeRestoresAppliesAndAcknowledgesExactlyOnce(GmParlorOutcomeKind kind)
    {
        GmParlorMatch match = MatchWithPendingOutcome(kind);
        Assert.That(match.MarkOutcomeDurable(match.OutcomeSequence), Is.True);
        Assert.That(GmRunStore.TrySetParlorMatch(match.ExportSnapshot(), out string storeError),
            Is.True, storeError);
        Assert.That(GmSaveSystem.Save(), Is.True);

        var go = new GameObject("restore");
        var rules = go.AddComponent<GmParlorRules>();
        int delivered = 0;
        rules.OnOutcomeReady += (_, outcome) => { delivered++; Assert.That(outcome.Kind, Is.EqualTo(kind)); };
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            Assert.That(delivered, Is.Zero, "restore must not deliver before subscribers activate it");
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(rules.Match.HighestAcknowledgedOutcomeSequence,
                Is.EqualTo(rules.Match.OutcomeSequence));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence,
                Is.EqualTo(rules.Match.OutcomeSequence));
            Assert.That(rules.DeliverPendingOutcome(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(delivered, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void AcknowledgedMatchAbandonResetsDurableNamespaceAndFreshMatchAppliesOnce()
    {
        GmParlorRules rules = NewAwaitingCheat(out GameObject go);
        try
        {
            Assert.That(rules.ReadAldricPlay(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(1));
            int catchesAfterFirstMatch = GmRunStore.CheatsCaughtCount;
            ulong oldNamespace = GmRunStore.ParlorOutcomeNamespace;

            Assert.That(rules.AbandonSavedMatch(), Is.True);
            Assert.That(rules.Match, Is.Null);
            Assert.That(GmRunStore.HasParlorMatch, Is.False);
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.Zero);
            Assert.That(GmRunStore.ParlorOutcomeNamespace, Is.GreaterThan(oldNamespace));

            GmSaveData abandoned = JsonUtility.FromJson<GmSaveData>(backend.LastJson);
            Assert.That(abandoned.parlorAppliedOutcomeSequence, Is.Zero);
            Assert.That(abandoned.parlorOutcomeNamespace,
                Is.EqualTo(GmRunStore.ParlorOutcomeNamespace));
            Assert.That(abandoned.parlorMatch == null || abandoned.parlorMatch.randomState == 0,
                Is.True, "explicit abandon must not leave a real Parlor snapshot in JSON");

            GmRunStore.BeginNewRun();
            GmRunStore.LoadFromSaveData(abandoned);
            Assert.That(GmRunStore.ParlorRestoreError, Is.Empty);
            Assert.That(GmRunStore.HasParlorMatch, Is.False);
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.Zero);

            var freshGo = new GameObject("fresh-after-abandon");
            var fresh = freshGo.AddComponent<GmParlorRules>();
            try
            {
                int seed = FindCheatSeed();
                Assert.That(fresh.StartGame(seed, 4, catchesAfterFirstMatch, true, true),
                    Is.EqualTo(GmParlorInitializeResult.StartedNew));
                int lead = FindVulnerableLead(fresh.Match);
                Assert.That(fresh.PlayPlayerCard(lead), Is.EqualTo(GmParlorActionError.None));
                Assert.That(fresh.Match.AldricCheated, Is.True);
                int catchesBeforeNewOutcome = GmRunStore.CheatsCaughtCount;
                Assert.That(fresh.ReadAldricPlay(), Is.EqualTo(GmParlorActionError.None));
                Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(1));
                Assert.That(GmRunStore.CheatsCaughtCount,
                    Is.EqualTo(catchesBeforeNewOutcome + 1));
                Assert.That(fresh.DeliverPendingOutcome(), Is.EqualTo(GmParlorActionError.None));
                Assert.That(GmRunStore.CheatsCaughtCount,
                    Is.EqualTo(catchesBeforeNewOutcome + 1));
            }
            finally { UnityEngine.Object.DestroyImmediate(freshGo); }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void AcknowledgedMatchAbandonSaveFailureRetainsSnapshotCursorAndNamespace()
    {
        GmParlorRules rules = NewAwaitingCheat(out GameObject go);
        try
        {
            Assert.That(rules.ReadAldricPlay(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(1));
            string before = rules.Match.PublicStateBytes;
            string storedBefore = SnapshotBytes(GmRunStore.GetParlorMatchSnapshot());
            ulong cursorBefore = GmRunStore.ParlorAppliedOutcomeSequence;
            ulong namespaceBefore = GmRunStore.ParlorOutcomeNamespace;
            string durableJson = backend.LastJson;
            backend.Fail = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected"));

            Assert.That(rules.AbandonSavedMatch(), Is.False);
            Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
            Assert.That(SnapshotBytes(GmRunStore.GetParlorMatchSnapshot()),
                Is.EqualTo(storedBefore));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(cursorBefore));
            Assert.That(GmRunStore.ParlorOutcomeNamespace, Is.EqualTo(namespaceBefore));
            Assert.That(backend.LastJson, Is.EqualTo(durableJson));
        }
        finally { backend.Fail = false; UnityEngine.Object.DestroyImmediate(go); }
    }

    GmParlorRules NewAwaitingCheat(out GameObject go)
    {
        int seed = FindCheatSeed();
        go = new GameObject("rules");
        var rules = go.AddComponent<GmParlorRules>();
        rules.StartGame(seed, 4, 0, true);
        int lead = FindVulnerableLead(rules.Match);
        Assert.That(rules.PlayPlayerCard(lead), Is.EqualTo(GmParlorActionError.None));
        Assert.That(rules.Match.AldricCheated, Is.True);
        Assert.That(GmSaveSystem.Flush(), Is.True);
        return rules;
    }

    void AssertBoundaryRollbackAndRetry(GmParlorRules rules,
        Func<GmParlorActionError> transition, GmParlorMatchPhase expectedPhase,
        Action<Action<bool>> subscribe, Func<bool> completion)
    {
        Assert.That(GmSaveSystem.Flush(), Is.True);
        string before = rules.Match.PublicStateBytes;
        string storedBefore = SnapshotBytes(GmRunStore.GetParlorMatchSnapshot());
        int events = 0;
        subscribe(_ => events++);
        backend.Fail = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("injected"));
        Assert.That(transition(), Is.EqualTo(GmParlorActionError.PersistenceFailed));
        Assert.That(rules.Match.PublicStateBytes, Is.EqualTo(before));
        Assert.That(SnapshotBytes(GmRunStore.GetParlorMatchSnapshot()), Is.EqualTo(storedBefore));
        Assert.That(completion(), Is.False);
        Assert.That(events, Is.Zero);

        backend.Fail = false;
        Assert.That(transition(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(rules.Match.Phase, Is.EqualTo(expectedPhase));
        Assert.That(events, Is.EqualTo(1));
    }

    GmParlorRules RestoreRules(GmParlorMatch match, out GameObject go)
    {
        Assert.That(GmRunStore.TrySetParlorMatch(match.ExportSnapshot(), out string error),
            Is.True, error);
        Assert.That(GmSaveSystem.Save(), Is.True);
        go = new GameObject("restored-boundary");
        var rules = go.AddComponent<GmParlorRules>();
        Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
        return rules;
    }

    static GmParlorMatch MatchBeforeRoundResult()
    {
        GmParlorMatch match = NewMatch(810);
        int guard = 400;
        while (guard-- > 0)
        {
            DriveOne(match);
            if (match.Phase == GmParlorMatchPhase.TrickResult &&
                (match.PlayerTricks >= GmParlorMatch.TricksToWinRound ||
                 match.AldricTricks >= GmParlorMatch.TricksToWinRound))
            {
                if (match.TryPeekOutcome(out _, out ulong sequence))
                {
                    match.MarkOutcomeDurable(sequence);
                    match.AcknowledgeOutcome(sequence);
                }
                return match;
            }
        }
        Assert.Fail("could not reach pre-round boundary");
        return null;
    }

    static GmParlorMatch MatchBeforeMatchResult()
    {
        GmParlorMatch match = NewMatch(811);
        int guard = 1200;
        while (guard-- > 0)
        {
            DriveOne(match);
            if (match.Phase == GmParlorMatchPhase.RoundResult &&
                (match.PlayerRounds >= GmParlorMatch.RoundsToWinMatch ||
                 match.AldricRounds >= GmParlorMatch.RoundsToWinMatch))
                return match;
        }
        Assert.Fail("could not reach pre-match boundary");
        return null;
    }

    static GmParlorMatch NewMatch(int seed)
    {
        var match = new GmParlorMatch(seed, 3, 2, true);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static void DriveOne(GmParlorMatch match)
    {
        if (match.Phase == GmParlorMatchPhase.PlayerLeads ||
            match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
        {
            for (int i = 0; i < match.PlayerHand.Count; i++)
            {
                if (match.GetPlayerCardError(i) != GmParlorActionError.None) continue;
                match.PlayPlayerCard(i);
                break;
            }
        }
        else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            match.ContinueJudgement();
        else if (match.Phase == GmParlorMatchPhase.TrickResult)
        {
            if (match.TryPeekOutcome(out _, out ulong sequence))
            {
                match.MarkOutcomeDurable(sequence);
                match.AcknowledgeOutcome(sequence);
            }
            match.Continue();
        }
        else if (match.Phase == GmParlorMatchPhase.RoundResult)
            match.Continue();
    }

    static GmParlorMatch FindAwaiting(bool cheated)
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            GmParlorMatch match = NewMatch(seed);
            for (int i = 0; i < match.PlayerHand.Count; i++)
            {
                GmParlorMatch probe = NewMatch(seed);
                if (probe.PlayPlayerCard(i) == GmParlorActionError.None &&
                    probe.AldricCheated == cheated) return probe;
            }
        }
        Assert.Fail("could not find judgement state");
        return null;
    }

    static string SnapshotBytes(GmParlorMatchSnapshot snapshot) => JsonUtility.ToJson(snapshot);

    static GmParlorMatch MatchWithPendingOutcome(GmParlorOutcomeKind kind)
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            var match = new GmParlorMatch(seed, 4, 0, true);
            match.Start();
            int lead = FindVulnerableLead(match);
            if (lead < 0) continue;
            match.PlayPlayerCard(lead);
            if (kind == GmParlorOutcomeKind.CheatCaught && match.AldricCheated) match.Read();
            else if (kind == GmParlorOutcomeKind.CheatMissed && match.AldricCheated) match.ContinueJudgement();
            else if (kind == GmParlorOutcomeKind.FalseReadPenalty && !match.AldricCheated) match.Read();
            else continue;
            if (match.LastOutcome.Kind == kind) return match;
        }
        Assert.Fail("no pending outcome " + kind);
        return null;
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
        return -1;
    }

    static int FindVulnerableLead(GmParlorMatch match)
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

    sealed class MutableBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public int FailAtWrite = -1;
        public int WriteCount;
        public string LastJson;
        public void WriteAtomic(string target, string json)
        {
            WriteCount++;
            if (Fail || WriteCount == FailAtWrite)
                throw new IOException("injected durability failure");
            LastJson = json;
        }
    }
}
