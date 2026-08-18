using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct GmParlorFocusCard
{
    public readonly int Index;
    public readonly string Label;
    public readonly bool Legal;
    public readonly bool Focused;

    internal GmParlorFocusCard(int index, string label, bool legal, bool focused)
    {
        Index = index;
        Label = label;
        Legal = legal;
        Focused = focused;
    }
}

public sealed class GmParlorFocusModel
{
    public GmParlorFocusCard[] Cards { get; internal set; } = Array.Empty<GmParlorFocusCard>();
    public string[] Evidence { get; internal set; } = Array.Empty<string>();
    public string[] VisibleOpposingCards { get; internal set; } = Array.Empty<string>();
    public string Score { get; internal set; } = string.Empty;
    public string Action { get; internal set; } = string.Empty;
    public string Feedback { get; internal set; } = string.Empty;
    public string Lead { get; internal set; } = string.Empty;
    public string Follow { get; internal set; } = string.Empty;
    public bool ReadAvailable { get; internal set; }
}

/// <summary>
/// An equivalent readable projection of the physical table. It delegates every mutation to the
/// canonical controller and can only consume the public rules adapter plus observed facts.
/// </summary>
public sealed class GmParlorFocusView : MonoBehaviour
{
    GmParlorRules rules;
    GmParlorController controller;
    GmParlorEvidenceLog evidence;

    public bool IsConfigured => rules != null && controller != null && evidence != null;
    public bool IsOpen { get; private set; }
    public GmParlorFocusModel Model { get; private set; } = new GmParlorFocusModel();

    public event Action OnChanged;

    void Awake()
    {
        GmParlorRules foundRules = FindAnyObjectByType<GmParlorRules>();
        GmParlorController foundController = FindAnyObjectByType<GmParlorController>();
        GmParlorEvidenceLog foundEvidence = FindAnyObjectByType<GmParlorEvidenceLog>();
        if (foundRules != null && foundController != null && foundEvidence != null)
            TryConfigure(foundRules, foundController, foundEvidence, out _);
    }

    public bool TryConfigure(GmParlorRules parlorRules, GmParlorController tableController,
        GmParlorEvidenceLog evidenceLog, out string error)
    {
        if (parlorRules == null || tableController == null || evidenceLog == null)
        {
            error = "Parlor focus view needs rules, controller, and observed evidence";
            return false;
        }
        Detach();
        rules = parlorRules;
        controller = tableController;
        evidence = evidenceLog;
        controller.OnFocusChanged += HandleControllerChanged;
        controller.OnActionResolved += HandleActionResolved;
        evidence.OnChanged += HandleEvidenceChanged;
        Refresh();
        error = string.Empty;
        return true;
    }

    public void Open()
    {
        if (!IsConfigured || !controller.IsActivated || IsOpen) return;
        IsOpen = true;
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        OnChanged?.Invoke();
    }

    public void Move(int delta)
    {
        if (!IsConfigured || !controller.IsActivated ||
            rules.PlayerHand.Count == 0 || delta == 0) return;
        int count = rules.PlayerHand.Count;
        int next = (controller.FocusedCardIndex + Math.Sign(delta)) % count;
        if (next < 0) next += count;
        controller.SetFocusedCardIndex(next);
    }

    public void Select(int index)
    {
        if (!IsConfigured || !controller.IsActivated) return;
        controller.SetFocusedCardIndex(index);
    }

    public GmParlorActionError Confirm() => IsConfigured
        ? controller.ConfirmFocusedAction() : GmParlorActionError.WrongPhase;

    public GmParlorActionError Read() => IsConfigured
        ? controller.CallRead() : GmParlorActionError.WrongPhase;

    public GmParlorFocusModel Refresh()
    {
        if (!IsConfigured) return Model;
        var cards = new GmParlorFocusCard[rules.PlayerHand.Count];
        for (int index = 0; index < cards.Length; index++)
        {
            GmCard card = rules.PlayerHand[index];
            cards[index] = new GmParlorFocusCard(index, card.TableLabel,
                rules.GetPlayerCardError(index) == GmParlorActionError.None,
                index == controller.FocusedCardIndex);
        }
        Model = new GmParlorFocusModel
        {
            Cards = cards,
            // The durable log keeps command identity for replay protection. The journal is a
            // player-facing reading surface, so repeated identical observations appear once.
            Evidence = evidence.Facts.Select(fact => fact.Text)
                .Distinct(StringComparer.Ordinal).ToArray(),
            VisibleOpposingCards = rules.VisibleOpposingCards.Select(card => card.TableLabel).ToArray(),
            Score = $"Round {rules.RoundNumber}   Tricks {rules.PlayerTricksWon}-{rules.HostTricksWon}   " +
                $"Matches {rules.PlayerRoundsWon}-{rules.HostRoundsWon}",
            Action = ActionFor(rules.Phase, rules.ReadEnabled),
            Feedback = controller.LastPlayerFeedback,
            Lead = rules.CurrentLeadCard?.TableLabel ?? string.Empty,
            Follow = rules.CurrentFollowCard?.TableLabel ?? string.Empty,
            ReadAvailable = rules.ReadEnabled,
        };
        OnChanged?.Invoke();
        return Model;
    }

    void HandleControllerChanged(int _) => Refresh();
    void HandleActionResolved(GmParlorActionError _) => Refresh();
    void HandleEvidenceChanged() => Refresh();

    void OnDestroy() => Detach();

    void Detach()
    {
        if (controller != null)
        {
            controller.OnFocusChanged -= HandleControllerChanged;
            controller.OnActionResolved -= HandleActionResolved;
        }
        if (evidence != null) evidence.OnChanged -= HandleEvidenceChanged;
    }

    static string ActionFor(GmParlorMatchPhase phase, bool readAvailable)
    {
        return phase switch
        {
            GmParlorMatchPhase.PlayerLeads => "Choose a card to lead",
            GmParlorMatchPhase.PlayerFollowsAldricLead => "Follow the lead suit if you can",
            GmParlorMatchPhase.AwaitingAldricJudgement => readAvailable
                ? "Accept the play, or Read the evidence" : "Accept Aldric's play",
            GmParlorMatchPhase.TrickResult => "Continue to the next trick",
            GmParlorMatchPhase.RoundResult => "Continue to the next round",
            GmParlorMatchPhase.MatchResult => "Leave for Court, or begin a rematch",
            _ => "The table is waiting",
        };
    }
}
