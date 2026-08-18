using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public enum GmParlorMatchPhase
{
    NotStarted,
    PlayerLeads,
    PlayerFollowsAldricLead,
    AwaitingAldricJudgement,
    TrickResult,
    RoundResult,
    MatchResult,
}

public enum GmParlorActionError
{
    None,
    WrongPhase,
    InvalidCardIndex,
    MustFollowSuit,
    ReadLocked,
    ScriptedTestRequiresVulnerableLead,
    PersistenceFailed,
    OutcomeHandlerFailed,
}

public enum GmReadTestState
{
    None,
    Pending,
    Active,
    Completed,
}

public enum GmParlorOutcomeKind
{
    None,
    HonestAccepted,
    CheatMissed,
    CheatCaught,
    FalseReadPenalty,
}

public enum GmTellObservation
{
    Calm,
    Suspicious,
}

[Serializable]
public struct GmParlorOutcome
{
    public GmParlorOutcomeKind Kind;
    public int CatchDelta;
    public int CorruptionDelta;
    public int SuspicionDelta;
    public int SanityDelta;
    public int DefianceDelta;
    public int ComplianceDelta;
}

/// <summary>
/// Pure deterministic authority for one complete Parlor match. It owns every deal and transition,
/// while scene components translate player intent and apply the exposed outcome deltas elsewhere.
/// </summary>
public sealed class GmParlorMatch
{
    public const int TricksToWinRound = 4;
    public const int RoundsToWinMatch = 2;
    public const double PreReadCheatProbability = 0.55d;

    const int PreReadCheatThreshold = 5500;
    static readonly int[] ReadCheatThreshold = { 0, 2800, 5000, 6800, 8200 };
    static readonly int[] TellReliabilityThreshold = { 0, 5800, 7000, 8400, 9500 };

    public static double GetPostReadCheatProbability(int corruptionTier)
    {
        ValidateTier(corruptionTier);
        return ReadCheatThreshold[corruptionTier] / 10000d;
    }

    public static double GetTellReliability(int corruptionTier)
    {
        ValidateTier(corruptionTier);
        return TellReliabilityThreshold[corruptionTier] / 10000d;
    }

    public static bool PassesBasisPointThreshold(int roll, int threshold)
    {
        if (roll < 0 || roll >= 10000) throw new ArgumentOutOfRangeException(nameof(roll));
        if (threshold < 0 || threshold > 10000)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return roll < threshold;
    }

    public static bool TryMapNonZeroRandomToIndex(uint nonZeroValue, int bound, out int index)
    {
        if (nonZeroValue == 0) throw new ArgumentOutOfRangeException(nameof(nonZeroValue));
        if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound));

        const ulong domainSize = uint.MaxValue;
        ulong unsignedBound = (uint)bound;
        ulong acceptedCount = domainSize - domainSize % unsignedBound;
        ulong zeroBasedValue = (ulong)nonZeroValue - 1;
        if (zeroBasedValue >= acceptedCount)
        {
            index = -1;
            return false;
        }

        index = (int)(zeroBasedValue % unsignedBound);
        return true;
    }

    int seed;
    uint randomState;

    readonly List<GmCard> playerHand = new List<GmCard>();
    readonly List<GmCard> aldricHand = new List<GmCard>();
    readonly List<GmCard> revealedAldricCards = new List<GmCard>();
    readonly ReadOnlyCollection<GmCard> playerHandView;
    readonly ReadOnlyCollection<GmCard> aldricHandView;
    readonly ReadOnlyCollection<GmCard> revealedAldricCardsView;
    GmParlorAdaptivePackage adaptivePackage;
    GmParlorBehaviorAccumulator behaviorAccumulator;

    public IReadOnlyList<GmCard> PlayerHand => playerHandView;
    public IReadOnlyList<GmCard> AldricHand => aldricHandView;
    public IReadOnlyList<GmCard> RevealedAldricCards => revealedAldricCardsView;
    public GmParlorAdaptivePackage AdaptivePackage => adaptivePackage.DeepCopy();
    public GmParlorBehaviorAccumulator BehaviorAccumulator => behaviorAccumulator.DeepCopy();
    public byte[] ReplayHash => ExportSnapshot().ReplayHash;

    public GmParlorMatchPhase Phase { get; private set; } = GmParlorMatchPhase.NotStarted;
    public GmReadTestState ReadTestState { get; private set; }
    GmParlorOutcome lastOutcome;
    bool outcomePending;
    public GmParlorOutcome LastOutcome => outcomePending ? lastOutcome : default;
    public ulong OutcomeSequence { get; private set; }
    public ulong HighestDurableOutcomeSequence { get; private set; }
    public ulong HighestAcknowledgedOutcomeSequence { get; private set; }
    public GmTrickOwner LastTrickWinner { get; private set; }
    public GmTrickOwner EffectiveWinner { get; private set; }
    public GmTrickOwner RoundWinner { get; private set; }
    public GmTrickOwner MatchWinner { get; private set; }

    public int CorruptionTier { get; private set; }
    public int PriorCatchCount { get; private set; }
    public int CatchesThisMatch { get; private set; }
    public int Suspicion { get; private set; }
    public int Sanity { get; private set; } = 80;
    public int Defiance { get; private set; }
    public int Compliance { get; private set; }
    public int RoundNumber { get; private set; }
    public int PlayerRounds { get; private set; }
    public int AldricRounds { get; private set; }
    public int PlayerTricks { get; private set; }
    public int AldricTricks { get; private set; }
    public int TrickNumber { get; private set; }

    public bool ReadUnlocked { get; private set; }
    public bool AldricCheated { get; private set; }
    public GmTellObservation TellObservation { get; private set; }
    public bool TellShown => TellObservation == GmTellObservation.Suspicious;
    public bool EyesExposeAldricHand { get; private set; }
    public bool BonesReturnedThisRound { get; private set; }
    public GmParlorCheatKind AldricCheatKind { get; private set; }
    public string AuthoritativeCheatTell { get; private set; } = string.Empty;
    public int AldricPaidIndex { get; private set; } = -1;
    public GmCard? AldricPaidCard { get; private set; }
    public GmCard? CurrentLeadCard { get; private set; }
    public GmCard? CurrentFollowCard { get; private set; }
    public bool AldricLeadsCurrentTrick => Phase == GmParlorMatchPhase.PlayerFollowsAldricLead ||
        (CurrentLeadCard.HasValue && LastLeadWasAldric);
    public uint RandomState => randomState;
    public bool ReadTestDeferred => Phase == GmParlorMatchPhase.MatchResult && !ReadUnlocked &&
        (ReadTestState == GmReadTestState.Pending || ReadTestState == GmReadTestState.Active);
    public bool RematchRequiredForReadTest => ReadTestDeferred;

    bool LastLeadWasAldric { get; set; }

    public GmParlorMatch(int seed, int corruptionTier, int priorCatchCount,
        bool readInitiallyUnlocked, ulong initialOutcomeSequence = 0)
        : this(seed, corruptionTier, priorCatchCount, readInitiallyUnlocked,
            null, initialOutcomeSequence)
    {
    }

    public GmParlorMatch(int seed, int corruptionTier, int priorCatchCount,
        bool readInitiallyUnlocked, GmParlorAdaptivePackage adaptivePackage,
        ulong initialOutcomeSequence = 0)
    {
        this.seed = seed;
        playerHandView = playerHand.AsReadOnly();
        aldricHandView = aldricHand.AsReadOnly();
        revealedAldricCardsView = revealedAldricCards.AsReadOnly();
        randomState = SeedState(seed);
        CorruptionTier = Clamp(corruptionTier, 1, 4);
        PriorCatchCount = Math.Max(0, priorCatchCount);
        ReadUnlocked = readInitiallyUnlocked;
        ReadTestState = readInitiallyUnlocked ? GmReadTestState.Completed : GmReadTestState.None;
        OutcomeSequence = initialOutcomeSequence;
        HighestDurableOutcomeSequence = initialOutcomeSequence;
        HighestAcknowledgedOutcomeSequence = initialOutcomeSequence;
        this.adaptivePackage = adaptivePackage?.DeepCopy() ??
            GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary);
        if (!this.adaptivePackage.TryValidate(out string packageError))
            throw new ArgumentException(packageError, nameof(adaptivePackage));
        behaviorAccumulator = new GmParlorBehaviorAccumulator(this.adaptivePackage);
    }

    public GmParlorActionError Start()
    {
        if (Phase != GmParlorMatchPhase.NotStarted) return GmParlorActionError.WrongPhase;
        RoundNumber = 1;
        DealRound();
        return GmParlorActionError.None;
    }

    public GmParlorActionError StartRematch()
    {
        if (Phase != GmParlorMatchPhase.MatchResult) return GmParlorActionError.WrongPhase;
        if (!behaviorAccumulator.currentMatchSealed) return GmParlorActionError.WrongPhase;
        behaviorAccumulator.BeginRematch();
        PlayerRounds = 0;
        AldricRounds = 0;
        RoundWinner = default;
        MatchWinner = default;
        RoundNumber = 1;
        if (!ReadUnlocked && Suspicion >= 2) ReadTestState = GmReadTestState.Pending;
        ClearPendingOutcome();
        DealRound();
        return GmParlorActionError.None;
    }

    public GmParlorActionError PlayPlayerCard(int index)
    {
        GmParlorActionError validation = GetPlayerCardError(index);
        if (validation != GmParlorActionError.None) return validation;

        GmCard card = PlayerHand[index];
        playerHand.RemoveAt(index);
        if (Phase == GmParlorMatchPhase.PlayerLeads)
        {
            behaviorAccumulator.RecordPlayerLead(card, TrickNumber,
                GmParlorCore.HandSize);
            LastLeadWasAldric = false;
            CurrentLeadCard = card;
            EyesExposeAldricHand = card.Suit == GmSuit.Eyes;
            PlayAldricFollow(ReadTestState == GmReadTestState.Active);
            Phase = GmParlorMatchPhase.AwaitingAldricJudgement;
        }
        else
        {
            behaviorAccumulator.RecordPlayerFollow(card);
            CurrentFollowCard = card;
            EffectiveWinner = GmParlorCore.LeadWins(CurrentLeadCard.Value, card)
                ? GmTrickOwner.Aldric : GmTrickOwner.Player;
            ResolveTrick(EffectiveWinner,
                new GmParlorOutcome { Kind = GmParlorOutcomeKind.HonestAccepted });
        }
        return GmParlorActionError.None;
    }

    public GmParlorActionError GetPlayerCardError(int index)
    {
        if (Phase != GmParlorMatchPhase.PlayerLeads &&
            Phase != GmParlorMatchPhase.PlayerFollowsAldricLead)
            return GmParlorActionError.WrongPhase;
        if (index < 0 || index >= PlayerHand.Count) return GmParlorActionError.InvalidCardIndex;
        if (!GmParlorCore.IsLegal(PlayerHand, index, CurrentLeadCard))
            return GmParlorActionError.MustFollowSuit;

        GmCard card = PlayerHand[index];
        if (Phase == GmParlorMatchPhase.PlayerLeads && ReadTestState == GmReadTestState.Active &&
            HasLegalAldricWinner(card))
            return GmParlorActionError.ScriptedTestRequiresVulnerableLead;
        return GmParlorActionError.None;
    }

    public GmParlorActionError ContinueJudgement()
    {
        if (Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
            return GmParlorActionError.WrongPhase;

        GmTellObservation committedObservation = TellObservation;
        GmParlorOutcome outcome;
        if (AldricCheated)
        {
            int oldSuspicion = Suspicion;
            int oldCorruption = CorruptionTier;
            int oldSanity = Sanity;
            int suspicionDelta = ReadUnlocked ? 0 : 1;
            Suspicion += suspicionDelta;
            CorruptionTier = Clamp(CorruptionTier + 1, 1, 4);
            Sanity = Clamp(Sanity - 3, 0, 100);
            if (!ReadUnlocked && Suspicion >= 2 && ReadTestState == GmReadTestState.None)
                ReadTestState = GmReadTestState.Pending;
            else if (!ReadUnlocked && ReadTestState == GmReadTestState.Active)
                ReadTestState = GmReadTestState.Pending;
            outcome = new GmParlorOutcome
            {
                Kind = GmParlorOutcomeKind.CheatMissed,
                CorruptionDelta = CorruptionTier - oldCorruption,
                SuspicionDelta = Suspicion - oldSuspicion,
                SanityDelta = Sanity - oldSanity,
            };
        }
        else
        {
            outcome = new GmParlorOutcome { Kind = GmParlorOutcomeKind.HonestAccepted };
        }

        ResolveTrick(EffectiveWinner, outcome);
        behaviorAccumulator.RecordAccept(committedObservation);
        return GmParlorActionError.None;
    }

    public GmParlorActionError Read()
    {
        if (Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
            return GmParlorActionError.WrongPhase;
        if (!ReadUnlocked && ReadTestState != GmReadTestState.Active)
            return GmParlorActionError.ReadLocked;

        GmTellObservation committedObservation = TellObservation;
        GmParlorOutcome committedOutcome;
        if (AldricCheated)
        {
            int oldSanity = Sanity;
            int oldDefiance = Defiance;
            CatchesThisMatch++;
            Sanity = Clamp(Sanity + 3, 0, 100);
            Defiance = Clamp(Defiance + 2, 0, 20);
            if (ReadTestState == GmReadTestState.Active)
            {
                ReadUnlocked = true;
                ReadTestState = GmReadTestState.Completed;
            }
            committedOutcome = new GmParlorOutcome
            {
                Kind = GmParlorOutcomeKind.CheatCaught,
                CatchDelta = 1,
                SanityDelta = Sanity - oldSanity,
                DefianceDelta = Defiance - oldDefiance,
            };
            ResolveTrick(GmTrickOwner.Player, committedOutcome);
        }
        else
        {
            int oldSanity = Sanity;
            int oldCompliance = Compliance;
            Sanity = Clamp(Sanity - 6, 0, 100);
            Compliance = Clamp(Compliance + 1, 0, 20);
            committedOutcome = new GmParlorOutcome
            {
                Kind = GmParlorOutcomeKind.FalseReadPenalty,
                SanityDelta = Sanity - oldSanity,
                ComplianceDelta = Compliance - oldCompliance,
            };
            ResolveTrick(EffectiveWinner, committedOutcome);
        }
        behaviorAccumulator.RecordRead(committedObservation, committedOutcome.Kind);
        return GmParlorActionError.None;
    }

    public GmParlorActionError Continue()
    {
        if (Phase == GmParlorMatchPhase.TrickResult)
        {
            ClearPendingOutcome();
            if (PlayerTricks >= TricksToWinRound || AldricTricks >= TricksToWinRound ||
                PlayerHand.Count == 0 || AldricHand.Count == 0)
            {
                FinishRound();
                return GmParlorActionError.None;
            }

            ClearTable();
            if (LastTrickWinner == GmTrickOwner.Player) BeginPlayerLead();
            else BeginAldricLead();
            return GmParlorActionError.None;
        }

        if (Phase == GmParlorMatchPhase.RoundResult)
        {
            if (PlayerRounds >= RoundsToWinMatch || AldricRounds >= RoundsToWinMatch)
            {
                MatchWinner = PlayerRounds >= RoundsToWinMatch
                    ? GmTrickOwner.Player : GmTrickOwner.Aldric;
                Phase = GmParlorMatchPhase.MatchResult;
                behaviorAccumulator.SealCompletedMatch(
                    adaptivePackage, behaviorAccumulator.matchOrdinal, 0);
                return GmParlorActionError.None;
            }

            RoundNumber++;
            DealRound();
            return GmParlorActionError.None;
        }

        return GmParlorActionError.WrongPhase;
    }

    void DealRound()
    {
        var deck = new List<GmCard>(GmParlorCore.TotalCards);
        GmSuit[] suits = { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones };
        foreach (GmSuit suit in suits)
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
                deck.Add(new GmCard(suit, rank));
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int swap = NextInt(i + 1);
            GmCard held = deck[i];
            deck[i] = deck[swap];
            deck[swap] = held;
        }

        playerHand.Clear();
        aldricHand.Clear();
        for (int i = 0; i < GmParlorCore.HandSize; i++) playerHand.Add(deck[i]);
        for (int i = 0; i < GmParlorCore.HandSize; i++) aldricHand.Add(deck[i + GmParlorCore.HandSize]);
        revealedAldricCards.Clear();
        PlayerTricks = 0;
        AldricTricks = 0;
        TrickNumber = 1;
        BonesReturnedThisRound = false;
        EyesExposeAldricHand = false;
        ClearTable();
        BeginPlayerLead();
    }

    void BeginPlayerLead()
    {
        LastLeadWasAldric = false;
        if (ReadTestState == GmReadTestState.Pending && HasVulnerablePlayerLead())
            ReadTestState = GmReadTestState.Active;
        Phase = GmParlorMatchPhase.PlayerLeads;
    }

    void BeginAldricLead()
    {
        int index = GmParlorCore.ChooseAldricLead(AldricHand,
            adaptivePackage.honestStrategyId);
        if (index < 0)
        {
            FinishRound();
            return;
        }
        LastLeadWasAldric = true;
        CurrentLeadCard = RemoveAldricCardAt(index);
        AldricPaidCard = CurrentLeadCard;
        AldricCheated = false;
        AldricCheatKind = GmParlorCheatKind.None;
        AuthoritativeCheatTell = string.Empty;
        AldricPaidIndex = index;
        TellObservation = GmTellObservation.Calm;
        Phase = GmParlorMatchPhase.PlayerFollowsAldricLead;
    }

    void PlayAldricFollow(bool scriptedTest)
    {
        int cheatThreshold = ReadUnlocked
            ? ReadCheatThreshold[CorruptionTier] : PreReadCheatThreshold;
        int cheatRoll = NextInt(10000);
        bool allowCheat = scriptedTest || PassesBasisPointThreshold(cheatRoll, cheatThreshold);
        GmAldricPlay play = GmParlorCore.ChooseAldricFollow(
            AldricHand, CurrentLeadCard.Value, allowCheat,
            adaptivePackage.honestStrategyId);
        AldricPaidIndex = play.RemovedIndex;
        AldricPaidCard = RemoveAldricCardAt(play.RemovedIndex);
        CurrentFollowCard = play.Card;
        AldricCheated = play.Cheated;
        AldricCheatKind = play.CheatKind;
        AuthoritativeCheatTell = play.Cheated ? play.Tell ?? string.Empty : string.Empty;
        EffectiveWinner = play.Cheated || !GmParlorCore.LeadWins(CurrentLeadCard.Value, play.Card)
            ? GmTrickOwner.Aldric : GmTrickOwner.Player;
        int tellRoll = NextInt(10000);
        bool tellCorrect = scriptedTest || PassesBasisPointThreshold(
            tellRoll, TellReliabilityThreshold[CorruptionTier]);
        bool suspicious = tellCorrect ? AldricCheated : !AldricCheated;
        TellObservation = suspicious ? GmTellObservation.Suspicious : GmTellObservation.Calm;
    }

    void ResolveTrick(GmTrickOwner winner, GmParlorOutcome outcome)
    {
        LastTrickWinner = winner;
        lastOutcome = outcome;
        outcomePending = true;
        OutcomeSequence++;
        GmCard playerCard = LastLeadWasAldric ? CurrentFollowCard.Value : CurrentLeadCard.Value;
        if (winner == GmTrickOwner.Player) PlayerTricks++;
        else AldricTricks++;

        if (winner == GmTrickOwner.Aldric && playerCard.Suit == GmSuit.Bones &&
            !BonesReturnedThisRound)
        {
            playerHand.Add(playerCard);
            BonesReturnedThisRound = true;
        }

        if (winner == GmTrickOwner.Player && playerCard.Suit == GmSuit.Teeth && AldricHand.Count > 0)
        {
            GmCard revealed = AldricHand[NextInt(AldricHand.Count)];
            if (!revealedAldricCards.Contains(revealed)) revealedAldricCards.Add(revealed);
        }

        EyesExposeAldricHand = false;
        Phase = GmParlorMatchPhase.TrickResult;
        TrickNumber++;
    }

    void FinishRound()
    {
        RoundWinner = PlayerTricks >= TricksToWinRound
            ? GmTrickOwner.Player : GmTrickOwner.Aldric;
        if (RoundWinner == GmTrickOwner.Player) PlayerRounds++;
        else AldricRounds++;
        Phase = GmParlorMatchPhase.RoundResult;
    }

    void ClearTable()
    {
        CurrentLeadCard = null;
        CurrentFollowCard = null;
        AldricCheated = false;
        TellObservation = GmTellObservation.Calm;
        AldricCheatKind = GmParlorCheatKind.None;
        AuthoritativeCheatTell = string.Empty;
        AldricPaidIndex = -1;
        AldricPaidCard = null;
        EyesExposeAldricHand = false;
    }

    bool HasLegalAldricWinner(GmCard lead)
    {
        for (int i = 0; i < AldricHand.Count; i++)
            if (GmParlorCore.IsLegal(AldricHand, i, lead) &&
                !GmParlorCore.LeadWins(lead, AldricHand[i])) return true;
        return false;
    }

    bool HasVulnerablePlayerLead()
    {
        for (int i = 0; i < PlayerHand.Count; i++)
            if (!HasLegalAldricWinner(PlayerHand[i])) return true;
        return false;
    }

    GmCard RemoveAldricCardAt(int index)
    {
        GmCard removed = aldricHand[index];
        aldricHand.RemoveAt(index);
        revealedAldricCards.Remove(removed);
        return removed;
    }

    public bool TryConsumeOutcome(out GmParlorOutcome outcome, out ulong sequence)
    {
        if (!outcomePending)
        {
            outcome = default;
            sequence = OutcomeSequence;
            return false;
        }
        outcome = lastOutcome;
        sequence = OutcomeSequence;
        HighestDurableOutcomeSequence = Math.Max(HighestDurableOutcomeSequence, sequence);
        AcknowledgeOutcome(sequence);
        return true;
    }

    public bool TryPeekOutcome(out GmParlorOutcome outcome, out ulong sequence)
    {
        outcome = outcomePending ? lastOutcome : default;
        sequence = OutcomeSequence;
        return outcomePending;
    }

    public bool MarkOutcomeDurable(ulong sequence)
    {
        if (!outcomePending || sequence != OutcomeSequence) return false;
        HighestDurableOutcomeSequence = Math.Max(HighestDurableOutcomeSequence, sequence);
        return true;
    }

    public bool AcknowledgeOutcome(ulong sequence)
    {
        if (!outcomePending || sequence != OutcomeSequence ||
            HighestDurableOutcomeSequence < sequence) return false;
        HighestAcknowledgedOutcomeSequence = Math.Max(HighestAcknowledgedOutcomeSequence, sequence);
        outcomePending = false;
        return true;
    }

    void ClearPendingOutcome()
    {
        if (outcomePending)
        {
            HighestDurableOutcomeSequence = Math.Max(HighestDurableOutcomeSequence, OutcomeSequence);
            HighestAcknowledgedOutcomeSequence = Math.Max(
                HighestAcknowledgedOutcomeSequence, OutcomeSequence);
        }
        outcomePending = false;
    }

    uint NextUInt()
    {
        uint value = randomState;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        randomState = value == 0 ? 0xA341316Cu : value;
        return randomState;
    }

    int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        uint value;
        int index;
        do value = NextUInt();
        while (!TryMapNonZeroRandomToIndex(value, exclusiveMax, out index));
        return index;
    }

    static uint SeedState(int value)
    {
        uint state = unchecked((uint)value) ^ 0x9E3779B9u;
        return state == 0 ? 0xA341316Cu : state;
    }

    static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    static void ValidateTier(int corruptionTier)
    {
        if (corruptionTier < 1 || corruptionTier > 4)
            throw new ArgumentOutOfRangeException(nameof(corruptionTier), corruptionTier,
                "Parlor corruption tier must be between 1 and 4.");
    }

    public GmParlorMatchSnapshot ExportSnapshot()
    {
        return new GmParlorMatchSnapshot
        {
            seed = seed,
            randomState = randomState,
            phase = Phase,
            readTestState = ReadTestState,
            readUnlocked = ReadUnlocked,
            corruptionTier = CorruptionTier,
            priorCatchCount = PriorCatchCount,
            catchesThisMatch = CatchesThisMatch,
            suspicion = Suspicion,
            sanity = Sanity,
            defiance = Defiance,
            compliance = Compliance,
            roundNumber = RoundNumber,
            playerRounds = PlayerRounds,
            aldricRounds = AldricRounds,
            playerTricks = PlayerTricks,
            aldricTricks = AldricTricks,
            trickNumber = TrickNumber,
            lastLeadWasAldric = LastLeadWasAldric,
            aldricCheated = AldricCheated,
            tellObservation = TellObservation,
            eyesExposeAldricHand = EyesExposeAldricHand,
            bonesReturnedThisRound = BonesReturnedThisRound,
            aldricCheatKind = AldricCheatKind,
            authoritativeCheatTell = AuthoritativeCheatTell,
            aldricPaidIndex = AldricPaidIndex,
            hasAldricPaidCard = AldricPaidCard.HasValue,
            aldricPaidCard = AldricPaidCard.GetValueOrDefault(),
            hasCurrentLeadCard = CurrentLeadCard.HasValue,
            currentLeadCard = CurrentLeadCard.GetValueOrDefault(),
            hasCurrentFollowCard = CurrentFollowCard.HasValue,
            currentFollowCard = CurrentFollowCard.GetValueOrDefault(),
            effectiveWinner = EffectiveWinner,
            lastTrickWinner = LastTrickWinner,
            roundWinner = RoundWinner,
            matchWinner = MatchWinner,
            playerHand = new List<GmCard>(playerHand),
            aldricHand = new List<GmCard>(aldricHand),
            revealedAldricCards = new List<GmCard>(revealedAldricCards),
            outcomeSequence = OutcomeSequence,
            highestDurableOutcomeSequence = HighestDurableOutcomeSequence,
            highestAcknowledgedOutcomeSequence = HighestAcknowledgedOutcomeSequence,
            outcomePending = outcomePending,
            lastOutcome = lastOutcome,
            adaptivePackage = adaptivePackage.DeepCopy(),
            behaviorAccumulator = behaviorAccumulator.DeepCopy(),
        };
    }

    public static bool TryRestore(GmParlorMatchSnapshot snapshot, out GmParlorMatch match,
        out string error)
    {
        match = null;
        if (!TryDispatchVersion(snapshot, out GmParlorMatchSnapshot current, out error) ||
            !TryValidateSnapshot(current, out error)) return false;
        var restored = new GmParlorMatch(current.seed, current.corruptionTier,
            current.priorCatchCount, current.readUnlocked);
        restored.ApplyValidatedSnapshot(current);
        match = restored;
        return true;
    }

    public bool TryImport(GmParlorMatchSnapshot snapshot, out string error)
    {
        if (!TryDispatchVersion(snapshot, out GmParlorMatchSnapshot current, out error) ||
            !TryValidateSnapshot(current, out error)) return false;
        ApplyValidatedSnapshot(current);
        return true;
    }

    void ApplyValidatedSnapshot(GmParlorMatchSnapshot snapshot)
    {
        seed = snapshot.seed;
        randomState = snapshot.randomState;
        Phase = snapshot.phase;
        ReadTestState = snapshot.readTestState;
        ReadUnlocked = snapshot.readUnlocked;
        CorruptionTier = snapshot.corruptionTier;
        PriorCatchCount = snapshot.priorCatchCount;
        CatchesThisMatch = snapshot.catchesThisMatch;
        Suspicion = snapshot.suspicion;
        Sanity = snapshot.sanity;
        Defiance = snapshot.defiance;
        Compliance = snapshot.compliance;
        RoundNumber = snapshot.roundNumber;
        PlayerRounds = snapshot.playerRounds;
        AldricRounds = snapshot.aldricRounds;
        PlayerTricks = snapshot.playerTricks;
        AldricTricks = snapshot.aldricTricks;
        TrickNumber = snapshot.trickNumber;
        LastLeadWasAldric = snapshot.lastLeadWasAldric;
        AldricCheated = snapshot.aldricCheated;
        TellObservation = snapshot.tellObservation;
        EyesExposeAldricHand = snapshot.eyesExposeAldricHand;
        BonesReturnedThisRound = snapshot.bonesReturnedThisRound;
        AldricCheatKind = snapshot.aldricCheatKind;
        AuthoritativeCheatTell = snapshot.authoritativeCheatTell;
        AldricPaidIndex = snapshot.aldricPaidIndex;
        AldricPaidCard = snapshot.hasAldricPaidCard ? snapshot.aldricPaidCard : (GmCard?)null;
        CurrentLeadCard = snapshot.hasCurrentLeadCard ? snapshot.currentLeadCard : (GmCard?)null;
        CurrentFollowCard = snapshot.hasCurrentFollowCard ? snapshot.currentFollowCard : (GmCard?)null;
        EffectiveWinner = snapshot.effectiveWinner;
        LastTrickWinner = snapshot.lastTrickWinner;
        RoundWinner = snapshot.roundWinner;
        MatchWinner = snapshot.matchWinner;
        playerHand.Clear();
        playerHand.AddRange(snapshot.playerHand);
        aldricHand.Clear();
        aldricHand.AddRange(snapshot.aldricHand);
        revealedAldricCards.Clear();
        revealedAldricCards.AddRange(snapshot.revealedAldricCards);
        OutcomeSequence = snapshot.outcomeSequence;
        HighestDurableOutcomeSequence = snapshot.highestDurableOutcomeSequence;
        HighestAcknowledgedOutcomeSequence = snapshot.highestAcknowledgedOutcomeSequence;
        outcomePending = snapshot.outcomePending;
        lastOutcome = snapshot.lastOutcome;
        adaptivePackage = snapshot.adaptivePackage.DeepCopy();
        behaviorAccumulator = snapshot.behaviorAccumulator.DeepCopy();
    }

    static bool TryValidateSnapshot(GmParlorMatchSnapshot snapshot, out string error)
    {
        if (snapshot == null) return Fail("snapshot is null", out error);
        if (snapshot.version != GmParlorMatchSnapshot.CurrentVersion)
            return Fail($"unsupported snapshot version {snapshot.version}", out error);
        if (snapshot.randomState == 0) return Fail("random state cannot be zero", out error);
        if (!Enum.IsDefined(typeof(GmParlorMatchPhase), snapshot.phase))
            return Fail("phase enum is invalid", out error);
        if (!Enum.IsDefined(typeof(GmReadTestState), snapshot.readTestState))
            return Fail("Read test state enum is invalid", out error);
        if (!Enum.IsDefined(typeof(GmTellObservation), snapshot.tellObservation))
            return Fail("tell observation enum is invalid", out error);
        if (!Enum.IsDefined(typeof(GmParlorCheatKind), snapshot.aldricCheatKind))
            return Fail("cheat kind enum is invalid", out error);
        if (!Enum.IsDefined(typeof(GmParlorOutcomeKind), snapshot.lastOutcome.Kind))
            return Fail("outcome kind enum is invalid", out error);
        if (!ValidOwner(snapshot.effectiveWinner) || !ValidOwner(snapshot.lastTrickWinner) ||
            !ValidOwner(snapshot.roundWinner) || !ValidOwner(snapshot.matchWinner))
            return Fail("winner enum is invalid", out error);
        if (snapshot.corruptionTier < 1 || snapshot.corruptionTier > 4)
            return Fail("corruption tier is outside 1..4", out error);
        if (snapshot.priorCatchCount < 0 || snapshot.catchesThisMatch < 0 || snapshot.suspicion < 0)
            return Fail("catch or suspicion count is negative", out error);
        if (snapshot.sanity < 0 || snapshot.sanity > 100 || snapshot.defiance < 0 ||
            snapshot.defiance > 20 || snapshot.compliance < 0 || snapshot.compliance > 20)
            return Fail("sanity/defiance/compliance validation failed", out error);
        if (snapshot.playerRounds < 0 || snapshot.playerRounds > 2 || snapshot.aldricRounds < 0 ||
            snapshot.aldricRounds > 2 || snapshot.playerTricks < 0 || snapshot.playerTricks > 7 ||
            snapshot.aldricTricks < 0 || snapshot.aldricTricks > 7)
            return Fail("round or trick score is impossible", out error);
        if (snapshot.roundNumber < 0 || snapshot.roundNumber > 3 || snapshot.trickNumber < 0 ||
            snapshot.trickNumber > 9)
            return Fail("round or trick number is impossible", out error);
        if (snapshot.playerHand == null || snapshot.aldricHand == null ||
            snapshot.revealedAldricCards == null)
            return Fail("card lists cannot be null", out error);
        if (snapshot.authoritativeCheatTell == null)
            return Fail("authoritative tell cannot be null", out error);
        if (snapshot.adaptivePackage == null)
            return Fail("adaptive package validation failed: package is missing", out error);
        if (!snapshot.adaptivePackage.TryValidate(out string adaptiveError))
            return Fail("adaptive package validation failed: " + adaptiveError, out error);
        if (snapshot.behaviorAccumulator == null)
            return Fail("behavior accumulator validation failed: accumulator is missing", out error);
        if (!snapshot.behaviorAccumulator.TryValidate(out string behaviorError))
            return Fail("behavior accumulator validation failed: " + behaviorError, out error);
        if (!ValidateAdaptivePackageBinding(snapshot.adaptivePackage,
            snapshot.behaviorAccumulator, out error)) return false;
        if ((snapshot.phase == GmParlorMatchPhase.MatchResult) !=
            snapshot.behaviorAccumulator.currentMatchSealed)
            return Fail("match phase and behavior seal state are contradictory", out error);

        var physical = new HashSet<GmCard>();
        if (!ValidateUniqueCards(snapshot.playerHand, physical, "player hand", out error) ||
            !ValidateUniqueCards(snapshot.aldricHand, physical, "Aldric hand", out error)) return false;
        for (int i = 0; i < snapshot.revealedAldricCards.Count; i++)
        {
            GmCard card = snapshot.revealedAldricCards[i];
            if (!ValidCanonicalCard(card)) return Fail("revealed card has invalid suit or rank", out error);
            if (!snapshot.aldricHand.Contains(card))
                return Fail("revealed card is not in Aldric hand", out error);
            if (snapshot.revealedAldricCards.IndexOf(card) != i)
                return Fail("duplicate revealed card", out error);
        }

        if (snapshot.hasCurrentLeadCard && !ValidCanonicalCard(snapshot.currentLeadCard))
            return Fail("lead table card has invalid suit or rank", out error);
        bool impossibleFollow = snapshot.aldricCheatKind == GmParlorCheatKind.ImpossibleEighthRank &&
            snapshot.aldricCheated && !snapshot.lastLeadWasAldric;
        if (snapshot.hasCurrentFollowCard &&
            !(ValidCanonicalCard(snapshot.currentFollowCard) ||
              (impossibleFollow && ValidImpossibleCard(snapshot.currentFollowCard))))
            return Fail("follow table card has invalid suit or rank", out error);
        if (snapshot.hasAldricPaidCard && !ValidCanonicalCard(snapshot.aldricPaidCard))
            return Fail("paid card has invalid suit or rank", out error);

        if (!ValidatePhaseShape(snapshot, out error)) return false;
        if (!ValidateCountsAndPhysicalCards(snapshot, physical, out error)) return false;
        if (!ValidateDerivedWinnersAndOutcome(snapshot, out error)) return false;
        if (snapshot.readUnlocked && snapshot.readTestState != GmReadTestState.Completed)
            return Fail("Read unlocked without completed teaching", out error);
        if (snapshot.outcomeSequence == 0 && !OutcomeIsDefault(snapshot.lastOutcome))
            return Fail("outcome payload exists before the first result", out error);
        if (snapshot.outcomePending && snapshot.phase != GmParlorMatchPhase.TrickResult)
            return Fail("pending outcome exists outside trick result", out error);
        if (snapshot.outcomePending && snapshot.lastOutcome.Kind == GmParlorOutcomeKind.None)
            return Fail("pending outcome has no result kind", out error);
        if (snapshot.highestAcknowledgedOutcomeSequence > snapshot.highestDurableOutcomeSequence ||
            snapshot.highestDurableOutcomeSequence > snapshot.outcomeSequence)
            return Fail("outcome durability/acknowledgement sequence is contradictory", out error);
        if (snapshot.outcomePending &&
            snapshot.highestAcknowledgedOutcomeSequence >= snapshot.outcomeSequence)
            return Fail("pending outcome is already acknowledged", out error);
        if (!snapshot.outcomePending && snapshot.outcomeSequence > 0 &&
            snapshot.highestAcknowledgedOutcomeSequence != snapshot.outcomeSequence)
            return Fail("consumed outcome lacks acknowledgement", out error);
        return true;
    }

    static bool ValidateAdaptivePackageBinding(GmParlorAdaptivePackage package,
        GmParlorBehaviorAccumulator behavior, out string error)
    {
        byte[] expectedHash = package.CanonicalHash;
        if (behavior.boundPackageHash == null ||
            !behavior.boundPackageHash.SequenceEqual(expectedHash))
            return Fail("adaptive package binding does not match the behavior accumulator",
                out error);
        if (behavior.sealedSummary != null &&
            !SummaryMatchesPackage(behavior.sealedSummary, package, expectedHash))
            return Fail("adaptive package binding does not match the sealed behavior summary",
                out error);
        for (int index = 0; index < behavior.completedMatchSummaries.Count; index++)
            if (!SummaryMatchesPackage(behavior.completedMatchSummaries[index], package,
                expectedHash))
                return Fail("adaptive package binding does not match completed behavior history",
                    out error);
        error = string.Empty;
        return true;
    }

    static bool SummaryMatchesPackage(GmParlorCompletedMatchSummary summary,
        GmParlorAdaptivePackage package, byte[] expectedHash)
    {
        return summary != null && summary.packageId == package.primaryCounterPlanId &&
            summary.honestStrategyId == package.honestStrategyId &&
            summary.packageTargetTendency == package.targetTendency &&
            summary.packageHash != null && summary.packageHash.SequenceEqual(expectedHash);
    }

    static bool TryDispatchVersion(GmParlorMatchSnapshot snapshot,
        out GmParlorMatchSnapshot current, out string error)
    {
        current = null;
        if (snapshot == null) return Fail("snapshot is null", out error);
        if (snapshot.version == GmParlorMatchSnapshot.CurrentVersion)
        {
            current = snapshot.DeepCopy();
            error = string.Empty;
            return true;
        }
        if (snapshot.version != 0 && snapshot.version != 1 && snapshot.version != 2)
            return Fail($"unsupported snapshot version {snapshot.version}", out error);

        current = snapshot.DeepCopy();
        current.version = GmParlorMatchSnapshot.CurrentVersion;
        current.adaptivePackage = GmParlorAdaptivePackage.LegacyBaseline();
        current.behaviorAccumulator =
            new GmParlorBehaviorAccumulator(current.adaptivePackage);
        if (snapshot.version == 0)
        {
            if (current.aldricCheatKind == GmParlorCheatKind.ImpossibleEighthRank &&
                current.hasCurrentFollowCard)
                return Fail("legacy v0 impossible-eight snapshot cannot reconstruct the paid card", out error);
            if (current.phase != GmParlorMatchPhase.PlayerLeads &&
                current.phase != GmParlorMatchPhase.NotStarted)
            {
                current.hasAldricPaidCard = true;
                current.aldricPaidCard = current.lastLeadWasAldric
                    ? current.currentLeadCard : current.currentFollowCard;
            }
        }
        if (current.outcomeSequence > 0)
        {
            current.highestDurableOutcomeSequence = current.outcomeSequence;
            current.highestAcknowledgedOutcomeSequence = current.outcomePending
                ? current.outcomeSequence - 1
                : current.outcomeSequence;
        }
        if (current.phase == GmParlorMatchPhase.MatchResult)
            current.behaviorAccumulator.SealCompletedMatch(
                current.adaptivePackage, matchOrdinal: 1, completedRematches: 0);
        error = string.Empty;
        return true;
    }

    static bool ValidatePhaseShape(GmParlorMatchSnapshot snapshot, out string error)
    {
        if (snapshot.phase == GmParlorMatchPhase.NotStarted)
        {
            if (snapshot.roundNumber != 0 || snapshot.trickNumber != 0 ||
                snapshot.playerHand.Count != 0 || snapshot.aldricHand.Count != 0 ||
                snapshot.hasCurrentLeadCard || snapshot.hasCurrentFollowCard)
                return Fail("NotStarted phase has dealt cards or table state", out error);
            error = string.Empty;
            return true;
        }
        if (snapshot.roundNumber < 1 || snapshot.trickNumber < 1)
            return Fail("started phase lacks round/trick counters", out error);
        if (snapshot.playerHand.Count > GmParlorCore.HandSize ||
            snapshot.aldricHand.Count > GmParlorCore.HandSize)
            return Fail("hand count exceeds the deal", out error);

        if (snapshot.phase == GmParlorMatchPhase.PlayerLeads)
        {
            if (snapshot.hasCurrentLeadCard || snapshot.hasCurrentFollowCard ||
                snapshot.lastLeadWasAldric || snapshot.hasAldricPaidCard || snapshot.aldricPaidIndex != -1)
                return Fail("PlayerLeads table shape is invalid", out error);
        }
        else if (snapshot.phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
        {
            if (!snapshot.hasCurrentLeadCard || snapshot.hasCurrentFollowCard ||
                !snapshot.lastLeadWasAldric || !snapshot.hasAldricPaidCard ||
                snapshot.aldricPaidCard != snapshot.currentLeadCard)
                return Fail("PlayerFollows table shape is invalid", out error);
        }
        else
        {
            if (!snapshot.hasCurrentLeadCard || !snapshot.hasCurrentFollowCard ||
                !snapshot.hasAldricPaidCard)
                return Fail("resolved/judgement table shape is invalid", out error);
        }

        if (snapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement &&
            snapshot.lastLeadWasAldric)
            return Fail("judgement phase cannot follow an Aldric lead", out error);
        if (snapshot.aldricCheated != (snapshot.aldricCheatKind != GmParlorCheatKind.None))
            return Fail("cheat flag and kind disagree", out error);
        if (snapshot.aldricCheated && string.IsNullOrWhiteSpace(snapshot.authoritativeCheatTell))
            return Fail("cheated play lacks authoritative tell", out error);
        if (!snapshot.aldricCheated && snapshot.authoritativeCheatTell.Length != 0)
            return Fail("honest play carries a cheat tell", out error);
        if (snapshot.hasAldricPaidCard &&
            (snapshot.aldricPaidIndex < 0 || snapshot.aldricPaidIndex > snapshot.aldricHand.Count))
            return Fail("paid card index is outside the pre-play hand", out error);

        if (snapshot.hasAldricPaidCard)
        {
            GmCard shownAldricCard = snapshot.lastLeadWasAldric
                ? snapshot.currentLeadCard : snapshot.currentFollowCard;
            if (snapshot.aldricCheatKind == GmParlorCheatKind.ImpossibleEighthRank)
            {
                if (!ValidImpossibleCard(shownAldricCard) ||
                    shownAldricCard.Suit != snapshot.currentLeadCard.Suit)
                    return Fail("impossible-rank cheat table evidence is invalid", out error);
            }
            else if (snapshot.aldricPaidCard != shownAldricCard)
                return Fail("paid card does not match Aldric's table card", out error);
        }
        error = string.Empty;
        return true;
    }

    static bool ValidateCountsAndPhysicalCards(GmParlorMatchSnapshot snapshot,
        HashSet<GmCard> physical, out string error)
    {
        if (snapshot.phase == GmParlorMatchPhase.NotStarted)
        {
            error = string.Empty;
            return true;
        }

        int completedTricks = snapshot.playerTricks + snapshot.aldricTricks;
        if (snapshot.trickNumber != completedTricks + 1)
            return Fail("trick number does not match the trick scores", out error);

        bool beforeResolution = snapshot.phase == GmParlorMatchPhase.PlayerLeads ||
            snapshot.phase == GmParlorMatchPhase.PlayerFollowsAldricLead ||
            snapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement;
        int expectedPlayer = GmParlorCore.HandSize + (snapshot.bonesReturnedThisRound ? 1 : 0) -
            completedTricks;
        int expectedAldric = GmParlorCore.HandSize - completedTricks;
        if (beforeResolution && snapshot.phase == GmParlorMatchPhase.AwaitingAldricJudgement)
            expectedPlayer--;
        if (beforeResolution && snapshot.phase != GmParlorMatchPhase.PlayerLeads)
            expectedAldric--;
        if (snapshot.playerHand.Count != expectedPlayer || snapshot.aldricHand.Count != expectedAldric)
            return Fail("impossible hand count for the phase and trick scores", out error);

        int finishedRounds = snapshot.playerRounds + snapshot.aldricRounds;
        int expectedFinishedRounds = snapshot.phase == GmParlorMatchPhase.RoundResult ||
            snapshot.phase == GmParlorMatchPhase.MatchResult
            ? snapshot.roundNumber : snapshot.roundNumber - 1;
        if (finishedRounds != expectedFinishedRounds)
            return Fail("round score does not match the round number and phase", out error);
        if (snapshot.phase == GmParlorMatchPhase.MatchResult &&
            snapshot.playerRounds != RoundsToWinMatch && snapshot.aldricRounds != RoundsToWinMatch)
            return Fail("match result has no match winner", out error);

        if (snapshot.hasAldricPaidCard && !physical.Add(snapshot.aldricPaidCard))
            return Fail("duplicate physical paid card", out error);

        if (snapshot.hasCurrentLeadCard || snapshot.hasCurrentFollowCard)
        {
            GmCard playerTableCard = snapshot.lastLeadWasAldric
                ? snapshot.currentFollowCard : snapshot.currentLeadCard;
            bool returnedBonesAlias = !beforeResolution && snapshot.bonesReturnedThisRound &&
                snapshot.lastTrickWinner == GmTrickOwner.Aldric &&
                playerTableCard.Suit == GmSuit.Bones && snapshot.playerHand.Contains(playerTableCard);
            if (!returnedBonesAlias && !physical.Add(playerTableCard))
                return Fail("duplicate physical player table card", out error);
        }

        error = string.Empty;
        return true;
    }

    static bool ValidateDerivedWinnersAndOutcome(GmParlorMatchSnapshot snapshot, out string error)
    {
        bool resultPhase = snapshot.phase == GmParlorMatchPhase.TrickResult ||
            snapshot.phase == GmParlorMatchPhase.RoundResult ||
            snapshot.phase == GmParlorMatchPhase.MatchResult;
        bool acknowledgedWithoutHistoricalPayload = snapshot.outcomeSequence > 0 &&
            !snapshot.outcomePending &&
            snapshot.highestAcknowledgedOutcomeSequence == snapshot.outcomeSequence &&
            OutcomeIsDefault(snapshot.lastOutcome);
        if (resultPhase)
        {
            GmTrickOwner clean = snapshot.lastLeadWasAldric
                ? (GmParlorCore.LeadWins(snapshot.currentLeadCard, snapshot.currentFollowCard)
                    ? GmTrickOwner.Aldric : GmTrickOwner.Player)
                : (snapshot.aldricCheated ||
                   !GmParlorCore.LeadWins(snapshot.currentLeadCard, snapshot.currentFollowCard)
                    ? GmTrickOwner.Aldric : GmTrickOwner.Player);
            if (snapshot.effectiveWinner != clean)
                return Fail("effective winner contradicts the played cards/evidence", out error);
            if (acknowledgedWithoutHistoricalPayload)
            {
                bool plausibleCaughtCheat = snapshot.aldricCheated &&
                    snapshot.lastTrickWinner == GmTrickOwner.Player;
                if (snapshot.lastTrickWinner != clean && !plausibleCaughtCheat)
                    return Fail("last winner contradicts the played cards without historical outcome", out error);
            }
            else
            {
                GmTrickOwner expectedLast = snapshot.lastOutcome.Kind == GmParlorOutcomeKind.CheatCaught
                    ? GmTrickOwner.Player : clean;
                if (snapshot.lastTrickWinner != expectedLast)
                    return Fail("last winner contradicts the judgement and played cards", out error);
            }
        }

        if (snapshot.phase == GmParlorMatchPhase.RoundResult ||
            snapshot.phase == GmParlorMatchPhase.MatchResult)
        {
            GmTrickOwner expectedRound = snapshot.playerTricks >= TricksToWinRound
                ? GmTrickOwner.Player : GmTrickOwner.Aldric;
            if (snapshot.roundWinner != expectedRound)
                return Fail("round winner contradicts the round score", out error);
        }
        if (snapshot.phase == GmParlorMatchPhase.MatchResult)
        {
            GmTrickOwner expectedMatch = snapshot.playerRounds >= RoundsToWinMatch
                ? GmTrickOwner.Player : GmTrickOwner.Aldric;
            if (snapshot.matchWinner != expectedMatch)
                return Fail("match winner contradicts the match score", out error);
        }

        if (snapshot.outcomeSequence > 0 && resultPhase && !acknowledgedWithoutHistoricalPayload)
        {
            GmParlorOutcome outcome = snapshot.lastOutcome;
            switch (outcome.Kind)
            {
                case GmParlorOutcomeKind.HonestAccepted:
                    if (!OutcomeDeltasAre(outcome, 0, 0, 0, 0, 0, 0) || snapshot.aldricCheated)
                        return Fail("honest outcome contradicts its evidence/deltas", out error);
                    break;
                case GmParlorOutcomeKind.CheatCaught:
                    if (!snapshot.aldricCheated || outcome.CatchDelta != 1 ||
                        outcome.CorruptionDelta != 0 || outcome.SuspicionDelta != 0 ||
                        outcome.SanityDelta < 0 || outcome.SanityDelta > 3 ||
                        outcome.DefianceDelta < 0 || outcome.DefianceDelta > 2 ||
                        outcome.ComplianceDelta != 0)
                        return Fail("caught-cheat outcome contradicts its evidence/deltas", out error);
                    break;
                case GmParlorOutcomeKind.CheatMissed:
                    if (!snapshot.aldricCheated || outcome.CatchDelta != 0 ||
                        outcome.CorruptionDelta < 0 || outcome.CorruptionDelta > 1 ||
                        outcome.SuspicionDelta < 0 || outcome.SuspicionDelta > 1 ||
                        outcome.SanityDelta != -3 || outcome.DefianceDelta != 0 ||
                        outcome.ComplianceDelta != 0)
                        return Fail("missed-cheat outcome contradicts its evidence/deltas", out error);
                    break;
                case GmParlorOutcomeKind.FalseReadPenalty:
                    if (snapshot.aldricCheated || outcome.CatchDelta != 0 ||
                        outcome.CorruptionDelta != 0 || outcome.SuspicionDelta != 0 ||
                        outcome.SanityDelta != -6 || outcome.DefianceDelta != 0 ||
                        outcome.ComplianceDelta < 0 || outcome.ComplianceDelta > 1)
                        return Fail("false-Read outcome contradicts its evidence/deltas", out error);
                    break;
                default:
                    return Fail("pending outcome kind is invalid", out error);
            }
        }
        error = string.Empty;
        return true;
    }

    static bool OutcomeDeltasAre(GmParlorOutcome outcome, int catches, int corruption,
        int suspicion, int sanity, int defiance, int compliance) =>
        outcome.CatchDelta == catches && outcome.CorruptionDelta == corruption &&
        outcome.SuspicionDelta == suspicion && outcome.SanityDelta == sanity &&
        outcome.DefianceDelta == defiance && outcome.ComplianceDelta == compliance;

    static bool ValidateUniqueCards(List<GmCard> cards, HashSet<GmCard> physical,
        string location, out string error)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (!ValidCanonicalCard(cards[i]))
                return Fail($"{location} card has invalid suit or rank", out error);
            if (!physical.Add(cards[i])) return Fail($"duplicate physical card in {location}", out error);
        }
        error = string.Empty;
        return true;
    }

    static bool ValidCanonicalCard(GmCard card) => Enum.IsDefined(typeof(GmSuit), card.Suit) &&
        card.Rank >= 1 && card.Rank <= GmParlorCore.RanksPerSuit;

    static bool ValidImpossibleCard(GmCard card) => Enum.IsDefined(typeof(GmSuit), card.Suit) &&
        card.Rank == GmParlorCore.RanksPerSuit + 1;

    static bool ValidOwner(GmTrickOwner owner) => Enum.IsDefined(typeof(GmTrickOwner), owner);

    static bool OutcomeIsDefault(GmParlorOutcome outcome) => outcome.Kind == GmParlorOutcomeKind.None &&
        outcome.CatchDelta == 0 && outcome.CorruptionDelta == 0 && outcome.SuspicionDelta == 0 &&
        outcome.SanityDelta == 0 && outcome.DefianceDelta == 0 && outcome.ComplianceDelta == 0;

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    public string PublicStateBytes
    {
        get { return Convert.ToBase64String(ExportSnapshot().ToCanonicalBytes()); }
    }
}
