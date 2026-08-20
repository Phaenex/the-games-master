using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class GmBonesAudio : MonoBehaviour
{
    GmBonesController controller;
    AudioSource source;
    AudioClip roll;
    int visibleThrowRevision;

    public int VisibleThrowRevision => visibleThrowRevision;

    public bool TryConfigure(GmBonesController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Bones audio needs an initialized controller";
            return false;
        }
        if (controller != null) controller.OnStateChanged -= HandleStateChanged;
        controller = tableController;
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        roll = Resources.Load<AudioClip>("Sfx/stb_bone_dice_roll");
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
        if (roll != null) source.PlayOneShot(roll);
    }

    void OnDestroy() { if (controller != null) controller.OnStateChanged -= HandleStateChanged; }
}
