using UnityEngine;

public sealed class GmStudyAudio : MonoBehaviour
{
    GmStudyController controller;
    GmAudioManager audioManager;
    int visibleMoveRevision;
    bool audioSuspended;

    public int VisibleMoveRevision => visibleMoveRevision;
    public bool UsesSharedAudioManager => audioManager != null;

    public bool TryConfigure(GmStudyController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Study audio needs an initialized controller";
            return false;
        }
        if (controller != null) controller.OnStateChanged -= HandleStateChanged;
        controller = tableController;
        audioManager = GmAudioManager.EnsureExists();
        if (audioManager == null)
        {
            error = "Study audio could not acquire the shared audio manager";
            return false;
        }
        visibleMoveRevision = controller.PlayerDecisionCount;
        if (!audioSuspended) controller.OnStateChanged += HandleStateChanged;
        error = string.Empty;
        return true;
    }

    void HandleStateChanged()
    {
        int next = controller.PlayerDecisionCount;
        if (next <= visibleMoveRevision) return;
        visibleMoveRevision = next;
        audioManager.PlayPieceMove();
    }

    public void SuspendAudio()
    {
        audioSuspended = true;
        if (controller != null) controller.OnStateChanged -= HandleStateChanged;
    }

    public void ResumeAudio()
    {
        audioSuspended = false;
        if (controller == null || audioManager == null) return;
        visibleMoveRevision = controller.PlayerDecisionCount;
        controller.OnStateChanged -= HandleStateChanged;
        controller.OnStateChanged += HandleStateChanged;
    }

    void OnDestroy() { if (controller != null) controller.OnStateChanged -= HandleStateChanged; }
}
