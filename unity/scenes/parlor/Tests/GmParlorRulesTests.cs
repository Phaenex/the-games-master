using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GmParlorRulesTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void DeckBuilds28CardsWithFourCustomSuits()
    {
        List<GmCard> deck = GmDeckUtility.CreateFreshDeck();
        Assert.AreEqual(28, deck.Count);

        var suitsCount = new Dictionary<GmSuit, int>();
        foreach (var card in deck)
        {
            if (!suitsCount.ContainsKey(card.Suit)) suitsCount[card.Suit] = 0;
            suitsCount[card.Suit]++;
            Assert.IsTrue(card.Rank >= 1 && card.Rank <= 7);
        }

        Assert.AreEqual(7, suitsCount[GmSuit.Flames]);
        Assert.AreEqual(7, suitsCount[GmSuit.Eyes]);
        Assert.AreEqual(7, suitsCount[GmSuit.Bones]);
        Assert.AreEqual(7, suitsCount[GmSuit.Teeth]);
    }

    [Test]
    public void FlamesTrumpOffSuit()
    {
        var lead = new GmCard(GmSuit.Eyes, 7);
        var follow = new GmCard(GmSuit.Flames, 1);

        // 1 of Flames trumps 7 of Eyes
        bool leadWins = GmDeckUtility.LeadWinsTrick(lead, follow);
        Assert.IsFalse(leadWins, "1 of Flames must trump 7 of Eyes");
    }

    [Test]
    public void LeadSuitBeatsOffSuitNonTrump()
    {
        var lead = new GmCard(GmSuit.Eyes, 2);
        var follow = new GmCard(GmSuit.Teeth, 7);

        // 2 of Eyes beats 7 of Teeth when Eyes was led
        bool leadWins = GmDeckUtility.LeadWinsTrick(lead, follow);
        Assert.IsTrue(leadWins, "2 of lead suit must beat off-suit non-trump");
    }

    [Test]
    public void HigherRankWinsInSuit()
    {
        var lead = new GmCard(GmSuit.Bones, 3);
        var follow = new GmCard(GmSuit.Bones, 5);

        // 5 of Bones beats 3 of Bones
        bool leadWins = GmDeckUtility.LeadWinsTrick(lead, follow);
        Assert.IsFalse(leadWins, "Higher rank in-suit must win trick");
    }

    [Test]
    public void LegalPlayRequiresFollowingSuitIfHeld()
    {
        var hand = new List<GmCard>
        {
            new GmCard(GmSuit.Eyes, 3),
            new GmCard(GmSuit.Bones, 6),
            new GmCard(GmSuit.Flames, 2)
        };

        var leadEyes = new GmCard(GmSuit.Eyes, 5);

        // Playing Eyes is legal
        Assert.IsTrue(GmDeckUtility.IsLegalPlay(hand, leadEyes, new GmCard(GmSuit.Eyes, 3)));

        // Playing Bones or Flames while holding Eyes is ILLEGAL
        Assert.IsFalse(GmDeckUtility.IsLegalPlay(hand, leadEyes, new GmCard(GmSuit.Bones, 6)));
        Assert.IsFalse(GmDeckUtility.IsLegalPlay(hand, leadEyes, new GmCard(GmSuit.Flames, 2)));

        // If player has NO teeth, they can play any card when Teeth is led
        var leadTeeth = new GmCard(GmSuit.Teeth, 4);
        Assert.IsTrue(GmDeckUtility.IsLegalPlay(hand, leadTeeth, new GmCard(GmSuit.Eyes, 3)));
        Assert.IsTrue(GmDeckUtility.IsLegalPlay(hand, leadTeeth, new GmCard(GmSuit.Flames, 2)));
    }

    [Test]
    public void HostAINeverCheatsFromSafety()
    {
        var hostAIObj = new GameObject("TestHostAI");
        var hostAI = hostAIObj.AddComponent<GmHostAI>();

        var hand = new List<GmCard>
        {
            new GmCard(GmSuit.Eyes, 6),
            new GmCard(GmSuit.Bones, 2)
        };

        var playerLead = new GmCard(GmSuit.Eyes, 4);

        // Aldric can legally win with Eyes 6; he should NOT cheat
        GmCard chosen = hostAI.ChooseFollowCard(hand, playerLead, 0, 0);
        Assert.AreEqual(new GmCard(GmSuit.Eyes, 6), chosen);
        Assert.IsFalse(hostAI.LastPlayWasCheated);

        Object.DestroyImmediate(hostAIObj);
    }

    [Test]
    public void HostAICheatsOnlyWhenAboutToLose()
    {
        var hostAIObj = new GameObject("TestHostAI");
        var hostAI = hostAIObj.AddComponent<GmHostAI>();

        // Holding Eyes forces the follow, so the trump is the card he is not allowed to play.
        var hand = new List<GmCard>
        {
            new GmCard(GmSuit.Eyes, 2),
            new GmCard(GmSuit.Flames, 6)
        };

        var playerLead = new GmCard(GmSuit.Eyes, 7);

        // Player is at match point (3 tricks won). Aldric cannot legally win. Reactive cheat triggers!
        GmCard chosen = hostAI.ChooseFollowCard(hand, playerLead, 3, 0);
        Assert.IsTrue(hostAI.LastPlayWasCheated);
        Assert.AreEqual(new GmCard(GmSuit.Flames, 6), chosen);
        Assert.IsTrue(hand.Contains(chosen), "the palm must come from the hand GmParlorRules checks");
        Assert.IsFalse(GmDeckUtility.IsLegalPlay(hand, playerLead, chosen),
            "a palm that was legal all along is not a cheat");

        Object.DestroyImmediate(hostAIObj);
    }

    [Test]
    public void HostAIClaimsNoCheatItCannotPlay()
    {
        var hostAIObj = new GameObject("TestHostAI");
        var hostAI = hostAIObj.AddComponent<GmHostAI>();

        var hand = new List<GmCard>
        {
            new GmCard(GmSuit.Bones, 2),
            new GmCard(GmSuit.Teeth, 3)
        };

        var playerLead = new GmCard(GmSuit.Eyes, 7);

        // Match point, and no card in hand takes the trick. There is no palm to make, so he discards
        // instead of announcing a cheat the table would reject.
        GmCard chosen = hostAI.ChooseFollowCard(hand, playerLead, 3, 0);
        Assert.IsFalse(hostAI.LastPlayWasCheated);
        Assert.AreEqual(new GmCard(GmSuit.Bones, 2), chosen);
        Assert.IsTrue(hand.Contains(chosen));

        Object.DestroyImmediate(hostAIObj);
    }

    [Test]
    public void HostAILeadsHighestFlamesOnlyWhenShortOfSideSuits()
    {
        var hostAIObj = new GameObject("TestHostAI");
        var hostAI = hostAIObj.AddComponent<GmHostAI>();

        var allTrump = new List<GmCard>
        {
            new GmCard(GmSuit.Flames, 3),
            new GmCard(GmSuit.Flames, 7)
        };
        Assert.AreEqual(new GmCard(GmSuit.Flames, 7), hostAI.ChooseLeadCard(allTrump));

        var mixed = new List<GmCard>
        {
            new GmCard(GmSuit.Flames, 7),
            new GmCard(GmSuit.Eyes, 5)
        };
        Assert.AreEqual(new GmCard(GmSuit.Eyes, 5), hostAI.ChooseLeadCard(mixed),
            "a side suit pulls trumps; the trump lead is the short-handed fallback");

        Object.DestroyImmediate(hostAIObj);
    }
}
