using UnityEngine;

/// <summary>
/// Collectible estate item (keys, diary pages, debtor ledger notes, clockwork components).
/// Persists in GmRunStore and provides immediate HUD feedback on pickup.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmEstateKeyItem : MonoBehaviour, IGmInteractionReceiver
{
    [SerializeField] string keyClueId = "key:brass_skeleton_key";
    [SerializeField] string itemDisplayName = "Brass Skeleton Key";
    [SerializeField, TextArea] string pickupMessage = "Acquired the Brass Skeleton Key.";
    [SerializeField, TextArea] string loreText = "An ornate, tarnished brass key bearing the Blackwood crest.";
    [SerializeField] string soundResource = "key_pickup";

    GmInteractable interactable;

    public string KeyClueId => keyClueId;
    public string ItemDisplayName => itemDisplayName;

    void Awake()
    {
        // If already held in persistent run state, hide immediately
        if (!string.IsNullOrEmpty(keyClueId) && GmRunStore.HasClue(keyClueId))
        {
            gameObject.SetActive(false);
            return;
        }

        interactable = GetComponent<GmInteractable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<GmInteractable>();
        }

        interactable.Configure(
            id: $"pickup_{keyClueId}",
            actionVerb: "Take",
            maximumRange: 2.8f,
            maximumFocusAngle: 8.0f,
            policy: GmInteractionRepeatPolicy.FirstOnly,
            audioResource: soundResource
        );
        interactable.BindContent(loreText, loreText);
    }

    public void OnGmInteraction(GmInteractable source)
    {
        if (!string.IsNullOrEmpty(keyClueId))
        {
            GmRunStore.RecordClue(keyClueId);
        }

        var hud = UnityEngine.Object.FindAnyObjectByType<GmGameHud>();
        if (hud != null)
        {
            hud.ShowPrompt(pickupMessage, "[E] Acquired");
        }

        // Hide collected item
        gameObject.SetActive(false);
    }

    public void Configure(string clueId, string name, string message, string lore)
    {
        keyClueId = clueId;
        itemDisplayName = name;
        pickupMessage = message;
        loreText = lore;
        if (interactable != null)
        {
            interactable.Configure($"pickup_{clueId}", "Take", 2.8f, 8.0f);
            interactable.BindContent(lore, lore);
        }
    }
}
