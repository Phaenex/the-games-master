using UnityEngine;
using UnityEngine.InputSystem;

public enum GmParlorFrameIntent
{
    None,
    Cancel,
    Read,
    Confirm,
}

public readonly struct GmParlorFrameIntentResult
{
    public readonly GmParlorFrameIntent Consumed;
    public readonly GmParlorActionError ActionError;

    public GmParlorFrameIntentResult(GmParlorFrameIntent consumed,
        GmParlorActionError actionError)
    {
        Consumed = consumed;
        ActionError = actionError;
    }
}

public sealed class GmParlorInput : MonoBehaviour
{
    const int SubscriberBindFrames = 1;

    InputActionAsset controls;
    InputActionMap gameplay;
    InputAction navigation;
    InputAction confirm;
    InputAction read;
    InputAction cancel;
    GmParlorController controller;
    GmParlorFocusView focus;
    GmPlayer player;
    int activationFramesObserved;
    bool activationAttempted;

    public bool HasRequiredActions { get; private set; }
    public bool IsConfigured => controller != null && focus != null;

    void LateUpdate()
    {
        // Scene restore stays silent through Awake/Start so HUD, focus, evidence, and outcome
        // observers can bind. The shipping input owner opens the table only after the bounded
        // subscriber window has completed;
        // hand-built graphs without this component retain their explicit activation boundary.
        if (activationAttempted) return;
        if (activationFramesObserved++ < SubscriberBindFrames) return;
        activationAttempted = true;
        if (controller != null && !controller.IsActivated) controller.Activate();
    }

    void OnEnable()
    {
        controller = GetComponent<GmParlorController>() ?? FindAnyObjectByType<GmParlorController>();
        focus = GetComponent<GmParlorFocusView>() ?? FindAnyObjectByType<GmParlorFocusView>();
        player = GetComponent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        InputActionAsset shared = Resources.Load<InputActionAsset>("Input/GmControls");
        if (shared == null) return;
        if (controls == null)
        {
            controls = Instantiate(shared);
            controls.name = "GmParlorControls";
        }
        gameplay = controls.FindActionMap("Gameplay", false);
        navigation = gameplay?.FindAction("Move", false);
        confirm = gameplay?.FindAction("Interact", false);
        read = gameplay?.FindAction("CallTell", false);
        cancel = gameplay?.FindAction("Cancel", false);
        HasRequiredActions = gameplay != null && navigation != null && confirm != null &&
            read != null && cancel != null;
        if (HasRequiredActions) gameplay.Enable();
    }

    void Update()
    {
        if (!HasRequiredActions || controller == null) return;
        HandleNavigationIntent(navigation.ReadValue<Vector2>());
        ResolveFrameIntents(cancel.WasPressedThisFrame(), read.WasPressedThisFrame(),
            confirm.WasPressedThisFrame());
    }

    public bool TryConfigure(GmParlorController tableController, GmParlorFocusView focusView,
        out string error)
    {
        if (tableController == null || focusView == null || !focusView.IsConfigured)
        {
            error = "Parlor input needs a controller and configured focus view";
            return false;
        }
        controller = tableController;
        focus = focusView;
        player = GetComponent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        error = string.Empty;
        return true;
    }

    public void HandleNavigationIntent(Vector2 navigationIntent)
    {
        if (GameplayIsPaused()) return;
        controller?.ApplyNavigation(navigationIntent);
    }

    /// <summary>
    /// Resolves all buttons sampled in one frame through one deterministic priority. A chord can
    /// never advance two canonical actions, and a paused player owns the whole frame.
    /// </summary>
    public GmParlorFrameIntentResult ResolveFrameIntents(bool cancelPressed,
        bool readPressed, bool confirmPressed)
    {
        if (GameplayIsPaused())
            return new GmParlorFrameIntentResult(GmParlorFrameIntent.None,
                GmParlorActionError.None);
        if (cancelPressed)
        {
            HandleCancelIntent();
            return new GmParlorFrameIntentResult(GmParlorFrameIntent.Cancel,
                GmParlorActionError.None);
        }
        if (readPressed)
            return new GmParlorFrameIntentResult(GmParlorFrameIntent.Read,
                HandleReadIntent());
        if (confirmPressed)
            return new GmParlorFrameIntentResult(GmParlorFrameIntent.Confirm,
                HandleConfirmIntent());
        return new GmParlorFrameIntentResult(GmParlorFrameIntent.None,
            GmParlorActionError.None);
    }

    public GmParlorActionError HandleConfirmIntent()
    {
        if (controller == null) return GmParlorActionError.WrongPhase;
        if (focus != null && !focus.IsOpen)
        {
            focus.Open();
            return GmParlorActionError.None;
        }
        if (focus == null) return controller.ConfirmFocusedAction();
        GmParlorActionError result = focus.Confirm();
        // Once a valid intent leaves card selection, the equivalent reading surface yields to the
        // physical table. Cancel during the authored motion must fast-forward that motion; it must
        // not spend its first press closing a stale selection overlay.
        if (result == GmParlorActionError.None) focus.Close();
        return result;
    }

    public GmParlorActionError HandleReadIntent() => focus != null
        ? focus.Read() : controller != null
            ? controller.CallRead() : GmParlorActionError.WrongPhase;

    public bool HandleCancelIntent()
    {
        if (focus != null && focus.IsOpen)
        {
            focus.Close();
            return true;
        }
        return controller != null && controller.CancelOrFastForward();
    }

    bool GameplayIsPaused()
    {
        if (player == null) player = GetComponent<GmPlayer>() ?? FindAnyObjectByType<GmPlayer>();
        return player != null && player.IsPaused;
    }

    void OnDisable()
    {
        gameplay?.Disable();
    }

    void OnDestroy()
    {
        if (controls != null) Destroy(controls);
    }
}
