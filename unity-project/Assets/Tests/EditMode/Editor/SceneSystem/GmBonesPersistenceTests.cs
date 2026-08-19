using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class GmBonesPersistenceTests
{
    string directory;
    string path;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-bones-store-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(directory, "save.json");
        Directory.CreateDirectory(directory);
        GmSaveSystem.ConfigureForTests(path);
        GmRunSeed.ForceForReview(2026);
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        GmSaveSystem.ResetTestConfiguration();
        GmRunSeed.ResetForTests();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void OldJsonWithoutBonesLoadsAsNoMatchAndNewRunClearsBones()
    {
        File.WriteAllText(path, "{\"corruptionTier\":3}");
        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasBonesMatch, Is.False);
        Assert.That(GmRunStore.BonesRestoreError, Is.Empty);

        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        Assert.That(GmRunStore.HasBonesMatch, Is.True);
        GmRunStore.BeginNewRun();
        Assert.That(GmRunStore.HasBonesMatch, Is.False);
    }

    [Test]
    public void StoreAndSaveDataOwnIndependentBonesSnapshots()
    {
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        GmBonesMatchSnapshot first = GmRunStore.GetBonesMatchSnapshot();
        string fingerprint = first.stateFingerprint;
        first.stateFingerprint = "caller-forged";
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));

        GmSaveData save = GmRunStore.ToSaveData();
        save.bonesMatch.stateFingerprint = "save-forged";
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint, Is.EqualTo(fingerprint));
    }

    [Test]
    public void ModernDiskJsonDeclaresEnvelopeAndOmitsInventedNestedEvidence()
    {
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        string json = File.ReadAllText(path);

        StringAssert.Contains("\"bonesEnvelopeVersion\": 1", json);
        StringAssert.Contains("\"bonesPayloadPresent\": true", json);
        StringAssert.Contains("\"bonesTurnEvidencePresent\": false", json);
        StringAssert.Contains("\"bonesSessionEventPresent\": false", json);
        StringAssert.DoesNotContain("\"interventionReceipt\"", json);
        StringAssert.DoesNotContain("\"intervention\"", json);
        Assert.That(GmSaveData.FromJson(json).bonesMatch, Is.Not.Null);
    }

    [Test]
    public void CorruptBonesSnapshotIsRetainedAndControllerRefusesFreshFallback()
    {
        var match = new GmBonesMatch(9UL, null);
        GmBonesMatchSnapshot corrupt = match.ExportSnapshot();
        corrupt.randomState = 0;
        GmRunStore.LoadFromSaveData(new GmSaveData { bonesMatch = corrupt });
        Assert.That(GmRunStore.HasBonesMatch, Is.True);
        Assert.That(GmRunStore.GetBonesMatchSnapshot().randomState, Is.Zero);
        Assert.That(GmRunStore.BonesRestoreError, Is.Not.Empty);

        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(), Is.EqualTo(GmBonesInitializeResult.CorruptSavedState));
        Assert.That(controller.Snapshot, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExplicitEmptyBonesPayloadIsPresentAndCorrupt(bool modernEnvelope)
    {
        string envelope = modernEnvelope
            ? "\"bonesEnvelopeVersion\":1,\"bonesPayloadPresent\":true,"
            : string.Empty;
        File.WriteAllText(path, "{" + envelope + "\"bonesMatch\":{}}");

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasBonesMatch, Is.True);
        Assert.That(GmRunStore.BonesRestoreError, Is.Not.Empty);
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(),
            Is.EqualTo(GmBonesInitializeResult.CorruptSavedState));
    }

    [TestCase("interventionReceipt")]
    [TestCase("session-intervention")]
    public void ExplicitEmptyNestedBonesEvidenceRemainsCorruptThroughRealJson(string target)
    {
        var match = new GmBonesMatch(19UL, null);
        var data = new GmSaveData
        {
            bonesEnvelopeVersion = 1,
            bonesPayloadPresent = true,
            bonesMatch = match.ExportSnapshot()
        };
        string json;
        if (target == "interventionReceipt")
        {
            data.bonesTurnEvidencePresent = true;
            data.bonesMatch.interventionReceipt = new GmBonesInterventionReceipt();
            json = data.ToJson();
        }
        else
        {
            json = data.ToJson().Replace("\"bonesSessionEventPresent\":false",
                "\"bonesSessionEventPresent\":true");
            json = InsertObject(json, "session", "\"intervention\":{},");
        }
        File.WriteAllText(path, json);

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasBonesMatch, Is.True);
        Assert.That(GmRunStore.BonesRestoreError, Is.Not.Empty);
    }

    [TestCase("payload-false-turn-bit")]
    [TestCase("payload-false-session-bit")]
    [TestCase("payload-false-outer-present")]
    [TestCase("payload-true-outer-absent")]
    [TestCase("turn-true-object-absent")]
    [TestCase("turn-false-object-present")]
    [TestCase("session-true-object-absent")]
    [TestCase("session-false-object-present")]
    public void ModernEnvelopeContradictionsFailClosedFromPhysicalJson(string scenario)
    {
        File.WriteAllText(path, ContradictoryJson(scenario));

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        StringAssert.Contains("envelope", GmRunStore.BonesRestoreError.ToLowerInvariant());
        var controller = new GmBonesController();
        Assert.That(controller.InitializeOrRestore(),
            Is.EqualTo(GmBonesInitializeResult.CorruptSavedState));
    }

    [TestCase("fresh")]
    [TestCase("pending")]
    [TestCase("resolved")]
    [TestCase("complete")]
    public void ValidModernEnvelopeRestoresEveryBonesLifecycleShape(string state)
    {
        GmBonesMatchSnapshot snapshot = ValidSnapshot(state);
        var data = new GmSaveData
        {
            bonesEnvelopeVersion = 1,
            bonesPayloadPresent = true,
            bonesTurnEvidencePresent = snapshot.interventionReceipt != null,
            bonesSessionEventPresent = snapshot.session?.intervention != null,
            bonesMatch = snapshot
        };
        File.WriteAllText(path, data.ToJson());

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.BonesRestoreError, Is.Empty);
        Assert.That(GmRunStore.GetBonesMatchSnapshot().stateFingerprint,
            Is.EqualTo(snapshot.stateFingerprint));
    }

    static string ContradictoryJson(string scenario)
    {
        var data = new GmSaveData
        {
            bonesEnvelopeVersion = 1,
            bonesPayloadPresent = true,
            bonesMatch = new GmBonesMatch(31UL, null).ExportSnapshot()
        };
        string json = data.ToJson();
        switch (scenario)
        {
            case "payload-false-turn-bit":
                return "{\"bonesEnvelopeVersion\":1,\"bonesPayloadPresent\":false," +
                    "\"bonesTurnEvidencePresent\":true}";
            case "payload-false-session-bit":
                return "{\"bonesEnvelopeVersion\":1,\"bonesPayloadPresent\":false," +
                    "\"bonesSessionEventPresent\":true}";
            case "payload-false-outer-present":
                return json.Replace("\"bonesPayloadPresent\":true",
                    "\"bonesPayloadPresent\":false");
            case "payload-true-outer-absent":
                return "{\"bonesEnvelopeVersion\":1,\"bonesPayloadPresent\":true}";
            case "turn-true-object-absent":
                return json.Replace("\"bonesTurnEvidencePresent\":false",
                    "\"bonesTurnEvidencePresent\":true");
            case "turn-false-object-present":
                return InsertObject(json, "bonesMatch", "\"interventionReceipt\":{},");
            case "session-true-object-absent":
                return json.Replace("\"bonesSessionEventPresent\":false",
                    "\"bonesSessionEventPresent\":true");
            case "session-false-object-present":
                return InsertObject(json, "session", "\"intervention\":{},");
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    static string InsertObject(string json, string owner, string property)
    {
        string token = "\"" + owner + "\":{";
        int index = json.IndexOf(token, StringComparison.Ordinal);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        return json.Insert(index + token.Length, property);
    }

    static GmBonesMatchSnapshot ValidSnapshot(string state)
    {
        if (state == "fresh") return new GmBonesMatch(41UL, null).ExportSnapshot();
        if (state == "complete")
        {
            int[] ordinaryDice = { 6,6,6, 4,4,4, 4,4,4, 6,6,6, 6,6,6, 4,4,4 };
            var complete = new GmBonesMatch(5UL, ordinaryDice);
            complete.TryChoose(GmBonesChoice.Bank, -1, out _);
            complete.TryChoose(GmBonesChoice.Bank, -1, out _);
            complete.TryChoose(GmBonesChoice.Bank, -1, out _);
            return complete.ExportSnapshot();
        }

        int[] dice = { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2 };
        var intervention = new GmBonesMatch(3UL, dice);
        intervention.TryChoose(GmBonesChoice.Bank, -1, out _);
        intervention.TryChoose(GmBonesChoice.Bank, -1, out _);
        intervention.TryChoose(GmBonesChoice.Bank, -1, out _);
        if (state == "resolved") intervention.TryResolveIntervention(true, out _);
        else if (state != "pending")
            throw new ArgumentOutOfRangeException(nameof(state), state, null);
        return intervention.ExportSnapshot();
    }
}
