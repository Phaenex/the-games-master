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
        GmSaveData old = JsonUtility.FromJson<GmSaveData>("{\"corruptionTier\":3}");
        Assert.That(() => GmRunStore.LoadFromSaveData(old), Throws.Nothing);
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
        Assert.That(controller.Match, Is.Null);
    }
}
