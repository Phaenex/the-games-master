using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmParlorState
{
    NotStarted,
    Dealing,
    PlayerTurn,
    HostTurn,
    TrickResolving,
    AccusationWindow,
    HandComplete,
    GameOver
}

public sealed class GmParlorRules : MonoBehaviour
{
    public const int HandSize = 7;
    public const int TricksToWin = 4;

    public List<GmCard> PlayerHand { get; private set; } = new List<GmCard>();
    public List<GmCard> HostHand { get; private set; } = new List<GmCard>();

    public GmParlorState State { get; private set; } = GmParlorState.NotStarted;

    public int PlayerTricksWon { get; private set; }
    public int HostTricksWon { get; private set; }
    public int TrickNumber { get; private set; }

    public GmCard? CurrentLeadCard { get; private set; }
    public GmCard? CurrentFollowCard { get; private set; }
    public bool HostLeadsCurrentTrick { get; private set; }

    public float Suspicion { get; private set; }
    public bool ReadEnabled => Suspicion >= 0.35f;

    public event Action OnStateChanged;
    public event Action<bool> OnTrickCompleted; // true if player won trick

    public void StartGame(int seed = 42)
    {
        var deck = GmDeckUtility.CreateFreshDeck();
        GmDeckUtility.Shuffle(deck, seed);

        PlayerHand.Clear();
        HostHand.Clear();

        for (int i = 0; i < HandSize; i++)
        {
            PlayerHand.Add(deck[i * 2]);
            HostHand.Add(deck[i * 2 + 1]);
        }

        PlayerTricksWon = 0;
        HostTricksWon = 0;
        TrickNumber = 1;
        Suspicion = 0.1f;
        HostLeadsCurrentTrick = false; // Player leads first trick
        CurrentLeadCard = null;
        CurrentFollowCard = null;

        State = GmParlorState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

    public bool PlayPlayerCard(GmCard card)
    {
        if (State != GmParlorState.PlayerTurn) return false;
        if (!PlayerHand.Contains(card)) return false;

        if (CurrentLeadCard.HasValue)
        {
            // Player is following
            if (!GmDeckUtility.IsLegalPlay(PlayerHand, CurrentLeadCard.Value, card))
                return false;

            CurrentFollowCard = card;
            PlayerHand.Remove(card);
            ResolveTrick();
        }
        else
        {
            // Player is leading
            CurrentLeadCard = card;
            PlayerHand.Remove(card);
            State = GmParlorState.HostTurn;
            OnStateChanged?.Invoke();
        }

        return true;
    }

    public bool PlayHostCard(GmCard card)
    {
        if (State != GmParlorState.HostTurn) return false;
        if (!HostHand.Contains(card)) return false;

        if (CurrentLeadCard.HasValue)
        {
            // Host is following
            CurrentFollowCard = card;
            HostHand.Remove(card);
            ResolveTrick();
        }
        else
        {
            // Host is leading
            CurrentLeadCard = card;
            HostHand.Remove(card);
            State = GmParlorState.PlayerTurn;
            OnStateChanged?.Invoke();
        }

        return true;
    }

    void ResolveTrick()
    {
        if (!CurrentLeadCard.HasValue || !CurrentFollowCard.HasValue) return;

        State = GmParlorState.TrickResolving;
        bool leadWins = GmDeckUtility.LeadWinsTrick(CurrentLeadCard.Value, CurrentFollowCard.Value);

        bool playerWon;
        if (HostLeadsCurrentTrick)
        {
            // Host led, Player followed
            playerWon = !leadWins;
        }
        else
        {
            // Player led, Host followed
            playerWon = leadWins;
        }

        if (playerWon)
        {
            PlayerTricksWon++;
            HostLeadsCurrentTrick = false;
        }
        else
        {
            HostTricksWon++;
            HostLeadsCurrentTrick = true;
        }

        Suspicion = Mathf.Clamp01(Suspicion + 0.12f);
        OnTrickCompleted?.Invoke(playerWon);

        CurrentLeadCard = null;
        CurrentFollowCard = null;
        TrickNumber++;

        if (PlayerHand.Count == 0 || PlayerTricksWon >= TricksToWin || HostTricksWon >= TricksToWin)
        {
            State = GmParlorState.GameOver;
        }
        else
        {
            State = HostLeadsCurrentTrick ? GmParlorState.HostTurn : GmParlorState.PlayerTurn;
        }

        OnStateChanged?.Invoke();
    }
}
