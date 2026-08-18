using UnityEngine;

public sealed class GmSceneTransitionTrigger : MonoBehaviour
{
    public string TargetSceneId = "entry-hall";
    public string TargetScenePath = "Assets/Scenes/EntryHall.unity";
    public string InteractionPrompt = "Open double doors into the Parlor";
    public bool UseCurtain = false;
    public string CompleteRoomOnTransitionId = "";
    public bool CompletedRoomCountsAsTableGame = false;
    public string RequiredCompletedRoomId = "";

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
        if (!string.IsNullOrWhiteSpace(RequiredCompletedRoomId) &&
            !GmRunStore.IsRoomComplete(RequiredCompletedRoomId))
        {
            Debug.Log($"[GmSceneTransitionTrigger] Refusing {TargetSceneId} until " +
                      $"{RequiredCompletedRoomId} is complete");
            return;
        }
        IsTriggered = true;

        if (!string.IsNullOrWhiteSpace(CompleteRoomOnTransitionId))
            GmRunStore.CompleteRoom(CompleteRoomOnTransitionId, CompletedRoomCountsAsTableGame);

        if (UseCurtain && GmSceneCurtain.Instance != null)
            GmSceneCurtain.Instance.Raise();

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

    /// Identified by component, not by tag.
    ///
    /// This used to read CompareTag("Player") and NOTHING in the project ever set that tag --
    /// GmPlayerRig tags the camera MainCamera and leaves the body untagged. So the only way this
    /// component could ever fire was a test calling TriggerTransition() directly, which every test
    /// did. Written, covered, and dead on the one path a human uses.
    ///
    /// A tag is a string that silently is not there. A component is the identity that actually
    /// matters here, and GetComponentInParent finds it whether the collider hit is the body's own
    /// CharacterController or a child.
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<GmPlayer>() == null) return;
        TriggerTransition();
    }
}
