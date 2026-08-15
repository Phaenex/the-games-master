using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GmHostAI : MonoBehaviour
{
    public bool CheatArmed { get; set; } = false;
    public bool LastPlayWasCheated { get; private set; } = false;
    public string LastCheatType { get; private set; } = "";

    public event Action<string> OnCheatExecuted;

    /// <summary>
    /// Selects a legal card to lead from Aldric's hand.
    /// </summary>
    public GmCard ChooseLeadCard(List<GmCard> hand)
    {
        if (hand == null || hand.Count == 0)
            throw new ArgumentException("Hand cannot be empty");

        LastPlayWasCheated = false;

        // Strategic lead: prefer high non-trump cards to pull trumps, or lead high flames if short.
        // Any non-trump displaces a trump, so the rank comparison only ever runs within one of the
        // two groups: it picks the highest side suit, and falls to the highest Flame on a trump hand.
        GmCard bestLead = hand[0];
        foreach (var card in hand)
        {
            bool cardIsTrump = card.Suit == GmSuit.Flames;
            bool bestIsTrump = bestLead.Suit == GmSuit.Flames;
            if (cardIsTrump && !bestIsTrump) continue;
            if ((!cardIsTrump && bestIsTrump) || card.Rank > bestLead.Rank)
            {
                bestLead = card;
            }
        }

        return bestLead;
    }

    /// <summary>
    /// Selects a follow card for Aldric following strict legal follow rules,
    /// or executes a reactive cheat ONLY IF Aldric is about to lose a critical trick.
    /// </summary>
    public GmCard ChooseFollowCard(List<GmCard> hand, GmCard playerLeadCard, int playerTricksWon, int hostTricksWon)
    {
        if (hand == null || hand.Count == 0)
            throw new ArgumentException("Hand cannot be empty");

        LastPlayWasCheated = false;

        // Find all strictly legal moves
        var legalCards = new List<GmCard>();
        foreach (var card in hand)
        {
            if (GmDeckUtility.IsLegalPlay(hand, playerLeadCard, card))
                legalCards.Add(card);
        }

        // Find best legal card that wins the trick
        GmCard? winningLegalCard = null;
        foreach (var card in legalCards)
        {
            bool leadWins = GmDeckUtility.LeadWinsTrick(playerLeadCard, card);
            if (!leadWins) // Follow (Host) wins
            {
                if (!winningLegalCard.HasValue || card.Rank < winningLegalCard.Value.Rank)
                {
                    winningLegalCard = card; // Smallest card that still wins
                }
            }
        }

        if (winningLegalCard.HasValue)
        {
            // Aldric wins legally without needing to cheat
            return winningLegalCard.Value;
        }

        // Aldric cannot win legally. Check if reactive cheating triggers:
        // Canon Rule: Aldric cheats ONLY when about to lose, never from safety.
        bool playerAtMatchPoint = playerTricksWon >= 3;
        bool cheatTriggered = CheatArmed || playerAtMatchPoint;

        // The palm has to come out of the hand he is actually holding: GmParlorRules.PlayHostCard
        // rejects any card HostHand does not contain, so a fabricated Flame announces a cheat that
        // the table then refuses, leaving the turn stuck on a play the AI already reported as made.
        if (cheatTriggered && GmRunStore.CorruptionTier >= 1 &&
            TryPalmWinningFlames(hand, playerLeadCard, out GmCard palmed))
        {
            // Execute reactive cheat: Palm a winning Trump he is not allowed to play
            LastPlayWasCheated = true;
            LastCheatType = "parlor-palm-flames";
            OnCheatExecuted?.Invoke(LastCheatType);
            Debug.Log($"[GmHostAI] Aldric Voss executed reactive cheat: Palmed {palmed.ShortName}!");

            return palmed;
        }

        // If not cheating, play the lowest legal discard
        GmCard lowestDiscard = legalCards[0];
        foreach (var card in legalCards)
        {
            if (card.Rank < lowestDiscard.Rank) lowestDiscard = card;
        }

        return lowestDiscard;
    }

    /// <summary>
    /// Highest Flame in hand that takes the trick from the player's lead. Only a trump can qualify
    /// at this point: a higher card of the led suit would have been a legal follow, and every legal
    /// winner was already taken above. Returns false when the hand holds no such card, in which case
    /// there is no palm to make and Aldric discards without claiming a cheat he cannot play.
    /// </summary>
    static bool TryPalmWinningFlames(List<GmCard> hand, GmCard playerLeadCard, out GmCard palmed)
    {
        palmed = default;
        bool found = false;
        foreach (var card in hand)
        {
            if (card.Suit != GmSuit.Flames) continue;
            if (GmDeckUtility.LeadWinsTrick(playerLeadCard, card)) continue;
            if (!found || card.Rank > palmed.Rank)
            {
                palmed = card;
                found = true;
            }
        }
        return found;
    }
}
