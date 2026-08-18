using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GmParlorPresentationCoordinator : MonoBehaviour
{
    readonly Queue<GmParlorPresentationCommand> queue =
        new Queue<GmParlorPresentationCommand>(8);
    readonly GmParlorCardMotion motion = new GmParlorCardMotion();

    GmParlorPropBinder binder;
    GmParlorAldricPresenter aldricPresenter;
    GmPlayer player;
    GmParlorAccessibilityProfile accessibility = GmParlorAccessibilityProfile.Default;
    GmParlorMatchSnapshot targetSnapshot;
    bool hasActiveMotion;
    float cueSecondsRemaining;
    float captionSecondsRemaining;
    bool captionBlocksInput;
    bool captionIsReconstructed;
    bool hasCurrentObservation;
    GmParlorEvidenceCue currentObservation;
    ulong activeCaptionCommandId;
    string activeCaptionText = string.Empty;

    public bool ReducedMotion { get; private set; }
    public GmParlorAccessibilityProfile Accessibility => accessibility;
    public bool IsConfigured => binder != null && binder.IsConfigured;
    public bool HasAldricPresenter => aldricPresenter != null && aldricPresenter.IsConfigured;
    public bool IsBlocking => hasActiveMotion || queue.Count > 0 ||
        (captionBlocksInput && cueSecondsRemaining > 0f);
    public string LastError { get; private set; } = string.Empty;
    public GmParlorMotionPhase CurrentMotionPhase => hasActiveMotion
        ? motion.Phase : GmParlorMotionPhase.Complete;
    public GmParlorPresentationAction? CurrentPresentationAction =>
        hasActiveMotion && queue.Count > 0 ? queue.Peek().Action : null;
    public float CurrentMotionApproachHeight => hasActiveMotion ? motion.ApproachHeight : 0f;
    public bool HasActiveCaption => activeCaptionCommandId != 0 && activeCaptionText.Length > 0;
    public ulong ActiveCaptionCommandId => activeCaptionCommandId;
    public string ActiveCaptionText => activeCaptionText;
    public event Action OnActiveCaptionChanged;
    public event Action OnCaptionStateReconstructed;

    void Awake()
    {
        player = GetComponentInParent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        if (IsConfigured && HasAldricPresenter)
        {
            AttachAccessibility();
            return;
        }
        GmParlorPropBinder localBinder = GetComponent<GmParlorPropBinder>();
        if (localBinder == null) return;
        if (!localBinder.IsConfigured)
            localBinder.TryConfigure(localBinder.GetComponentsInChildren<GmParlorCardView>(true), out _);
        GmParlorAldricPresenter presenter = FindAnyObjectByType<GmParlorAldricPresenter>();
        if (presenter != null && presenter.TryAutoConfigure(out _))
            TryConfigure(localBinder, presenter, GmParlorAccessibilityProfile.FromGlobal(), out _);
        else
            TryConfigure(localBinder, out _);
    }

    public bool TryConfigure(GmParlorPropBinder propBinder, out string error)
    {
        if (propBinder == null || !propBinder.IsConfigured)
        {
            error = "Parlor presentation needs a configured prop binder";
            return false;
        }
        binder = propBinder;
        AttachAccessibility();
        LastError = string.Empty;
        error = string.Empty;
        return true;
    }

    public bool TryConfigure(GmParlorPropBinder propBinder,
        GmParlorAldricPresenter presenter, GmParlorAccessibilityProfile profile,
        out string error)
    {
        if (!TryConfigure(propBinder, out error)) return false;
        if (presenter == null || !presenter.IsConfigured)
        {
            error = "Parlor presentation needs a configured Aldric presenter";
            return false;
        }
        aldricPresenter = presenter;
        // Keep the profile parameter for source compatibility with existing builders and tests,
        // but the player-wide authority owns the live value from this point forward.
        _ = profile;
        ApplyAccessibility();
        error = string.Empty;
        return true;
    }

    public bool TryEnqueue(IReadOnlyList<GmParlorPresentationCommand> commands,
        GmParlorMatchSnapshot canonicalTarget, out string error)
    {
        if (binder == null || !binder.IsConfigured)
            return Fail("Parlor presentation coordinator is not configured", canonicalTarget, out error);
        if (commands == null || canonicalTarget == null)
            return Fail("Parlor presentation commands and target cannot be null", canonicalTarget, out error);
        if (IsBlocking)
            return Fail("Parlor presentation is already processing a transition", canonicalTarget, out error);

        GmParlorCardBinding[] bindings;
        try
        {
            bindings = GmParlorTableLayout.Build(canonicalTarget);
        }
        catch (Exception exception)
        {
            return Fail(exception.Message, canonicalTarget, out error);
        }
        for (int index = 0; index < commands.Count; index++)
        {
            GmParlorPresentationCommand command = commands[index];
            if (!IsCardMotion(command.Action)) continue;
            if (!TryFindTarget(command, bindings, out GmParlorCardBinding binding) ||
                !binder.TryGetView(binding.PhysicalCard, out GmParlorCardView view) || view == null)
                return Fail($"Missing physical card view for {command.Action} {command.Card}",
                    canonicalTarget, out error);
        }

        targetSnapshot = canonicalTarget.DeepCopy();
        if (targetSnapshot.phase != GmParlorMatchPhase.AwaitingAldricJudgement)
        {
            hasCurrentObservation = false;
            currentObservation = default;
        }
        captionSecondsRemaining = 0f;
        ClearActiveCaption();
        for (int index = 0; index < commands.Count; index++) queue.Enqueue(commands[index]);
        LastError = string.Empty;
        error = string.Empty;
        BeginNext();
        return true;
    }

    /// <summary>
    /// Reconstructs only the currently visible canonical table. It never queues card motion or
    /// emits a cue event, so acknowledged effects cannot replay after a process restart.
    /// </summary>
    public bool TryRestoreCanonicalState(GmParlorMatchSnapshot canonical,
        out string error)
    {
        if (binder == null || !binder.IsConfigured)
            return Fail("Parlor presentation coordinator is not configured", canonical, out error);
        if (canonical == null)
            return Fail("Parlor restore snapshot cannot be null", null, out error);

        ResetTransientState();
        targetSnapshot = canonical.DeepCopy();
        if (!binder.TryApply(targetSnapshot, out error))
        {
            LastError = error;
            return false;
        }

        if (canonical.phase == GmParlorMatchPhase.AwaitingAldricJudgement &&
            aldricPresenter != null)
        {
            GmParlorPresentationCommand command;
            try { command = GmParlorPresentationJournal.RestoreOpenJudgement(canonical); }
            catch (Exception exception)
            {
                LastError = exception.Message;
                error = LastError;
                return false;
            }
            if (!aldricPresenter.TryRestoreCurrentObservation(command, accessibility,
                out GmParlorEvidenceCue cue, out error))
            {
                LastError = error;
                return false;
            }
            hasCurrentObservation = true;
            currentObservation = cue;
            if (accessibility.Captions)
            {
                captionSecondsRemaining = cue.MinimumReadableSeconds;
                captionBlocksInput = false;
                SetActiveCaption(cue, reconstructed: true);
            }
        }

        LastError = string.Empty;
        error = string.Empty;
        return true;
    }

    public void ResetTransientState()
    {
        if (hasActiveMotion) motion.FastForward();
        hasActiveMotion = false;
        cueSecondsRemaining = 0f;
        captionSecondsRemaining = 0f;
        captionBlocksInput = false;
        queue.Clear();
        targetSnapshot = null;
        hasCurrentObservation = false;
        currentObservation = default;
        ClearActiveCaption();
        LastError = string.Empty;
    }

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if ((player != null && player.IsPaused) ||
            (Time.timeScale <= 0f && AudioListener.pause)) return;
        if (!IsBlocking && cueSecondsRemaining <= 0f && captionSecondsRemaining <= 0f) return;
        bool hadCueTimer = cueSecondsRemaining > 0f;
        bool hadCaptionTimer = captionSecondsRemaining > 0f;
        if (hadCueTimer)
            cueSecondsRemaining = Mathf.Max(0f, cueSecondsRemaining - deltaSeconds);
        if (hadCaptionTimer)
        {
            captionSecondsRemaining = Mathf.Max(0f,
                captionSecondsRemaining - deltaSeconds);
            if (captionSecondsRemaining <= 0f) ClearActiveCaption();
        }
        if (hadCueTimer)
        {
            if (cueSecondsRemaining > 0f) return;
            captionBlocksInput = false;
            BeginNext();
            return;
        }
        if (hadCaptionTimer) return;
        if (!hasActiveMotion)
        {
            BeginNext();
            return;
        }
        motion.Advance(deltaSeconds);
        if (motion.Phase != GmParlorMotionPhase.Complete) return;
        hasActiveMotion = false;
        if (queue.Count > 0) queue.Dequeue();
        BeginNext();
    }

    public void FastForwardToCanonicalState()
    {
        if (hasActiveMotion) motion.FastForward();
        hasActiveMotion = false;
        cueSecondsRemaining = 0f;
        captionSecondsRemaining = 0f;
        captionBlocksInput = false;
        ClearActiveCaption();
        queue.Clear();
        if (targetSnapshot != null && !binder.TryApply(targetSnapshot, out string error))
            LastError = error;
    }

    void Update()
    {
        if (IsBlocking || cueSecondsRemaining > 0f || captionSecondsRemaining > 0f)
            Advance(Time.unscaledDeltaTime);
    }

    void AttachAccessibility()
    {
        GmAccessibilitySettings.OnChanged -= ApplyAccessibility;
        GmAccessibilitySettings.OnChanged += ApplyAccessibility;
        ApplyAccessibility();
    }

    void ApplyAccessibility()
    {
        // Scene teardown can destroy a coordinator before Unity dispatches OnDestroy in an editor
        // scene swap. The next settings event prunes that stale static subscriber before it can
        // touch a card view from the unloaded scene.
        if (this == null)
        {
            GmAccessibilitySettings.OnChanged -= ApplyAccessibility;
            return;
        }
        GmParlorAccessibilityProfile previous = accessibility;
        GmParlorAccessibilityProfile next = GmParlorAccessibilityProfile.FromGlobal();
        bool enablingReducedMotion = !ReducedMotion && next.ReducedMotion;
        accessibility = next;
        ReducedMotion = next.ReducedMotion;
        if (hasCurrentObservation && aldricPresenter != null)
        {
            aldricPresenter.RefreshAccessibility(accessibility);
            currentObservation = aldricPresenter.LastCue;
        }
        bool awaitingObservation = hasCurrentObservation && targetSnapshot != null &&
            targetSnapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement;
        if (awaitingObservation && !next.Captions)
        {
            captionSecondsRemaining = 0f;
            ClearActiveCaption();
        }
        else if (awaitingObservation && next.Captions && !previous.Captions)
        {
            captionSecondsRemaining = currentObservation.MinimumReadableSeconds;
            SetActiveCaption(currentObservation, reconstructed: true);
        }
        if (!enablingReducedMotion) return;

        if (hasActiveMotion)
        {
            motion.FastForward();
            hasActiveMotion = false;
            if (queue.Count > 0) queue.Dequeue();
            BeginNext();
        }
    }

    void OnDestroy()
    {
        GmAccessibilitySettings.OnChanged -= ApplyAccessibility;
    }

    void BeginNext()
    {
        while (queue.Count > 0)
        {
            GmParlorPresentationCommand command = queue.Peek();
            if (!IsCardMotion(command.Action))
            {
                if (command.Action == GmParlorPresentationAction.OpenJudgement &&
                    aldricPresenter != null)
                {
                    if (!aldricPresenter.TryPresent(command, accessibility,
                        out GmParlorEvidenceCue cue, out string cueError))
                    {
                        Fail(cueError, targetSnapshot, out _);
                        return;
                    }
                    queue.Dequeue();
                    hasCurrentObservation = true;
                    currentObservation = cue;
                    cueSecondsRemaining = cue.MinimumReadableSeconds;
                    captionBlocksInput = true;
                    if (accessibility.Captions)
                    {
                        captionSecondsRemaining = cue.MinimumReadableSeconds;
                        SetActiveCaption(cue, reconstructed: false);
                    }
                    else
                    {
                        captionSecondsRemaining = 0f;
                        ClearActiveCaption();
                    }
                    if (!aldricPresenter.TryNotifyCuePresented(out string observerError))
                        ContainCueObserverFailure(observerError);
                    return;
                }
                queue.Dequeue();
                continue;
            }

            GmParlorCardBinding[] bindings = GmParlorTableLayout.Build(targetSnapshot);
            if (!TryFindTarget(command, bindings, out GmParlorCardBinding binding) ||
                !binder.TryGetView(binding.PhysicalCard, out GmParlorCardView view) || view == null)
            {
                Fail($"Missing physical card view for {command.Action} {command.Card}",
                    targetSnapshot, out _);
                return;
            }
            motion.Begin(view, binding, ReducedMotion);
            hasActiveMotion = true;
            return;
        }

        if (targetSnapshot != null && !binder.TryApply(targetSnapshot, out string error))
            LastError = error;
    }

    bool Fail(string message, GmParlorMatchSnapshot canonicalTarget, out string error)
    {
        if (hasActiveMotion) motion.FastForward();
        hasActiveMotion = false;
        cueSecondsRemaining = 0f;
        captionSecondsRemaining = 0f;
        captionBlocksInput = false;
        ClearActiveCaption();
        queue.Clear();
        targetSnapshot = canonicalTarget?.DeepCopy();
        if (targetSnapshot != null && binder != null)
            binder.SnapAvailable(targetSnapshot);
        LastError = message;
        Debug.LogError($"[GmParlorPresentation] {message}");
        error = message;
        return false;
    }

    void SetActiveCaption(GmParlorEvidenceCue cue, bool reconstructed)
    {
        activeCaptionCommandId = cue.CommandId;
        activeCaptionText = cue.CaptionText;
        captionIsReconstructed = reconstructed;
        if (reconstructed) OnCaptionStateReconstructed?.Invoke();
        else OnActiveCaptionChanged?.Invoke();
    }

    void ClearActiveCaption()
    {
        if (activeCaptionCommandId == 0 && activeCaptionText.Length == 0) return;
        bool reconstructed = captionIsReconstructed;
        activeCaptionCommandId = 0;
        activeCaptionText = string.Empty;
        captionIsReconstructed = false;
        if (reconstructed) OnCaptionStateReconstructed?.Invoke();
        else OnActiveCaptionChanged?.Invoke();
    }

    void ContainCueObserverFailure(string error)
    {
        if (hasActiveMotion) motion.FastForward();
        hasActiveMotion = false;
        cueSecondsRemaining = 0f;
        captionSecondsRemaining = 0f;
        captionBlocksInput = false;
        ClearActiveCaption();
        queue.Clear();
        if (targetSnapshot != null && binder != null)
            binder.TryApply(targetSnapshot, out _);
        LastError = string.IsNullOrEmpty(error)
            ? "Aldric cue observer failed" : error;
    }

    static bool TryFindTarget(GmParlorPresentationCommand command,
        IReadOnlyList<GmParlorCardBinding> bindings, out GmParlorCardBinding target)
    {
        GmParlorCardZone zone = command.Action == GmParlorPresentationAction.PlayerCardToLead ||
            command.Action == GmParlorPresentationAction.AldricCardToLead
            ? GmParlorCardZone.Lead : GmParlorCardZone.Follow;
        for (int index = 0; index < bindings.Count; index++)
        {
            GmParlorCardBinding candidate = bindings[index];
            if (candidate.Zone == zone && command.Card.HasValue &&
                candidate.DisplayCard == command.Card.Value)
            {
                target = candidate;
                return true;
            }
        }
        target = default;
        return false;
    }

    static bool IsCardMotion(GmParlorPresentationAction action)
    {
        return action == GmParlorPresentationAction.PlayerCardToLead ||
            action == GmParlorPresentationAction.PlayerCardToFollow ||
            action == GmParlorPresentationAction.AldricCardToLead ||
            action == GmParlorPresentationAction.AldricCardToFollow;
    }
}
