using System;
using UnityEngine;

public sealed class GmTheReadController : MonoBehaviour
{
    public float TimeDilationScale = 0.4f;
    public bool IsReadActive { get; private set; } = false;
    public bool TellWindowOpen { get; private set; } = false;

    GmHostAI hostAI;
    GmParlorRules rules;

    public event Action OnReadStarted;
    public event Action OnReadEnded;
    public event Action<bool> OnAccusationResolved; // true = successful catch

    void Awake()
    {
        hostAI = FindAnyObjectByType<GmHostAI>();
        rules = FindAnyObjectByType<GmParlorRules>();
    }

    public void BeginRead()
    {
        if (IsReadActive) return;
        // Fail closed. A missing rules reference must not hand out a Read that the Suspicion gate
        // would have refused; the same safe default the hostAI check in TriggerAccusation uses.
        if (rules == null || !rules.ReadEnabled) return;

        IsReadActive = true;
        Time.timeScale = TimeDilationScale;
        // The world dilates, so the mix goes under with it. GmAudioManager owns the listener filter.
        if (GmAudioManager.Instance != null) GmAudioManager.Instance.SetReadTimeDilationFilter(true);
        OnReadStarted?.Invoke();
        Debug.Log("[GmTheRead] Time dilated - The Read active.");
    }

    public void EndRead()
    {
        if (!IsReadActive) return;

        IsReadActive = false;
        Time.timeScale = 1.0f;
        if (GmAudioManager.Instance != null) GmAudioManager.Instance.SetReadTimeDilationFilter(false);
        OnReadEnded?.Invoke();
        Debug.Log("[GmTheRead] Time restored.");
    }

    public bool TriggerAccusation()
    {
        if (!IsReadActive) return false;

        bool wasCheated = hostAI != null && hostAI.LastPlayWasCheated;

        if (wasCheated)
        {
            // Successful catch!
            string clue = !string.IsNullOrEmpty(hostAI.LastCheatType) ? hostAI.LastCheatType : "parlor-aldric-cheat";
            GmRunStore.RecordCatch(clue);
            GmRunStore.ApplySanityDelta(GmFeelConfig.Active.parlorReadCatchSanityGain);
            Debug.Log($"[GmTheRead] SUCCESSFUL CATCH! Clue recorded: {clue}");
            OnAccusationResolved?.Invoke(true);
            EndRead();
            return true;
        }
        else
        {
            // False accusation penalty
            GmRunStore.RecordMiss();
            GmRunStore.RaiseCorruption("False accusation during honest trick");
            Debug.LogWarning("[GmTheRead] FALSE ACCUSATION: Aldric was playing legally!");
            OnAccusationResolved?.Invoke(false);
            EndRead();
            return false;
        }
    }
}
