using System;
using UnityEngine;

public enum GmHuntsmanState
{
    Patrolling,
    Alerted,
    Pursuing,
    TellWindowOpen,
    Stunned
}

public sealed class GmHuntsmanAI : MonoBehaviour
{
    public GmHuntsmanState State { get; private set; } = GmHuntsmanState.Patrolling;

    // Seeded in Awake, not by a field initializer: the distance comes from GmFeelConfig and Unity
    // refuses Resources.Load from a MonoBehaviour constructor, which is where an initializer runs.
    // Still settable, so a maze section can tighten the stalk without editing the shipped default.
    public float StalkDistance { get; set; }
    public bool TellFlickerActive { get; private set; } = false;

    public event Action<string> OnHuntsmanEvent;

    void Awake()
    {
        StalkDistance = GmFeelConfig.Active.huntsmanStalkDistanceMetres;
    }

    public void TriggerLanternTell()
    {
        State = GmHuntsmanState.TellWindowOpen;
        TellFlickerActive = true;
        OnHuntsmanEvent?.Invoke("Huntsman lantern tell flash!");
        Debug.Log("[GmHuntsmanAI] Huntsman lantern tell: 1-frame visual flash active!");
    }

    public bool EvadeOrDefend(bool hadMirror)
    {
        if (State != GmHuntsmanState.TellWindowOpen) return false;

        TellFlickerActive = false;
        if (hadMirror)
        {
            State = GmHuntsmanState.Stunned;
            GmRunStore.RecordCatch("labyrinth-huntsman-mirrored");
            GmRunStore.ApplySanityDelta(GmFeelConfig.Active.huntsmanMirrorSanityGain);
            OnHuntsmanEvent?.Invoke("Huntsman blinded by mirror reflection!");
            Debug.Log("[GmHuntsmanAI] Huntsman blinded by full mirror reflection!");
            return true;
        }
        else
        {
            State = GmHuntsmanState.Patrolling;
            GmRunStore.RecordCatch("labyrinth-huntsman-evaded");
            OnHuntsmanEvent?.Invoke("Huntsman charge evaded!");
            Debug.Log("[GmHuntsmanAI] Huntsman charge successfully dodged!");
            return true;
        }
    }
}
