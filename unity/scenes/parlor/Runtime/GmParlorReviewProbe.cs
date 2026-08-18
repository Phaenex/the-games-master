using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GmParlorReviewDecision
{
    Accept,
    CorrectRead,
    FalseRead,
    LateRead,
    LockedRead,
}

[Serializable]
public readonly struct GmParlorReviewCase
{
    public readonly string Id;
    public readonly int Seed;
    public readonly int CorruptionTier;
    public readonly bool ReadUnlocked;
    public readonly GmSuit RequiredSuit;
    public readonly bool ExpectCheat;
    public readonly GmTellObservation ExpectedObservation;
    public readonly GmParlorCheatKind ExpectedCheatKind;
    public readonly GmParlorReviewDecision Decision;
    public readonly GmTrickOwner? RequiredMatchWinner;
    public readonly bool RequireRematch;
    public readonly bool RequireRestore;

    public GmParlorReviewCase(string id, int seed, int corruptionTier, bool readUnlocked,
        GmSuit requiredSuit, bool expectCheat, GmTellObservation expectedObservation,
        GmParlorCheatKind expectedCheatKind = GmParlorCheatKind.None,
        GmParlorReviewDecision decision = GmParlorReviewDecision.Accept,
        GmTrickOwner? requiredMatchWinner = null, bool requireRematch = false,
        bool requireRestore = false)
    {
        Id = id;
        Seed = seed;
        CorruptionTier = corruptionTier;
        ReadUnlocked = readUnlocked;
        RequiredSuit = requiredSuit;
        ExpectCheat = expectCheat;
        ExpectedObservation = expectedObservation;
        ExpectedCheatKind = expectedCheatKind;
        Decision = decision;
        RequiredMatchWinner = requiredMatchWinner;
        RequireRematch = requireRematch;
        RequireRestore = requireRestore;
    }
}

/// <summary>
/// Opt-in deterministic review driver. It observes hidden truth only after public controller intent
/// has produced a judgement, and never uses that truth to select the player's action.
/// </summary>
public sealed class GmParlorReviewProbe : MonoBehaviour
{
    public const int SeedCatalogVersion = 1;

    // Generated and independently revalidated by the authored seed-catalog test. Runtime review
    // never searches for a favorable deal or rerolls a case.
    public static readonly GmParlorReviewCase[] FrozenSeedMatrix =
    {
        new GmParlorReviewCase("honest-calm-flames", 1, 1, true,
            GmSuit.Flames, false, GmTellObservation.Calm),
        new GmParlorReviewCase("cheat-true-tell-eyes", 8, 4, true,
            GmSuit.Eyes, true, GmTellObservation.Suspicious,
            GmParlorCheatKind.RenegedWithHeldFlame,
            GmParlorReviewDecision.CorrectRead),
        new GmParlorReviewCase("honest-false-tell-teeth", 27, 4, true,
            GmSuit.Teeth, false, GmTellObservation.Suspicious,
            decision: GmParlorReviewDecision.FalseRead),
        new GmParlorReviewCase("cheat-calm-bones", 152, 4, true,
            GmSuit.Bones, true, GmTellObservation.Calm,
            GmParlorCheatKind.ImpossibleEighthRank),
        new GmParlorReviewCase("cheat-late-read-flames", 13, 4, true,
            GmSuit.Flames, true, GmTellObservation.Suspicious,
            GmParlorCheatKind.ImpossibleEighthRank,
            GmParlorReviewDecision.LateRead),
        new GmParlorReviewCase("honest-locked-read-eyes", 1, 1, false,
            GmSuit.Eyes, false, GmTellObservation.Calm,
            decision: GmParlorReviewDecision.LockedRead),
        new GmParlorReviewCase("bones-player-win-rematch", 21, 2, true,
            GmSuit.Bones, false, GmTellObservation.Calm,
            requiredMatchWinner: GmTrickOwner.Player, requireRematch: true),
        new GmParlorReviewCase("aldric-win-restore", 27, 3, true,
            GmSuit.Flames, true, GmTellObservation.Calm,
            GmParlorCheatKind.ImpossibleEighthRank,
            requiredMatchWinner: GmTrickOwner.Aldric,
            requireRestore: true),
    };

    [SerializeField] GmParlorRules rules;
    [SerializeField] GmParlorController controller;
    [SerializeField] GmParlorInput input;
    [SerializeField] GmParlorFocusView focus;
    [SerializeField] GmParlorPresentationCoordinator presentation;
    [SerializeField] bool armed;

    public bool IsConfigured => rules != null && controller != null && input != null &&
        focus != null && presentation != null;
    public bool IsArmed => armed;

    void Awake()
    {
        if (IsConfigured) return;
        GmParlorRules foundRules = GetComponent<GmParlorRules>() ??
            FindAnyObjectByType<GmParlorRules>();
        GmParlorController foundController = GetComponent<GmParlorController>() ??
            FindAnyObjectByType<GmParlorController>();
        GmParlorInput foundInput = GetComponent<GmParlorInput>() ??
            FindAnyObjectByType<GmParlorInput>();
        GmParlorFocusView foundFocus = GetComponent<GmParlorFocusView>() ??
            FindAnyObjectByType<GmParlorFocusView>();
        GmParlorPresentationCoordinator foundPresentation =
            FindAnyObjectByType<GmParlorPresentationCoordinator>();
        if (foundRules != null && foundController != null && foundInput != null &&
            foundFocus != null && foundPresentation != null)
            TryConfigure(foundRules, foundController, foundInput, foundFocus,
                foundPresentation, out _);
    }

    public bool TryConfigure(GmParlorRules parlorRules, GmParlorController tableController,
        GmParlorInput parlorInput, GmParlorFocusView focusView,
        GmParlorPresentationCoordinator coordinator, out string error)
    {
        if (parlorRules == null || tableController == null || parlorInput == null ||
            focusView == null || coordinator == null)
        {
            error = "Parlor review probe needs rules, controller, input, focus, and presentation";
            return false;
        }
        rules = parlorRules;
        controller = tableController;
        input = parlorInput;
        focus = focusView;
        presentation = coordinator;
        error = string.Empty;
        return true;
    }

    public bool ProveHeldNavigation(out string error)
    {
        if (!EnsureConfigured(out error)) return false;
        controller.SetFocusedCardIndex(0);
        controller.ApplyNavigation(Vector2.zero);
        controller.ApplyNavigation(Vector2.right);
        controller.ApplyNavigation(Vector2.right);
        controller.ApplyNavigation(Vector2.right);
        if (controller.FocusedCardIndex == 1) return true;
        error = $"held navigation repeated to index {controller.FocusedCardIndex}";
        return false;
    }

    public bool StageFirstJudgement(bool readUnlocked, out string error)
    {
        if (!EnsureConfigured(out error)) return false;
        GmParlorInitializeResult started = rules.StartGame(42, 4, 0, readUnlocked,
            forceRestart: true);
        if (started != GmParlorInitializeResult.StartedNew)
        {
            error = $"could not start deterministic review match: {started}";
            return false;
        }
        presentation.ResetTransientState();
        if (!controller.IsActivated && controller.Activate() != GmParlorActionError.None)
        {
            error = "controller activation failed";
            return false;
        }
        int legal = Enumerable.Range(0, rules.PlayerHand.Count)
            .FirstOrDefault(index => rules.GetPlayerCardError(index) == GmParlorActionError.None);
        controller.SetFocusedCardIndex(legal);
        GmParlorActionError result = controller.ConfirmFocusedAction();
        if (result != GmParlorActionError.None ||
            rules.Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
        {
            error = $"public confirm did not open judgement: {result}/{rules.Phase}";
            return false;
        }
        presentation.FastForwardToCanonicalState();
        error = string.Empty;
        return true;
    }

    public bool StageCaseJudgement(GmParlorReviewCase item, out string error)
    {
        if (!EnsureConfigured(out error)) return false;
        GmParlorInitializeResult started = rules.StartGame(item.Seed, item.CorruptionTier, 0,
            item.ReadUnlocked, forceRestart: true);
        if (started != GmParlorInitializeResult.StartedNew)
        {
            error = $"could not start {item.Id}: {started}";
            return false;
        }
        presentation.ResetTransientState();
        if (controller.Activate() != GmParlorActionError.None)
        {
            error = $"could not activate {item.Id}";
            return false;
        }
        int legal = Enumerable.Range(0, rules.PlayerHand.Count)
            .Where(index => rules.PlayerHand[index].Suit == item.RequiredSuit)
            .Where(index => rules.GetPlayerCardError(index) == GmParlorActionError.None)
            .OrderBy(index => rules.PlayerHand[index].Rank)
            .DefaultIfEmpty(-1)
            .First();
        if (legal < 0)
        {
            error = $"{item.Id} has no legal {item.RequiredSuit}";
            return false;
        }
        controller.SetFocusedCardIndex(legal);
        GmParlorActionError action = controller.ConfirmFocusedAction();
        if (action != GmParlorActionError.None ||
            rules.Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
        {
            error = $"{item.Id} did not open judgement: {action}/{rules.Phase}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool ResolveReviewDecision(GmParlorReviewCase item, out string error)
    {
        if (!EnsureConfigured(out error) ||
            rules.Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
        {
            error = string.IsNullOrEmpty(error) ? "review decision needs an open judgement" : error;
            return false;
        }
        presentation.FastForwardToCanonicalState();
        GmParlorActionError result;
        switch (item.Decision)
        {
            case GmParlorReviewDecision.CorrectRead:
            case GmParlorReviewDecision.FalseRead:
                result = controller.CallRead();
                break;
            case GmParlorReviewDecision.LateRead:
                result = controller.ConfirmFocusedAction();
                if (result != GmParlorActionError.None)
                {
                    error = $"accept before late Read failed: {result}";
                    return false;
                }
                presentation.FastForwardToCanonicalState();
                result = controller.CallRead();
                if (result != GmParlorActionError.WrongPhase)
                {
                    error = $"late Read produced {result}";
                    return false;
                }
                error = string.Empty;
                return true;
            case GmParlorReviewDecision.LockedRead:
                result = controller.CallRead();
                if (result != GmParlorActionError.ReadLocked)
                {
                    error = $"locked Read produced {result}";
                    return false;
                }
                result = controller.ConfirmFocusedAction();
                break;
            default:
                result = controller.ConfirmFocusedAction();
                break;
        }
        if (result != GmParlorActionError.None)
        {
            error = $"{item.Decision} produced {result}";
            return false;
        }
        presentation.FastForwardToCanonicalState();
        error = string.Empty;
        return true;
    }

    public bool DriveToMatchResult(GmTrickOwner desiredWinner, out string error)
    {
        if (!EnsureConfigured(out error)) return false;
        presentation.FastForwardToCanonicalState();
        int guard = 256;
        while (rules.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            GmParlorActionError action;
            switch (rules.Phase)
            {
                case GmParlorMatchPhase.PlayerLeads:
                case GmParlorMatchPhase.PlayerFollowsAldricLead:
                    int index = ChoosePlayerCard(desiredWinner);
                    if (index < 0)
                    {
                        error = "controller policy found no legal player card";
                        return false;
                    }
                    controller.SetFocusedCardIndex(index);
                    action = controller.ConfirmFocusedAction();
                    break;
                case GmParlorMatchPhase.AwaitingAldricJudgement:
                case GmParlorMatchPhase.TrickResult:
                case GmParlorMatchPhase.RoundResult:
                    action = controller.ConfirmFocusedAction();
                    break;
                default:
                    error = $"unexpected review phase {rules.Phase}";
                    return false;
            }
            if (action != GmParlorActionError.None)
            {
                error = $"controller policy failed in {rules.Phase}: {action}";
                return false;
            }
            presentation.FastForwardToCanonicalState();
        }
        if (rules.Phase != GmParlorMatchPhase.MatchResult ||
            rules.Match.MatchWinner != desiredWinner)
        {
            error = $"controller policy ended {rules.Phase}/{rules.Match.MatchWinner}, " +
                $"wanted {desiredWinner}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool DriveToPhase(GmParlorMatchPhase target, GmTrickOwner desiredWinner,
        out string error)
    {
        if (!EnsureConfigured(out error)) return false;
        presentation.FastForwardToCanonicalState();
        int guard = 256;
        while (rules.Phase != target && rules.Phase != GmParlorMatchPhase.MatchResult &&
            guard-- > 0)
        {
            GmParlorActionError action;
            switch (rules.Phase)
            {
                case GmParlorMatchPhase.PlayerLeads:
                case GmParlorMatchPhase.PlayerFollowsAldricLead:
                    int index = ChoosePlayerCard(desiredWinner);
                    if (index < 0)
                    {
                        error = "phase policy found no legal player card";
                        return false;
                    }
                    controller.SetFocusedCardIndex(index);
                    action = controller.ConfirmFocusedAction();
                    break;
                case GmParlorMatchPhase.AwaitingAldricJudgement:
                case GmParlorMatchPhase.TrickResult:
                case GmParlorMatchPhase.RoundResult:
                    action = controller.ConfirmFocusedAction();
                    break;
                default:
                    error = $"unexpected review phase {rules.Phase}";
                    return false;
            }
            if (action != GmParlorActionError.None)
            {
                error = $"phase policy failed in {rules.Phase}: {action}";
                return false;
            }
            presentation.FastForwardToCanonicalState();
        }
        if (rules.Phase != target)
        {
            error = $"phase policy ended in {rules.Phase}, wanted {target}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool StageMotionPhase(GmParlorMotionPhase desired, out string error)
    {
        if (!StageFirstJudgement(readUnlocked: true, out error)) return false;
        // StageFirstJudgement deliberately settles for intent tests. Produce a fresh transition for
        // motion interruption without importing a snapshot or touching a card transform.
        if (rules.StartGame(42, 4, 0, true, forceRestart: true) !=
            GmParlorInitializeResult.StartedNew)
        {
            error = "could not restart motion fixture";
            return false;
        }
        presentation.ResetTransientState();
        int legal = Enumerable.Range(0, rules.PlayerHand.Count)
            .First(index => rules.GetPlayerCardError(index) == GmParlorActionError.None);
        controller.SetFocusedCardIndex(legal);
        if (controller.ConfirmFocusedAction() != GmParlorActionError.None)
        {
            error = "public confirm did not start card motion";
            return false;
        }
        for (int step = 0; step < 10000 && presentation.IsBlocking; step++)
        {
            if (presentation.CurrentMotionPhase == desired)
            {
                error = string.Empty;
                return true;
            }
            presentation.Advance(0.001f);
        }
        error = $"motion never reached {desired}; current={presentation.CurrentMotionPhase}";
        return false;
    }

    bool EnsureConfigured(out string error)
    {
        if (IsConfigured)
        {
            error = string.Empty;
            return true;
        }
        error = "Parlor review probe is not configured";
        return false;
    }

    int ChoosePlayerCard(GmTrickOwner desiredWinner)
    {
        var legal = new List<int>();
        for (int index = 0; index < rules.PlayerHand.Count; index++)
            if (rules.GetPlayerCardError(index) == GmParlorActionError.None) legal.Add(index);
        if (legal.Count == 0) return -1;
        if (rules.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
        {
            bool WantsWin(int index) => !GmParlorCore.LeadWins(
                rules.CurrentLeadCard.Value, rules.PlayerHand[index]);
            IEnumerable<int> preferred = desiredWinner == GmTrickOwner.Player
                ? legal.Where(WantsWin) : legal.Where(index => !WantsWin(index));
            if (preferred.Any()) legal = preferred.ToList();
        }
        return desiredWinner == GmTrickOwner.Player
            ? legal.OrderByDescending(index => rules.PlayerHand[index].Suit == GmSuit.Flames)
                .ThenByDescending(index => rules.PlayerHand[index].Rank).First()
            : legal.OrderBy(index => rules.PlayerHand[index].Suit == GmSuit.Flames)
                .ThenBy(index => rules.PlayerHand[index].Rank).First();
    }
}
