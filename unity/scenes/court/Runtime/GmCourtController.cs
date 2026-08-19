using System;
using System.Collections.Generic;
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
    public sealed class EvidenceCard
    {
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }

        public EvidenceCard(string id, string title, string body)
        {
            Id = id;
            Title = title;
            Body = body;
        }
    }

    static readonly EvidenceCard[] Deck =
    {
        new EvidenceCard("ledger", "Ledger extract",
            "Nine names. One blank line bears the grain of ink scraped away."),
        new EvidenceCard("wax", "Host's mark, wax seal V",
            "The invitation and three exhibits carry the same fault in the same seal."),
        new EvidenceCard("percival", "Guest register: Percival",
            "A second hand wrote kept his own counsel. No clerk used that hand."),
        new EvidenceCard("chandelier", "Chandelier inventory",
            "One fixture is entered twice: brass, then not brass. Do not polish."),
        new EvidenceCard("latch", "Door-latch schedule",
            "The outer gate was locked from inside. No key was assigned or listed."),
    };

    static readonly string[] Arguments =
    {
        "Whose record proves a guest was deliberately singled out?",
        "What ties the invitation to evidence filed years apart?",
        "What proves the house was never governed by an ordinary key?",
    };

    // Each argument has one answer. The old prototype had one globally true card and three seals,
    // making a clean win impossible once that single card was consumed.
    static readonly string[] CorrectEvidence = { "percival", "wax", "latch" };

    readonly HashSet<string> presentedEvidence = new HashSet<string>(StringComparer.Ordinal);
    readonly HashSet<string> attemptedEvidence = new HashSet<string>(StringComparer.Ordinal);
    GmPlayer player;
    // Seals and clock are seeded in Awake, not by a field initializer: they come from GmFeelConfig
    // and Unity refuses Resources.Load from a MonoBehaviour constructor, which is where an
    // initializer runs.
    public int WaxSealsRemaining { get; private set; }
    public float PressureTimeRemaining { get; private set; }
    public bool GavelIsTarnished { get; private set; } = false;
    public bool ShardTwoCollected { get; private set; } = false;
    public GmCourtPhase Phase { get; private set; } = GmCourtPhase.OpeningAddress;
    public int CurrentArgumentIndex { get; private set; }
    public int SelectedEvidenceIndex { get; private set; }
    public int Revision { get; private set; }
    public string Feedback { get; private set; } = "The hearing has not begun.";
    public IReadOnlyList<EvidenceCard> EvidenceDeck => Deck;
    public string CurrentArgument => Arguments[Mathf.Clamp(CurrentArgumentIndex, 0, Arguments.Length - 1)];
    public bool HearingResolved => Phase == GmCourtPhase.Verdict;

    public event Action OnStateChanged;
    public event Action<bool> OnEvidenceSubmitted; // true if true evidence, false if rigged
    public event Action OnHearingLost;
    public event Action OnHearingWon;

    void Awake()
    {
        ResetSealsAndClock();
    }

    void Start()
    {
        EnsurePresentationComponents();
        if (Phase == GmCourtPhase.OpeningAddress) StartHearing();
        GmAudioManager.EnsureExists();
    }

    public void EnsurePresentationComponents()
    {
        if (GetComponent<GmCourtHud>() == null) gameObject.AddComponent<GmCourtHud>();
        if (GetComponent<GmCourtInput>() == null) gameObject.AddComponent<GmCourtInput>();
        if (GetComponent<GmCourtPresenter>() == null) gameObject.AddComponent<GmCourtPresenter>();
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
        ShardTwoCollected = GmRunStore.MirrorShards[1];
        CurrentArgumentIndex = 0;
        SelectedEvidenceIndex = 0;
        presentedEvidence.Clear();
        attemptedEvidence.Clear();
        Feedback = "Three arguments. Five exhibits. The clock is already moving.";
        Phase = GmCourtPhase.PlayerDefense;
        Changed();
    }

    public bool IsEvidencePresented(string evidenceId) =>
        !string.IsNullOrEmpty(evidenceId) && presentedEvidence.Contains(evidenceId);

    public bool IsEvidenceUnavailableForCurrentArgument(string evidenceId) =>
        IsEvidencePresented(evidenceId) || attemptedEvidence.Contains(AttemptKey(evidenceId));

    public bool SelectEvidence(int index)
    {
        if (HearingResolved || index < 0 || index >= Deck.Length) return false;
        SelectedEvidenceIndex = index;
        Feedback = Deck[index].Body;
        Changed();
        return true;
    }

    public bool MoveSelection(int direction)
    {
        if (HearingResolved || direction == 0) return false;
        int next = (SelectedEvidenceIndex + Math.Sign(direction) + Deck.Length) % Deck.Length;
        return SelectEvidence(next);
    }

    public bool PresentSelectedEvidence()
    {
        if (HearingResolved) return false;
        EvidenceCard card = Deck[SelectedEvidenceIndex];
        if (!attemptedEvidence.Add(AttemptKey(card.Id)))
        {
            Feedback = "That exhibit has already been tested against this argument.";
            Changed();
            return false;
        }

        bool correct = string.Equals(card.Id, CorrectEvidence[CurrentArgumentIndex],
            StringComparison.Ordinal);
        if (!correct)
        {
            PressureTimeRemaining = Mathf.Max(0f, PressureTimeRemaining - 12f);
            GmRunStore.RecordMiss();
            GmRunStore.ApplySanityDelta(-0.06f);
            Feedback = "Marked for the record. The seal does not move. Twelve seconds are gone.";
            Changed();
            return false;
        }

        presentedEvidence.Add(card.Id);

        // Aldric only risks the rig on the last seal, when the hearing is genuinely about to be
        // lost, and only once the run has enough corruption to authorize the behaviour.
        bool reactiveRig = WaxSealsRemaining == 1 && GmRunStore.CorruptionTier >= 3;
        bool accepted = PresentEvidence(card.Id, reactiveRig);
        if (accepted && !HearingResolved)
        {
            CurrentArgumentIndex = Mathf.Min(CurrentArgumentIndex + 1, Arguments.Length - 1);
            Feedback = reactiveRig
                ? "The gavel's gold goes dull. You saw the rig, and the argument still stands."
                : "The wax splits. The argument stands.";
            Changed();
        }
        return accepted;
    }

    string AttemptKey(string evidenceId) => $"{CurrentArgumentIndex}:{evidenceId}";

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

        // A rigged hearing changes the tell, not whether correct evidence is correct. The previous
        // branch tarnished the gavel but refused to crack the final seal, soft-locking a player who
        // caught the cheat at exactly the intended moment.
        if (WaxSealsRemaining > 0)
        {
            WaxSealsRemaining--;
            Debug.Log($"[GmCourt] True evidence verified. Seals remaining: {WaxSealsRemaining}");
        }
        if (!isRigged) OnEvidenceSubmitted?.Invoke(true);
        GmAudioManager.Instance?.PlayGavelStrike();

        if (WaxSealsRemaining == 0)
        {
            Phase = GmCourtPhase.Verdict;
            GmRunStore.RecordCatch("court-verdict-cleared");
            GmRunStore.RecordDefiance();
            GmRunStore.CompleteRoom("court", countsAsTableGame: false);
            OnHearingWon?.Invoke();
            Feedback = "The third seal breaks. The verdict passage releases behind you.";
        }

        Changed();
        return true;
    }

    public bool CollectEvidenceShard()
    {
        if (ShardTwoCollected) return false;
        ShardTwoCollected = true;
        GmRunStore.CollectShard(1); // Shard #2 (index 1)
        Debug.Log("[GmCourt] Mirror Shard #2 retrieved from evidence files!");
        Feedback = "The mislabeled glass is not an exhibit. It is a piece of the same broken mirror.";
        Changed();
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
                Feedback = "Time. The argument is entered against you, but the night continues.";
                Changed();
            }
        }
    }

    void Changed()
    {
        SynchronizePlayerControl();
        Revision++;
        OnStateChanged?.Invoke();
    }

    public void SynchronizePlayerControl()
    {
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (player == null) return;
        bool hearingOwnsInput = Phase == GmCourtPhase.PlayerDefense ||
            Phase == GmCourtPhase.EvidencePresentation;
        if (player.ControlBlocked != hearingOwnsInput)
            player.SetControlBlocked(hearingOwnsInput);
    }
}
