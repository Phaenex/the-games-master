// The grounds tell: the game's core verb, taught outside before the card table.
//
// These exist because the defect they cover was invisible to 332 passing tests. The opening carries
// roughly a dozen authored discrepancies shaped exactly like a caught cheat, and every one of them
// used to be handed to the player as narration on a second Examine press. A game whose entire loop
// is catching a cheat let its narrator do the catching for ten minutes, and nothing failed.
using NUnit.Framework;
using UnityEngine;

public sealed class GmGroundsTellTests
{
    GameObject host;
    GmDesignRuntime runtime;
    GmInteractable target;

    const string Observation = "Dry for years. Coins fused to the basin — wishes nobody ever collected.";
    const string Tell = "All the coins are heads-down. Every one.";

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        host = new GameObject("TellFixture");
        runtime = host.AddComponent<GmDesignRuntime>();
        var targetObject = new GameObject("GardenBasin");
        targetObject.transform.SetParent(host.transform);
        target = targetObject.AddComponent<GmInteractable>();
        target.Configure("garden-basin", "Examine", 3.8f, 8f);
        target.BindContent(Observation, "");
        target.BindTell(Tell);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
    }

    [Test]
    public void ExamineGivesTheObservationAndNeverTheTell()
    {
        target.Interact(runtime);
        Assert.AreEqual(Observation, runtime.lastExamine);

        // The old behaviour: a second press handed over the discrepancy for free. If this ever
        // returns the tell again, the narrator has taken the verb back off the player.
        target.Interact(runtime);
        Assert.AreEqual(Observation, runtime.lastExamine,
            "examining twice revealed the tell — the player is meant to call it, not be told it");
        Assert.IsFalse(target.TellFound);
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount);
    }

    [Test]
    public void CallingATellBeforeLookingAtAnythingDoesNothing()
    {
        Assert.AreEqual(GmTellCall.Unavailable, target.CallTell(runtime),
            "a tell was callable on an object the player never examined");
        Assert.IsFalse(target.TellFound);
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount);
    }

    [Test]
    public void CallingARealTellRevealsItAndBanksACatch()
    {
        target.Interact(runtime);
        Assert.AreEqual(GmTellCall.Caught, target.CallTell(runtime));
        Assert.AreEqual(Tell, runtime.lastExamine);
        Assert.IsTrue(target.TellFound);
    }

    [Test]
    public void TheSameTellCannotBeBankedTwice()
    {
        target.Interact(runtime);
        Assert.AreEqual(GmTellCall.Caught, target.CallTell(runtime));
        Assert.AreEqual(GmTellCall.AlreadyFound, target.CallTell(runtime),
            "re-calling a caught tell reported a fresh catch, which would let a player farm one object");
    }

    [Test]
    public void CallingATellOnSomethingInnocentIsAFalseRead()
    {
        var innocentObject = new GameObject("ArrivalCar");
        innocentObject.transform.SetParent(host.transform);
        var innocent = innocentObject.AddComponent<GmInteractable>();
        innocent.Configure("arrival-car", "Examine", 3.8f, 8f);
        innocent.BindContent("The keys are still in it. Nobody took them. Nobody needed to.", "");
        innocent.BindTell("");

        Assert.IsFalse(innocent.CarriesTell);
        innocent.Interact(runtime);
        Assert.AreEqual(GmTellCall.False, innocent.CallTell(runtime),
            "calling a tell on an innocent object cost nothing — the correct play would be to shout at everything");
    }

    [Test]
    public void CatchingATellOutsideCarriesTheReadInToTheTable()
    {
        // The Read was gated purely behind suspicion >= 2, and suspicion only rises by LETTING a
        // cheat past. So the teaching sequence for the core verb was: be cheated twice, lose both,
        // then receive the button -- while a player who had been calling tells correctly all the way
        // up the drive arrived and was told they had not earned it.
        var houseObject = new GameObject("HouseBeginningFixture");
        houseObject.transform.SetParent(host.transform);
        var house = houseObject.AddComponent<GmHouseBeginning>();
        Assert.IsFalse(house.ReadUnlocked, "the Read should start locked for a player who caught nothing");

        GmRunStore.RecordCatch("grounds-tell-garden-basin");
        Assert.IsTrue(house.ReadUnlocked,
            "a player who used the verb on the grounds still had to lose two tricks to be given it indoors");
    }

    [Test]
    public void AldricsCardIsOnlyAcknowledgedIfThePlayerCaughtIt()
    {
        Assert.IsFalse(GmHouseBeginning.PlayerCaughtAldricsCard,
            "the host acknowledged a slip the player never found");

        // Deliberately routed through GmHouseProgress.CatchCheat rather than poking GmRunStore
        // directly, because that is the call GmInteractionScanner actually makes. Catching writes to
        // TWO ledgers -- the clue ledger via Discover and the catch ledger via RecordCatch -- and an
        // earlier version of this test wrote only the second, passed nothing, and would have hidden
        // a real break in the wiring between the scanner and the table.
        var progressObject = new GameObject("HouseProgressFixture");
        progressObject.transform.SetParent(host.transform);
        var progress = progressObject.AddComponent<GmHouseProgress>();
        progress.CatchCheat(GmHouseBeginning.GateCardTellClue);

        Assert.IsTrue(GmHouseBeginning.PlayerCaughtAldricsCard,
            "the player caught Aldric's card and the table never pays it off");
    }

    [Test]
    public void TheGroundsCannotHandOutEnoughCatchesToWinTheTrueEndingOnTheirOwn()
    {
        // The balance hole this file could not see. Its innocent-object case builds a FABRICATED
        // interactable with an empty tell and the id "arrival-car" -- and in shipped data arrival-car
        // carried a tell and returned Caught. The test passed while testing something that was not
        // in the game.
        //
        // Catching a tell records a catch AND a defiance point, and True Escape gates on eight
        // catches, so fourteen callable tells on the walk in decided the ending before the player
        // sat down at a single table. Canon is seven games a night; the games have to matter more
        // than the driveway.
        var design = GmDesignRuntime.LoadDesign();
        Assert.IsNotNull(design, "the prologue design data did not load");

        int callable = 0, innocent = 0;
        foreach (GmDesignRuntime.Poi poi in design.pois)
        {
            bool carries = poi.tell && !string.IsNullOrWhiteSpace(poi.text2);
            if (carries) callable++; else innocent++;
        }

        Assert.Greater(innocent, 0,
            "every POI on the grounds carries a tell, so the verb cannot be got wrong and the " +
            "false-read line is unreachable");
        Assert.Less(callable, GmEndingManager.TrueEscapeCatchesRequired,
            $"the grounds alone offer {callable} catches against a True Escape threshold of " +
            $"{GmEndingManager.TrueEscapeCatchesRequired} — the seven games decide nothing");
    }

    [Test]
    public void EveryInnocentPoiStillSaysItsSecondLine()
    {
        // Making the verb risky must not cost the writing. An innocent POI keeps text2 as a second
        // Examine, so the daughter's drawing and the watch chain with no watch are still read --
        // they simply are not evidence of anything.
        var design = GmDesignRuntime.LoadDesign();
        var silent = new System.Collections.Generic.List<string>();
        foreach (GmDesignRuntime.Poi poi in design.pois)
            if (!poi.tell && string.IsNullOrWhiteSpace(poi.text2)) silent.Add(poi.id);

        Assert.IsEmpty(silent, "these POIs were made innocent by DELETING their line rather than " +
            "by demoting it to an observation:\n- " + string.Join("\n- ", silent));
    }
}
