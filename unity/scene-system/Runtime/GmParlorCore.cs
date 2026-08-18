using System;
using System.Collections.Generic;
using System.Linq;

public enum GmSuit
{
    Flames = 0,
    Eyes = 1,
    Bones = 2,
    Teeth = 3,
}

public enum GmTrickOwner
{
    Player,
    Aldric,
}

public enum GmParlorCheatKind
{
    None,
    RenegedWithHeldFlame,
    ImpossibleEighthRank,
}

public enum GmAldricLeadPolicy
{
    LowestSideSuit,
    HighestSideSuit,
}

public enum GmAldricFollowPolicy
{
    HouseFirstHeldFlame,
    ShippingHighestWinningFlame,
}

public enum GmParlorDeckOrder
{
    Canonical,
    ShippingLegacy,
}

[Serializable]
public struct GmCard : IEquatable<GmCard>
{
    public GmSuit Suit;
    public int Rank;

    public GmCard(GmSuit suit, int rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public string ShortName => $"{Suit.ToString()[0]}{Rank}";
    public string TableLabel => $"{Rank} {Suit}";
    public string DisplayName => $"{Rank} of {Suit}";

    public bool Equals(GmCard other) => Suit == other.Suit && Rank == other.Rank;
    public override bool Equals(object obj) => obj is GmCard other && Equals(other);
    public override int GetHashCode() => ((int)Suit * 397) ^ Rank;
    public override string ToString() => DisplayName;

    public static bool operator ==(GmCard left, GmCard right) => left.Equals(right);
    public static bool operator !=(GmCard left, GmCard right) => !left.Equals(right);
}

public struct GmAldricPlay
{
    public GmCard Card;
    public int RemovedIndex;
    public GmParlorCheatKind CheatKind;
    public string Tell;

    public bool Cheated => CheatKind != GmParlorCheatKind.None;
}

/// <summary>
/// The single pure authority for Parlor card legality, trick resolution, seeded deals and
/// Aldric's deterministic card choice. Scene components decide when cheating is permitted; this
/// type decides what the hand can actually pay and records exactly how a dishonest play was made.
/// </summary>
public static class GmParlorCore
{
    public const int Suits = 4;
    public const int RanksPerSuit = 7;
    public const int TotalCards = Suits * RanksPerSuit;
    public const int HandSize = 7;

    public static List<GmCard> ShuffledDeck(
        int seed, GmParlorDeckOrder order = GmParlorDeckOrder.Canonical)
    {
        var deck = new List<GmCard>(TotalCards);
        GmSuit[] suits = order == GmParlorDeckOrder.ShippingLegacy
            ? new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Bones, GmSuit.Teeth }
            : new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones };
        foreach (GmSuit suit in suits)
            for (int rank = 1; rank <= RanksPerSuit; rank++)
                deck.Add(new GmCard(suit, rank));

        var random = new Random(seed);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            GmCard held = deck[i];
            deck[i] = deck[swap];
            deck[swap] = held;
        }
        return deck;
    }

    public static bool MustFollow(IReadOnlyList<GmCard> hand, GmCard lead)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        for (int i = 0; i < hand.Count; i++)
            if (hand[i].Suit == lead.Suit) return true;
        return false;
    }

    public static bool IsLegal(IReadOnlyList<GmCard> hand, int index, GmCard? lead)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        if (index < 0 || index >= hand.Count) return false;
        return !lead.HasValue || !MustFollow(hand, lead.Value) || hand[index].Suit == lead.Value.Suit;
    }

    public static bool IsLegal(IReadOnlyList<GmCard> hand, GmCard lead, GmCard card)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] == card) return IsLegal(hand, i, lead);
        return false;
    }

    public static bool LeadWins(GmCard lead, GmCard follow)
    {
        if (follow.Suit == GmSuit.Flames && lead.Suit != GmSuit.Flames) return false;
        if (lead.Suit == GmSuit.Flames && follow.Suit != GmSuit.Flames) return true;
        if (lead.Suit != follow.Suit) return true;
        return lead.Rank >= follow.Rank;
    }

    public static int ChooseAldricLead(
        IReadOnlyList<GmCard> hand, GmAldricLeadPolicy policy = GmAldricLeadPolicy.LowestSideSuit)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        if (hand.Count == 0) return -1;
        int bestSideSuit = -1;
        int bestFlame = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].Suit == GmSuit.Flames)
            {
                if (bestFlame < 0 || (policy == GmAldricLeadPolicy.HighestSideSuit &&
                                      hand[i].Rank > hand[bestFlame].Rank)) bestFlame = i;
                continue;
            }

            if (bestSideSuit < 0 ||
                (policy == GmAldricLeadPolicy.LowestSideSuit && hand[i].Rank < hand[bestSideSuit].Rank) ||
                (policy == GmAldricLeadPolicy.HighestSideSuit && hand[i].Rank > hand[bestSideSuit].Rank))
                bestSideSuit = i;
        }
        return bestSideSuit >= 0 ? bestSideSuit : bestFlame;
    }

    public static int ChooseAldricLead(IReadOnlyList<GmCard> hand,
        GmParlorHonestStrategyId strategy)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        if (hand.Count == 0) return -1;
        if (!Enum.IsDefined(typeof(GmParlorHonestStrategyId), strategy))
            throw new ArgumentOutOfRangeException(nameof(strategy));

        if (TryPreferredSuit(strategy, out GmSuit preferred))
        {
            int nonPreferred = SelectByRank(hand, card => card.Suit != preferred,
                highest: true);
            return nonPreferred >= 0 ? nonPreferred : SelectByRank(hand,
                card => card.Suit == preferred, highest: false);
        }

        bool highest = strategy == GmParlorHonestStrategyId.TrumpReserve ||
            strategy == GmParlorHonestStrategyId.CounterConservation ||
            strategy == GmParlorHonestStrategyId.SecondDealHigh;
        if (strategy == GmParlorHonestStrategyId.SecondDealReserve)
        {
            int side = SelectByRank(hand, card => card.Suit != GmSuit.Flames, highest: true);
            return side >= 0 ? side : SelectByRank(hand, card => true, highest: false);
        }
        return ChooseAldricLead(hand, highest ? GmAldricLeadPolicy.HighestSideSuit :
            GmAldricLeadPolicy.LowestSideSuit);
    }

    public static GmAldricPlay ChooseAldricFollow(
        IReadOnlyList<GmCard> hand, GmCard playerLead, bool allowCheat,
        GmAldricFollowPolicy policy = GmAldricFollowPolicy.HouseFirstHeldFlame)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        if (hand.Count == 0)
            throw new ArgumentException("Aldric needs a card to follow", nameof(hand));

        int cleanWinner = -1;
        int cleanLoss = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            if (!IsLegal(hand, i, playerLead)) continue;
            bool wins = !LeadWins(playerLead, hand[i]);
            if (wins && (cleanWinner < 0 || hand[i].Rank < hand[cleanWinner].Rank)) cleanWinner = i;
            if (!wins && (cleanLoss < 0 || hand[i].Rank < hand[cleanLoss].Rank)) cleanLoss = i;
        }

        if (cleanWinner >= 0) return Honest(hand, cleanWinner);
        if (cleanLoss < 0) throw new InvalidOperationException("A non-empty hand must have a legal response");
        if (!allowCheat) return Honest(hand, cleanLoss);

        if (MustFollow(hand, playerLead) && playerLead.Suit != GmSuit.Flames)
        {
            int winningFlame = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].Suit != GmSuit.Flames ||
                    LeadWins(playerLead, hand[i])) continue;
                if (winningFlame < 0 ||
                    (policy == GmAldricFollowPolicy.ShippingHighestWinningFlame &&
                     hand[i].Rank > hand[winningFlame].Rank)) winningFlame = i;
            }
            if (winningFlame >= 0)
            {
                return new GmAldricPlay
                {
                    Card = hand[winningFlame],
                    RemovedIndex = winningFlame,
                    CheatKind = GmParlorCheatKind.RenegedWithHeldFlame,
                    Tell = $"He held {playerLead.Suit}, but answered in Flames.",
                };
            }
        }

        return new GmAldricPlay
        {
            Card = new GmCard(playerLead.Suit, RanksPerSuit + 1),
            RemovedIndex = cleanLoss,
            CheatKind = GmParlorCheatKind.ImpossibleEighthRank,
            Tell = $"There are seven ranks. Aldric laid down an eight of {playerLead.Suit}.",
        };
    }

    public static GmAldricPlay ChooseAldricFollow(IReadOnlyList<GmCard> hand,
        GmCard playerLead, bool allowCheat, GmParlorHonestStrategyId strategy)
    {
        if (hand == null) throw new ArgumentNullException(nameof(hand));
        if (hand.Count == 0)
            throw new ArgumentException("Aldric needs a card to follow", nameof(hand));
        if (!Enum.IsDefined(typeof(GmParlorHonestStrategyId), strategy))
            throw new ArgumentOutOfRangeException(nameof(strategy));

        var winners = new List<int>();
        var losses = new List<int>();
        for (int index = 0; index < hand.Count; index++)
        {
            if (!IsLegal(hand, index, playerLead)) continue;
            if (!LeadWins(playerLead, hand[index])) winners.Add(index);
            else losses.Add(index);
        }

        if (winners.Count > 0)
            return Honest(hand, SelectAdaptiveResponse(hand, winners, strategy, winning: true));
        if (losses.Count == 0)
            throw new InvalidOperationException("A non-empty hand must have a legal response");
        if (!allowCheat)
            return Honest(hand, SelectAdaptiveResponse(hand, losses, strategy, winning: false));

        if (MustFollow(hand, playerLead) && playerLead.Suit != GmSuit.Flames)
        {
            var cheatingFlames = new List<int>();
            for (int index = 0; index < hand.Count; index++)
                if (hand[index].Suit == GmSuit.Flames && !LeadWins(playerLead, hand[index]))
                    cheatingFlames.Add(index);
            if (cheatingFlames.Count > 0)
            {
                bool courtesy = strategy == GmParlorHonestStrategyId.TrumpReserve;
                int flame = courtesy ? cheatingFlames[0] : cheatingFlames
                    .OrderByDescending(index => hand[index].Rank).First();
                return new GmAldricPlay
                {
                    Card = hand[flame],
                    RemovedIndex = flame,
                    CheatKind = GmParlorCheatKind.RenegedWithHeldFlame,
                    Tell = $"He held {playerLead.Suit}, but answered in Flames.",
                };
            }
        }

        int paid = SelectAdaptiveResponse(hand, losses, strategy, winning: false);
        return new GmAldricPlay
        {
            Card = new GmCard(playerLead.Suit, RanksPerSuit + 1),
            RemovedIndex = paid,
            CheatKind = GmParlorCheatKind.ImpossibleEighthRank,
            Tell = $"There are seven ranks. Aldric laid down an eight of {playerLead.Suit}.",
        };
    }

    static int SelectAdaptiveResponse(IReadOnlyList<GmCard> hand, List<int> candidates,
        GmParlorHonestStrategyId strategy, bool winning)
    {
        if (TryPreferredSuit(strategy, out GmSuit preferred))
        {
            List<int> outside = candidates.Where(index => hand[index].Suit != preferred).ToList();
            if (outside.Count > 0) candidates = outside;
        }
        if (strategy == GmParlorHonestStrategyId.TrumpReserve ||
            strategy == GmParlorHonestStrategyId.SecondDealReserve ||
            strategy == GmParlorHonestStrategyId.CounterConservation)
        {
            List<int> side = candidates.Where(index => hand[index].Suit != GmSuit.Flames).ToList();
            if (side.Count > 0) candidates = side;
        }
        bool highest = strategy == GmParlorHonestStrategyId.SecondDealHigh ||
            (!winning && (strategy == GmParlorHonestStrategyId.TrumpReserve ||
                          strategy == GmParlorHonestStrategyId.CounterConservation));
        return highest
            ? candidates.OrderByDescending(index => hand[index].Rank).ThenBy(index => index).First()
            : candidates.OrderBy(index => hand[index].Rank).ThenBy(index => index).First();
    }

    static bool TryPreferredSuit(GmParlorHonestStrategyId strategy, out GmSuit suit)
    {
        switch (strategy)
        {
            case GmParlorHonestStrategyId.PreferredSuitReserveFlames:
                suit = GmSuit.Flames;
                return true;
            case GmParlorHonestStrategyId.PreferredSuitReserveEyes:
                suit = GmSuit.Eyes;
                return true;
            case GmParlorHonestStrategyId.PreferredSuitReserveBones:
                suit = GmSuit.Bones;
                return true;
            case GmParlorHonestStrategyId.PreferredSuitReserveTeeth:
                suit = GmSuit.Teeth;
                return true;
            default:
                suit = default;
                return false;
        }
    }

    static int SelectByRank(IReadOnlyList<GmCard> hand, Func<GmCard, bool> predicate,
        bool highest)
    {
        int selected = -1;
        for (int index = 0; index < hand.Count; index++)
        {
            if (!predicate(hand[index])) continue;
            if (selected < 0 || (highest && hand[index].Rank > hand[selected].Rank) ||
                (!highest && hand[index].Rank < hand[selected].Rank)) selected = index;
        }
        return selected;
    }

    static GmAldricPlay Honest(IReadOnlyList<GmCard> hand, int index) => new GmAldricPlay
    {
        Card = hand[index],
        RemovedIndex = index,
        CheatKind = GmParlorCheatKind.None,
        Tell = string.Empty,
    };
}
