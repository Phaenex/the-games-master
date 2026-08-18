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

        ResetLastPlay();
        return hand[GmParlorCore.ChooseAldricLead(hand, GmAldricLeadPolicy.HighestSideSuit)];
    }

    /// <summary>
    /// Selects a follow card for Aldric following strict legal follow rules,
    /// or executes a reactive cheat ONLY IF Aldric is about to lose a critical trick.
    /// </summary>
    public GmCard ChooseFollowCard(List<GmCard> hand, GmCard playerLeadCard, int playerTricksWon, int hostTricksWon)
    {
        if (hand == null || hand.Count == 0)
            throw new ArgumentException("Hand cannot be empty");

        ResetLastPlay();
        _ = hostTricksWon;
        bool playerAtMatchPoint = playerTricksWon >= 3;
        bool allowCheat = (CheatArmed || playerAtMatchPoint) && GmRunStore.CorruptionTier >= 1;
        GmAldricPlay play = GmParlorCore.ChooseAldricFollow(
            hand, playerLeadCard, allowCheat, GmAldricFollowPolicy.ShippingHighestWinningFlame);

        // This adapter returns a card and the shipping table requires that card to be physically
        // present in HostHand. The shared core's impossible eight pays a real index, but it needs the
        // later match controller to remove that index separately. Until then, stay honest instead of
        // claiming a cheat this component's public API cannot execute.
        if (play.CheatKind == GmParlorCheatKind.ImpossibleEighthRank)
            play = GmParlorCore.ChooseAldricFollow(
                hand, playerLeadCard, false, GmAldricFollowPolicy.ShippingHighestWinningFlame);

        if (play.Cheated)
        {
            LastPlayWasCheated = true;
            LastCheatType = "parlor-palm-flames";
            OnCheatExecuted?.Invoke(LastCheatType);
            Debug.Log($"[GmHostAI] Aldric Voss executed reactive cheat: Palmed {play.Card.ShortName}!");
        }
        return play.Card;
    }

    void ResetLastPlay()
    {
        LastPlayWasCheated = false;
        LastCheatType = string.Empty;
    }
}
