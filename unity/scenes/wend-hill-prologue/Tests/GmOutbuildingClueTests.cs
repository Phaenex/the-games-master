using NUnit.Framework;
using UnityEngine;

public sealed class GmOutbuildingClueTests
{
    GameObject go;

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
        GmOutbuildingClue.ResetRegistryForTests();
        GmRunStore.BeginNewRun();
        GmSaveSystem.DeleteSave();
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
