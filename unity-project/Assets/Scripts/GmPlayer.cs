// First-person controller driven by one project input map. Gameplay code consumes intentions rather
// than polling device keys, so keyboard/mouse and controller share movement and interaction logic.
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GmPlayer : MonoBehaviour
{
    public float walkSpeed = 3.5f;
    public float lookSensitivity = 0.18f;
    public float controllerLookSpeed = 115f;
    CharacterController cc;
    Transform cam;
    InputActionMap gameplay;
    InputAction moveAction, lookAction, interactAction, cancelAction, pauseAction, reviewWindAction,
        quitAction, brightnessDownAction, brightnessUpAction, callTellAction;
    GmInteractionScanner interactionScanner;
    GmDisplayCalibration displayCalibration;
    float pitch;
    int suppressLookFrames;
    bool usingGamepad;
    bool paused;
    float timeScaleBeforePause = 1f;

    public bool ControlBlocked { get; private set; }
    public bool HasPointerCapture => Cursor.lockState == CursorLockMode.Locked;
    public bool UsingGamepad => usingGamepad;
    public bool IsPaused => paused;
    public bool QuitRequested { get; private set; }
    public GmDisplayCalibration DisplayCalibration => displayCalibration;
    public bool InteractPressedThisFrame => interactAction != null && interactAction.WasPressedThisFrame();
    public bool CancelPressedThisFrame => cancelAction != null && cancelAction.WasPressedThisFrame();
    public bool ReviewWindPressedThisFrame => !ControlBlocked && !paused && reviewWindAction != null &&
        reviewWindAction.WasPressedThisFrame();
    public GmInteractionScanner InteractionScanner => interactionScanner;
    public bool CallTellPressedThisFrame => !ControlBlocked && !paused &&
        callTellAction != null && callTellAction.WasPressedThisFrame();

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>()?.transform;
        interactionScanner = GetComponent<GmInteractionScanner>() ?? gameObject.AddComponent<GmInteractionScanner>();
        displayCalibration = FindAnyObjectByType<GmDisplayCalibration>();
        var controls = Resources.Load<InputActionAsset>("Input/GmControls");
        if (controls == null)
            Debug.LogError("[GmPlayer] Resources/Input/GmControls.inputactions is missing");
        else
        {
            gameplay = controls.FindActionMap("Gameplay", true);
            moveAction = gameplay.FindAction("Move", true);
            lookAction = gameplay.FindAction("Look", true);
            interactAction = gameplay.FindAction("Interact", true);
            cancelAction = gameplay.FindAction("Cancel", true);
            pauseAction = gameplay.FindAction("Pause", true);
            reviewWindAction = gameplay.FindAction("ReviewWind", true);
            quitAction = gameplay.FindAction("Quit", true);
            brightnessDownAction = gameplay.FindAction("BrightnessDown", true);
            brightnessUpAction = gameplay.FindAction("BrightnessUp", true);
            callTellAction = gameplay.FindAction("CallTell", true);
            gameplay.Enable();
        }
        LockPointer();
    }

    void Update()
    {
        UpdateActiveDevice();

        if (!ControlBlocked && pauseAction != null && pauseAction.WasPressedThisFrame())
        {
            SetPaused(!paused);
            return;
        }

        if (paused)
        {
            if (brightnessDownAction != null && brightnessDownAction.WasPressedThisFrame())
            {
                displayCalibration?.Step(-1);
                return;
            }
            if (brightnessUpAction != null && brightnessUpAction.WasPressedThisFrame())
            {
                displayCalibration?.Step(1);
                return;
            }
            if (quitAction != null && quitAction.WasPressedThisFrame())
            {
                RequestQuit();
                return;
            }
            if (InteractPressedThisFrame || CancelPressedThisFrame) SetPaused(false);
            return;
        }

        if (ControlBlocked) return;

        if (CancelPressedThisFrame && !usingGamepad)
        {
            ReleasePointer();
            return;
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !HasPointerCapture)
        {
            usingGamepad = false;
            LockPointer();
            return;
        }

        Vector2 rawLook = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        Vector2 rawMove = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        if (!HasPointerCapture && !usingGamepad) return;

        if (cam != null && rawLook.sqrMagnitude > 0f)
        {
            bool controllerLook = lookAction?.activeControl?.device is Gamepad;
            if (controllerLook) ApplyControllerLook(rawLook);
            else if (suppressLookFrames > 0) suppressLookFrames--;
            else ApplyLookDelta(rawLook);
        }

        if (cc == null || !cc.enabled) return;
        Vector3 dir = transform.forward * rawMove.y + transform.right * rawMove.x;
        cc.SimpleMove(dir.normalized * walkSpeed);

        if (InteractPressedThisFrame)
            interactionScanner?.TryInteract();

        // The core verb. Deliberately a separate press from Interact: Examine is looking, this is
        // judging, and a game about catching a cheat should never conflate the two.
        if (CallTellPressedThisFrame)
            interactionScanner?.TryCallTell();
    }

    void UpdateActiveDevice()
    {
        var pad = Gamepad.current;
        bool gamepadActivity = pad != null &&
            (pad.rightStick.ReadValue().sqrMagnitude > 0.0025f ||
             pad.leftStick.ReadValue().sqrMagnitude > 0.0025f ||
             pad.dpad.ReadValue().sqrMagnitude > 0.0025f ||
             pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
             pad.buttonNorth.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
             pad.startButton.wasPressedThisFrame);
        if (gamepadActivity)
        {
            usingGamepad = true;
            return;
        }

        var mouse = Mouse.current;
        bool mouseActivity = mouse != null &&
            (mouse.delta.ReadValue().sqrMagnitude > 0.01f || mouse.leftButton.wasPressedThisFrame);
        bool keyboardActivity = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        if (mouseActivity || keyboardActivity) usingGamepad = false;
    }

    public void SetPitch(float newPitch)
    {
        pitch = Mathf.Clamp(newPitch, -89f, 89f);
        if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void SetControlBlocked(bool blocked)
    {
        ControlBlocked = blocked;
        if (blocked) ReleasePointer();
        else if (!paused) LockPointer();
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (paused)
        {
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ReleasePointer();
            GmExperienceTelemetry.Record("pause", usingGamepad ? "controller" : "keyboard");
        }
        else
        {
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
            if (!ControlBlocked) LockPointer();
            GmExperienceTelemetry.Record("resume", usingGamepad ? "controller" : "keyboard");
        }
    }

    void RequestQuit()
    {
        QuitRequested = true;
        GmExperienceTelemetry.Record("quit", usingGamepad ? "controller" : "keyboard");
        Debug.Log("[GmPlayer] quit requested from pause menu");
        if (!Application.isEditor) Application.Quit(0);
    }

    public void ApplyLookDelta(Vector2 rawDelta)
    {
        if (cam == null) cam = GetComponentInChildren<Camera>()?.transform;
        if (cam == null) return;
        Vector2 delta = rawDelta * lookSensitivity;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void ApplyControllerLook(Vector2 stick)
    {
        if (cam == null) cam = GetComponentInChildren<Camera>()?.transform;
        if (cam == null) return;
        Vector2 delta = stick * controllerLookSpeed * Time.unscaledDeltaTime;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleasePointer();
        else if (!ControlBlocked) LockPointer();
    }

    void LockPointer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        suppressLookFrames = 4;
    }

    void ReleasePointer()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        if (paused)
        {
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
        }
        gameplay?.Disable();
    }
}

