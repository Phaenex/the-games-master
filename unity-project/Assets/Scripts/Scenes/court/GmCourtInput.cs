using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GmCourtInput : MonoBehaviour
{
    GmCourtController court;
    InputActionAsset controls;
    InputActionMap gameplay;
    InputAction move;
    InputAction present;
    bool navigationLatched;

    public bool HasRequiredActions { get; private set; }

    void OnEnable()
    {
        court = GetComponent<GmCourtController>() ?? FindAnyObjectByType<GmCourtController>();
        if (controls != null)
        {
            if (HasRequiredActions) gameplay.Enable();
            return;
        }
        InputActionAsset shared = Resources.Load<InputActionAsset>("Input/GmControls");
        if (shared == null) return;
        controls = Instantiate(shared);
        controls.name = "GmCourtControls";
        gameplay = controls.FindActionMap("Gameplay", false);
        move = gameplay?.FindAction("Move", false);
        present = gameplay?.FindAction("Interact", false);
        HasRequiredActions = gameplay != null && move != null && present != null;
        if (HasRequiredActions) gameplay.Enable();
    }

    void Update()
    {
        if (court == null || court.HearingResolved) return;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) court.SelectEvidence(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) court.SelectEvidence(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) court.SelectEvidence(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) court.SelectEvidence(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) court.SelectEvidence(4);
        }
        if (!HasRequiredActions) return;
        Vector2 navigation = move.ReadValue<Vector2>();
        if (navigation.sqrMagnitude < 0.08f) navigationLatched = false;
        else if (!navigationLatched && Mathf.Abs(navigation.x) > 0.55f)
        {
            court.MoveSelection(navigation.x > 0f ? 1 : -1);
            navigationLatched = true;
        }
        if (present.WasPressedThisFrame()) court.PresentSelectedEvidence();
    }

    void OnDisable() => gameplay?.Disable();

    void OnDestroy()
    {
        if (controls != null) Destroy(controls);
    }
}
