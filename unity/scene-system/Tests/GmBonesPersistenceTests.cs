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
        if (target == "interventionReceipt")
        {
            data.bonesTurnEvidencePresent = true;
            data.bonesMatch.interventionReceipt = new GmBonesInterventionReceipt();
        }
        else
        {
            data.bonesSessionEventPresent = true;
        }
        string json = data.ToJson();
        File.WriteAllText(path, json);

        Assert.That(GmSaveSystem.Load(), Is.True, GmSaveSystem.LastError);
        Assert.That(GmRunStore.HasBonesMatch, Is.True);
        Assert.That(GmRunStore.BonesRestoreError, Is.Not.Empty);
    }
}
