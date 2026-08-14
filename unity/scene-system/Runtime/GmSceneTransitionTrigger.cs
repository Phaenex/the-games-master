using UnityEngine;

public sealed class GmSceneTransitionTrigger : MonoBehaviour
{
    public string TargetSceneId = "entry-hall";
    public string TargetScenePath = "Assets/Scenes/EntryHall.unity";
    public string InteractionPrompt = "Open double doors into the Parlor";

    public bool IsTriggered { get; private set; } = false;

    // Set when the trigger fired but no director existed to take it. IsTriggered is a one-shot and
    // OnTriggerEnter early-outs on it, so without this the transition is simply lost: walking back
    // through the volume after a director spawns would do nothing, forever. The hold is unbounded
    // on purpose -- a timeout here would be a pacing decision, and dropping the player's exit is
    // the worse failure.
    bool pending;

    public void TriggerTransition()
    {
        if (IsTriggered) return;
        IsTriggered = true;

        if (HandOff()) return;

        pending = true;
        Debug.Log($"[GmSceneTransitionTrigger] No GmSceneDirector yet — holding transition to {TargetSceneId} ({TargetScenePath})");
    }

    void Update()
    {
        if (!pending) return;
        if (HandOff()) pending = false;
    }

    bool HandOff()
    {
        if (GmSceneDirector.Instance == null) return false;
        GmSceneDirector.Instance.TransitionTo(TargetSceneId, TargetScenePath);
        return true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            TriggerTransition();
        }
    }
}
