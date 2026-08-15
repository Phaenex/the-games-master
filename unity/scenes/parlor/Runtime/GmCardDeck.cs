using System;
using System.Collections.Generic;

public enum GmSuit
{
    Flames = 0, // Trump suit
    Eyes = 1,
    Bones = 2,
    Teeth = 3
}

[Serializable]
public struct GmCard : IEquatable<GmCard>
{
    public GmSuit Suit;
    public int Rank; // 1 through 7

    public GmCard(GmSuit suit, int rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public string ShortName => $"{Suit.ToString()[0]}{Rank}";
    public string DisplayName => $"{Rank} of {Suit}";

    public bool Equals(GmCard other) => Suit == other.Suit && Rank == other.Rank;
    public override bool Equals(object obj) => obj is GmCard other && Equals(other);
    public override int GetHashCode() => ((int)Suit * 397) ^ Rank;
    public override string ToString() => DisplayName;

    public static bool operator ==(GmCard left, GmCard right) => left.Equals(right);
    public static bool operator !=(GmCard left, GmCard right) => !left.Equals(right);
}

public static class GmDeckUtility
{
    public const int CardsPerSuit = 7;
    public const int TotalCards = 28;

    public static List<GmCard> CreateFreshDeck()
    {
        var deck = new List<GmCard>(TotalCards);
        foreach (GmSuit suit in (GmSuit[])Enum.GetValues(typeof(GmSuit)))
        {
            for (int rank = 1; rank <= CardsPerSuit; rank++)
            {
                deck.Add(new GmCard(suit, rank));
            }
        }
        return deck;
    }

    public static void Shuffle(List<GmCard> cards, int seed = 42)
    {
        var rng = new System.Random(seed);
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            GmCard temp = cards[i];
            cards[i] = cards[swapIndex];
            cards[swapIndex] = temp;
        }
    }

    /// <summary>
    /// Determines the winner of a trick according to canon rules:
    /// 1. Flames is the permanent trump suit. If one or more Flames are played, the highest Flames card wins.
    /// 2. If no Flames are played, the highest card in the led suit wins.
    /// 3. Off-suit non-trump cards cannot win.
    /// </summary>
    /// <returns>True if lead card wins, False if follow card wins.</returns>
    public static bool LeadWinsTrick(GmCard leadCard, GmCard followCard)
    {
        // Case 1: Same suit - higher rank wins
        if (leadCard.Suit == followCard.Suit)
        {
            return leadCard.Rank >= followCard.Rank;
        }

        // Case 2: Follow played Trump (Flames) while Lead was non-trump
        if (followCard.Suit == GmSuit.Flames && leadCard.Suit != GmSuit.Flames)
        {
            return false; // Follow wins with trump
        }

        // Case 3: Lead played Trump (Flames) while Follow was non-trump
        if (leadCard.Suit == GmSuit.Flames && followCard.Suit != GmSuit.Flames)
        {
            return true; // Lead wins with trump
        }

        // Case 4: Follow played off-suit non-trump
        return true; // Lead wins (off-suit cannot beat lead)
    }

    public static bool IsLegalPlay(List<GmCard> hand, GmCard leadCard, GmCard cardToPlay)
    {
        if (!hand.Contains(cardToPlay)) return false;

        // If player has cards of the lead suit, they MUST follow suit
        bool hasLeadSuit = false;
        foreach (var c in hand)
        {
            if (c.Suit == leadCard.Suit)
            {
                hasLeadSuit = true;
                break;
            }
        }

        if (hasLeadSuit)
        {
            return cardToPlay.Suit == leadCard.Suit;
        }

        // If player does not have lead suit, any card is legal (trump or discard)
        return true;
    }
}
