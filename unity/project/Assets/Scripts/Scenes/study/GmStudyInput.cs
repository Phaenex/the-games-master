using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GmStudyFrameIntent { None, Pause, Cancel, Challenge, Confirm }

public readonly struct GmStudyFrameIntentResult
{
    public readonly GmStudyFrameIntent Consumed;
    public readonly GmStudyActionError ActionError;
    public GmStudyFrameIntentResult(GmStudyFrameIntent consumed, GmStudyActionError error)
    {
        Consumed = consumed;
        ActionError = error;
    }
}

public sealed class GmStudyInput : MonoBehaviour
{
    const float EngageThreshold = 0.55f;
    const float ReleaseThreshold = 0.08f;
    InputActionAsset controls;
    InputActionMap gameplay;
    InputAction move;
    InputAction confirm;
    InputAction challenge;
    InputAction cancel;
    InputAction pause;
    GmStudyController controller;
    GmPlayer player;
    Func<bool> pauseProbe;
    bool navigationLatched;
    bool interactionsSuspended;

    public bool HasRequiredActions { get; private set; }
    public bool IsConfigured => controller != null;
    public bool InteractionsEnabled => !interactionsSuspended && enabled;
    public bool IsConfiguredFor(GmStudyController tableController) =>
        ReferenceEquals(controller, tableController);
    public GmStudyActionError LastActionError { get; private set; }
    public string FeedbackMessage { get; private set; } = string.Empty;
    public event Action OnFeedbackChanged;

    void OnEnable()
    {
        InputActionAsset shared = Resources.Load<InputActionAsset>("Input/GmControls");
        if (shared == null) return;
        if (controls == null)
        {
            controls = Instantiate(shared);
            controls.name = "GmStudyControls";
        }
        gameplay = controls.FindActionMap("Gameplay", false);
        move = gameplay?.FindAction("Move", false);
        confirm = gameplay?.FindAction("Interact", false);
        challenge = gameplay?.FindAction("CallTell", false);
        cancel = gameplay?.FindAction("Cancel", false);
        pause = gameplay?.FindAction("Pause", false);
        HasRequiredActions = gameplay != null && move != null && confirm != null && challenge != null &&
            cancel != null && pause != null;
        if (HasRequiredActions && !interactionsSuspended) gameplay.Enable();
    }

    void Update()
    {
        if (interactionsSuspended || !HasRequiredActions || controller == null) return;
        GmStudyFrameIntentResult result = ResolveFrameIntents(pause.WasPressedThisFrame(),
            cancel.WasPressedThisFrame(), challenge.WasPressedThisFrame(), confirm.WasPressedThisFrame());
        if (result.Consumed == GmStudyFrameIntent.None)
            HandleNavigationIntent(move.ReadValue<Vector2>());
    }

    public bool TryConfigure(GmStudyController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Study input needs an initialized controller";
            return false;
        }
        controller = tableController;
        player = GetComponent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        error = string.Empty;
        return true;
    }

    public void ConfigureForTests(GmStudyController tableController, Func<bool> isPaused)
    {
        controller = tableController;
        pauseProbe = isPaused;
    }

    public GmStudyFrameIntentResult ResolveFrameIntents(bool pausePressed, bool cancelPressed,
        bool challengePressed, bool confirmPressed)
    {
        if (interactionsSuspended)
            return Result(GmStudyFrameIntent.None, GmStudyActionError.NotInitialized);
        if (GameplayIsPaused()) return Result(GmStudyFrameIntent.None, GmStudyActionError.None);
        if (pausePressed) return Result(GmStudyFrameIntent.Pause, GmStudyActionError.None);
        if (cancelPressed) return Result(GmStudyFrameIntent.Cancel, GmStudyActionError.None);
        if (challengePressed)
        {
            if (GameplayIsPaused()) return Result(GmStudyFrameIntent.None, GmStudyActionError.None);
            GmStudyActionError error = ChallengeAction();
            return Result(GmStudyFrameIntent.Challenge, error);
        }
        if (confirmPressed)
        {
            if (GameplayIsPaused()) return Result(GmStudyFrameIntent.None, GmStudyActionError.None);
            GmStudyActionError error = ConfirmAction();
            return Result(GmStudyFrameIntent.Confirm, error);
        }
        return Result(GmStudyFrameIntent.None, GmStudyActionError.None);
    }

    public GmStudyActionError ConfirmAction() => Execute(controller == null
        ? null : controller.ConfirmFocusedAction);

    public GmStudyActionError ChallengeAction() => Execute(controller == null
        ? null : controller.Challenge);

    public GmStudyActionError FocusThenConfirm(int focusIndex)
    {
        if (interactionsSuspended) return Execute(null);
        if (controller == null) return Execute(null);
        if (focusIndex < 0 || focusIndex > 2)
        {
            PublishFeedback(GmStudyActionError.WrongPhase);
            return GmStudyActionError.WrongPhase;
        }
        controller.MoveFocus(focusIndex - controller.FocusIndex);
        return ConfirmAction();
    }

    GmStudyActionError Execute(Func<GmStudyActionError> action)
    {
        if (interactionsSuspended) action = null;
        GmStudyActionError error = action?.Invoke() ?? GmStudyActionError.NotInitialized;
        PublishFeedback(error);
        return error;
    }

    public void HandleNavigationIntent(Vector2 intent)
    {
        if (interactionsSuspended || GameplayIsPaused()) return;
        float magnitude = Mathf.Max(Mathf.Abs(intent.x), Mathf.Abs(intent.y));
        if (navigationLatched)
        {
            if (magnitude <= ReleaseThreshold) navigationLatched = false;
            return;
        }
        if (magnitude < EngageThreshold) return;
        navigationLatched = true;
        float axis = Mathf.Abs(intent.x) >= Mathf.Abs(intent.y) ? intent.x : -intent.y;
        controller?.MoveFocus(axis >= 0 ? 1 : -1);
    }

    bool GameplayIsPaused()
    {
        if (pauseProbe != null) return pauseProbe();
        if (player == null) player = GetComponent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        return player != null && player.IsPaused;
    }

    void PublishFeedback(GmStudyActionError error)
    {
        string message = FeedbackFor(error, controller);
        if (LastActionError == error && FeedbackMessage == message) return;
        LastActionError = error;
        FeedbackMessage = message;
        OnFeedbackChanged?.Invoke();
    }

    public static string FeedbackFor(GmStudyActionError error, GmStudyController tableController)
    {
        switch (error)
        {
            case GmStudyActionError.None: return string.Empty;
            case GmStudyActionError.WrongPhase:
                return "That move is not available right now.";
            case GmStudyActionError.PersistenceFailed:
                string detail = tableController?.LastPersistenceError;
                return string.IsNullOrEmpty(detail)
                    ? "The save failed. Retry the action."
                    : $"The save failed: {detail}. Retry the action.";
            default: return "The Study table is not ready.";
        }
    }

    static GmStudyFrameIntentResult Result(GmStudyFrameIntent intent, GmStudyActionError error) =>
        new GmStudyFrameIntentResult(intent, error);

    public void SuspendInteractions()
    {
        interactionsSuspended = true;
        navigationLatched = false;
        gameplay?.Disable();
    }

    public void ResumeInteractions()
    {
        interactionsSuspended = false;
        if (enabled && HasRequiredActions) gameplay.Enable();
    }

    void OnDisable() => gameplay?.Disable();
    void OnDestroy() { if (controls != null) Destroy(controls); }
}
