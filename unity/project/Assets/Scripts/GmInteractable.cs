using UnityEngine;

/// <summary>
/// Semantic interaction hook used by room/game directors. The interactable still owns focus,
/// repetition and authored text; receivers react only after that interaction has actually succeeded.
/// This keeps story progression out of the ray scanner and makes stable interaction IDs reusable.
/// </summary>
public interface IGmInteractionReceiver
{
    void OnGmInteraction(GmInteractable source);
}

public enum GmInteractionRepeatPolicy
{
    FirstThenSecond,
    FirstOnly,
    RepeatSecond
}

[DisallowMultipleComponent]
public sealed class GmInteractable : MonoBehaviour
{
    [SerializeField] string interactionId;
    [SerializeField] string verb = "Examine";
    [SerializeField, Min(0.5f)] float range = 3.2f;
    [SerializeField, Range(0.5f, 12f)] float focusAngle = 5f;
    [SerializeField] GmInteractionRepeatPolicy repeatPolicy = GmInteractionRepeatPolicy.FirstThenSecond;
    [SerializeField, TextArea] string firstText;
    [SerializeField, TextArea] string secondText;
    [SerializeField] string soundResource;
    int uses;

    public string InteractionId => interactionId;
    public string Verb => verb;
    public float Range => range;
    public float FocusAngle => focusAngle;
    public string Prompt => string.IsNullOrWhiteSpace(verb) ? "INTERACT" : verb.ToUpperInvariant();
    public int Uses => uses;
    public bool HasContent => !string.IsNullOrWhiteSpace(firstText);

    public void Configure(string id, string actionVerb, float maximumRange, float maximumFocusAngle,
        GmInteractionRepeatPolicy policy = GmInteractionRepeatPolicy.FirstThenSecond,
        string audioResource = "")
    {
        interactionId = id;
        verb = string.IsNullOrWhiteSpace(actionVerb) ? "Examine" : actionVerb;
        range = Mathf.Max(0.5f, maximumRange);
        focusAngle = Mathf.Clamp(maximumFocusAngle, 0.5f, 12f);
        repeatPolicy = policy;
        soundResource = audioResource ?? "";
    }

    public void BindContent(string primary, string secondary)
    {
        firstText = primary ?? "";
        secondText = secondary ?? "";
    }

    public bool Interact(GmDesignRuntime runtime)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(interactionId) || !HasContent) return false;
        uses++;
        string text = firstText;
        if (repeatPolicy != GmInteractionRepeatPolicy.FirstOnly && uses > 1 &&
            !string.IsNullOrWhiteSpace(secondText))
            text = secondText;
        if (repeatPolicy == GmInteractionRepeatPolicy.RepeatSecond && uses > 2) uses = 2;
        if (!runtime.ShowExamine(interactionId, text)) return false;
        if (!string.IsNullOrWhiteSpace(soundResource))
            FindAnyObjectByType<GmAmbience>()?.PlayOneShot(soundResource, 0.18f);
        foreach (MonoBehaviour behaviour in GetComponentsInParent<MonoBehaviour>(true))
            if (behaviour is IGmInteractionReceiver receiver) receiver.OnGmInteraction(this);
        return true;
    }
}
