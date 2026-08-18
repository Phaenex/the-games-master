using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmOutbuildingClueTests
{
    GameObject go;
    string saveDirectory;

    [SetUp]
    public void SetUp()
    {
        saveDirectory = Path.Combine(Path.GetTempPath(), "gm-outbuilding-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(saveDirectory, "save.json"));
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
        GmOutbuildingClue.ResetRegistryForTests();
        GmRunStore.BeginNewRun();
        GmSaveSystem.Flush();
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
    }

    [Test]
    public void FirstInteractionBanksThePersistentClue()
    {
        go = new GameObject("clue");
        go.AddComponent<GmInteractable>().Configure("outbuilding-coach-house-tally-marks", "Examine", 3f, 8f);
        GmOutbuildingClue clue = go.AddComponent<GmOutbuildingClue>();
        clue.Configure("outbuilding-coach-house-tally-marks", "coach-house");

        Assert.IsFalse(GmRunStore.HasClue("outbuilding-coach-house-tally-marks"));
        clue.OnGmInteraction(go.GetComponent<GmInteractable>());
        Assert.IsTrue(GmRunStore.HasClue("outbuilding-coach-house-tally-marks"));
    }

    [Test]
    public void RepeatInteractionDoesNotDoubleCountTheDiscovery()
    {
        go = new GameObject("clue");
        var interactable = go.AddComponent<GmInteractable>();
        interactable.Configure("outbuilding-coach-house-tally-marks", "Examine", 3f, 8f);
        GmOutbuildingClue clue = go.AddComponent<GmOutbuildingClue>();
        clue.Configure("outbuilding-coach-house-tally-marks", "coach-house");

        clue.OnGmInteraction(interactable);
        int countAfterFirst = GmRunStore.DiscoveredCluesCount;
        clue.OnGmInteraction(interactable);
        int countAfterSecond = GmRunStore.DiscoveredCluesCount;

        Assert.AreEqual(countAfterFirst, countAfterSecond,
            "re-reading an already-banked clue must not record it a second time.");
    }

    [Test]
    public void UnconfiguredClueIdDoesNothing()
    {
        go = new GameObject("clue");
        go.AddComponent<GmInteractable>().Configure("some-interaction", "Examine", 3f, 8f);
        GmOutbuildingClue clue = go.AddComponent<GmOutbuildingClue>();
        // Configure() never called -- clueId stays null/empty.

        Assert.DoesNotThrow(() => clue.OnGmInteraction(go.GetComponent<GmInteractable>()));
        Assert.AreEqual(0, GmRunStore.DiscoveredCluesCount);
    }
}
