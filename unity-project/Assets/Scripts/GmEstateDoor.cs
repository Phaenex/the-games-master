using UnityEngine;

/// <summary>
/// Interactive estate door. Closed, locked, barred, and secret-latched leaves keep a solid
/// collider in the opening. Open leaves disable that collider. Unlock state persists on GmRunStore.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmEstateDoor : MonoBehaviour, IGmInteractionReceiver
{
    public const string BarrierName = "DoorBarrier";

    [Header("Door Configuration")]
    [SerializeField] string doorId = "door_standard";
    [SerializeField] string doorName = "Oak Door";
    [SerializeField] string requiredKeyClueId = "";
    [SerializeField] string keyDisplayName = "";
    [SerializeField] bool isLocked;
    [SerializeField] bool isBarred;
    [SerializeField] bool isSecretMechanism;
    [SerializeField] bool startOpen;

    [Header("Animation & Geometry")]
    [SerializeField] Transform doorLeaf;
    [SerializeField] BoxCollider barrier;
    [SerializeField] float openAngle = 90f;
    [SerializeField] float closedAngle;
    [SerializeField] Vector3 rotationAxis = Vector3.up;
    [SerializeField] float swingSpeed = 4.0f;

    [Header("Audio & Prompts")]
    [SerializeField] string soundResource = "door_creak";

    bool isOpen;
    Quaternion targetRotation;
    Quaternion initialLocalRotation;
    GmInteractable interactable;
    bool poseInitialized;

    public string DoorId => doorId;
    public string DoorName => doorName;
    public bool IsLocked => isLocked;
    public bool IsBarred => isBarred;
    public bool IsSecretMechanism => isSecretMechanism;
    public bool IsOpen => isOpen;
    public string RequiredKey => requiredKeyClueId;
    public BoxCollider Barrier => barrier;

    /// Lane G: a player-sized body cannot walk the opening while this is true.
    public bool BlocksPassage =>
        barrier != null && barrier.enabled && !barrier.isTrigger;

    void Awake()
    {
        EnsurePose();
        if (!string.IsNullOrEmpty(doorId) && GmRunStore.HasClue($"unlocked:{doorId}"))
            isLocked = false;

        interactable = GetComponent<GmInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<GmInteractable>();

        if (startOpen && !isBarred && !isLocked)
            SetOpen(true, immediate: true);
        else
            ApplyBarrier();

        UpdateInteractableConfig();
    }

    void Update()
    {
        if (doorLeaf == null) return;
        doorLeaf.localRotation = Quaternion.Slerp(
            doorLeaf.localRotation, targetRotation, Time.deltaTime * swingSpeed);
    }

    public void OnGmInteraction(GmInteractable source)
    {
        if (isBarred)
        {
            ShowFeedback($"{doorName} is barred firmly from the other side.");
            return;
        }

        if (isSecretMechanism && isLocked)
        {
            if (HasRequiredKey())
                UnlockAndOpen($"You triggered the concealed release. {doorName} swings open.");
            else
                ShowFeedback(
                    "A solid mahogany panel with faint hairline seams. It appears locked by an unseen latch.");
            return;
        }

        if (isLocked)
        {
            if (HasRequiredKey())
            {
                string keyBit = string.IsNullOrEmpty(keyDisplayName)
                    ? "the key"
                    : $"the {keyDisplayName}";
                UnlockAndOpen($"Unlocked {doorName} using {keyBit}.");
            }
            else
            {
                string keyPrompt = string.IsNullOrEmpty(keyDisplayName) ? "a key" : $"the {keyDisplayName}";
                ShowFeedback($"{doorName} is locked. It requires {keyPrompt}.");
            }
            return;
        }

        ToggleDoor();
    }

    public void UnlockAndOpen(string feedbackMessage = "")
    {
        isLocked = false;
        if (!string.IsNullOrEmpty(doorId))
            GmRunStore.RecordClue($"unlocked:{doorId}");
        SetOpen(true, immediate: true);
        UpdateInteractableConfig();
        if (!string.IsNullOrEmpty(feedbackMessage))
            ShowFeedback(feedbackMessage);
    }

    public void RelockClosed()
    {
        isLocked = true;
        SetOpen(false, immediate: true);
        UpdateInteractableConfig();
    }

    public void ToggleDoor()
    {
        SetOpen(!isOpen, immediate: true);
        UpdateInteractableConfig();
    }

    public void Configure(string id, string name, string requiredKey, string keyName,
        bool locked, bool barred, float openAng = 90f, Transform leaf = null,
        bool secret = false, bool openAtStart = false, BoxCollider barrierCollider = null,
        Vector3 swingAxis = default)
    {
        doorId = id;
        doorName = name;
        requiredKeyClueId = requiredKey ?? "";
        keyDisplayName = keyName ?? "";
        isLocked = locked;
        isBarred = barred;
        isSecretMechanism = secret;
        startOpen = openAtStart;
        openAngle = openAng;
        if (leaf != null) doorLeaf = leaf;
        if (barrierCollider != null) barrier = barrierCollider;
        if (swingAxis.sqrMagnitude > 0.01f)
            rotationAxis = swingAxis.normalized;
        EnsurePose();
        if (startOpen && !isBarred && !isLocked)
            SetOpen(true, immediate: true);
        else
            ApplyBarrier();
        UpdateInteractableConfig();
    }

    public BoxCollider EnsureBarrier(Vector3 localSize)
    {
        EnsurePose();
        Transform host = doorLeaf != null ? doorLeaf : transform;
        if (barrier == null)
        {
            Transform existing = host.Find(BarrierName);
            if (existing != null)
                barrier = existing.GetComponent<BoxCollider>();
        }

        if (barrier == null)
        {
            var holder = new GameObject(BarrierName);
            holder.transform.SetParent(host, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            barrier = holder.AddComponent<BoxCollider>();
        }

        barrier.size = localSize;
        barrier.center = Vector3.zero;
        barrier.isTrigger = false;
        ApplyBarrier();
        return barrier;
    }

    void SetOpen(bool open, bool immediate)
    {
        EnsurePose();
        isOpen = open;
        float angle = isOpen ? openAngle : closedAngle;
        targetRotation = initialLocalRotation * Quaternion.AngleAxis(angle, rotationAxis);
        if (immediate && doorLeaf != null)
            doorLeaf.localRotation = targetRotation;
        ApplyBarrier();
    }

    void ApplyBarrier()
    {
        if (barrier == null) EnsureBarrier(new Vector3(0.14f, 2.2f, 1.15f));
        bool block = isBarred || isLocked || !isOpen;
        barrier.enabled = block;
        barrier.isTrigger = false;
    }

    void EnsurePose()
    {
        if (doorLeaf == null) doorLeaf = transform;
        if (!poseInitialized)
        {
            initialLocalRotation = doorLeaf.localRotation;
            targetRotation = initialLocalRotation;
            poseInitialized = true;
        }
    }

    bool HasRequiredKey()
    {
        return !string.IsNullOrEmpty(requiredKeyClueId) && GmRunStore.HasClue(requiredKeyClueId);
    }

    void UpdateInteractableConfig()
    {
        if (interactable == null) return;
        string verb = isBarred ? "Examine" : (isLocked ? "Unlock" : (isOpen ? "Close" : "Open"));
        interactable.Configure(
            id: $"interact_{doorId}",
            actionVerb: verb,
            maximumRange: 3.5f,
            maximumFocusAngle: 8.0f,
            policy: GmInteractionRepeatPolicy.RepeatSecond,
            audioResource: soundResource
        );
        string desc = isBarred ? "Heavy iron bars secure this doorway." :
                      isLocked ? "A heavy door locked by an antique keyhole." :
                      (isOpen ? "An open passageway." : "A closed Victorian door.");
        interactable.BindContent(desc, desc);
    }

    void ShowFeedback(string message)
    {
        var hud = Object.FindAnyObjectByType<GmGameHud>();
        if (hud != null)
            hud.ShowPrompt(message, "[E] Interact");
    }
}
