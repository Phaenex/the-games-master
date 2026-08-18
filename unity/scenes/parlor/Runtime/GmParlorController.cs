using System;
using UnityEngine;

public sealed class GmParlorController : MonoBehaviour
{
    const float NavigationPressThreshold = 0.55f;
    const float NavigationReleaseThreshold = 0.25f;

    GmParlorRules rules;
    GmParlorPropBinder binder;
    GmParlorPresentationCoordinator presentation;
    GmParlorEvidenceLog evidence;
    GmParlorFocusView focusView;
    GmPlayer player;
    bool navigationLatched;
    bool activated;
    bool restoreReconstructionRequired;
    bool restoreLifecycleCompleted;
    bool restoreEvidenceLoaded;
    bool configurationBlocked;
    string lastPresentationError = string.Empty;

    public int FocusedCardIndex { get; private set; }
    public GmParlorActionError LastActionError { get; private set; }
    public string LastPlayerFeedback { get; private set; } = string.Empty;
    public string LastPresentationError => presentation != null &&
        !string.IsNullOrEmpty(presentation.LastError)
            ? presentation.LastError : lastPresentationError;
    public bool IsConfigured => rules != null && binder != null && presentation != null &&
        !configurationBlocked;
    public bool IsActivated => activated;
    public bool CanAcceptInput => IsConfigured && activated && rules.Match != null &&
        !presentation.IsBlocking;
    public bool PlayerControlShouldBeBlocked => rules == null || rules.Match == null ||
        rules.Match.Phase != GmParlorMatchPhase.MatchResult;
    public GmCard? FocusedCard => rules != null && FocusedCardIndex >= 0 &&
        FocusedCardIndex < rules.PlayerHand.Count ? rules.PlayerHand[FocusedCardIndex] : (GmCard?)null;

    public event Action<int> OnFocusChanged;
    public event Action<GmParlorActionError> OnActionResolved;

    void Awake()
    {
        if (IsConfigured) return;
        GmParlorRules foundRules = GetComponent<GmParlorRules>() ?? FindAnyObjectByType<GmParlorRules>();
        GmParlorPropBinder foundBinder = FindAnyObjectByType<GmParlorPropBinder>();
        GmParlorPresentationCoordinator foundPresentation =
            FindAnyObjectByType<GmParlorPresentationCoordinator>();
        if (foundBinder != null && !foundBinder.IsConfigured)
            foundBinder.TryConfigure(foundBinder.GetComponentsInChildren<GmParlorCardView>(true), out _);
        if (foundPresentation != null && !foundPresentation.IsConfigured && foundBinder != null)
        {
            GmParlorAldricPresenter foundPresenter = FindAnyObjectByType<GmParlorAldricPresenter>();
            if (foundPresenter != null && foundPresenter.TryAutoConfigure(out _))
                foundPresentation.TryConfigure(foundBinder, foundPresenter,
                    GmParlorAccessibilityProfile.Default, out _);
            else
                foundPresentation.TryConfigure(foundBinder, out _);
        }
        if (foundRules != null && foundBinder != null && foundPresentation != null)
            TryConfigure(foundRules, foundBinder, foundPresentation, out _);
    }

    void Start()
    {
        // Restore activation is an explicit lifecycle boundary. Unity Start may run before HUD,
        // focus, and external outcome observers have subscribed, so it must never deliver here.
    }

    public bool TryConfigure(GmParlorRules parlorRules, GmParlorPropBinder propBinder,
        GmParlorPresentationCoordinator coordinator, out string error)
    {
        if (parlorRules == null)
        {
            error = "Parlor controller needs rules";
            return false;
        }
        if (propBinder == null || !propBinder.IsConfigured)
        {
            error = "Parlor controller needs a configured prop binder";
            return false;
        }
        if (coordinator == null || !coordinator.IsConfigured)
        {
            error = "Parlor controller needs a configured presentation coordinator";
            return false;
        }
        rules = parlorRules;
        binder = propBinder;
        presentation = coordinator;
        configurationBlocked = false;
        evidence = GetComponentInChildren<GmParlorEvidenceLog>(true) ??
            FindAnyObjectByType<GmParlorEvidenceLog>();
        focusView = GetComponentInChildren<GmParlorFocusView>(true) ??
            FindAnyObjectByType<GmParlorFocusView>();
        GmParlorMatchSnapshot storedSnapshot = rules.GetPresentationRestoreSnapshot();
        restoreReconstructionRequired = rules.LastInitializeResult ==
            GmParlorInitializeResult.Restored ||
            (rules.Match == null && storedSnapshot != null);
        restoreLifecycleCompleted = false;
        restoreEvidenceLoaded = false;
        activated = rules.Match != null &&
            !restoreReconstructionRequired;
        FocusedCardIndex = 0;
        navigationLatched = false;
        if (restoreReconstructionRequired)
        {
            presentation.ResetTransientState();
            GmParlorMatchSnapshot snapshot = rules.Match != null
                ? rules.Match.ExportSnapshot() : storedSnapshot;
            if (!binder.TryApply(snapshot, out error))
            {
                configurationBlocked = true;
                activated = false;
                return false;
            }
            if (evidence != null && !evidence.TryRestoreFromRunStore(out error))
            {
                configurationBlocked = true;
                activated = false;
                return false;
            }
            restoreEvidenceLoaded = evidence != null;
            focusView?.Close();
        }
        evidence?.EnableDurablePersistence();
        LastActionError = GmParlorActionError.None;
        lastPresentationError = string.Empty;
        error = string.Empty;
        return true;
    }

    public GmParlorActionError Activate()
    {
        if (!IsConfigured || rules.Match == null)
            return Resolve(GmParlorActionError.WrongPhase);
        if (!restoreLifecycleCompleted &&
            rules.LastInitializeResult == GmParlorInitializeResult.Restored)
            restoreReconstructionRequired = true;
        if (restoreReconstructionRequired)
        {
            evidence ??= GetComponentInChildren<GmParlorEvidenceLog>(true) ??
                FindAnyObjectByType<GmParlorEvidenceLog>();
            if (evidence != null && !restoreEvidenceLoaded &&
                !evidence.TryRestoreFromRunStore(out string evidenceError))
            {
                lastPresentationError = evidenceError;
                configurationBlocked = true;
                activated = false;
                return Resolve(GmParlorActionError.WrongPhase);
            }
            restoreEvidenceLoaded = evidence != null;
            evidence?.EnableDurablePersistence();
        }
        GmParlorActionError error = rules.ActivateRestoredOutcomes();
        if (error != GmParlorActionError.None)
        {
            activated = false;
            return Resolve(error);
        }
        if (!binder.TryApply(rules.Match.ExportSnapshot(), out string bindError))
        {
            lastPresentationError = bindError;
            activated = false;
            return Resolve(GmParlorActionError.WrongPhase);
        }
        if (restoreReconstructionRequired &&
            !presentation.TryRestoreCanonicalState(rules.Match.ExportSnapshot(),
                out string restoreError))
        {
            lastPresentationError = restoreError;
            activated = false;
            return Resolve(GmParlorActionError.WrongPhase);
        }
        FocusedCardIndex = 0;
        navigationLatched = false;
        focusView?.Close();
        restoreReconstructionRequired = false;
        restoreLifecycleCompleted = true;
        activated = true;
        player = FindAnyObjectByType<GmPlayer>();
        SynchronizePlayerControl();
        return Resolve(GmParlorActionError.None);
    }

    public void ApplyNavigation(Vector2 navigation)
    {
        if (navigation.sqrMagnitude <= NavigationReleaseThreshold * NavigationReleaseThreshold)
        {
            navigationLatched = false;
            return;
        }
        if (!CanAcceptInput || navigationLatched ||
            navigation.sqrMagnitude < NavigationPressThreshold * NavigationPressThreshold ||
            rules.PlayerHand.Count == 0) return;

        int direction = Mathf.Abs(navigation.x) >= Mathf.Abs(navigation.y)
            ? (navigation.x >= 0f ? 1 : -1)
            : (navigation.y <= 0f ? 1 : -1);
        SetFocusedCardIndex(Wrap(FocusedCardIndex + direction, rules.PlayerHand.Count));
        navigationLatched = true;
    }

    public void SetFocusedCardIndex(int index)
    {
        int count = rules != null ? rules.PlayerHand.Count : 0;
        int next = count == 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
        if (FocusedCardIndex == next) return;
        FocusedCardIndex = next;
        OnFocusChanged?.Invoke(next);
    }

    public GmParlorActionError ConfirmFocusedAction()
    {
        if (!CanAcceptInput) return Resolve(GmParlorActionError.WrongPhase);
        switch (rules.Match.Phase)
        {
            case GmParlorMatchPhase.PlayerLeads:
            case GmParlorMatchPhase.PlayerFollowsAldricLead:
                GmParlorActionError validation = rules.Match.GetPlayerCardError(FocusedCardIndex);
                if (validation != GmParlorActionError.None) return Resolve(validation);
                return ApplyTransition(() => rules.PlayPlayerCard(FocusedCardIndex));
            case GmParlorMatchPhase.AwaitingAldricJudgement:
                return ApplyTransition(rules.AcceptAldricPlay);
            case GmParlorMatchPhase.TrickResult:
            case GmParlorMatchPhase.RoundResult:
                return ApplyTransition(rules.ContinueResult);
            case GmParlorMatchPhase.MatchResult:
                return ApplyTransition(rules.StartRematch);
            default:
                return Resolve(GmParlorActionError.WrongPhase);
        }
    }

    public GmParlorActionError CallRead()
    {
        if (!CanAcceptInput) return Resolve(GmParlorActionError.WrongPhase, readIntent: true);
        return ApplyTransition(rules.ReadAldricPlay, readIntent: true);
    }

    public bool CancelOrFastForward()
    {
        if (!IsConfigured || !presentation.IsBlocking) return false;
        presentation.FastForwardToCanonicalState();
        lastPresentationError = presentation.LastError;
        return true;
    }

    GmParlorActionError ApplyTransition(Func<GmParlorActionError> transition,
        bool readIntent = false)
    {
        GmParlorMatchSnapshot before = rules.Match.ExportSnapshot();
        string beforePublicState = rules.Match.PublicStateBytes;
        GmParlorActionError error = transition();
        GmParlorMatchSnapshot after = rules.Match.ExportSnapshot();
        if (error != GmParlorActionError.None)
        {
            if (rules.Match.PublicStateBytes != beforePublicState)
                PresentTransition(before, after);
            return Resolve(error, readIntent);
        }

        PresentTransition(before, after);
        return Resolve(GmParlorActionError.None, readIntent);
    }

    void PresentTransition(GmParlorMatchSnapshot before, GmParlorMatchSnapshot after)
    {
        if (!GmParlorPresentationJournal.TryBuild(before, after,
            out GmParlorPresentationCommand[] commands, out string journalError))
        {
            binder.TryApply(after, out _);
            lastPresentationError = journalError;
        }
        else if (!presentation.TryEnqueue(commands, after, out string presentationError))
        {
            lastPresentationError = presentationError;
        }
        else
        {
            lastPresentationError = string.Empty;
        }

        FocusedCardIndex = Mathf.Clamp(FocusedCardIndex, 0,
            Mathf.Max(0, rules.PlayerHand.Count - 1));
        SynchronizePlayerControl();
    }

    public void SynchronizePlayerControl()
    {
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (player != null) player.SetControlBlocked(PlayerControlShouldBeBlocked);
    }

    GmParlorActionError Resolve(GmParlorActionError error, bool readIntent = false)
    {
        LastActionError = error;
        LastPlayerFeedback = readIntent ? error switch
        {
            GmParlorActionError.None => rules.LastResolvedOutcomeKind switch
            {
                GmParlorOutcomeKind.CheatCaught =>
                    "You caught Aldric cheating. The trick is yours.",
                GmParlorOutcomeKind.FalseReadPenalty =>
                    "Aldric was honest. Your false Read costs you.",
                _ => "Your Read resolved.",
            },
            GmParlorActionError.ReadLocked => "The Read is locked until you have caught a tell.",
            GmParlorActionError.WrongPhase => "The Read window has closed.",
            _ => $"The Read failed: {error}.",
        } : string.Empty;
        OnActionResolved?.Invoke(error);
        return error;
    }

    static int Wrap(int value, int count)
    {
        if (count <= 0) return 0;
        int wrapped = value % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
