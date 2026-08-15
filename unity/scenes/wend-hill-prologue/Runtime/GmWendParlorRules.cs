using System;
using System.Collections.Generic;
using System.Linq;

public enum GmCardSuit
{
    Flames,
    Eyes,
    Teeth,
    Bones,
}

public enum GmTrickOwner
{
    Player,
    Aldric,
}

[Serializable]
public struct GmParlorCard : IEquatable<GmParlorCard>
{
    public GmCardSuit Suit;
    public int Rank;

    public GmParlorCard(GmCardSuit suit, int rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public string ShortName => $"{Rank} {Suit}";
    public bool Equals(GmParlorCard other) => Suit == other.Suit && Rank == other.Rank;
    public override bool Equals(object obj) => obj is GmParlorCard other && Equals(other);
    public override int GetHashCode() => ((int)Suit * 397) ^ Rank;
    public override string ToString() => ShortName;
}

public struct GmAldricPlay
{
    public GmParlorCard Card;
    public int RemovedIndex;
    public bool Cheated;
    public string Tell;
}

/// <summary>
/// Pure, deterministic rules for the first game. Aldric's constraint lives here rather than in
/// presentation code: a legal winning card must always be played cleanly; cheating is available only
/// when every legal response loses. Tests can therefore prove the character rule mechanically.
/// </summary>
public static class GmWendParlorRules
{
    public const int Suits = 4;
    public const int RanksPerSuit = 7;
    public const int HandSize = 7;

    public static List<GmParlorCard> ShuffledDeck(int seed)
    {
        var deck = new List<GmParlorCard>(Suits * RanksPerSuit);
        foreach (GmCardSuit suit in Enum.GetValues(typeof(GmCardSuit)))
            for (int rank = 1; rank <= RanksPerSuit; rank++) deck.Add(new GmParlorCard(suit, rank));
        var random = new Random(seed);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            GmParlorCard held = deck[i];
            deck[i] = deck[swap];
            deck[swap] = held;
        }
        return deck;
    }

    public static bool MustFollow(IReadOnlyList<GmParlorCard> hand, GmParlorCard lead) =>
        hand.Any(card => card.Suit == lead.Suit);

    public static bool IsLegal(IReadOnlyList<GmParlorCard> hand, int index, GmParlorCard? lead)
    {
        if (index < 0 || index >= hand.Count) return false;
        if (!lead.HasValue || !MustFollow(hand, lead.Value)) return true;
        return hand[index].Suit == lead.Value.Suit;
    }

    public static GmTrickOwner Winner(GmParlorCard lead, GmParlorCard follow)
    {
        if (follow.Suit == GmCardSuit.Flames && lead.Suit != GmCardSuit.Flames) return GmTrickOwner.Aldric;
        if (lead.Suit == GmCardSuit.Flames && follow.Suit != GmCardSuit.Flames) return GmTrickOwner.Player;
        if (lead.Suit != follow.Suit) return GmTrickOwner.Player;
        return follow.Rank > lead.Rank ? GmTrickOwner.Aldric : GmTrickOwner.Player;
    }

    public static int ChooseAldricLead(IReadOnlyList<GmParlorCard> hand)
    {
        if (hand == null || hand.Count == 0) return -1;
        int best = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].Suit == GmCardSuit.Flames) continue;
            if (best < 0 || hand[i].Rank < hand[best].Rank) best = i;
        }
        return best >= 0 ? best : 0;
    }

    public static GmAldricPlay ChooseAldricFollow(
        IReadOnlyList<GmParlorCard> hand, GmParlorCard playerLead, bool allowCheat)
    {
        if (hand == null || hand.Count == 0) throw new ArgumentException("Aldric needs a card to follow", nameof(hand));
        var legal = new List<int>();
        for (int i = 0; i < hand.Count; i++) if (IsLegal(hand, i, playerLead)) legal.Add(i);

        int cleanWinner = -1;
        foreach (int index in legal)
        {
            if (Winner(playerLead, hand[index]) != GmTrickOwner.Aldric) continue;
            if (cleanWinner < 0 || hand[index].Rank < hand[cleanWinner].Rank) cleanWinner = index;
        }
        if (cleanWinner >= 0)
            return new GmAldricPlay { Card = hand[cleanWinner], RemovedIndex = cleanWinner };

        int cleanLoss = legal.OrderBy(index => hand[index].Rank).First();
        if (!allowCheat)
            return new GmAldricPlay { Card = hand[cleanLoss], RemovedIndex = cleanLoss };

        // Prefer a physical renege the player can infer from the hand. If no held trump can create
        // one, Aldric produces the house's impossible eighth rank and still pays a real card from his
        // hand. Both paths preserve hand size and both are unambiguously catchable by Read.
        if (MustFollow(hand, playerLead) && playerLead.Suit != GmCardSuit.Flames)
        {
            for (int i = 0; i < hand.Count; i++)
                if (hand[i].Suit == GmCardSuit.Flames)
                    return new GmAldricPlay
                    {
                        Card = hand[i], RemovedIndex = i, Cheated = true,
                        Tell = $"He held {playerLead.Suit}, but answered in Flames.",
                    };
        }

        return new GmAldricPlay
        {
            Card = new GmParlorCard(playerLead.Suit, RanksPerSuit + 1),
            RemovedIndex = cleanLoss,
            Cheated = true,
            Tell = $"There are seven ranks. Aldric laid down an eight of {playerLead.Suit}.",
        };
    }
}
