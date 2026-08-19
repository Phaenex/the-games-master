using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class GmHouseMemoryTests
{
    string directory;

    [SetUp]
    public void SetUp() => directory = Path.Combine(Path.GetTempPath(),
        "gm-house-memory-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        string parent = Path.GetDirectoryName(directory);
        string name = Path.GetFileName(directory);
        if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
        {
            foreach (string sibling in Directory.GetDirectories(parent, name + ".*"))
                Directory.Delete(sibling, true);
            foreach (string sibling in Directory.GetFiles(parent, name + ".*"))
                File.Delete(sibling);
        }
    }

    [Test]
    public void ReceiptCodecHasFrozenDomainSeparatedBigEndianGoldenVectors()
    {
        var identity = new GmHouseRunIdentity(
            "00112233445566778899aabbccddeeff", 7,
            "ffeeddccbbaa99887766554433221100", 42);
        Assert.That(GmHouseReceiptCodec.ComputeReceiptId(identity, 1), Is.EqualTo(
            "c0115d3966a021c173a17c8b6f854d36d77460be6c121f5a1bfc0f7b566f9868"));
        Assert.That(GmHouseReceiptCodec.CanonicalIdentityBytes(identity, 1), Is.EqualTo(Hex(
            "54474d2f484f5553452f524543454950542d49442f563100" +
            "000000203030313132323333343435353636373738383939616162626363646465656666" +
            "0000000000000007" +
            "000000206666656564646363626261613939383837373636353534343333323231313030" +
            "000000000000002a0000000000000001")));
    }

    [Test]
    public void FullReceiptPayloadHasFrozenGoldenLengthAndDigest()
    {
        GmParlorAdaptivePackage package = GmHouseModeDirector.FreezeOrdinary(99);
        var binding = new GmHousePackageBinding();
        SetProperty(binding, "LineageId", "00112233445566778899aabbccddeeff");
        SetProperty(binding, "Epoch", 7L);
        SetProperty(binding, "ProfileGeneration", 0L);
        SetProperty(binding, "ProfileDigest", new byte[32]);
        SetProperty(binding, "HistoryDigest", new byte[32]);
        SetProperty(binding, "PackageHash", package.CanonicalHash);
        SetProperty(binding, "SelectedReceiptIds", Array.Empty<string>());
        SetProperty(binding, "ConsultedProfile", false);
        var receipt = new GmHouseTerminalReceipt();
        SetReceiptProperty(receipt, "LineageId", "00112233445566778899aabbccddeeff");
        SetReceiptProperty(receipt, "Epoch", 7L);
        SetReceiptProperty(receipt, "RunId", "ffeeddccbbaa99887766554433221100");
        SetReceiptProperty(receipt, "RunOrdinal", 42L);
        SetReceiptProperty(receipt, "OutcomeSequence", 1L);
        SetReceiptProperty(receipt, "Mode", GmParlorAdaptiveMode.Ordinary);
        SetReceiptProperty(receipt, "Ending", GmEndingType.TrueEscape);
        SetReceiptProperty(receipt, "ContributesToLearning", true);
        SetReceiptProperty(receipt, "FrozenPackage", package);
        SetReceiptProperty(receipt, "PackageBinding", binding);
        SetReceiptProperty(receipt, "Summary", CompletedAccumulator(package)
            .CreateCompletedRunSummary());

        byte[] payload = GmHouseReceiptCodec.EncodeCanonicalPayload(receipt);
        string digest = string.Concat(GmHouseReceiptCodec.ComputePayloadHash(receipt)
            .Select(value => value.ToString("x2")));
        Assert.That(payload.Length, Is.EqualTo(631));
        Assert.That(digest, Is.EqualTo(
            "4b5de69cc4dd8dc61b34836da973f4e6902e34ddf6157eb87aad88b1dc294526"));
        Assert.That(Convert.ToBase64String(payload), Is.EqualTo(
            "VEdNL0hPVVNFL1JFQ0VJUFQtUEFZTE9BRC9WMQAAAAABAAAAIDAwMTEyMjMzNDQ1NTY2Nzc4ODk5YWFiYmNjZGRlZWZmAAAAAAAAAAcAAAAgZmZlZWRkY2NiYmFhOTk4ODc3NjY1NTQ0MzMyMjExMDAAAAAAAAAAKgAAAAAAAAABAAAAAAAAAAUBAAAAaAAAAAIAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAqQAAAAEAAAAgMDAxMTIyMzM0NDU1NjY3Nzg4OTlhYWJiY2NkZGVlZmYAAAAAAAAABwAAAAAAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIKtaVEjeoLJ6hl2J+ETFDa8wqggMKiRn6qHpmwJN/KTIAAAAAAAAAADQAAAAAgAAAAEAAAABAAAAAQAAAAAAAAABAAAAAAAAAAAAAAABAAAAAAAAAAAAAAABAAAAAAAAAAAAAAABAAAAAAAAAAEAAAAAAAAAAAAAAAEAAAABAAAAAQAAAAQAAAABAAAAAAAAAAAAAAAAAAAABAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAQAAAAAAAAADAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAACCrWlRI3qCyeoZdifhExQ2vMKoIDCokZ+qh6ZsCTfykyA=="));
    }

    [Test]
    public void EmptyDomainCreatesGenesisButMissingRootInNonemptyDomainInhibitsWrites()
    {
        var store = new GmHouseMemoryStore(directory);
        Assert.That(store.TryOpenOrCreate(out GmHouseProfileGeneration profile,
            out string error), Is.True, error);
        Assert.That(profile.LineageId, Has.Length.EqualTo(32));
        Assert.That(profile.Epoch, Is.EqualTo(1));
        Assert.That(store.RootCommitRecords.Count, Is.EqualTo(1));
        Assert.That(store.ProfileCommitRecords.Count, Is.EqualTo(1));

        Directory.Delete(Path.Combine(directory, "root"), true);
        var reopened = new GmHouseMemoryStore(directory);
        Assert.That(reopened.TryOpenOrCreate(out _, out error), Is.False);
        StringAssert.Contains("root", error.ToLowerInvariant());
        Assert.That(reopened.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary,
            1, out _, out error), Is.False);
    }

    [Test]
    public void InterruptedGenesisResumesOnlyFromExactCommittedEmptyRoot()
    {
        foreach (GmHouseDurabilityPoint point in new[]
                 { GmHouseDurabilityPoint.RootGeneration,
                   GmHouseDurabilityPoint.RootCommitRecord,
                   GmHouseDurabilityPoint.ProfileGeneration,
                   GmHouseDurabilityPoint.ProfileCommitRecord })
        {
            string root = Path.Combine(directory, point.ToString());
            var faults = new GmHouseFaultInjectingFileSystem(new GmHousePhysicalFileSystem())
                { FailNext = point };
            var interrupted = new GmHouseMemoryStore(root, faults);
            Assert.That(interrupted.TryOpenOrCreate(out _, out string error), Is.False);

            var restarted = new GmHouseMemoryStore(root);
            Assert.That(restarted.TryOpenOrCreate(out GmHouseProfileGeneration profile,
                out error), Is.True, error);
            Assert.That(profile.Generation, Is.EqualTo(1));
            Assert.That(profile.Receipts, Is.Empty);
            Assert.That(restarted.CurrentRoot.NextRunOrdinal, Is.EqualTo(1));
        }
    }

    [Test]
    public void EveryCommittedArtifactUsesBoundedChecksummedBinaryEnvelope()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt receipt = Prepare(store, 3, GmEndingType.TrappedLoop);
        Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
            out _, out string error), Is.True, error);

        string[] artifacts = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".house-memory.lease", StringComparison.Ordinal))
            .ToArray();
        Assert.That(artifacts, Is.Not.Empty);
        foreach (string path in artifacts)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Take(8).ToArray(), Is.EqualTo(System.Text.Encoding.ASCII
                .GetBytes("TGMHOUSE")), $"{path} has no House envelope magic");
            Assert.That(bytes.Length, Is.LessThanOrEqualTo(4 * 1024 * 1024));
        }
    }

    [Test]
    public void OrdinaryFreezesBaselineWithoutProfileInputButAllocatesCommittedIdentity()
    {
        GmParlorAdaptivePackage baseline = GmHouseModeDirector.FreezeOrdinary(4815);
        Assert.That(baseline.provenance,
            Is.EqualTo(GmParlorPackageProvenance.OrdinaryBaseline));
        Assert.That(baseline.historyDigest, Is.All.Zero);

        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 4815,
            out GmHouseRunGeneration run, out string error), Is.True, error);
        Assert.That(run.Identity.RunOrdinal, Is.EqualTo(1));
        Assert.That(run.FrozenPackage.CanonicalHash, Is.EqualTo(baseline.CanonicalHash));
        Assert.That(store.CurrentRoot.Allocations, Has.Count.EqualTo(1));
        Assert.That(store.CurrentRoot.Allocations[0].RunId, Is.EqualTo(run.Identity.RunId));
    }

    [Test]
    public void RootRetainsAllocationsAndNeverReusesOrdinalAcrossReset()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 1,
            out GmHouseRunGeneration abandoned, out string error), Is.True, error);
        Assert.That(store.TryResetHouseMemory(false, true, out _, out error), Is.True, error);
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 2,
            out GmHouseRunGeneration next, out error), Is.True, error);
        Assert.That(next.Identity.RunOrdinal, Is.EqualTo(abandoned.Identity.RunOrdinal + 1));
        Assert.That(store.CurrentRoot.Allocations.Select(item => item.RunOrdinal),
            Is.EqualTo(new[] { abandoned.Identity.RunOrdinal, next.Identity.RunOrdinal }));
        Assert.That(store.CurrentRoot.Epoch, Is.EqualTo(2));
    }

    [Test]
    public void TwoStaleWritersCasOutOfOrderWithoutLostUpdate()
    {
        GmHouseMemoryStore first = OpenStore();
        var stale = new GmHouseMemoryStore(directory);
        Assert.That(stale.TryOpenExisting(out GmHouseProfileGeneration snapshot,
            out string error), Is.True, error);
        GmHouseTerminalReceipt one = Prepare(first, 11, GmEndingType.DefiantSacrifice);
        GmHouseTerminalReceipt two = Prepare(stale, 12, GmEndingType.HostSuccession);
        Assert.That(stale.TryApplyReceipt(two, snapshot.Cas, out _, out error), Is.True, error);
        Assert.That(first.TryApplyReceipt(one, snapshot.Cas,
            out GmHouseProfileGeneration merged, out error), Is.True, error);
        Assert.That(merged.Receipts.Select(item => item.RunOrdinal),
            Is.EqualTo(new long[] { 1, 2 }));
    }

    [Test]
    public void CasRejectsImpossibleFutureOrSameGenerationHashMismatch()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt receipt = Prepare(store, 13, GmEndingType.TrappedLoop);
        GmHouseProfileCas current = store.CurrentProfile.Cas;
        var forgedHash = new GmHouseProfileCas(current.LineageId, current.Epoch,
            current.Generation, new string('0', 64));
        Assert.That(store.TryApplyReceipt(receipt, forgedHash, out _, out string error),
            Is.False);
        StringAssert.Contains("CAS", error);

        var future = new GmHouseProfileCas(current.LineageId, current.Epoch,
            current.Generation + 1, current.GenerationHash);
        Assert.That(store.TryApplyReceipt(receipt, future, out _, out error), Is.False);
        StringAssert.Contains("CAS", error);
        Assert.That(store.CurrentProfile.Receipts, Is.Empty);
    }

    [Test]
    public void DuplicateIsIdempotentAndIdentityCollisionInhibits()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 19,
            out GmHouseRunGeneration run, out string error), Is.True, error);
        GmParlorBehaviorAccumulator behavior = CompletedAccumulator(run.FrozenPackage);
        Assert.That(store.TryCreateReceipt(run, GmEndingType.TrappedLoop, behavior,
            out GmHouseTerminalReceipt receipt, out error), Is.True, error);
        Assert.That(store.TryCommitPreparedRun(run, receipt, new GmSaveData
        { currentSceneId = "labyrinth", lastCheckpoint = "ending",
            houseRunId = run.Identity.RunId }, out _, out error), Is.True, error);
        Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
            out GmHouseProfileGeneration once, out error), Is.True, error);
        Assert.That(store.TryApplyReceipt(receipt, once.Cas,
            out GmHouseProfileGeneration twice, out error), Is.True, error);
        Assert.That(twice.Cas, Is.EqualTo(once.Cas));

        Assert.That(store.TryCreateReceipt(run, GmEndingType.TrueEscape, behavior,
            out GmHouseTerminalReceipt collision, out error), Is.True, error);
        Assert.That(collision.ReceiptId, Is.EqualTo(receipt.ReceiptId));
        Assert.That(collision.PayloadHash, Is.Not.EqualTo(receipt.PayloadHash));
        Assert.That(store.TryApplyReceipt(collision, twice.Cas, out _, out error), Is.False);
        StringAssert.Contains("different payload", error);
    }

    [Test]
    public void TerminalCheckpointPreservesExplicitEmptyBonesEvidenceAsCorrupt()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 21,
            out GmHouseRunGeneration run, out string error), Is.True, error);
        GmParlorBehaviorAccumulator behavior = CompletedAccumulator(run.FrozenPackage);
        Assert.That(store.TryCreateReceipt(run, GmEndingType.TrappedLoop, behavior,
            out GmHouseTerminalReceipt receipt, out error), Is.True, error);
        GmBonesMatchSnapshot corrupt = new GmBonesMatch(23UL, null).ExportSnapshot();
        corrupt.interventionReceipt = new GmBonesInterventionReceipt();
        var checkpoint = new GmSaveData
        {
            currentSceneId = "labyrinth",
            lastCheckpoint = "ending",
            houseRunId = run.Identity.RunId,
            houseRunPointerVersion = 1,
            bonesMatch = corrupt
        };
        Assert.That(store.TryCommitPreparedRun(run, receipt, checkpoint,
            out _, out error), Is.True, error);

        var restarted = new GmHouseMemoryStore(directory);
        Assert.That(restarted.TryOpenExisting(out _, out error), Is.True, error);
        var recovery = new GmHouseTerminalProtocol(restarted);
        Assert.That(recovery.TryRecover(run.Identity.RunId,
            out GmHouseRunGeneration loaded, out error), Is.True, error);
        GmRunStore.LoadFromSaveData(loaded.TerminalCheckpoint);
        Assert.That(GmRunStore.HasBonesMatch, Is.True);
        Assert.That(GmRunStore.BonesRestoreError, Is.Not.Empty);
    }

    [Test]
    public void TerminalCheckpointPreservesValidModernBonesEnvelope()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 22,
            out GmHouseRunGeneration run, out string error), Is.True, error);
        GmParlorBehaviorAccumulator behavior = CompletedAccumulator(run.FrozenPackage);
        Assert.That(store.TryCreateReceipt(run, GmEndingType.TrappedLoop, behavior,
            out GmHouseTerminalReceipt receipt, out error), Is.True, error);
        GmBonesMatchSnapshot snapshot = new GmBonesMatch(29UL, null).ExportSnapshot();
        var checkpoint = new GmSaveData
        {
            currentSceneId = "labyrinth",
            lastCheckpoint = "ending",
            houseRunId = run.Identity.RunId,
            houseRunPointerVersion = 1,
            bonesEnvelopeVersion = 1,
            bonesPayloadPresent = true,
            bonesMatch = snapshot
        };
        Assert.That(store.TryCommitPreparedRun(run, receipt, checkpoint,
            out _, out error), Is.True, error);

        var restarted = new GmHouseMemoryStore(directory);
        Assert.That(restarted.TryOpenExisting(out _, out error), Is.True, error);
        var recovery = new GmHouseTerminalProtocol(restarted);
        Assert.That(recovery.TryRecover(run.Identity.RunId,
            out GmHouseRunGeneration loaded, out error), Is.True, error);
        GmRunStore.LoadFromSaveData(loaded.TerminalCheckpoint);
        Assert.That(GmRunStore.BonesRestoreError, Is.Empty);
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint,
            Is.EqualTo(snapshot.stateFingerprint));
    }

    [TestCase("payload-false-turn-bit")]
    [TestCase("payload-false-session-bit")]
    [TestCase("payload-false-outer-present")]
    [TestCase("payload-true-outer-absent")]
    [TestCase("turn-true-object-absent")]
    [TestCase("turn-false-object-present")]
    [TestCase("session-true-object-absent")]
    [TestCase("session-false-object-present")]
    public void TerminalCheckpointRejectsModernBonesEnvelopeContradictions(string scenario)
    {
        string root = Path.Combine(directory, scenario);
        var store = new GmHouseMemoryStore(root);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 24,
            out GmHouseRunGeneration run, out error), Is.True, error);
        GmParlorBehaviorAccumulator behavior = CompletedAccumulator(run.FrozenPackage);
        Assert.That(store.TryCreateReceipt(run, GmEndingType.TrappedLoop, behavior,
            out GmHouseTerminalReceipt receipt, out error), Is.True, error);
        GmSaveData checkpoint = ContradictoryBonesEnvelope(scenario);
        checkpoint.currentSceneId = "labyrinth";
        checkpoint.lastCheckpoint = "ending";
        checkpoint.houseRunId = run.Identity.RunId;
        checkpoint.houseRunPointerVersion = 1;

        Assert.That(store.TryCommitPreparedRun(run, receipt, checkpoint,
            out _, out error), Is.False);
        StringAssert.Contains("envelope", error.ToLowerInvariant());
    }

    static GmSaveData ContradictoryBonesEnvelope(string scenario)
    {
        var data = new GmSaveData
        {
            bonesEnvelopeVersion = 1,
            bonesPayloadPresent = true,
            bonesMatch = new GmBonesMatch(37UL, null).ExportSnapshot()
        };
        switch (scenario)
        {
            case "payload-false-turn-bit":
                data.bonesPayloadPresent = false;
                data.bonesTurnEvidencePresent = true;
                data.bonesMatch = null;
                break;
            case "payload-false-session-bit":
                data.bonesPayloadPresent = false;
                data.bonesSessionEventPresent = true;
                data.bonesMatch = null;
                break;
            case "payload-false-outer-present":
                data.bonesPayloadPresent = false;
                break;
            case "payload-true-outer-absent":
                data.bonesMatch = null;
                break;
            case "turn-true-object-absent":
                data.bonesTurnEvidencePresent = true;
                break;
            case "turn-false-object-present":
                data.bonesMatch = PendingBonesIntervention();
                data.bonesSessionEventPresent = true;
                break;
            case "session-true-object-absent":
                data.bonesSessionEventPresent = true;
                break;
            case "session-false-object-present":
                data.bonesMatch = PendingBonesIntervention();
                data.bonesTurnEvidencePresent = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
        return data;
    }

    static GmBonesMatchSnapshot PendingBonesIntervention()
    {
        int[] dice = { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2 };
        var match = new GmBonesMatch(3UL, dice);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        match.TryChoose(GmBonesChoice.Bank, -1, out _);
        return match.ExportSnapshot();
    }

    [Test]
    public void OutcomeSequenceOtherThanOneIsRejectedBeforeProfileMutation()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt receipt = Prepare(store, 23, GmEndingType.TrappedLoop);
        SetReceiptProperty(receipt, "OutcomeSequence", 2L);
        SetReceiptProperty(receipt, "ReceiptId", GmHouseReceiptCodec.ComputeReceiptId(
            new GmHouseRunIdentity(receipt.LineageId, receipt.Epoch, receipt.RunId,
                receipt.RunOrdinal), 2));
        SetReceiptProperty(receipt, "PayloadHash",
            GmHouseReceiptCodec.ComputePayloadHash(receipt));

        Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
            out _, out string error), Is.False);
        StringAssert.Contains("receipt", error.ToLowerInvariant());
        Assert.That(store.CurrentProfile.Receipts, Is.Empty);
    }

    [Test]
    public void TerminalProtocolRecoversEveryCrashWindowExactlyOnce()
    {
        foreach (GmHouseTerminalFault fault in new[]
                 { GmHouseTerminalFault.AfterPreparedRunCommit,
                   GmHouseTerminalFault.AfterProfileCommit,
                   GmHouseTerminalFault.AfterAcknowledgedRunCommit })
        {
            string root = Path.Combine(directory, fault.ToString());
            var store = new GmHouseMemoryStore(root);
            Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
            Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 31,
                out GmHouseRunGeneration run, out error), Is.True, error);
            var protocol = new GmHouseTerminalProtocol(store) { FaultAfter = fault };
            var checkpoint = new GmSaveData { currentSceneId = "ending", lastCheckpoint = "ending",
                houseRunId = run.Identity.RunId, corruptionTier = 4 };
            Assert.That(protocol.TryComplete(run, GmEndingType.DefiantSacrifice,
                CompletedAccumulator(run.FrozenPackage), checkpoint, out _, out error), Is.False);

            var restarted = new GmHouseMemoryStore(root);
            Assert.That(restarted.TryOpenExisting(out _, out error), Is.True, error);
            var recovery = new GmHouseTerminalProtocol(restarted);
            Assert.That(recovery.TryRecover(run.Identity.RunId,
                out GmHouseRunGeneration acknowledged, out error), Is.True, error);
            Assert.That(acknowledged.Stage, Is.EqualTo(GmHouseRunStage.Acknowledged));
            Assert.That(acknowledged.TerminalCheckpoint.currentSceneId, Is.EqualTo("ending"));
            Assert.That(acknowledged.TerminalCheckpoint.corruptionTier, Is.EqualTo(4));
            Assert.That(acknowledged.AcknowledgedProfileGeneration, Is.GreaterThanOrEqualTo(2));
            Assert.That(acknowledged.AcknowledgedProfileGenerationHash, Has.Length.EqualTo(64));
            Assert.That(acknowledged.AcknowledgedProfileCommitHash, Has.Length.EqualTo(64));
            Assert.That(restarted.CurrentProfile.Receipts, Has.Count.EqualTo(1));
            Assert.That(recovery.TryRecover(run.Identity.RunId, out var again, out error),
                Is.True, error);
            Assert.That(again.Generation, Is.EqualTo(acknowledged.Generation));
        }
    }

    [Test]
    public void AbandonCannotDiscardPreparedTerminalReceipt()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 37,
            out GmHouseRunGeneration run, out string error), Is.True, error);
        var protocol = new GmHouseTerminalProtocol(store)
            { FaultAfter = GmHouseTerminalFault.AfterPreparedRunCommit };
        Assert.That(protocol.TryComplete(run, GmEndingType.TrappedLoop,
            CompletedAccumulator(run.FrozenPackage), new GmSaveData
            { currentSceneId = "ending", lastCheckpoint = "ending", houseRunId = run.Identity.RunId },
            out _, out error), Is.False);
        Assert.That(store.TryAbandonRun(run.Identity.RunId, out error), Is.False);
        StringAssert.Contains("prepared", error.ToLowerInvariant());
    }

    [Test]
    public void PreparedRunRejectsReceiptFromAnotherAllocatedRun()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 701,
            out GmHouseRunGeneration first, out string error), Is.True, error);
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 702,
            out GmHouseRunGeneration second, out error), Is.True, error);
        Assert.That(store.TryCreateReceipt(second, GmEndingType.TrueEscape,
            CompletedAccumulator(second.FrozenPackage), out GmHouseTerminalReceipt foreign,
            out error), Is.True, error);

        Assert.That(store.TryCommitPreparedRun(first, foreign,
            new GmSaveData { currentSceneId = "ending", lastCheckpoint = "ending",
                houseRunId = first.Identity.RunId }, out _, out error), Is.False);
        StringAssert.Contains("receipt", error.ToLowerInvariant());
    }

    [Test]
    public void RootAllocationRetriesAfterGenerationRenameBeforeCommitWithoutOrdinalReuse()
    {
        var fs = new GmHouseFaultInjectingFileSystem(new GmHousePhysicalFileSystem());
        var store = new GmHouseMemoryStore(directory, fs);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        fs.FailArtifact = GmHouseDurabilityPoint.RootCommitRecord;
        fs.FailNextEdge = GmHouseDurabilityEdge.TempCreate;
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 801,
            out _, out error), Is.False);

        var restarted = new GmHouseMemoryStore(directory);
        Assert.That(restarted.TryOpenExisting(out _, out error), Is.True, error);
        Assert.That(restarted.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 802,
            out GmHouseRunGeneration run, out error), Is.True, error);
        Assert.That(run.Identity.RunOrdinal, Is.EqualTo(1));
        Assert.That(restarted.CurrentRoot.Allocations, Has.Count.EqualTo(1));
    }

    [Test]
    public void MissingPredecessorGenerationInhibitsCommitChainInsteadOfTrustingHeadOnly()
    {
        GmHouseMemoryStore store = OpenStore();
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 901,
            out _, out string error), Is.True, error);
        string oldest = Directory.GetFiles(Path.Combine(directory, "root", "generations"),
            "*.bin").OrderBy(path => path, StringComparer.Ordinal).First();
        File.Delete(oldest);

        var reopened = new GmHouseMemoryStore(directory);
        Assert.That(reopened.TryOpenExisting(out _, out error), Is.False);
        StringAssert.Contains("generation", error.ToLowerInvariant());
    }

    [Test]
    public void RecollectionAllocatesNothingRequiresExactKnownPackageAndNeverTeaches()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt learned = Prepare(store, 41,
            GmEndingType.DefiantSacrifice, GmParlorAdaptiveMode.Mirror);
        Assert.That(store.TryApplyReceipt(learned, store.CurrentProfile.Cas,
            out GmHouseProfileGeneration profile, out string error), Is.True, error);
        long nextOrdinal = store.CurrentRoot.NextRunOrdinal;

        Assert.That(store.TryBeginRecollection(learned.FrozenPackage,
            learned.PackageBinding, out GmHouseRunGeneration recollection,
            out error), Is.True, error);
        Assert.That(recollection.Identity.RunOrdinal, Is.Zero);
        Assert.That(recollection.CanTeachProfile, Is.False);
        Assert.That(store.CurrentRoot.NextRunOrdinal, Is.EqualTo(nextOrdinal));
        GmParlorAdaptivePackage forged = learned.FrozenPackage.DeepCopy();
        forged.fallbackReason += "-forged";
        Assert.That(store.TryBeginRecollection(forged, learned.PackageBinding,
            out _, out error), Is.False);
        StringAssert.Contains("known", error.ToLowerInvariant());
        Assert.That(profile.KnownMirrorPackages.Count, Is.EqualTo(1));
    }

    [Test]
    public void MadnessUnlocksMirrorButCannotEnterAdaptiveHistory()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt hollow = Prepare(store, 51, GmEndingType.Madness);
        Assert.That(store.TryApplyReceipt(hollow, store.CurrentProfile.Cas,
            out GmHouseProfileGeneration profile, out string error), Is.True, error);
        Assert.That(profile.MirrorUnlocked, Is.True);
        Assert.That(profile.AdaptiveReceipts, Is.Empty);
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Mirror, 52,
            out GmHouseRunGeneration mirror, out error), Is.True, error);
        Assert.That(mirror.FrozenPackage.attentionTier, Is.Zero);
        Assert.That(mirror.PackageBinding.SelectedReceiptIds, Is.Empty);
    }

    [Test]
    public void FirstMirrorMadnessRetainsExactPackageForRecollectionWithoutTeaching()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt mirrorHollow = Prepare(store, 56, GmEndingType.Madness,
            GmParlorAdaptiveMode.Mirror);
        Assert.That(store.TryApplyReceipt(mirrorHollow, store.CurrentProfile.Cas,
            out GmHouseProfileGeneration profile, out string error), Is.True, error);
        Assert.That(profile.AdaptiveReceipts.Select(item => item.ReceiptId),
            Does.Not.Contain(mirrorHollow.ReceiptId));
        Assert.That(profile.KnownMirrorPackages.Any(item =>
            item.Package.CanonicalHash.SequenceEqual(mirrorHollow.FrozenPackage.CanonicalHash)),
            Is.True);
        Assert.That(store.TryBeginRecollection(mirrorHollow.FrozenPackage,
            mirrorHollow.PackageBinding, out _, out error), Is.True, error);
    }

    [Test]
    public void ResetResumesPendingPhaseAndRejectsOldEpochReceipt()
    {
        var fs = new GmHouseFaultInjectingFileSystem(new GmHousePhysicalFileSystem());
        var store = new GmHouseMemoryStore(directory, fs);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        GmHouseTerminalReceipt old = Prepare(store, 61, GmEndingType.TrappedLoop);
        fs.FailNext = GmHouseDurabilityPoint.ProfileCommitRecord;
        Assert.That(store.TryResetHouseMemory(false, true, out _, out error), Is.False);
        Assert.That(store.CurrentRoot.ResetPending, Is.True);

        var restarted = new GmHouseMemoryStore(directory);
        Assert.That(restarted.TryResumePendingReset(out GmHouseProfileGeneration reset,
            out error), Is.True, error);
        Assert.That(reset.Epoch, Is.EqualTo(old.Epoch + 1));
        Assert.That(restarted.CurrentRoot.ResetPending, Is.False);
        Assert.That(restarted.TryApplyReceipt(old, reset.Cas, out _, out error), Is.False);
        StringAssert.Contains("epoch", error.ToLowerInvariant());
    }

    [Test]
    public void OrdinaryProductionOpenAutomaticallyFinishesPendingReset()
    {
        var fs = new GmHouseFaultInjectingFileSystem(new GmHousePhysicalFileSystem());
        var store = new GmHouseMemoryStore(directory, fs);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        fs.FailNext = GmHouseDurabilityPoint.ProfileCommitRecord;
        Assert.That(store.TryResetHouseMemory(false, true, out _, out error), Is.False);

        var restarted = new GmHouseMemoryStore(directory);
        Assert.That(restarted.TryOpenExisting(out GmHouseProfileGeneration profile,
            out error), Is.True, error);
        Assert.That(restarted.CurrentRoot.ResetPending, Is.False);
        Assert.That(profile.Epoch, Is.EqualTo(restarted.CurrentRoot.Epoch));
        Assert.That(restarted.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 66,
            out _, out error), Is.True, error);
    }

    [Test]
    public void CorruptNewestCommitInhibitsInsteadOfRollingBack()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt receipt = Prepare(store, 71, GmEndingType.TrappedLoop);
        Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
            out _, out string error), Is.True, error);
        string newest = store.ProfileCommitRecords.Last();
        byte[] bytes = File.ReadAllBytes(newest);
        bytes[bytes.Length / 2] ^= 0x5a;
        File.WriteAllBytes(newest, bytes);
        var reopened = new GmHouseMemoryStore(directory);
        Assert.That(reopened.TryOpenExisting(out _, out error), Is.False);
        StringAssert.Contains("profile", error.ToLowerInvariant());
    }

    [Test]
    public void ProfileReopensReceiptBlobAndCrossValidatesEffectsBeforeCommit()
    {
        var fs = new GmHouseFaultInjectingFileSystem(new GmHousePhysicalFileSystem());
        var store = new GmHouseMemoryStore(directory, fs);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        GmHouseTerminalReceipt receipt = Prepare(store, 81, GmEndingType.TrueEscape);
        fs.MutateNextReceiptBlobAfterWrite = true;
        Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
            out _, out error), Is.False);
        StringAssert.Contains("receipt blob", error.ToLowerInvariant());
        Assert.That(store.CurrentProfile.Receipts, Is.Empty);
    }

    [Test]
    public void EveryDurableWriteEdgeRecoversPriorOrNewGenerationNeverHybrid()
    {
        foreach (GmHouseDurabilityEdge edge in new[]
                 {
                     GmHouseDurabilityEdge.TempCreate,
                     GmHouseDurabilityEdge.PayloadWrite,
                     GmHouseDurabilityEdge.PayloadFlush,
                     GmHouseDurabilityEdge.AtomicRename,
                     GmHouseDurabilityEdge.DirectoryFlush,
                     GmHouseDurabilityEdge.ReopenValidation,
                     GmHouseDurabilityEdge.LeaseRelease,
                 })
        {
            string root = Path.Combine(directory, "edge-" + edge);
            var fs = new GmHouseFaultInjectingFileSystem(new GmHousePhysicalFileSystem());
            var store = new GmHouseMemoryStore(root, fs);
            Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
            GmHouseTerminalReceipt receipt = Prepare(store, 91, GmEndingType.TrappedLoop);
            fs.FailArtifact = GmHouseDurabilityPoint.ProfileCommitRecord;
            fs.FailNextEdge = edge;

            Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
                out _, out error), Is.False, $"{edge} was not reached");

            var restarted = new GmHouseMemoryStore(root);
            Assert.That(restarted.TryOpenExisting(out GmHouseProfileGeneration priorOrNew,
                out error), Is.True, error);
            Assert.That(priorOrNew.Receipts.Count,
                Is.EqualTo(0).Or.EqualTo(1));
            Assert.That(restarted.TryApplyReceipt(receipt, priorOrNew.Cas,
                out GmHouseProfileGeneration recovered, out error), Is.True, error);
            Assert.That(recovered.Receipts.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void ReceiptWhitelistExcludesInputPresentationAccessibilityAndTimestamps()
    {
        string[] forbidden = { "input", "focus", "pause", "device", "accessibility",
            "highlight", "cursor", "reaction", "truth", "secret", "uncommitted",
            "presentation", "timestamp" };
        string[] names = typeof(GmHouseTerminalReceipt)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.Name.ToLowerInvariant()).ToArray();
        foreach (string word in forbidden)
            Assert.That(names.Any(name => name.Contains(word)), Is.False,
                $"receipt leaked forbidden field family '{word}'");
    }

    [Test]
    public void SixDiskPlaythroughsRetainAllButSelectOnlyLatestFive()
    {
        GmHouseMemoryStore store = OpenStore();
        for (int index = 0; index < 6; index++)
        {
            GmHouseTerminalReceipt receipt = Prepare(store, 100 + index,
                GmEndingType.TrappedLoop);
            Assert.That(store.TryApplyReceipt(receipt, store.CurrentProfile.Cas,
                out _, out string error), Is.True, error);
        }
        Assert.That(store.CurrentProfile.Receipts.Count, Is.EqualTo(6));
        Assert.That(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Mirror, 200,
            out GmHouseRunGeneration mirror, out string finalError), Is.True, finalError);
        Assert.That(mirror.PackageBinding.SelectedReceiptIds.Count, Is.EqualTo(5));
        Assert.That(mirror.PackageBinding.SelectedReceiptIds,
            Does.Not.Contain(store.CurrentProfile.Receipts.First().ReceiptId));
    }

    [Test]
    public void UnreadableEnvelopeInhibitsWritesUntilAuthorizedRecoveryQuarantinesDomain()
    {
        GmHouseMemoryStore store = OpenStore();
        string originalLineage = store.CurrentProfile.LineageId;
        byte[] garbage = UnreadableEnvelopeBytes();
        string commit = Directory.GetFiles(Path.Combine(directory, "root", "commits"),
            "*.commit").Single();
        File.WriteAllBytes(commit, garbage);

        var blocked = new GmHouseMemoryStore(directory);
        Assert.That(blocked.TryOpenOrCreate(out _, out string error), Is.False);
        StringAssert.Contains("magic", error.ToLowerInvariant());
        Assert.That(blocked.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary,
            11, out _, out error), Is.False);

        Assert.That(blocked.TryAuthorizeUnreadableDomainRecovery(
            out string incidentId, out GmHouseProfileGeneration recovered, out error),
            Is.True, error);
        Assert.That(incidentId, Has.Length.EqualTo(32));
        Assert.That(recovered.LineageId, Has.Length.EqualTo(32));
        Assert.That(recovered.LineageId, Is.Not.EqualTo(originalLineage));
        Assert.That(recovered.Epoch, Is.EqualTo(1));
        Assert.That(recovered.Receipts, Is.Empty);

        string quarantine = directory + ".quarantine-" + incidentId;
        Assert.That(Directory.Exists(quarantine), Is.True);
        Assert.That(File.ReadAllBytes(Path.Combine(quarantine, "root", "commits",
            Path.GetFileName(commit))), Is.EqualTo(garbage));
        Assert.That(File.Exists(directory + ".recovery-intent"), Is.False);

        var reopened = new GmHouseMemoryStore(directory);
        Assert.That(reopened.TryOpenOrCreate(out GmHouseProfileGeneration live,
            out error), Is.True, error);
        Assert.That(live.LineageId, Is.EqualTo(recovered.LineageId));
        Assert.That(reopened.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary,
            12, out _, out error), Is.True, error);
    }

    [Test]
    public void PendingRecoveryIntentResumesQuarantineAndGenesisWithoutMergingOldArtifacts()
    {
        GmHouseMemoryStore store = OpenStore();
        string originalLineage = store.CurrentProfile.LineageId;
        string commit = Directory.GetFiles(Path.Combine(directory, "root", "commits"),
            "*.commit").Single();
        File.WriteAllBytes(commit, UnreadableEnvelopeBytes());

        string incidentId = Guid.NewGuid().ToString("N");
        string intent = directory + ".recovery-intent";
        GmHouseRecoveryIntent.WritePending(intent, incidentId);

        var resumed = new GmHouseMemoryStore(directory);
        Assert.That(resumed.TryOpenOrCreate(out GmHouseProfileGeneration profile,
            out string error), Is.True, error);
        Assert.That(profile.LineageId, Is.Not.EqualTo(originalLineage));
        Assert.That(File.Exists(intent), Is.False);
        Assert.That(Directory.Exists(directory + ".quarantine-" + incidentId), Is.True);

        string extra = Path.Combine(directory, "root", "commits",
            "reintroduced-old-lineage.commit");
        File.Copy(Directory.GetFiles(Path.Combine(directory + ".quarantine-" + incidentId,
            "root", "commits"), "*.commit").Single(), extra, true);
        var merged = new GmHouseMemoryStore(directory);
        Assert.That(merged.TryOpenOrCreate(out _, out error), Is.False,
            "reintroduced old artifacts merged into the recovered lineage");
        StringAssert.Contains("commit", error.ToLowerInvariant());
        Assert.That(File.ReadAllBytes(Directory.GetFiles(
            Path.Combine(directory, "root", "commits"), "root-commit-*.commit").Single())
            .Take(8).ToArray(),
            Is.EqualTo(System.Text.Encoding.ASCII.GetBytes("TGMHOUSE")));
    }

    [Test]
    public void RecoveryIntentCannotReplaceAnExistingPendingIncident()
    {
        string path = directory + ".recovery-intent";
        string first = "00112233445566778899aabbccddeeff";
        string second = "ffeeddccbbaa99887766554433221100";
        GmHouseRecoveryIntent.WritePending(path, first);

        Assert.Throws<IOException>(() => GmHouseRecoveryIntent.WritePending(path, second));
        Assert.That(GmHouseRecoveryIntent.TryRead(path, out string stored,
            out string error), Is.True, error);
        Assert.That(stored, Is.EqualTo(first));
    }

    [Test]
    public void RestoreLastValidProfileQuarantinesDamagedTailAndReopensValidatedPredecessor()
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt first = Prepare(store, 701, GmEndingType.TrueEscape);
        Assert.That(store.TryApplyReceipt(first, store.CurrentProfile.Cas,
            out _, out string error), Is.True, error);
        GmHouseTerminalReceipt second = Prepare(store, 702, GmEndingType.TrappedLoop);
        Assert.That(store.TryApplyReceipt(second, store.CurrentProfile.Cas,
            out _, out error), Is.True, error);

        string newestGeneration = Directory.GetFiles(
            Path.Combine(directory, "profile", "generations"), "*.bin")
            .OrderBy(path => path, StringComparer.Ordinal).Last();
        File.WriteAllBytes(newestGeneration, UnreadableEnvelopeBytes());
        byte[] damagedBytes = File.ReadAllBytes(newestGeneration);
        var blocked = new GmHouseMemoryStore(directory);
        Assert.That(blocked.TryOpenOrCreate(out _, out error), Is.False);

        Assert.That(blocked.TryRestoreLastValidProfile(out string incidentId,
            out GmHouseProfileGeneration restored, out error), Is.True, error);
        Assert.That(restored.Generation, Is.EqualTo(2));
        Assert.That(restored.Receipts.Select(item => item.ReceiptId),
            Is.EqualTo(new[] { first.ReceiptId }));
        string quarantine = directory + ".profile-quarantine-" + incidentId;
        string quarantinedGeneration = Directory.GetFiles(
            Path.Combine(quarantine, "generations"), "*.bin")
            .Single(path => Path.GetFileName(path) == Path.GetFileName(newestGeneration));
        Assert.That(File.ReadAllBytes(quarantinedGeneration), Is.EqualTo(damagedBytes));
        Assert.That(File.Exists(directory + ".profile-restore-intent"), Is.False);

        var reopened = new GmHouseMemoryStore(directory);
        Assert.That(reopened.TryOpenOrCreate(out GmHouseProfileGeneration live,
            out error), Is.True, error);
        Assert.That(live.Generation, Is.EqualTo(2));
        Assert.That(live.Receipts.Select(item => item.ReceiptId),
            Is.EqualTo(new[] { first.ReceiptId }));
    }

    [Test]
    public void RestoreLastValidProfileRefusesWhenGenesisHasNoValidatedPredecessor()
    {
        OpenStore();
        string generation = Directory.GetFiles(
            Path.Combine(directory, "profile", "generations"), "*.bin").Single();
        File.WriteAllBytes(generation, UnreadableEnvelopeBytes());
        byte[] before = File.ReadAllBytes(generation);

        var blocked = new GmHouseMemoryStore(directory);
        Assert.That(blocked.TryRestoreLastValidProfile(out _, out _,
            out string error), Is.False);
        StringAssert.Contains("no prior profile generation", error.ToLowerInvariant());
        Assert.That(File.ReadAllBytes(generation), Is.EqualTo(before));
        Assert.That(File.Exists(directory + ".profile-restore-intent"), Is.False);
        Assert.That(Directory.GetDirectories(Path.GetDirectoryName(directory),
            Path.GetFileName(directory) + ".profile-quarantine-*"), Is.Empty);
    }

    [TestCase(GmHouseProfileRestoreFault.AfterIntent)]
    [TestCase(GmHouseProfileRestoreFault.AfterQuarantine)]
    public void InterruptedProfileRestoreResumesFromIntentWithoutChangingQuarantinedBytes(
        GmHouseProfileRestoreFault fault)
    {
        GmHouseMemoryStore store = OpenStore();
        GmHouseTerminalReceipt first = Prepare(store, 711, GmEndingType.TrueEscape);
        Assert.That(store.TryApplyReceipt(first, store.CurrentProfile.Cas,
            out _, out string error), Is.True, error);
        GmHouseTerminalReceipt second = Prepare(store, 712, GmEndingType.TrappedLoop);
        Assert.That(store.TryApplyReceipt(second, store.CurrentProfile.Cas,
            out _, out error), Is.True, error);
        string newest = Directory.GetFiles(
            Path.Combine(directory, "profile", "generations"), "*.bin")
            .OrderBy(path => path, StringComparer.Ordinal).Last();
        File.WriteAllBytes(newest, UnreadableEnvelopeBytes());
        byte[] damaged = File.ReadAllBytes(newest);

        var interrupted = new GmHouseMemoryStore(directory)
            { ProfileRestoreFaultAfter = fault };
        Assert.That(interrupted.TryRestoreLastValidProfile(out string incidentId,
            out _, out error), Is.False);
        StringAssert.Contains("injected profile restore interruption", error);
        Assert.That(File.Exists(directory + ".profile-restore-intent"), Is.True);

        var resumed = new GmHouseMemoryStore(directory);
        Assert.That(resumed.TryOpenOrCreate(out GmHouseProfileGeneration restored,
            out error), Is.True, error);
        Assert.That(restored.Generation, Is.EqualTo(2));
        Assert.That(restored.Receipts.Select(item => item.ReceiptId),
            Is.EqualTo(new[] { first.ReceiptId }));
        string quarantine = directory + ".profile-quarantine-" + incidentId;
        string quarantined = Directory.GetFiles(Path.Combine(quarantine, "generations"), "*.bin")
            .Single(path => Path.GetFileName(path) == Path.GetFileName(newest));
        Assert.That(File.ReadAllBytes(quarantined), Is.EqualTo(damaged));
        Assert.That(File.Exists(directory + ".profile-restore-intent"), Is.False);
    }

    [Test]
    public void SuccessfulProfileRestoreFlushesTheDirectoryAfterClearingItsIntent()
    {
        var fileSystem = new RecordingFlushFileSystem(new GmHousePhysicalFileSystem());
        var store = new GmHouseMemoryStore(directory, fileSystem);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        GmHouseTerminalReceipt first = Prepare(store, 721, GmEndingType.TrueEscape);
        Assert.That(store.TryApplyReceipt(first, store.CurrentProfile.Cas,
            out _, out error), Is.True, error);
        GmHouseTerminalReceipt second = Prepare(store, 722, GmEndingType.TrappedLoop);
        Assert.That(store.TryApplyReceipt(second, store.CurrentProfile.Cas,
            out _, out error), Is.True, error);
        string newest = Directory.GetFiles(
            Path.Combine(directory, "profile", "generations"), "*.bin")
            .OrderBy(path => path, StringComparer.Ordinal).Last();
        File.WriteAllBytes(newest, UnreadableEnvelopeBytes());

        Assert.That(store.TryRestoreLastValidProfile(out _, out _, out error),
            Is.True, error);
        Assert.That(File.Exists(directory + ".profile-restore-intent"), Is.False);
        Assert.That(fileSystem.LastFlushedDirectory,
            Is.EqualTo(Path.GetDirectoryName(directory)));
    }

    GmHouseMemoryStore OpenStore()
    {
        var store = new GmHouseMemoryStore(directory);
        Assert.That(store.TryOpenOrCreate(out _, out string error), Is.True, error);
        return store;
    }

    static GmHouseTerminalReceipt Prepare(GmHouseMemoryStore store, int seed,
        GmEndingType ending, GmParlorAdaptiveMode mode = GmParlorAdaptiveMode.Ordinary)
    {
        if (mode == GmParlorAdaptiveMode.Mirror && !store.CurrentProfile.MirrorUnlocked)
        {
            GmHouseTerminalReceipt unlock = Prepare(store, seed - 1,
                GmEndingType.TrappedLoop);
            Assert.That(store.TryApplyReceipt(unlock, store.CurrentProfile.Cas,
                out _, out string unlockError), Is.True, unlockError);
        }
        Assert.That(store.TryAllocateCampaignRun(mode, seed,
            out GmHouseRunGeneration run, out string error), Is.True, error);
        Assert.That(store.TryCreateReceipt(run, ending,
            CompletedAccumulator(run.FrozenPackage),
            out GmHouseTerminalReceipt receipt, out error), Is.True, error);
        Assert.That(store.TryCommitPreparedRun(run, receipt, new GmSaveData
        { currentSceneId = "labyrinth", lastCheckpoint = "ending",
            houseRunId = run.Identity.RunId }, out _, out error), Is.True, error);
        return receipt;
    }

    static GmParlorBehaviorAccumulator CompletedAccumulator(
        GmParlorAdaptivePackage package)
    {
        var accumulator = new GmParlorBehaviorAccumulator(package);
        accumulator.RecordPlayerLead(new GmCard(GmSuit.Flames, 7), 1, 7);
        accumulator.RecordRead(GmTellObservation.Suspicious,
            GmParlorOutcomeKind.CheatCaught);
        accumulator.SealCompletedMatch(package, 1, 0);
        return accumulator;
    }

    static byte[] UnreadableEnvelopeBytes()
    {
        var bytes = new byte[128];
        System.Text.Encoding.ASCII.GetBytes("NOTHOUSE").CopyTo(bytes, 0);
        return bytes;
    }

    static byte[] Hex(string value)
    {
        var result = new byte[value.Length / 2];
        for (int index = 0; index < result.Length; index++)
            result[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
        return result;
    }

    static void SetReceiptProperty(GmHouseTerminalReceipt receipt,string name,object value) =>
        SetProperty(receipt,name,value);

    static void SetProperty(object target,string name,object value) =>
        target.GetType().GetProperty(name,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .SetValue(target,value);

    sealed class RecordingFlushFileSystem : IGmHouseFileSystem
    {
        readonly IGmHouseFileSystem inner;
        public string LastFlushedDirectory { get; private set; }

        public RecordingFlushFileSystem(IGmHouseFileSystem inner) => this.inner = inner;
        public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public string[] GetFiles(string path,string pattern) => inner.GetFiles(path,pattern);
        public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);
        public void WriteNewDurable(string path,byte[] bytes,GmHouseDurabilityPoint point,
            Action<GmHouseDurabilityEdge> observeEdge=null) =>
            inner.WriteNewDurable(path,bytes,point,observeEdge);
        public void FlushDirectory(string path)
        {
            inner.FlushDirectory(path);
            LastFlushedDirectory=path;
        }
    }
}
