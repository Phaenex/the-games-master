using UnityEngine;

/// <summary>
/// The physical end of the built run. Resolves accumulated state once, holds the final frame under
/// the persistent curtain, and blocks player control so walking in circles cannot resolve again.
/// Full authored ending sequences remain separate content; this is the honest playable spine.
/// </summary>
public sealed class GmEndingTrigger : MonoBehaviour
{
    public bool IsTriggered { get; private set; }
    public GmEndingType ResolvedEnding { get; private set; } = GmEndingType.TrappedLoop;

    public GmEndingType TriggerEnding(GmPlayer player)
    {
        if (IsTriggered) return ResolvedEnding;

        GmRunStore.CompleteRoom("labyrinth", countsAsTableGame: false);
        GmRunStore.LastCheckpoint = "ending";
        ResolvedEnding = GmSceneDirector.Instance != null
            ? GmSceneDirector.Instance.ResolveAndShowEnding()
            : GmEndingManager.ResolveEnding();

        if (Application.isPlaying)
        {
            GmParlorMatchSnapshot parlor = GmRunStore.GetParlorMatchSnapshot();
            if (GmHousePersistenceCoordinator.ActiveRun != null)
            {
                string houseError = "sealed Parlor accumulator missing";
                if (parlor?.behaviorAccumulator == null ||
                    !GmHousePersistenceCoordinator.TryCompleteEnding(ResolvedEnding,
                        parlor.behaviorAccumulator, out houseError))
                {
                    Debug.LogError($"[GmEndingTrigger] House receipt was not acknowledged: {houseError}");
                    return ResolvedEnding;
                }
            }
            if (!GmSaveSystem.SaveGame())
            {
                Debug.LogError($"[GmEndingTrigger] terminal Continue checkpoint failed: {GmSaveSystem.LastError}");
            }
        }

        IsTriggered = true;
        if (player != null) player.SetControlBlocked(true);
        GmSceneCurtain curtain = GmSceneCurtain.Instance;
        if (curtain == null)
            curtain = FindAnyObjectByType<GmSceneCurtain>(FindObjectsInactive.Include);
        // A direct Labyrinth development start has no Boot-owned persistent curtain. Ending without
        // a visible result is worse than creating a local final-frame surface, and no later load has
        // to survive once the run is over.
        if (curtain == null) curtain = gameObject.AddComponent<GmSceneCurtain>();
        curtain.Raise();
        curtain.ShowCard(GmEndingManager.GetEndingTitle(ResolvedEnding));

        Debug.Log($"[GmEndingTrigger] RUN COMPLETE -> {GmEndingManager.GetEndingTitle(ResolvedEnding)}");
        return ResolvedEnding;
    }

    void OnTriggerEnter(Collider other)
    {
        GmPlayer player = other.GetComponentInParent<GmPlayer>();
        if (player == null) return;
        TriggerEnding(player);
    }
}
