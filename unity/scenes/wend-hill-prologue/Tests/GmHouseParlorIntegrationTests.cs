using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GmHouseParlorIntegrationTests
{
    GameObject root;
    GmHouseBeginning house;

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        root = new GameObject("HouseParlorTest");
        GameObject room = GameObject.CreatePrimitive(PrimitiveType.Cube);
        room.name = "MeasuredRoom";
        room.transform.SetParent(root.transform);
        house = root.AddComponent<GmHouseBeginning>();
        typeof(GmHouseBeginning).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(
            house, null);

        house.ReviewUnlockParlor();
        house.BeginHostIntroduction();
        int guard = 20;
        while (house.Phase == GmHousePhase.HostIntroduction && guard-- > 0) house.AdvanceDialogue();
        Assert.That(guard, Is.GreaterThan(0));
        Assert.That(house.Phase, Is.EqualTo(GmHousePhase.ParlorGame));
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void AuthoredTutorialDealTeachesSuspicionReadAndEyesBeforeRoundTwo()
    {
        Assert.That(Labels(house.PlayerHand), Is.EqualTo(
            "1 Flames,5 Eyes,7 Eyes,5 Teeth,7 Teeth,5 Bones,7 Bones"));
        Assert.That(Labels(house.AldricHand), Is.EqualTo(
            "1 Eyes,2 Eyes,3 Eyes,1 Teeth,2 Teeth,1 Bones,2 Bones"));

        PlayFirst();
        AssertCheat(new GmCard(GmSuit.Flames, 8), new GmCard(GmSuit.Eyes, 1));
        house.AllowTrick();
        Assert.That(house.Suspicion, Is.EqualTo(1));
        Assert.That(house.ReadUnlocked, Is.False);

        house.ContinueAfterResult();
        PlayFirst();
        AssertCheat(new GmCard(GmSuit.Eyes, 8), new GmCard(GmSuit.Eyes, 2));
        house.AllowTrick();
        Assert.That(house.Suspicion, Is.EqualTo(2));
        Assert.That(house.ReadUnlocked, Is.True);

        house.ContinueAfterResult();
        PlayFirst();
        AssertCheat(new GmCard(GmSuit.Eyes, 8), new GmCard(GmSuit.Eyes, 3));
        house.ReadHand();
        Assert.That(house.PlayerTricks, Is.EqualTo(1), "a correct Read flips the third trick to the player");
        Assert.That(house.AldricTricks, Is.EqualTo(2));
        Assert.That(house.Progress.CheatsCaught, Is.EqualTo(1));
        Assert.That(house.TableMessage, Does.StartWith("READ CORRECT."));
        Assert.That(house.RevealedCard, Does.StartWith("EYES:"));

        ReachRoundResult();
        house.ContinueAfterResult();
        Assert.That(house.RoundNumber, Is.EqualTo(2));
        string roundTwo = Labels(house.PlayerHand) + "|" + Labels(house.AldricHand);
        Assert.That(roundTwo, Is.EqualTo(
            "3 Flames,7 Eyes,2 Teeth,3 Teeth,6 Teeth,7 Teeth,4 Bones|" +
            "3 Bones,6 Flames,7 Bones,4 Flames,1 Flames,5 Eyes,6 Eyes"));
    }

    void PlayFirst()
    {
        Assert.That(house.TurnPhase, Is.EqualTo(GmParlorTurnPhase.ChooseCard));
        Assert.That(house.ReviewPlayFirstLegalCard(), Is.True);
        Assert.That(house.TurnPhase, Is.EqualTo(GmParlorTurnPhase.JudgePlay));
    }

    void AssertCheat(GmCard displayed, GmCard paidCard)
    {
        Assert.That(house.ReviewCurrentPlayWasCheat, Is.True);
        Assert.That(house.AldricCard, Is.EqualTo(displayed));
        Assert.That(house.AldricHand, Has.None.EqualTo(paidCard));
    }

    void ReachRoundResult()
    {
        int guard = 30;
        while (house.TurnPhase != GmParlorTurnPhase.RoundResult && guard-- > 0)
        {
            if (house.TurnPhase == GmParlorTurnPhase.TrickResult) house.ContinueAfterResult();
            else if (house.TurnPhase == GmParlorTurnPhase.ChooseCard) PlayFirst();
            else if (house.TurnPhase == GmParlorTurnPhase.JudgePlay)
            {
                if (house.ReviewCurrentPlayWasCheat && house.ReadUnlocked) house.ReadHand();
                else house.AllowTrick();
            }
            else Assert.Fail($"tutorial stalled in {house.TurnPhase}");
        }
        Assert.That(guard, Is.GreaterThan(0));
    }

    static string Labels(IEnumerable<GmCard> cards)
    {
        var labels = new List<string>();
        foreach (GmCard card in cards) labels.Add(card.TableLabel);
        return string.Join(",", labels);
    }
}
