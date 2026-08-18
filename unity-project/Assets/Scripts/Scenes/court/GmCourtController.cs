using System;
using UnityEngine;

public enum GmCourtPhase
{
    OpeningAddress,
    PlayerDefense,
    EvidencePresentation,
    Deliberation,
    Verdict
}

public sealed class GmCourtController : MonoBehaviour
{
    // Seals and clock are seeded in Awake, not by a field initializer: they come from GmFeelConfig
    // and Unity refuses Resources.Load from a MonoBehaviour constructor, which is where an
    // initializer runs.
    public int WaxSealsRemaining { get; private set; }
    public float PressureTimeRemaining { get; private set; }
    public bool GavelIsTarnished { get; private set; } = false;
    public bool ShardTwoCollected { get; private set; } = false;
    public GmCourtPhase Phase { get; private set; } = GmCourtPhase.OpeningAddress;

    public event Action OnStateChanged;
    public event Action<bool> OnEvidenceSubmitted; // true if true evidence, false if rigged
    public event Action OnHearingLost;
    public event Action OnHearingWon;

    void Awake()
    {
        ResetSealsAndClock();
    }

    void ResetSealsAndClock()
    {
        GmFeelConfig feel = GmFeelConfig.Active;
        WaxSealsRemaining = feel.courtWaxSeals;
        PressureTimeRemaining = feel.courtPressureSeconds;
    }

    public void StartHearing()
    {
        ResetSealsAndClock();
        GavelIsTarnished = false;
        Phase = GmCourtPhase.PlayerDefense;
        OnStateChanged?.Invoke();
    }

    public bool PresentEvidence(string evidenceId, bool isRigged)
    {
        if (Phase != GmCourtPhase.PlayerDefense && Phase != GmCourtPhase.EvidencePresentation)
            return false;

        if (isRigged)
        {
            // Rigged evidence tarnishes the gavel and raises suspicion
            GavelIsTarnished = true;
            GmRunStore.RecordCatch("court-rigged-evidence-tell");
            Debug.Log("[GmCourt] Rigged evidence submitted: Gavel tarnished!");
            OnEvidenceSubmitted?.Invoke(false);
        }
        else
        {
            // True evidence cracks a seal
            if (WaxSealsRemaining > 0)
            {
                WaxSealsRemaining--;
                Debug.Log($"[GmCourt] True evidence verified. Seals remaining: {WaxSealsRemaining}");
            }
            OnEvidenceSubmitted?.Invoke(true);
        }

        if (WaxSealsRemaining == 0)
        {
            Phase = GmCourtPhase.Verdict;
            GmRunStore.RecordCatch("court-verdict-cleared");
            GmRunStore.RecordDefiance();
            GmRunStore.CompleteRoom("court", countsAsTableGame: false);
            OnHearingWon?.Invoke();
        }

        OnStateChanged?.Invoke();
        return true;
    }

    public bool CollectEvidenceShard()
    {
        if (ShardTwoCollected) return false;
        ShardTwoCollected = true;
        GmRunStore.CollectShard(1); // Shard #2 (index 1)
        Debug.Log("[GmCourt] Mirror Shard #2 retrieved from evidence files!");
        OnStateChanged?.Invoke();
        return true;
    }

    void Update()
    {
        if (Phase == GmCourtPhase.PlayerDefense || Phase == GmCourtPhase.EvidencePresentation)
        {
            PressureTimeRemaining -= Time.deltaTime;
            if (PressureTimeRemaining <= 0f)
            {
                PressureTimeRemaining = 0f;
                Phase = GmCourtPhase.Verdict;
                GmRunStore.RecordMiss();
                GmRunStore.RaiseCorruption("Hearing lost: Time expired under pressure clock");
                GmRunStore.CompleteRoom("court", countsAsTableGame: false);
                OnHearingLost?.Invoke();
                OnStateChanged?.Invoke();
            }
        }
    }
}
