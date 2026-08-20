using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class GmBonesAudio : MonoBehaviour
{
    GmBonesController controller;
    GmAudioManager audioManager;
    int visibleThrowRevision;

    public int VisibleThrowRevision => visibleThrowRevision;
    public bool UsesSharedAudioManager => audioManager != null;

    public bool TryConfigure(GmBonesController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Bones audio needs an initialized controller";
            return false;
        }
        if (controller != null) controller.OnStateChanged -= HandleStateChanged;
        controller = tableController;
        audioManager = GmAudioManager.EnsureExists();
        if (audioManager == null)
        {
            error = "Bones audio could not acquire the shared audio manager";
            return false;
        }
        visibleThrowRevision = controller.PlayerDecisionCount;
        controller.OnStateChanged += HandleStateChanged;
        error = string.Empty;
        return true;
    }

    void HandleStateChanged()
    {
        int next = controller.PlayerDecisionCount;
        if (next <= visibleThrowRevision) return;
        visibleThrowRevision = next;
        audioManager.PlayDiceRoll();
    }

    void OnDestroy() { if (controller != null) controller.OnStateChanged -= HandleStateChanged; }
}
