using System.Collections.Generic;
using System.Linq;
using System;
using NUnit.Framework;

public sealed class GmParlorCoreTests
{
    [Test]
    public void SeededDeckContainsEveryCardExactlyOnce()
    {
        List<GmCard> first = GmParlorCore.ShuffledDeck(41);
        List<GmCard> second = GmParlorCore.ShuffledDeck(41);

        Assert.That(first.Count, Is.EqualTo(28));
        Assert.That(first.Distinct().Count(), Is.EqualTo(28));
        CollectionAssert.AreEqual(first, second);
        Assert.That(first.Count(card => card.Suit == GmSuit.Flames), Is.EqualTo(7));
    }

    [Test]
    public void FlamesTrumpAndOffSuitCannotBeatTheLead()
    {
        Assert.That(GmParlorCore.LeadWins(Card(GmSuit.Eyes, 7), Card(GmSuit.Flames, 1)), Is.False);
        Assert.That(GmParlorCore.LeadWins(Card(GmSuit.Flames, 1), Card(GmSuit.Eyes, 7)), Is.True);
        Assert.That(GmParlorCore.LeadWins(Card(GmSuit.Teeth, 2), Card(GmSuit.Bones, 7)), Is.True);
    }

    [Test]
    public void FollowSuitIsEnforcedByIndexAndCardOnlyWhileThatSuitIsHeld()
    {
        var hand = new List<GmCard> { Card(GmSuit.Eyes, 2), Card(GmSuit.Flames, 7) };
        GmCard lead = Card(GmSuit.Eyes, 6);

        Assert.That(GmParlorCore.IsLegal(hand, 0, lead), Is.True);
        Assert.That(GmParlorCore.IsLegal(hand, 1, lead), Is.False);
        Assert.That(GmParlorCore.IsLegal(hand, lead, hand[0]), Is.True);
        Assert.That(GmParlorCore.IsLegal(hand, lead, hand[1]), Is.False);
        Assert.That(GmParlorCore.IsLegal(hand, 1, Card(GmSuit.Bones, 6)), Is.True);
        Assert.That(GmParlorCore.IsLegal(hand, lead, Card(GmSuit.Teeth, 4)), Is.False,
            "a card outside the paid hand is never legal");
    }

    [Test]
    public void AldricUsesCleanWinningFollowEvenWhenCheatingIsAllowed()
    {
        var hand = new List<GmCard>
        {
            Card(GmSuit.Eyes, 6), Card(GmSuit.Eyes, 3), Card(GmSuit.Flames, 7),
        };

        GmAldricPlay play = GmParlorCore.ChooseAldricFollow(
            hand, Card(GmSuit.Eyes, 5), true, GmAldricFollowPolicy.HouseFirstHeldFlame);

        Assert.That(play.Cheated, Is.False);
        Assert.That(play.CheatKind, Is.EqualTo(GmParlorCheatKind.None));
        Assert.That(play.Card, Is.EqualTo(Card(GmSuit.Eyes, 6)));
        Assert.That(GmParlorCore.IsLegal(hand, play.RemovedIndex, Card(GmSuit.Eyes, 5)), Is.True);
    }

    [Test]
    public void AldricLeadSelectionStaysInsideTheSharedCorePolicies()
    {
        var hand = new List<GmCard>
        {
            Card(GmSuit.Flames, 7), Card(GmSuit.Eyes, 5), Card(GmSuit.Bones, 2),
        };

        Assert.That(GmParlorCore.ChooseAldricLead(hand), Is.EqualTo(2),
            "the house tutorial keeps its lowest-side-suit lead");
        Assert.That(GmParlorCore.ChooseAldricLead(hand, GmAldricLeadPolicy.HighestSideSuit), Is.EqualTo(1),
            "the shipping adapter keeps its highest-side-suit lead");
    }

    [Test]
    public void AldricCheatsOnlyWhenEveryLegalResponseLoses()
    {
        var hand = new List<GmCard>
        {
            Card(GmSuit.Eyes, 2), Card(GmSuit.Eyes, 3), Card(GmSuit.Flames, 4),
        };
        GmCard lead = Card(GmSuit.Eyes, 7);

        GmAldricPlay honest = GmParlorCore.ChooseAldricFollow(
            hand, lead, false, GmAldricFollowPolicy.HouseFirstHeldFlame);
        GmAldricPlay cheat = GmParlorCore.ChooseAldricFollow(
            hand, lead, true, GmAldricFollowPolicy.HouseFirstHeldFlame);

        Assert.That(honest.Cheated, Is.False);
        Assert.That(honest.Card.Suit, Is.EqualTo(GmSuit.Eyes));
        Assert.That(cheat.Cheated, Is.True);
        Assert.That(cheat.CheatKind, Is.EqualTo(GmParlorCheatKind.RenegedWithHeldFlame));
        Assert.That(cheat.Card, Is.EqualTo(Card(GmSuit.Flames, 4)));
        Assert.That(cheat.RemovedIndex, Is.EqualTo(2));
        Assert.That(hand[cheat.RemovedIndex], Is.EqualTo(cheat.Card), "the renege must pay its held Flame");
        Assert.That(GmParlorCore.IsLegal(hand, cheat.RemovedIndex, lead), Is.False);
        Assert.That(cheat.Tell, Does.Contain("held Eyes"));
    }

    [Test]
    public void FollowPolicyPreservesHouseFirstHeldAndShippingHighestFlame()
    {
        var hand = new List<GmCard>
        {
            Card(GmSuit.Eyes, 2), Card(GmSuit.Flames, 3), Card(GmSuit.Flames, 7),
        };
        GmCard lead = Card(GmSuit.Eyes, 7);

        GmAldricPlay house = GmParlorCore.ChooseAldricFollow(
            hand, lead, true, GmAldricFollowPolicy.HouseFirstHeldFlame);
        GmAldricPlay shipping = GmParlorCore.ChooseAldricFollow(
            hand, lead, true, GmAldricFollowPolicy.ShippingHighestWinningFlame);

        Assert.That(house.Card, Is.EqualTo(Card(GmSuit.Flames, 3)));
        Assert.That(house.RemovedIndex, Is.EqualTo(1));
        Assert.That(shipping.Card, Is.EqualTo(Card(GmSuit.Flames, 7)));
        Assert.That(shipping.RemovedIndex, Is.EqualTo(2));
    }

    [Test]
    public void ImpossibleEighthRankPaysTheSelectedRealHandIndex()
    {
        var hand = new List<GmCard> { Card(GmSuit.Bones, 2), Card(GmSuit.Bones, 1) };

        GmAldricPlay play = GmParlorCore.ChooseAldricFollow(
            hand, Card(GmSuit.Bones, 7), true, GmAldricFollowPolicy.HouseFirstHeldFlame);

        Assert.That(play.Cheated, Is.True);
        Assert.That(play.CheatKind, Is.EqualTo(GmParlorCheatKind.ImpossibleEighthRank));
        Assert.That(play.Card, Is.EqualTo(Card(GmSuit.Bones, 8)));
        Assert.That(play.RemovedIndex, Is.EqualTo(1), "the lowest losing card is the paid hand card");
        Assert.That(hand[play.RemovedIndex], Is.EqualTo(Card(GmSuit.Bones, 1)));
        Assert.That(play.Tell, Does.Contain("seven ranks"));
    }

    [Test]
    public void SuitValuesAndCardLabelsPreserveBothContracts()
    {
        Assert.That((int)GmSuit.Flames, Is.EqualTo(0));
        Assert.That((int)GmSuit.Eyes, Is.EqualTo(1));
        Assert.That((int)GmSuit.Bones, Is.EqualTo(2));
        Assert.That((int)GmSuit.Teeth, Is.EqualTo(3));

        GmCard flame = Card(GmSuit.Flames, 1);
        Assert.That(flame.ShortName, Is.EqualTo("F1"));
        Assert.That(flame.TableLabel, Is.EqualTo("1 Flames"));
        Assert.That(Card(GmSuit.Bones, 7).ShortName, Is.EqualTo("B7"));
        Assert.That(Card(GmSuit.Teeth, 4).TableLabel, Is.EqualTo("4 Teeth"));
    }

    [Test]
    public void HandApiNullAndEmptyContractsAreConsistent()
    {
        Assert.Throws<ArgumentNullException>(() => GmParlorCore.MustFollow(null, Card(GmSuit.Eyes, 1)));
        Assert.Throws<ArgumentNullException>(() => GmParlorCore.IsLegal(null, 0, Card(GmSuit.Eyes, 1)));
        Assert.Throws<ArgumentNullException>(() =>
            GmParlorCore.IsLegal(null, Card(GmSuit.Eyes, 1), Card(GmSuit.Eyes, 2)));
        Assert.Throws<ArgumentNullException>(() => GmParlorCore.ChooseAldricLead(null));
        Assert.That(GmParlorCore.ChooseAldricLead(new List<GmCard>()), Is.EqualTo(-1));
        Assert.Throws<ArgumentNullException>(() => GmParlorCore.ChooseAldricFollow(
            null, Card(GmSuit.Eyes, 1), false, GmAldricFollowPolicy.HouseFirstHeldFlame));
        Assert.Throws<ArgumentException>(() => GmParlorCore.ChooseAldricFollow(
            new List<GmCard>(), Card(GmSuit.Eyes, 1), false,
            GmAldricFollowPolicy.HouseFirstHeldFlame));
    }

    [Test]
    public void LegacyRuleTypesDoNotSurviveBesideTheSharedAuthority()
    {
        Assert.That(typeof(GmParlorCore).Assembly.GetType("GmCard" + "Suit"), Is.Null);
        Assert.That(typeof(GmParlorCore).Assembly.GetType("GmParlor" + "Card"), Is.Null);
        Assert.That(typeof(GmParlorCore).Assembly.GetType("GmWendParlor" + "Rules"), Is.Null);
        Assert.That(typeof(GmParlorCore).Assembly.GetType("GmDeck" + "Utility"), Is.Null);
    }

    static GmCard Card(GmSuit suit, int rank) => new GmCard(suit, rank);
}
