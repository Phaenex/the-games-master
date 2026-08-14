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

/// Outcome of the player calling a tell. Unavailable and AlreadyFound are deliberately distinct from
/// False: neither should cost the player anything, because neither is a wrong read -- one is a
/// mis-timed press and the other is re-reading something already caught.
public enum GmTellCall
{
    Unavailable,
    Caught,
    False,
    AlreadyFound,
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

    // The tell: what is WRONG about this object, and whether anything is.
    //
    // This used to live in secondText and was handed over for free on a second Examine, which meant
    // the narrator did the catching in a game whose entire core loop is the player catching a cheat.
    // Eleven objects on the grounds carry a discrepancy shaped exactly like a caught cheat -- coins
    // all heads-down, one mason's hand on stones a century apart, small boots going to the shed and
    // none coming back -- and the player's only involvement was pressing Examine twice.
    //
    // Now Examine gives the observation and nothing more. The tell is the reward for the player
    // saying "that is wrong" themselves.
    [SerializeField, TextArea] string tellText;
    [SerializeField] bool carriesTell;
    bool tellFound;

    public bool CarriesTell => carriesTell && !string.IsNullOrWhiteSpace(tellText);
    public bool TellFound => tellFound;
    /// True once examined: a player cannot call a tell on something they have not looked at.
    public bool Examined => uses > 0;

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

    /// Binds the discrepancy as something the PLAYER has to call, not something the narrator says.
    public void BindTell(string tell)
    {
        tellText = tell ?? "";
        carriesTell = !string.IsNullOrWhiteSpace(tellText);
    }

    /// The core verb. Returns the outcome so the caller can score it -- catching a real tell is the
    /// whole game, and calling one on an innocent object has to cost something or the correct play
    /// is to shout at everything.
    public GmTellCall CallTell(GmDesignRuntime runtime)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(interactionId)) return GmTellCall.Unavailable;
        // Looking comes before calling. Without this, a player could sweep the grounds calling tells
        // on objects they never examined and the verb would be a lottery rather than an observation.
        if (!Examined) return GmTellCall.Unavailable;
        if (!CarriesTell)
        {
            runtime.ShowExamine(interactionId + "-false", "I looked again. There is nothing wrong with it. I want there to be.");
            return GmTellCall.False;
        }
        if (tellFound) return GmTellCall.AlreadyFound;
        tellFound = true;
        runtime.ShowExamine(interactionId + "-tell", tellText);
        return GmTellCall.Caught;
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
