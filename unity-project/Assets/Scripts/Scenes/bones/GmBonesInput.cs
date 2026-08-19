using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GmBonesFrameIntent { None, Cancel, Challenge, Confirm }

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
    GmBonesController controller;
    GmPlayer player;
    Func<bool> pauseProbe;
    bool navigationLatched;

    public bool HasRequiredActions { get; private set; }
    public bool IsConfigured => controller != null;

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
        HasRequiredActions = gameplay != null && move != null && confirm != null && challenge != null && cancel != null;
        if (HasRequiredActions) gameplay.Enable();
    }

    void Update()
    {
        if (!HasRequiredActions || controller == null) return;
        GmBonesFrameIntentResult result = ResolveFrameIntents(cancel.WasPressedThisFrame(), challenge.WasPressedThisFrame(),
            confirm.WasPressedThisFrame());
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

    public GmBonesFrameIntentResult ResolveFrameIntents(bool cancelPressed, bool challengePressed,
        bool confirmPressed)
    {
        if (GameplayIsPaused()) return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
        if (cancelPressed) return Result(GmBonesFrameIntent.Cancel, GmBonesActionError.None);
        if (challengePressed)
        {
            if (GameplayIsPaused()) return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
            return Result(GmBonesFrameIntent.Challenge, controller?.CallTell() ?? GmBonesActionError.NotInitialized);
        }
        if (confirmPressed)
        {
            if (GameplayIsPaused()) return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
            return Result(GmBonesFrameIntent.Confirm, controller?.ConfirmFocusedAction() ?? GmBonesActionError.NotInitialized);
        }
        return Result(GmBonesFrameIntent.None, GmBonesActionError.None);
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

    static GmBonesFrameIntentResult Result(GmBonesFrameIntent intent, GmBonesActionError error) =>
        new GmBonesFrameIntentResult(intent, error);

    void OnDisable() => gameplay?.Disable();
    void OnDestroy() { if (controls != null) Destroy(controls); }
}
