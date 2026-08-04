using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class GmParlorRulesTests
{
    [Test]
    public void DeckIsDeterministicAndContainsEveryCardExactlyOnce()
    {
        List<GmParlorCard> first = GmParlorRules.ShuffledDeck(41);
        List<GmParlorCard> second = GmParlorRules.ShuffledDeck(41);
        Assert.That(first.Count, Is.EqualTo(28));
        Assert.That(first.Distinct().Count(), Is.EqualTo(28));
        CollectionAssert.AreEqual(first, second);
        Assert.That(first.Count(card => card.Suit == GmCardSuit.Flames), Is.EqualTo(7));
    }

    [Test]
    public void FlamesTrumpAndOffSuitCannotBeatTheLead()
    {
        Assert.That(GmParlorRules.Winner(Card(GmCardSuit.Eyes, 7), Card(GmCardSuit.Flames, 1)),
            Is.EqualTo(GmTrickOwner.Aldric));
        Assert.That(GmParlorRules.Winner(Card(GmCardSuit.Flames, 1), Card(GmCardSuit.Eyes, 7)),
            Is.EqualTo(GmTrickOwner.Player));
        Assert.That(GmParlorRules.Winner(Card(GmCardSuit.Teeth, 2), Card(GmCardSuit.Bones, 7)),
            Is.EqualTo(GmTrickOwner.Player));
    }

    [Test]
    public void FollowSuitIsEnforcedOnlyWhileThatSuitIsHeld()
    {
        var hand = new List<GmParlorCard> { Card(GmCardSuit.Eyes, 2), Card(GmCardSuit.Flames, 7) };
        Assert.That(GmParlorRules.IsLegal(hand, 0, Card(GmCardSuit.Eyes, 6)), Is.True);
        Assert.That(GmParlorRules.IsLegal(hand, 1, Card(GmCardSuit.Eyes, 6)), Is.False);
        Assert.That(GmParlorRules.IsLegal(hand, 1, Card(GmCardSuit.Bones, 6)), Is.True);
    }

    [Test]
    public void AldricMustUseALegalWinningCardWhenOneExists()
    {
        var hand = new List<GmParlorCard>
        {
            Card(GmCardSuit.Eyes, 6), Card(GmCardSuit.Eyes, 3), Card(GmCardSuit.Flames, 7),
        };
        GmAldricPlay play = GmParlorRules.ChooseAldricFollow(hand, Card(GmCardSuit.Eyes, 5), true);
        Assert.That(play.Cheated, Is.False);
        Assert.That(play.Card, Is.EqualTo(Card(GmCardSuit.Eyes, 6)));
        Assert.That(GmParlorRules.IsLegal(hand, play.RemovedIndex, Card(GmCardSuit.Eyes, 5)), Is.True);
    }

    [Test]
    public void AldricCheatsOnlyWhenEveryLegalPlayLoses()
    {
        var hand = new List<GmParlorCard>
        {
            Card(GmCardSuit.Eyes, 2), Card(GmCardSuit.Eyes, 3), Card(GmCardSuit.Flames, 4),
        };
        GmParlorCard lead = Card(GmCardSuit.Eyes, 7);
        GmAldricPlay honest = GmParlorRules.ChooseAldricFollow(hand, lead, false);
        Assert.That(honest.Cheated, Is.False);
        Assert.That(honest.Card.Suit, Is.EqualTo(GmCardSuit.Eyes));

        GmAldricPlay constrainedCheat = GmParlorRules.ChooseAldricFollow(hand, lead, true);
        Assert.That(constrainedCheat.Cheated, Is.True);
        Assert.That(constrainedCheat.Card.Suit, Is.EqualTo(GmCardSuit.Flames));
        Assert.That(GmParlorRules.IsLegal(hand, constrainedCheat.RemovedIndex, lead), Is.False);
        Assert.That(constrainedCheat.Tell, Does.Contain("held Eyes"));
    }

    [Test]
    public void ImpossibleEighthRankIsUsedWhenRenegeCannotWin()
    {
        var hand = new List<GmParlorCard> { Card(GmCardSuit.Bones, 1), Card(GmCardSuit.Bones, 2) };
        GmAldricPlay play = GmParlorRules.ChooseAldricFollow(hand, Card(GmCardSuit.Bones, 7), true);
        Assert.That(play.Cheated, Is.True);
        Assert.That(play.Card, Is.EqualTo(Card(GmCardSuit.Bones, 8)));
        Assert.That(play.Tell, Does.Contain("seven ranks"));
    }

    static GmParlorCard Card(GmCardSuit suit, int rank) => new GmParlorCard(suit, rank);
}
