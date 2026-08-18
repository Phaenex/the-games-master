using UnityEngine;

/// <summary>
/// A physical room exit that remains shut until the authored room completion is banked. The door
/// leaves open around real hinges; only after they clear the opening does the transition collider
/// arm and the blocker release.
/// </summary>
public sealed class GmSequenceExit : MonoBehaviour
{
    [SerializeField] string requiredRoomId;
    [SerializeField] string requiredCatchId;
    [SerializeField] GmSceneTransitionTrigger transition;
    [SerializeField] Collider transitionVolume;
    [SerializeField] Collider blocker;
    [SerializeField] Transform leftHinge;
    [SerializeField] Transform rightHinge;
    [SerializeField] Vector3 leftOpenEuler;
    [SerializeField] Vector3 rightOpenEuler;
    [SerializeField, Min(1f)] float degreesPerSecond = 105f;

    [SerializeField, HideInInspector] Quaternion leftClosed;
    [SerializeField, HideInInspector] Quaternion rightClosed;
    [SerializeField, HideInInspector] bool configured;
    bool subscribed;

    public bool IsUnlocked { get; private set; }
    public bool PassageOpen { get; private set; }

    public void Configure(string roomId, GmSceneTransitionTrigger sceneTransition,
        Collider triggerVolume, Collider physicalBlocker, Transform leftDoorHinge,
        Transform rightDoorHinge, Vector3 leftOpen, Vector3 rightOpen, bool authorClosed = false)
    {
        requiredRoomId = roomId;
        requiredCatchId = "";
        ConfigureParts(sceneTransition, triggerVolume, physicalBlocker, leftDoorHinge,
            rightDoorHinge, leftOpen, rightOpen, authorClosed);
    }

    public void ConfigureForCatch(string catchId, GmSceneTransitionTrigger sceneTransition,
        Collider triggerVolume, Collider physicalBlocker, Transform leftDoorHinge,
        Transform rightDoorHinge, Vector3 leftOpen, Vector3 rightOpen, bool authorClosed = false)
    {
        requiredRoomId = "";
        requiredCatchId = catchId;
        ConfigureParts(sceneTransition, triggerVolume, physicalBlocker, leftDoorHinge,
            rightDoorHinge, leftOpen, rightOpen, authorClosed);
    }

    void ConfigureParts(GmSceneTransitionTrigger sceneTransition, Collider triggerVolume,
        Collider physicalBlocker, Transform leftDoorHinge, Transform rightDoorHinge,
        Vector3 leftOpen, Vector3 rightOpen, bool authorClosed)
    {
        transition = sceneTransition;
        transitionVolume = triggerVolume;
        blocker = physicalBlocker;
        leftHinge = leftDoorHinge;
        rightHinge = rightDoorHinge;
        leftOpenEuler = leftOpen;
        rightOpenEuler = rightOpen;
        leftClosed = leftHinge != null ? leftHinge.localRotation : Quaternion.identity;
        rightClosed = rightHinge != null ? rightHinge.localRotation : Quaternion.identity;
        configured = true;
        Subscribe();
        if (authorClosed) ApplyLocked(); else RefreshFromRun();
    }

    void OnEnable()
    {
        Subscribe();
        if (configured) RefreshFromRun();
    }

    void OnDisable()
    {
        if (!subscribed) return;
        GmRunStore.OnStateChanged -= RefreshFromRun;
        subscribed = false;
    }

    void Subscribe()
    {
        if (subscribed) return;
        GmRunStore.OnStateChanged += RefreshFromRun;
        subscribed = true;
    }

    void RefreshFromRun()
    {
        if (!configured) return;
        IsUnlocked = !string.IsNullOrWhiteSpace(requiredCatchId)
            ? GmRunStore.HasCatch(requiredCatchId)
            : GmRunStore.IsRoomComplete(requiredRoomId);

        if (!IsUnlocked)
        {
            ApplyLocked();
            return;
        }

        // Builder/EditMode verification has no player loop. Put the authored end state in place so
        // a saved completion cannot rebuild a locked scene. Runtime opens over time below.
        if (!Application.isPlaying || (leftHinge == null && rightHinge == null))
        {
            if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(leftOpenEuler);
            if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(rightOpenEuler);
            ArmPassage();
            return;
        }

        if (transitionVolume != null) transitionVolume.enabled = false;
        if (blocker != null) blocker.enabled = true;
    }

    void ApplyLocked()
    {
        IsUnlocked = false;
        PassageOpen = false;
        if (transitionVolume != null) transitionVolume.enabled = false;
        if (blocker != null) blocker.enabled = true;
        if (leftHinge != null) leftHinge.localRotation = leftClosed;
        if (rightHinge != null) rightHinge.localRotation = rightClosed;
    }

    void Update()
    {
        if (!configured || !IsUnlocked || PassageOpen) return;
        bool leftDone = Rotate(leftHinge, Quaternion.Euler(leftOpenEuler));
        bool rightDone = Rotate(rightHinge, Quaternion.Euler(rightOpenEuler));
        if (leftDone && rightDone) ArmPassage();
    }

    /// <summary>
    /// Completes an already-earned opening in one frame for deterministic review capture. This does
    /// not unlock anything: an unmet room or catch remains shut, so a tour cannot accidentally turn
    /// a review helper into a second progression path.
    /// </summary>
    public bool SnapOpenForReview()
    {
        if (!configured || !IsUnlocked) return false;
        if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(leftOpenEuler);
        if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(rightOpenEuler);
        ArmPassage();
        return true;
    }

    bool Rotate(Transform hinge, Quaternion target)
    {
        if (hinge == null) return true;
        hinge.localRotation = Quaternion.RotateTowards(
            hinge.localRotation, target, degreesPerSecond * Time.deltaTime);
        return Quaternion.Angle(hinge.localRotation, target) < 0.1f;
    }

    void ArmPassage()
    {
        PassageOpen = true;
        if (blocker != null) blocker.enabled = false;
        if (transitionVolume != null) transitionVolume.enabled = true;
        string requirement = !string.IsNullOrWhiteSpace(requiredCatchId)
            ? $"catch {requiredCatchId}"
            : $"room {requiredRoomId} complete";
        Debug.Log($"[GmSequenceExit] {requirement} — onward passage armed");
    }
}
