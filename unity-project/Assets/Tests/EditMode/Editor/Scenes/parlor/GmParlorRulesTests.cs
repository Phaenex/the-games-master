using System.Collections.Generic;
using System.IO;
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
        List<GmCard> deck = GmParlorCore.ShuffledDeck(42);
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
    public void ShippingAdapterPreservesThePureEnginesSeededDeal()
    {
        var firstObject = new GameObject("FirstRules");
        var secondObject = new GameObject("SecondRules");
        var first = firstObject.AddComponent<GmParlorRules>();
        var second = secondObject.AddComponent<GmParlorRules>();
        try
        {
            first.StartGame(42);
            second.StartGame(42);
            Assert.AreEqual(Codes(first.PlayerHand) + "|" + Codes(first.HostHand),
                Codes(second.PlayerHand) + "|" + Codes(second.HostHand));
            Assert.That(first.PlayerHand, Has.Count.EqualTo(7));
            Assert.That(first.HostHand, Has.Count.EqualTo(7));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
        }
    }

    [Test]
    public void FlamesTrumpOffSuit()
    {
        var lead = new GmCard(GmSuit.Eyes, 7);
        var follow = new GmCard(GmSuit.Flames, 1);

        // 1 of Flames trumps 7 of Eyes
        Assert.IsFalse(GmParlorCore.LeadWins(lead, follow), "1 of Flames must trump 7 of Eyes");
    }

    [Test]
    public void LeadSuitBeatsOffSuitNonTrump()
    {
        var lead = new GmCard(GmSuit.Eyes, 2);
        var follow = new GmCard(GmSuit.Teeth, 7);

        // 2 of Eyes beats 7 of Teeth when Eyes was led
        Assert.IsTrue(GmParlorCore.LeadWins(lead, follow), "2 of lead suit must beat off-suit non-trump");
    }

    [Test]
    public void HigherRankWinsInSuit()
    {
        var lead = new GmCard(GmSuit.Bones, 3);
        var follow = new GmCard(GmSuit.Bones, 5);

        // 5 of Bones beats 3 of Bones
        Assert.IsFalse(GmParlorCore.LeadWins(lead, follow), "Higher rank in-suit must win trick");
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
        Assert.IsTrue(GmParlorCore.IsLegal(hand, leadEyes, new GmCard(GmSuit.Eyes, 3)));

        // Playing Bones or Flames while holding Eyes is ILLEGAL
        Assert.IsFalse(GmParlorCore.IsLegal(hand, leadEyes, new GmCard(GmSuit.Bones, 6)));
        Assert.IsFalse(GmParlorCore.IsLegal(hand, leadEyes, new GmCard(GmSuit.Flames, 2)));

        // If player has NO teeth, they can play any card when Teeth is led
        var leadTeeth = new GmCard(GmSuit.Teeth, 4);
        Assert.IsTrue(GmParlorCore.IsLegal(hand, leadTeeth, new GmCard(GmSuit.Eyes, 3)));
        Assert.IsTrue(GmParlorCore.IsLegal(hand, leadTeeth, new GmCard(GmSuit.Flames, 2)));
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
        Assert.IsFalse(GmParlorCore.IsLegal(hand, playerLead, chosen),
            "a palm that was legal all along is not a cheat");

        Object.DestroyImmediate(hostAIObj);
    }

    [Test]
    public void HostAIShippingPolicyPalmsTheHighestWinningHeldFlame()
    {
        var hostAIObj = new GameObject("TestHostAI");
        var hostAI = hostAIObj.AddComponent<GmHostAI>();
        var hand = new List<GmCard>
        {
            new GmCard(GmSuit.Eyes, 2),
            new GmCard(GmSuit.Flames, 3),
            new GmCard(GmSuit.Flames, 7),
        };

        GmCard chosen = hostAI.ChooseFollowCard(hand, new GmCard(GmSuit.Eyes, 7), 3, 0);

        Assert.IsTrue(hostAI.LastPlayWasCheated);
        Assert.AreEqual(new GmCard(GmSuit.Flames, 7), chosen);
        Object.DestroyImmediate(hostAIObj);
    }

    [Test]
    public void AdapterDelegatesPlayerLeadAndAutomaticAldricFollow()
    {
        var rulesObject = new GameObject("ParlorRules");
        var rules = rulesObject.AddComponent<GmParlorRules>();
        try
        {
            rules.StartGame(11, readInitiallyUnlocked: true);
            Assert.That(rules.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
            Assert.That(rules.State, Is.EqualTo(GmParlorState.AccusationWindow));
            Assert.That(rules.CurrentLeadCard, Is.Not.Null);
            Assert.That(rules.CurrentFollowCard, Is.Not.Null);
            Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(rules.State, Is.EqualTo(GmParlorState.TrickResolving));
            Assert.That(rules.PlayerTricksWon + rules.HostTricksWon, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(rulesObject); }
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

    [Test]
    public void HostAISelectionDelegatesToTheSharedCore()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts/Scenes/parlor/GmHostAI.cs"));

        StringAssert.Contains("GmParlorCore.ChooseAldricLead", source);
        StringAssert.Contains("GmParlorCore.ChooseAldricFollow", source);
        StringAssert.DoesNotContain("TryPalmWinningFlames", source);
    }

    [Test]
    public void FinishingAParlorMatchCompletesExactlyOneTableGame()
    {
        var rulesObject = new GameObject("ParlorRules");
        var rules = rulesObject.AddComponent<GmParlorRules>();
        int completed = 0;
        bool reportedPlayerWin = false;
        rules.OnGameCompleted += playerWon => { completed++; reportedPlayerWin = playerWon; };

        try
        {
            rules.StartGame(seed: 42, readInitiallyUnlocked: true);
            int guard = 180;
            while (rules.State != GmParlorState.GameOver && guard-- > 0)
            {
                if (rules.State == GmParlorState.PlayerTurn)
                {
                    int index = FirstLegalIndex(rules.Match);
                    Assert.That(rules.PlayPlayerCard(index), Is.EqualTo(GmParlorActionError.None));
                }
                else if (rules.State == GmParlorState.AccusationWindow)
                    Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.None));
                else if (rules.State == GmParlorState.TrickResolving ||
                         rules.State == GmParlorState.HandComplete)
                    Assert.That(rules.ContinueResult(), Is.EqualTo(GmParlorActionError.None));
                else Assert.Fail($"match stalled in {rules.State}");
            }

            Assert.Greater(guard, 0, "the deterministic match never reached a real completion");
            Assert.AreEqual(1, completed);
            Assert.AreEqual(rules.Match.MatchWinner == GmTrickOwner.Player, reportedPlayerWin);
            Assert.IsTrue(GmRunStore.IsRoomComplete("parlor"));
            Assert.AreEqual(1, GmRunStore.TableGameIndex);
        }
        finally { Object.DestroyImmediate(rulesObject); }
    }

    [Test]
    public void ShippingRulesAreAPresentationAdapterOverPureMatchAuthority()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts/Scenes/parlor/GmParlorRules.cs"));

        StringAssert.Contains("new GmParlorMatch", source);
        StringAssert.Contains("Match.PlayPlayerCard", source);
        StringAssert.Contains("Match.ContinueJudgement", source);
        StringAssert.Contains("Match.Read", source);
        StringAssert.Contains("Match.Continue", source);
        StringAssert.DoesNotContain("void ResolveTrick()", source);
        StringAssert.DoesNotContain("GmHostAI", source);
    }

    [Test]
    public void AdapterExposesJudgementAndRoundBoundariesAndCompletesRoomOnce()
    {
        var rulesObject = new GameObject("ParlorRules");
        var rules = rulesObject.AddComponent<GmParlorRules>();
        int games = 0;
        int rounds = 0;
        rules.OnGameCompleted += _ => games++;
        rules.OnRoundCompleted += _ => rounds++;

        try
        {
            rules.StartGame(seed: 918, corruptionTier: 2, priorCatchCount: 6,
                readInitiallyUnlocked: true);
            Assert.That(rules.Match, Is.Not.Null);
            Assert.That(rules.Match.PriorCatchCount, Is.EqualTo(6));

            int guard = 180;
            while (rules.State != GmParlorState.GameOver && guard-- > 0)
            {
                if (rules.State == GmParlorState.PlayerTurn)
                {
                    int index = FirstLegalIndex(rules.Match);
                    Assert.That(rules.PlayPlayerCard(index), Is.EqualTo(GmParlorActionError.None));
                }
                else if (rules.State == GmParlorState.AccusationWindow)
                    Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.None));
                else if (rules.State == GmParlorState.TrickResolving ||
                         rules.State == GmParlorState.HandComplete)
                    Assert.That(rules.ContinueResult(), Is.EqualTo(GmParlorActionError.None));
                else Assert.Fail("adapter stalled in " + rules.State);
            }

            Assert.That(guard, Is.GreaterThan(0));
            Assert.That(rounds, Is.InRange(2, 3));
            Assert.That(games, Is.EqualTo(1));
            Assert.That(GmRunStore.IsRoomComplete("parlor"), Is.True);
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
            Assert.That(rules.ContinueResult(), Is.EqualTo(GmParlorActionError.WrongPhase));
            Assert.That(games, Is.EqualTo(1));
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(rulesObject); }
    }

    [Test]
    public void AdapterHandsAreAllocationFreeReadOnlyViewsAndOutcomeEventIsExactlyOnce()
    {
        var rulesObject = new GameObject("ParlorRules");
        var rules = rulesObject.AddComponent<GmParlorRules>();
        int outcomes = 0;
        ulong deliveredSequence = 0;
        rules.OnOutcomeReady += (sequence, _) =>
        {
            outcomes++;
            deliveredSequence = sequence;
        };

        try
        {
            rules.StartGame(seed: 411, readInitiallyUnlocked: true);
            IReadOnlyList<GmCard> player = rules.PlayerHand;
            IReadOnlyList<GmCard> host = rules.HostHand;
            Assert.That(rules.PlayerHand, Is.SameAs(player));
            Assert.That(rules.HostHand, Is.SameAs(host));
            Assert.That(((IList<GmCard>)player).IsReadOnly, Is.True);
            Assert.That(((IList<GmCard>)host).IsReadOnly, Is.True);
            Assert.Throws<System.NotSupportedException>(() =>
                ((IList<GmCard>)player).RemoveAt(0));

            Assert.That(rules.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
            Assert.That(outcomes, Is.EqualTo(0));
            Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(deliveredSequence, Is.EqualTo(1));

            _ = rules.State;
            _ = rules.Match.LastOutcome;
            _ = rules.PlayerHand.Count;
            Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(rules.ContinueResult(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(outcomes, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(rulesObject); }
    }

    [Test]
    public void AdapterRematchUsesNormalEventsCarriesTeachingAndCompletesEachMatchOnce()
    {
        int seed = FindDeferredTeachingSeed();
        var rulesObject = new GameObject("ParlorRules");
        var rules = rulesObject.AddComponent<GmParlorRules>();
        int states = 0;
        int outcomes = 0;
        int games = 0;
        rules.OnStateChanged += () => states++;
        rules.OnOutcomeReady += (_, __) => outcomes++;
        rules.OnGameCompleted += _ => games++;

        try
        {
            rules.StartGame(seed, readInitiallyUnlocked: false);
            DriveAdapterAcceptingToGameOver(rules);
            Assert.That(rules.Match.ReadTestDeferred, Is.True);
            Assert.That(games, Is.EqualTo(1));
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1));
            int suspicion = rules.Match.Suspicion;
            int stateEventsBefore = states;
            int outcomeEventsBefore = outcomes;

            Assert.That(rules.StartRematch(), Is.EqualTo(GmParlorActionError.None));
            Assert.That(states, Is.EqualTo(stateEventsBefore + 1));
            Assert.That(outcomes, Is.EqualTo(outcomeEventsBefore));
            Assert.That(games, Is.EqualTo(1));
            Assert.That(rules.Match.Suspicion, Is.EqualTo(suspicion));
            Assert.That(rules.State, Is.EqualTo(GmParlorState.PlayerTurn));

            DriveAdapterAcceptingToGameOver(rules);
            Assert.That(games, Is.EqualTo(2), "each rematch reports exactly one result");
            Assert.That(GmRunStore.TableGameIndex, Is.EqualTo(1),
                "room completion persistence remains idempotent across rematches");
        }
        finally { Object.DestroyImmediate(rulesObject); }
    }

    static GmCard FirstLegal(List<GmCard> hand, GmCard? lead)
    {
        foreach (GmCard card in hand)
            if (!lead.HasValue || GmParlorCore.IsLegal(hand, lead.Value, card)) return card;
        Assert.Fail("hand has no legal card under follow-suit rules");
        return default;
    }

    static int FirstLegalIndex(GmParlorMatch match)
    {
        for (int i = 0; i < match.PlayerHand.Count; i++)
            if (match.GetPlayerCardError(i) == GmParlorActionError.None) return i;
        return -1;
    }

    static int FindDeferredTeachingSeed()
    {
        for (int seed = 1; seed <= 300; seed++)
        {
            var match = new GmParlorMatch(seed, 1, 0, readInitiallyUnlocked: false);
            match.Start();
            DrivePureAcceptingToGameOver(match);
            if (match.ReadTestDeferred) return seed;
        }
        Assert.Fail("no deferred teaching seed found");
        return -1;
    }

    static void DrivePureAcceptingToGameOver(GmParlorMatch match)
    {
        int guard = 180;
        while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            if (match.Phase == GmParlorMatchPhase.PlayerLeads ||
                match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
                Assert.That(match.PlayPlayerCard(FirstLegalIndex(match)),
                    Is.EqualTo(GmParlorActionError.None));
            else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
                Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
            else
                Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
        }
        Assert.That(guard, Is.GreaterThan(0));
    }

    static void DriveAdapterAcceptingToGameOver(GmParlorRules rules)
    {
        int guard = 180;
        while (rules.State != GmParlorState.GameOver && guard-- > 0)
        {
            if (rules.State == GmParlorState.PlayerTurn)
                Assert.That(rules.PlayPlayerCard(FirstLegalIndex(rules.Match)),
                    Is.EqualTo(GmParlorActionError.None));
            else if (rules.State == GmParlorState.AccusationWindow)
                Assert.That(rules.AcceptAldricPlay(), Is.EqualTo(GmParlorActionError.None));
            else
                Assert.That(rules.ContinueResult(), Is.EqualTo(GmParlorActionError.None));
        }
        Assert.That(guard, Is.GreaterThan(0));
    }

    static string Codes(IEnumerable<GmCard> cards) => string.Join(",", cards);
}
