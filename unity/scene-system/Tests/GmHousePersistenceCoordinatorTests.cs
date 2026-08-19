using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class GmHousePersistenceCoordinatorTests
{
    string directory;
    string runPath;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(),
            "gm-house-coordinator-" + Guid.NewGuid().ToString("N"));
        runPath = Path.Combine(directory, "run.json");
        GmSaveSystem.ConfigureForTests(runPath);
        GmHousePersistenceCoordinator.ConfigureForTests(
            Path.Combine(directory, "house"));
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmHousePersistenceCoordinator.ResetForTests();
        GmSaveSystem.ResetTestConfiguration();
        GmRunStore.BeginNewRun();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void BootNewRunAllocatesOrdinaryHouseIdentityWithoutCreatingFakeContinueSave()
    {
        var host = new GameObject("BootHouseFixture");
        try
        {
            var menu = host.AddComponent<GmBootMenu>();
            menu.NewRun();

            Assert.That(GmHousePersistenceCoordinator.ActiveRun, Is.Not.Null);
            Assert.That(GmHousePersistenceCoordinator.ActiveRun.Mode,
                Is.EqualTo(GmParlorAdaptiveMode.Ordinary));
            Assert.That(GmRunStore.HouseRunId,
                Is.EqualTo(GmHousePersistenceCoordinator.ActiveRun.Identity.RunId));
            Assert.That(File.Exists(runPath), Is.False,
                "allocating House identity fabricated a Continue save before a checkpoint");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void MutableRunJsonContainsOnlyHousePointerNeverProfileOrReceiptList()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(117,
            out string error), Is.True, error);
        Assert.That(GmSaveSystem.Save(), Is.True, GmSaveSystem.LastError);
        string json = File.ReadAllText(runPath);

        StringAssert.Contains("houseRunId", json);
        StringAssert.DoesNotContain("houseReceipts", json);
        StringAssert.DoesNotContain("houseProfile", json);
        Assert.That(typeof(GmSaveData).GetFields()
            .Any(field => field.Name.IndexOf("receipt", StringComparison.OrdinalIgnoreCase) >= 0),
            Is.False);
    }

    [Test]
    public void ContinueResumesExactFrozenHouseRunFromPointer()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(211,
            out string error), Is.True, error);
        byte[] packageHash = GmHousePersistenceCoordinator.ActiveRun.FrozenPackage.CanonicalHash;
        string runId = GmRunStore.HouseRunId;
        Assert.That(GmSaveSystem.Save(), Is.True);

        GmHousePersistenceCoordinator.ForgetActiveForTests();
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True);
        Assert.That(GmRunStore.HouseRunId, Is.EqualTo(runId));
        Assert.That(GmHousePersistenceCoordinator.TryResume(GmRunStore.HouseRunId,
            out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.ActiveRun.FrozenPackage.CanonicalHash,
            Is.EqualTo(packageHash));
    }

    [Test]
    public void RunPointerSurvivesPresentationUpdatesAndFreshRunClearsOldPointer()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(267,
            out string error), Is.True, error);
        string runId = GmRunStore.HouseRunId;
        var rulesHost = new GameObject("PointerPresentationFixture");
        try
        {
            var rules = rulesHost.AddComponent<GmParlorRules>();
            rules.InitializeOrRestore();
            Assert.That(GmRunStore.TrySetParlorPresentationState(
                GmRunStore.GetParlorPresentationState(), out error), Is.True, error);
            Assert.That(GmRunStore.HouseRunId, Is.EqualTo(runId));
        }
        finally { Object.DestroyImmediate(rulesHost); }

        GmRunStore.BeginNewRun();
        Assert.That(GmRunStore.HouseRunId, Is.Empty);
    }

    [Test]
    public void ParlorDealsFromCampaignFrozenPackageInsteadOfRerolling()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(313,
            out string error), Is.True, error);
        var host = new GameObject("ParlorHouseBindingFixture");
        try
        {
            GmRunStore.ClearParlorMatch();
            var rules = host.AddComponent<GmParlorRules>();
            rules.InitializeOrRestore();
            Assert.That(rules.Match, Is.Not.Null);
            Assert.That(rules.Match.AdaptivePackage.CanonicalHash,
                Is.EqualTo(GmHousePersistenceCoordinator.ActiveRun.FrozenPackage.CanonicalHash));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void EndingCompletionUsesPreparedProfileAcknowledgedProtocolAndRestartIsIdempotent()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(419,
            out string error), Is.True, error);
        GmHouseRunGeneration run = GmHousePersistenceCoordinator.ActiveRun;
        GmParlorBehaviorAccumulator behavior = Completed(run.FrozenPackage);
        GmRunStore.LastCheckpoint = "ending";

        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(
            GmEndingType.TrueEscape, behavior, out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.ActiveRun.Stage,
            Is.EqualTo(GmHouseRunStage.Acknowledged));
        Assert.That(GmHousePersistenceCoordinator.Profile.Receipts.Count, Is.EqualTo(1));

        string runId = run.Identity.RunId;
        GmHousePersistenceCoordinator.ForgetActiveForTests();
        Assert.That(GmHousePersistenceCoordinator.TryResume(runId, out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.TryRecoverTerminal(out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.Profile.Receipts.Count, Is.EqualTo(1));
    }

    [Test]
    public void ContinueAutoRecoversPreparedReceiptAndAuthoritativeTerminalCheckpoint()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(467,
            out string error), Is.True, error);
        GmHouseRunGeneration run = GmHousePersistenceCoordinator.ActiveRun;
        Assert.That(GmSaveSystem.Save(), Is.True, GmSaveSystem.LastError);

        var directStore = new GmHouseMemoryStore(Path.Combine(directory, "house"));
        Assert.That(directStore.TryOpenExisting(out _, out error), Is.True, error);
        var protocol = new GmHouseTerminalProtocol(directStore)
            { FaultAfter = GmHouseTerminalFault.AfterPreparedRunCommit };
        var checkpoint = GmRunStore.ToSaveData();
        checkpoint.currentSceneId = "labyrinth";
        checkpoint.lastCheckpoint = "ending";
        checkpoint.corruptionTier = 4;
        Assert.That(protocol.TryComplete(run, GmEndingType.CorruptedHost,
            Completed(run.FrozenPackage), checkpoint, out _, out error), Is.False);

        GmHousePersistenceCoordinator.ForgetActiveForTests();
        GmRunStore.BeginNewRun();
        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.LastCheckpoint, Is.Not.EqualTo("ending"));
        Assert.That(GmHousePersistenceCoordinator.TryResume(GmRunStore.HouseRunId,
            out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.ActiveRun.Stage,
            Is.EqualTo(GmHouseRunStage.Acknowledged));
        Assert.That(GmRunStore.LastCheckpoint, Is.EqualTo("ending"));
        Assert.That(GmRunStore.CorruptionTier, Is.EqualTo(4));
        Assert.That(GmHousePersistenceCoordinator.Profile.Receipts, Has.Count.EqualTo(1));
    }

    [Test]
    public void AcknowledgedEndingCanRetryAfterMutableContinueWriteFails()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(477,
            out string error), Is.True, error);
        GmHouseRunGeneration run = GmHousePersistenceCoordinator.ActiveRun;
        GmRunStore.LastCheckpoint = "ending";
        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.TrueEscape,
            Completed(run.FrozenPackage), out error), Is.True, error);

        GmSaveSystem.ConfigureForTests(runPath, new AlwaysFailBackend());
        LogAssert.Expect(LogType.Error,
            "[GmSaveSystem] Failed to save game: injected mutable Continue write failure");
        Assert.That(GmSaveSystem.SaveGame(), Is.False);
        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.TrueEscape,
            Completed(run.FrozenPackage), out error), Is.True, error);
        GmSaveSystem.ConfigureForTests(runPath);
        Assert.That(GmSaveSystem.SaveGame(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmHousePersistenceCoordinator.Profile.Receipts, Has.Count.EqualTo(1));
    }

    [Test]
    public void PreGateRetreatAbandonsRunDeletesContinueAndNeverTeaches()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(487,
            out string error), Is.True, error);
        string runId = GmHousePersistenceCoordinator.ActiveRun.Identity.RunId;
        Assert.That(GmSaveSystem.SaveGame(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmHousePersistenceCoordinator.TryAbandonActiveRun(out error),
            Is.True, error);
        Assert.That(GmSaveSystem.DeleteSave(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HouseRunId, Is.Empty);
        Assert.That(GmSaveSystem.HasSave(), Is.False);
        Assert.That(GmHousePersistenceCoordinator.Profile.Receipts, Is.Empty);
        Assert.That(GmHousePersistenceCoordinator.TryResume(runId, out error), Is.False);
        StringAssert.Contains("abandoned", error.ToLowerInvariant());
    }

    [Test]
    public void MalformedOrFutureHousePointerRefusesSaveLoad()
    {
        Assert.Throws<InvalidDataException>(() => GmRunStore.LoadFromSaveData(new GmSaveData
        {
            houseRunPointerVersion = 2,
            houseRunId = new string('a', 32),
        }));
        Assert.Throws<InvalidDataException>(() => GmRunStore.LoadFromSaveData(new GmSaveData
        {
            houseRunPointerVersion = 1,
            houseRunId = "not-a-run-id",
        }));
    }

    [Test]
    public void ExplicitCombinedResetClearsContinuePointerAndNeverSynthesizesSave()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(521,
            out string error), Is.True, error);
        Assert.That(GmSaveSystem.Save(), Is.True);
        Assert.That(GmSaveSystem.HasSave(), Is.True);

        Assert.That(GmHousePersistenceCoordinator.TryResetHouseMemory(
            explicitCombinedScope: true, out error), Is.True, error);

        Assert.That(GmSaveSystem.HasSave(), Is.False);
        Assert.That(GmRunStore.HouseRunId, Is.Empty);
        Assert.That(GmHousePersistenceCoordinator.ActiveRun, Is.Null);
    }

    [Test]
    public void TitleRowsReachMirrorThenExactKnownRecollectionWithoutCampaignPointer()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(601,
            out string error), Is.True, error);
        GmHouseRunGeneration ordinary = GmHousePersistenceCoordinator.ActiveRun;
        GmRunStore.LastCheckpoint = "ending";
        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.TrueEscape,
            Completed(ordinary.FrozenPackage), out error), Is.True, error);

        var host = new GameObject("HouseModeRowsFixture");
        try
        {
            var menu = host.AddComponent<GmBootMenu>();
            menu.Refresh();
            Assert.That(menu.MirrorAvailable, Is.True);
            menu.MoveFocus(1);
            Assert.That(menu.Focused, Is.EqualTo(GmBootMenu.Row.Mirror));
            Assert.That(menu.Activate(), Is.True);
            Assert.That(GmHousePersistenceCoordinator.ActiveRun.Mode,
                Is.EqualTo(GmParlorAdaptiveMode.Mirror));

            GmHouseRunGeneration mirror = GmHousePersistenceCoordinator.ActiveRun;
            GmRunStore.LastCheckpoint = "ending";
            Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.Madness,
                Completed(mirror.FrozenPackage), out error), Is.True, error);
            menu.Refresh();
            Assert.That(menu.RecollectionPackages.Count, Is.EqualTo(1));
            menu.MoveFocus(1);
            menu.MoveFocus(1);
            Assert.That(menu.Focused, Is.EqualTo(GmBootMenu.Row.Recollection));
            Assert.That(menu.Activate(), Is.True);
            Assert.That(GmHousePersistenceCoordinator.ActiveRun.Mode,
                Is.EqualTo(GmParlorAdaptiveMode.Recollection));
            Assert.That(GmRunStore.HouseRunId, Is.Empty);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void NewRunRetiresPreviousActiveAllocationBeforeReplacingPointer()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(701,
            out string error), Is.True, error);
        string oldRunId = GmHousePersistenceCoordinator.ActiveRun.Identity.RunId;
        Assert.That(GmSaveSystem.SaveGame(), Is.True, GmSaveSystem.LastError);
        var host = new GameObject("ReplaceCampaignFixture");
        try
        {
            var menu = host.AddComponent<GmBootMenu>();
            menu.NewRun();
            string replacement = GmHousePersistenceCoordinator.ActiveRun.Identity.RunId;
            Assert.That(replacement, Is.Not.EqualTo(oldRunId));
            Assert.That(GmRunStore.HouseRunId, Is.EqualTo(replacement));

            GmHousePersistenceCoordinator.ForgetActiveForTests();
            Assert.That(GmHousePersistenceCoordinator.TryResume(oldRunId, out error), Is.False);
            StringAssert.Contains("abandoned", error.ToLowerInvariant());
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void RecollectionActionsCannotAlterCampaignSaveProfileOrdinalOrRestoredRunState()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(801,
            out string error), Is.True, error);
        GmHouseRunGeneration ordinary = GmHousePersistenceCoordinator.ActiveRun;
        GmRunStore.LastCheckpoint = "ending";
        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.TrueEscape,
            Completed(ordinary.FrozenPackage), out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.TryBeginMirrorRun(802, out error),
            Is.True, error);
        GmHouseRunGeneration mirror = GmHousePersistenceCoordinator.ActiveRun;
        GmRunStore.LastCheckpoint = "ending";
        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.Madness,
            Completed(mirror.FrozenPackage), out error), Is.True, error);
        GmHouseKnownPackage known = GmHousePersistenceCoordinator.Profile.KnownMirrorPackages[0];

        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(803, out error),
            Is.True, error);
        Assert.That(GmSaveSystem.SaveGame(), Is.True, GmSaveSystem.LastError);
        byte[] campaignBytes = File.ReadAllBytes(runPath);
        int receiptCount = GmHousePersistenceCoordinator.Profile.Receipts.Count;
        long nextOrdinal = GmHousePersistenceCoordinator.ActiveRun.Identity.RunOrdinal + 1;

        Assert.That(GmHousePersistenceCoordinator.TryBeginRecollection(known.Package,
            known.Binding, out error), Is.True, error);
        Assert.That(GmRunStore.RecordCatch("recollection-only-catch"), Is.True);
        Assert.That(GmRunStore.CollectShard(0), Is.True);
        var host = new GameObject("RecollectionPersistenceFixture");
        try
        {
            var rules = host.AddComponent<GmParlorRules>();
            rules.InitializeOrRestore();
            Assert.That(rules.PlayPlayerCard(0), Is.Not.EqualTo(GmParlorActionError.PersistenceFailed));
            Assert.That(GmSaveSystem.SaveGame(), Is.True);
        }
        finally { Object.DestroyImmediate(host); }

        Assert.That(File.ReadAllBytes(runPath), Is.EqualTo(campaignBytes));
        Assert.That(GmHousePersistenceCoordinator.Profile.Receipts.Count,
            Is.EqualTo(receiptCount));
        GmHousePersistenceCoordinator.EndRecollectionShell();
        Assert.That(GmRunStore.HasCatch("recollection-only-catch"), Is.False);
        Assert.That(GmRunStore.MirrorShards[0], Is.False);
        Assert.That(GmRunStore.HouseRunId, Is.Not.Empty);

        var reopened = new GmHouseMemoryStore(Path.Combine(directory, "house"));
        Assert.That(reopened.TryOpenExisting(out _, out error), Is.True, error);
        Assert.That(reopened.CurrentRoot.NextRunOrdinal, Is.EqualTo(nextOrdinal));
    }

    [Test]
    public void ReplacementRefusesCorruptCommittedActiveRunWithoutAllocatingAnotherOrdinal()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(901,
            out string error), Is.True, error);
        string house = Path.Combine(directory, "house");
        string runId = GmHousePersistenceCoordinator.ActiveRun.Identity.RunId;
        string commit = Directory.GetFiles(Path.Combine(house, "runs", runId, "commits"),
            "*.commit").Single();
        byte[] damaged = File.ReadAllBytes(commit);
        damaged[damaged.Length / 2] ^= 0x44;
        File.WriteAllBytes(commit, damaged);
        string[] rootCommits = Directory.GetFiles(Path.Combine(house, "root", "commits"),
            "*.commit");
        byte[][] rootBytes = rootCommits.Select(File.ReadAllBytes).ToArray();

        Assert.That(GmHousePersistenceCoordinator.TryRetireActiveRunForReplacement(
            out error), Is.False);
        StringAssert.Contains("run", error.ToLowerInvariant());
        Assert.That(Directory.GetFiles(Path.Combine(house, "root", "commits"),
            "*.commit"), Is.EqualTo(rootCommits));
        for (int index = 0; index < rootCommits.Length; index++)
            Assert.That(File.ReadAllBytes(rootCommits[index]), Is.EqualTo(rootBytes[index]));
        Assert.That(GmHousePersistenceCoordinator.ActiveRun.Identity.RunId,
            Is.EqualTo(runId));
    }

    [Test]
    public void UnreadableHouseDomainRefusesPersistentNewRunButIsolatedOrdinaryDoesNotTeach()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(1001,
            out string error), Is.True, error);
        string house = Path.Combine(directory, "house");
        string commit = NewestRootCommit(house);
        File.WriteAllBytes(commit, UnreadableEnvelopeBytes());
        GmHousePersistenceCoordinator.ForgetActiveForTests();

        Assert.That(GmHousePersistenceCoordinator.TryGetTitleUnlocks(
            out bool mirror, out _, out error), Is.False);
        Assert.That(GmHousePersistenceCoordinator.HouseRecoveryRequired, Is.True);
        Assert.That(mirror, Is.False);
        StringAssert.Contains("magic", error.ToLowerInvariant());
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(1002,
            out error), Is.False);
        Assert.That(File.Exists(runPath), Is.False);

        Assert.That(GmHousePersistenceCoordinator.TryBeginIsolatedOrdinaryRun(1003,
            out error), Is.True, error);
        GmHouseRunGeneration isolated = GmHousePersistenceCoordinator.ActiveRun;
        Assert.That(isolated.Mode, Is.EqualTo(GmParlorAdaptiveMode.Ordinary));
        Assert.That(isolated.IsolatedRecovery, Is.True);
        Assert.That(isolated.CanTeachProfile, Is.False);
        Assert.That(GmRunStore.HouseRunId, Is.Empty);
        Assert.That(Directory.Exists(house + ".quarantine-" + isolated.Identity.LineageId),
            Is.False, "isolated New Run quarantined House files without authorization");

        GmRunStore.LastCheckpoint = "ending";
        Assert.That(GmHousePersistenceCoordinator.TryCompleteEnding(GmEndingType.TrueEscape,
            Completed(isolated.FrozenPackage), out error), Is.True, error);
        Assert.That(GmHousePersistenceCoordinator.TryGetTitleUnlocks(
            out mirror, out _, out error), Is.False);
        Assert.That(mirror, Is.False);
        Assert.That(GmHousePersistenceCoordinator.HouseRecoveryRequired, Is.True);
    }

    [Test]
    public void BootRecoveryNewRunStaysIsolatedAndConfirmedResetStartsANewLineage()
    {
        Assert.That(GmHousePersistenceCoordinator.TryBeginOrdinaryRun(1101,
            out string error), Is.True, error);
        string house = Path.Combine(directory, "house");
        string originalLineage = GmHousePersistenceCoordinator.Profile.LineageId;
        string commit = NewestRootCommit(house);
        byte[] garbage = UnreadableEnvelopeBytes();
        File.WriteAllBytes(commit, garbage);
        Assert.That(GmSaveSystem.Save(), Is.True);
        GmHousePersistenceCoordinator.ForgetActiveForTests();

        var host = new GameObject("HouseRecoveryMenuFixture");
        try
        {
            var menu = host.AddComponent<GmBootMenu>();
            menu.Refresh();
            Assert.That(menu.HouseRecoveryRequired, Is.True);
            Assert.That(menu.MirrorAvailable, Is.False);
            Assert.That(menu.ContinueAvailable, Is.False);
            Assert.That(menu.Focused, Is.EqualTo(GmBootMenu.Row.NewRun));
            menu.MoveFocus(1);
            Assert.That(menu.Focused, Is.EqualTo(GmBootMenu.Row.ResetHouseMemory));
            menu.MoveFocus(-1);
            Assert.That(menu.Activate(), Is.True);
            Assert.That(GmHousePersistenceCoordinator.ActiveRun.IsolatedRecovery, Is.True);
            Assert.That(GmRunStore.HouseRunId, Is.Empty);
            Assert.That(File.ReadAllBytes(commit), Is.EqualTo(garbage));

            menu.Refresh();
            menu.MoveFocus(1);
            Assert.That(menu.Activate(), Is.True,
                "first confirm should arm reset, not mutate the domain yet");
            Assert.That(File.ReadAllBytes(commit), Is.EqualTo(garbage));
            Assert.That(menu.Activate(), Is.True);
            Assert.That(menu.HouseRecoveryRequired, Is.False);
            Assert.That(GmHousePersistenceCoordinator.Profile.LineageId,
                Is.Not.EqualTo(originalLineage));
            Assert.That(GmSaveSystem.HasSave(), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    static byte[] UnreadableEnvelopeBytes()
    {
        var bytes = new byte[128];
        System.Text.Encoding.ASCII.GetBytes("NOTHOUSE").CopyTo(bytes, 0);
        return bytes;
    }

    static string NewestRootCommit(string house) =>
        Directory.GetFiles(Path.Combine(house, "root", "commits"), "*.commit")
            .OrderBy(path => path, StringComparer.Ordinal).Last();

    static GmParlorBehaviorAccumulator Completed(GmParlorAdaptivePackage package)
    {
        var accumulator = new GmParlorBehaviorAccumulator(package);
        accumulator.RecordPlayerLead(new GmCard(GmSuit.Bones, 7), 1, 7);
        accumulator.RecordAccept(GmTellObservation.Calm);
        accumulator.SealCompletedMatch(package, 1, 0);
        return accumulator;
    }

    sealed class AlwaysFailBackend : IGmAtomicSaveBackend
    {
        public void WriteAtomic(string target,string json) =>
            throw new IOException("injected mutable Continue write failure");
    }
}
