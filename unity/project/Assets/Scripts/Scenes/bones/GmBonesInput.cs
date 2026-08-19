using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GmBonesFrameIntent { None, Pause, Cancel, Challenge, Confirm }

public readonly struct GmBonesFrameIntentResult
{
    public readonly GmBonesFrameIntent Consumed;
    public readonly GmBonesActionError ActionError;
    public GmBonesFrameIntentResult(GmBonesFrameIntent consumed, GmBonesActionError error)
    {
        Consumed = consumed;
        ActionError = error;
    }
}

public sealed class GmBonesInput : MonoBehaviour
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
    GmBonesController controller;
    GmPlayer player;
    Func<bool> pauseProbe;
    bool navigationLatched;

    public bool HasRequiredActions { get; private set; }
    public bool IsConfigured => controller != null;
    public bool IsConfiguredFor(GmBonesController tableController) =>
        ReferenceEquals(controller, tableController);
    public GmBonesActionError LastActionError { get; private set; }
    public string FeedbackMessage { get; private set; } = string.Empty;
    public event Action OnFeedbackChanged;

    void OnEnable()
    {
        InputActionAsset shared = Resources.Load<InputActionAsset>("Input/GmControls");
        if (shared == null) return;
        if (controls == null)
        {
            controls = Instantiate(shared);
            controls.name = "GmBonesControls";
        }
        gameplay = controls.FindActionMap("Gameplay", false);
        move = gameplay?.FindAction("Move", false);
        confirm = gameplay?.FindAction("Interact", false);
        challenge = gameplay?.FindAction("CallTell", false);
        cancel = gameplay?.FindAction("Cancel", false);
        pause = gameplay?.FindAction("Pause", false);
        HasRequiredActions = gameplay != null && move != null && confirm != null && challenge != null &&
            cancel != null && pause != null;
        if (HasRequiredActions) gameplay.Enable();
    }

    void Update()
    {
        if (!HasRequiredActions || controller == null) return;
        GmBonesFrameIntentResult result = ResolveFrameIntents(pause.WasPressedThisFrame(),
            cancel.WasPressedThisFrame(), challenge.WasPressedThisFrame(), confirm.WasPressedThisFrame());
        if (result.Consumed == GmBonesFrameIntent.None)
            HandleNavigationIntent(move.ReadValue<Vector2>());
    }

    public bool TryConfigure(GmBonesController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Bones input needs an initialized controller";
            return false;
        }
        controller = tableController;
        player = GetComponent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        error = string.Empty;
        return true;
    }

    public void ConfigureForTests(GmBonesController tableController, Func<bool> isPaused)
    {
        controller = tableController;
        pauseProbe = isPaused;
    }

    public GmBonesFrameIntentResult ResolveFrameIntents(bool pausePressed, bool cancelPressed,
        bool challengePressed, bool confirmPressed)
    {
        if (GameplayIsPaused()) return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
        if (pausePressed) return Result(GmBonesFrameIntent.Pause, GmBonesActionError.None);
        if (cancelPressed) return Result(GmBonesFrameIntent.Cancel, GmBonesActionError.None);
        if (challengePressed)
        {
            if (GameplayIsPaused()) return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
            GmBonesActionError error = ChallengeAction();
            return Result(GmBonesFrameIntent.Challenge, error);
        }
        if (confirmPressed)
        {
            if (GameplayIsPaused()) return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
            GmBonesActionError error = ConfirmAction();
            return Result(GmBonesFrameIntent.Confirm, error);
        }
        return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
    }

    public GmBonesActionError ConfirmAction() => Execute(controller == null
        ? null : controller.ConfirmFocusedAction);

    public GmBonesActionError ChallengeAction() => Execute(controller == null
        ? null : controller.CallTell);

    public GmBonesActionError FocusThenConfirm(int focusIndex)
    {
        if (controller == null) return Execute(null);
        if (focusIndex < 0 || focusIndex > 3)
        {
            PublishFeedback(GmBonesActionError.WrongPhase);
            return GmBonesActionError.WrongPhase;
        }
        controller.MoveFocus(focusIndex - controller.FocusIndex);
        return ConfirmAction();
    }

    GmBonesActionError Execute(Func<GmBonesActionError> action)
    {
        GmBonesActionError error = action?.Invoke() ?? GmBonesActionError.NotInitialized;
        PublishFeedback(error);
        return error;
    }

    public void HandleNavigationIntent(Vector2 intent)
    {
        if (GameplayIsPaused()) return;
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

    void PublishFeedback(GmBonesActionError error)
    {
        string message = FeedbackFor(error, controller);
        if (LastActionError == error && FeedbackMessage == message) return;
        LastActionError = error;
        FeedbackMessage = message;
        OnFeedbackChanged?.Invoke();
    }

    public static string FeedbackFor(GmBonesActionError error, GmBonesController tableController)
    {
        switch (error)
        {
            case GmBonesActionError.None: return string.Empty;
            case GmBonesActionError.WrongPhase:
                return "That Bones action is not available right now.";
            case GmBonesActionError.PersistenceFailed:
                string detail = tableController?.LastPersistenceError;
                return string.IsNullOrEmpty(detail)
                    ? "The save failed. Retry the action."
                    : $"The save failed: {detail}. Retry the action.";
            default: return "The Bones table is not ready.";
        }
    }

    static GmBonesFrameIntentResult Result(GmBonesFrameIntent intent, GmBonesActionError error) =>
        new GmBonesFrameIntentResult(intent, error);

    void OnDisable() => gameplay?.Disable();
    void OnDestroy() { if (controls != null) Destroy(controls); }
}
