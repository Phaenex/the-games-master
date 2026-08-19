using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmParlorRunStorePersistenceTests
{
    string directory;
    string savePath;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-parlor-store-" +
            Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(directory, "save.json");
        GmSaveSystem.ConfigureForTests(savePath);
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
    public void RunStoreOwnsADeepCopiedOptionalSnapshotAndNewRunClearsIt()
    {
        var match = new GmParlorMatch(91, 2, 4, true);
        match.Start();
        GmParlorMatchSnapshot external = match.ExportSnapshot();

        Assert.That(GmRunStore.TrySetParlorMatch(external, out string error), Is.True, error);
        external.playerHand.Clear();
        GmParlorMatchSnapshot stored = GmRunStore.GetParlorMatchSnapshot();
        Assert.That(stored.playerHand, Has.Count.EqualTo(7));

        GmSaveData saved = GmRunStore.ToSaveData();
        saved.parlorMatch.playerHand.Clear();
        Assert.That(GmRunStore.GetParlorMatchSnapshot().playerHand, Has.Count.EqualTo(7));

        GmRunStore.BeginNewRun();
        Assert.That(GmRunStore.HasParlorMatch, Is.False);
        Assert.That(GmRunStore.GetParlorMatchSnapshot(), Is.Null);
    }

    [Test]
    public void OldSaveWithoutParlorStateLoadsBackwardCompatibly()
    {
        var oldData = new GmSaveData
        {
            corruptionTier = 3,
            currentSceneId = "parlor",
            parlorMatch = null,
        };

        Assert.DoesNotThrow(() => GmRunStore.LoadFromSaveData(oldData));
        Assert.That(GmRunStore.CorruptionTier, Is.EqualTo(3));
        Assert.That(GmRunStore.HasParlorMatch, Is.False);
    }

    [Test]
    public void LoadedCorruptSnapshotIsPreservedForFailClosedSceneRestore()
    {
        var match = new GmParlorMatch(12, 1, 0, true);
        match.Start();
        GmParlorMatchSnapshot corrupt = match.ExportSnapshot();
        corrupt.randomState = 0;
        corrupt.outcomeSequence = 99;
        corrupt.highestDurableOutcomeSequence = 99;
        corrupt.highestAcknowledgedOutcomeSequence = 99;

        GmRunStore.LoadFromSaveData(new GmSaveData { parlorMatch = corrupt });

        Assert.That(GmRunStore.HasParlorMatch, Is.True);
        Assert.That(GmRunStore.GetParlorMatchSnapshot().randomState, Is.Zero);
        Assert.That(GmRunStore.GetParlorMatchSnapshot().highestAcknowledgedOutcomeSequence,
            Is.EqualTo(99), "failed validation must retain the raw evidence unchanged");
        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.Zero,
            "an invalid raw snapshot must not advance the run cursor");
    }

    [Test]
    public void FaithfulV1ConsumedOutcomeDerivesCursorFromValidatedMigrationWithoutReplay()
    {
        GmParlorMatch match = FindPendingCheat();
        Assert.That(match.Read(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.TryPeekOutcome(out GmParlorOutcome outcome, out ulong sequence), Is.True);
        Assert.That(GmRunStore.TryApplyParlorOutcome(sequence, outcome, out string applyError),
            Is.True, applyError);
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
        int catches = GmRunStore.CheatsCaughtCount;

        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = SerializeAsFaithfulV1(match, consumed: true);
        data.parlorAppliedOutcomeSequence = 0;
        LoadJsonRoundTrip(data);

        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
        Assert.That(GmRunStore.GetParlorMatchSnapshot().version, Is.EqualTo(1));
        var go = new GameObject("v1-consumed-store-restore");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            int events = 0;
            rules.OnOutcomeReady += (_, __) => events++;
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events, Is.Zero);
            Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catches));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void FaithfulV1MultiSequencePendingDerivesPriorCursorAndDeliversExactlyOnce()
    {
        GmParlorMatch match = MatchWithSecondPendingOutcome();
        Assert.That(match.TryPeekOutcome(out _, out ulong sequence), Is.True);
        Assert.That(sequence, Is.GreaterThan(1));
        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = SerializeAsFaithfulV1(match, consumed: false);
        data.parlorAppliedOutcomeSequence = 0;
        LoadJsonRoundTrip(data);

        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence - 1));
        int catchesBefore = GmRunStore.CheatsCaughtCount;
        float sanityBefore = GmRunStore.Sanity;
        int defianceBefore = GmRunStore.Defiance;
        int complianceBefore = GmRunStore.Compliance;
        var go = new GameObject("v1-pending-store-restore");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            int events = 0;
            rules.OnOutcomeReady += (_, __) => events++;
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
            int catchesAfter = GmRunStore.CheatsCaughtCount;
            float sanityAfter = GmRunStore.Sanity;
            int defianceAfter = GmRunStore.Defiance;
            int complianceAfter = GmRunStore.Compliance;

            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(rules.DeliverPendingOutcome(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(GmRunStore.CheatsCaughtCount, Is.EqualTo(catchesAfter));
            Assert.That(GmRunStore.Sanity, Is.EqualTo(sanityAfter));
            Assert.That(GmRunStore.Defiance, Is.EqualTo(defianceAfter));
            Assert.That(GmRunStore.Compliance, Is.EqualTo(complianceAfter));
            Assert.That(catchesAfter, Is.GreaterThanOrEqualTo(catchesBefore));
            Assert.That(Math.Abs(sanityAfter - sanityBefore), Is.LessThanOrEqualTo(0.06f));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void CurrentV2ExplicitCursorLoadPathIsUnchanged()
    {
        GmParlorMatch match = FindPendingCheat();
        Assert.That(match.Read(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.TryPeekOutcome(out GmParlorOutcome outcome, out ulong sequence), Is.True);
        Assert.That(GmRunStore.TryApplyParlorOutcome(sequence, outcome, out string error),
            Is.True, error);
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = match.ExportSnapshot();
        data.parlorAppliedOutcomeSequence = sequence;

        LoadJsonRoundTrip(data);

        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
        Assert.That(GmRunStore.GetParlorMatchSnapshot().version,
            Is.EqualTo(GmParlorMatchSnapshot.CurrentVersion));
    }

    [Test]
    public void CursorAheadOfAcknowledgedSnapshotFailsClosedWithoutClampingRawState()
    {
        GmParlorMatch match = ConsumedFirstOutcome();
        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = match.ExportSnapshot();
        data.parlorAppliedOutcomeSequence = match.HighestAcknowledgedOutcomeSequence + 1;

        AssertCursorSaveRejected(data, "ahead");
    }

    [Test]
    public void NonzeroCursorBehindAcknowledgedSnapshotFailsClosedWithoutReplaying()
    {
        GmParlorMatch match = MatchWithSecondPendingOutcome();
        Assert.That(match.TryPeekOutcome(out GmParlorOutcome outcome, out ulong sequence), Is.True);
        Assert.That(GmRunStore.TryApplyParlorOutcome(sequence, outcome, out string error),
            Is.True, error);
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = match.ExportSnapshot();
        data.parlorAppliedOutcomeSequence = sequence - 1;

        AssertCursorSaveRejected(data, "behind");
    }

    [Test]
    public void ExactAcknowledgedCursorRestoresWithoutReplay()
    {
        GmParlorMatch match = ConsumedFirstOutcome();
        ulong sequence = match.HighestAcknowledgedOutcomeSequence;
        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = match.ExportSnapshot();
        data.parlorAppliedOutcomeSequence = sequence;
        LoadJsonRoundTrip(data);

        var go = new GameObject("exact-cursor");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            int events = 0;
            rules.OnOutcomeReady += (_, __) => events++;
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events, Is.Zero);
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void PendingDurableNextSequenceAcceptsCursorEqualToAcknowledgedAndDeliversOnce()
    {
        GmParlorMatch match = MatchWithSecondPendingOutcome();
        Assert.That(match.TryPeekOutcome(out _, out ulong sequence), Is.True);
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.HighestAcknowledgedOutcomeSequence, Is.EqualTo(sequence - 1));
        GmSaveData data = GmRunStore.ToSaveData();
        data.parlorMatch = match.ExportSnapshot();
        data.parlorAppliedOutcomeSequence = sequence - 1;
        LoadJsonRoundTrip(data);

        var go = new GameObject("pending-exact-cursor");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            Assert.That(rules.InitializeOrRestore(), Is.EqualTo(GmParlorInitializeResult.Restored));
            int events = 0;
            rules.OnOutcomeReady += (_, __) => events++;
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(sequence));
            Assert.That(rules.ActivateRestoredOutcomes(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(events, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void NonzeroCursorWithoutSnapshotFailsClosedInsteadOfStartingFresh()
    {
        string json = JsonUtility.ToJson(new CursorWithoutSnapshotFixture
        {
            parlorAppliedOutcomeSequence = 3,
        });
        Assert.That(json, Does.Not.Contain("parlorMatch"));
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(json));

        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(3));
        var go = new GameObject("orphan-cursor");
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            LogAssert.Expect(LogType.Error,
                new Regex("cursor.*without.*snapshot", RegexOptions.IgnoreCase));
            Assert.That(rules.InitializeOrRestore(),
                Is.EqualTo(GmParlorInitializeResult.CorruptSavedState));
            Assert.That(rules.Match, Is.Null);
            StringAssert.Contains("without", rules.LastRestoreError.ToLowerInvariant());
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static GmParlorMatch ConsumedFirstOutcome()
    {
        GmRunStore.BeginNewRun();
        GmParlorMatch match = FindPendingCheat();
        Assert.That(match.Read(), Is.EqualTo(GmParlorActionError.None));
        Assert.That(match.TryPeekOutcome(out GmParlorOutcome outcome, out ulong sequence), Is.True);
        Assert.That(GmRunStore.TryApplyParlorOutcome(sequence, outcome, out string error),
            Is.True, error);
        Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
        Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
        return match;
    }

    static void AssertCursorSaveRejected(GmSaveData data, string relation)
    {
        string raw = JsonUtility.ToJson(data.parlorMatch);
        ulong persistedCursor = data.parlorAppliedOutcomeSequence;
        LoadJsonRoundTrip(data);

        Assert.That(JsonUtility.ToJson(GmRunStore.GetParlorMatchSnapshot()), Is.EqualTo(raw));
        Assert.That(GmRunStore.ParlorAppliedOutcomeSequence, Is.EqualTo(persistedCursor),
            "corrupt cursor must remain visible, not be silently clamped");
        var go = new GameObject("bad-cursor-" + relation);
        var rules = go.AddComponent<GmParlorRules>();
        try
        {
            LogAssert.Expect(LogType.Error,
                new Regex("cursor.*" + relation, RegexOptions.IgnoreCase));
            Assert.That(rules.InitializeOrRestore(),
                Is.EqualTo(GmParlorInitializeResult.CorruptSavedState));
            Assert.That(rules.Match, Is.Null);
            StringAssert.Contains(relation, rules.LastRestoreError.ToLowerInvariant());
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static GmParlorMatch MatchWithSecondPendingOutcome()
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            GmRunStore.BeginNewRun();
            var match = new GmParlorMatch(seed, 4, 0, true);
            match.Start();
            if (!ResolveNextOutcome(match)) continue;
            Assert.That(match.TryPeekOutcome(out GmParlorOutcome first, out ulong firstSequence),
                Is.True);
            Assert.That(GmRunStore.TryApplyParlorOutcome(firstSequence, first,
                out string applyError), Is.True, applyError);
            match.MarkOutcomeDurable(firstSequence);
            match.AcknowledgeOutcome(firstSequence);
            match.Continue();
            if (ResolveNextOutcome(match) && match.OutcomeSequence > 1) return match;
        }
        Assert.Fail("could not produce a second pending outcome");
        return null;
    }

    static bool ResolveNextOutcome(GmParlorMatch match)
    {
        int guard = 30;
        while (guard-- > 0 && !match.TryPeekOutcome(out _, out _))
        {
            if (match.Phase == GmParlorMatchPhase.PlayerLeads ||
                match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
            {
                int index = -1;
                for (int i = 0; i < match.PlayerHand.Count; i++)
                    if (match.GetPlayerCardError(i) == GmParlorActionError.None) { index = i; break; }
                if (index < 0) return false;
                match.PlayPlayerCard(index);
            }
            else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
                match.ContinueJudgement();
            else return false;
        }
        return match.TryPeekOutcome(out _, out _);
    }

    static GmParlorMatch FindPendingCheat()
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            var match = new GmParlorMatch(seed, 4, 0, true);
            match.Start();
            for (int i = 0; i < match.PlayerHand.Count; i++)
            {
                var probe = new GmParlorMatch(seed, 4, 0, true);
                probe.Start();
                if (probe.PlayPlayerCard(i) == GmParlorActionError.None && probe.AldricCheated)
                    return probe;
            }
        }
        Assert.Fail("could not find pending cheat");
        return null;
    }

    static GmParlorMatchSnapshot SerializeAsFaithfulV1(GmParlorMatch match, bool consumed)
    {
        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
        snapshot.version = 1;
        if (consumed) snapshot.lastOutcome = default;
        string json = JsonUtility.ToJson(snapshot);
        json = Regex.Replace(json, ",\\\"highestDurableOutcomeSequence\\\":\\d+", string.Empty);
        json = Regex.Replace(json, ",\\\"highestAcknowledgedOutcomeSequence\\\":\\d+", string.Empty);
        return JsonUtility.FromJson<GmParlorMatchSnapshot>(json);
    }

    static void LoadJsonRoundTrip(GmSaveData data)
    {
        string json = data.ToJson();
        GmRunStore.BeginNewRun();
        GmRunStore.LoadFromSaveData(GmSaveData.FromJson(json));
    }

    [Serializable]
    sealed class CursorWithoutSnapshotFixture
    {
        public int corruptionTier = 1;
        public float sanity = 1f;
        public ulong parlorAppliedOutcomeSequence;
    }
}
