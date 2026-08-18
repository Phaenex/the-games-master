using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmParlorState
{
    NotStarted,
    Dealing,
    PlayerTurn,
    HostTurn,
    TrickResolving,
    AccusationWindow,
    HandComplete,
    GameOver
}

public enum GmParlorInitializeResult
{
    StartedNew,
    Restored,
    CorruptSavedState,
    AlreadyInitialized,
    PersistenceFailed,
}

/// <summary>
/// Scene-facing compatibility adapter. GmParlorMatch remains the sole rules authority; this type
/// only translates its phases to the existing room contract and applies room completion once.
/// </summary>
public sealed class GmParlorRules : MonoBehaviour
{
    public const int HandSize = GmParlorCore.HandSize;
    public const int TricksToWin = GmParlorMatch.TricksToWinRound;

    static readonly IReadOnlyList<GmCard> EmptyHand = Array.Empty<GmCard>();
    bool roomCompletionApplied;
    bool restoredOutcomesActivated;

    public GmParlorMatch Match { get; private set; }
    public GmParlorInitializeResult LastInitializeResult { get; private set; }
    public string LastRestoreError { get; private set; } = string.Empty;
    public string LastPersistenceError { get; private set; } = string.Empty;
    public GmParlorOutcomeKind LastResolvedOutcomeKind { get; private set; } =
        GmParlorOutcomeKind.None;
    public IReadOnlyList<GmCard> PlayerHand => Match != null ? Match.PlayerHand : EmptyHand;
    public IReadOnlyList<GmCard> HostHand => Match != null ? Match.AldricHand : EmptyHand;
    public IReadOnlyList<GmCard> VisibleOpposingCards => Match != null
        ? Match.RevealedAldricCards : EmptyHand;
    public GmParlorMatchPhase Phase => Match != null
        ? Match.Phase : GmParlorMatchPhase.NotStarted;
    public int RoundNumber => Match != null ? Match.RoundNumber : 0;
    public GmParlorState State => ToLegacyState(Match != null ? Match.Phase : GmParlorMatchPhase.NotStarted);
    public int PlayerTricksWon => Match != null ? Match.PlayerTricks : 0;
    public int HostTricksWon => Match != null ? Match.AldricTricks : 0;
    public int PlayerRoundsWon => Match != null ? Match.PlayerRounds : 0;
    public int HostRoundsWon => Match != null ? Match.AldricRounds : 0;
    public int TrickNumber => Match != null ? Match.TrickNumber : 0;
    public GmCard? CurrentLeadCard => Match != null ? Match.CurrentLeadCard : null;
    public GmCard? CurrentFollowCard => Match != null ? Match.CurrentFollowCard : null;
    public bool HostLeadsCurrentTrick => Match != null && Match.AldricLeadsCurrentTrick;
    public float Suspicion => Match != null ? Match.Suspicion / 2f : 0f;
    public bool ReadEnabled => Match != null && (Match.ReadUnlocked ||
        Match.ReadTestState == GmReadTestState.Active);
    public GmTellObservation TellObservation => Match != null
        ? Match.TellObservation : GmTellObservation.Calm;

    public GmParlorMatchSnapshot GetPresentationRestoreSnapshot() => Match != null
        ? Match.ExportSnapshot() : GmRunStore.GetParlorMatchSnapshot();

    public event Action OnStateChanged;
    public event Action<bool> OnTrickCompleted;
    public event Action<bool> OnRoundCompleted;
    public event Action<bool> OnGameCompleted;
    public event Action<ulong, GmParlorOutcome> OnOutcomeReady;

    void Awake()
    {
        LastInitializeResult = InitializeOrRestore();
    }

    public GmParlorInitializeResult InitializeOrRestore(int seed = 42, int corruptionTier = 0,
        int priorCatchCount = -1, bool? readInitiallyUnlocked = null)
    {
        if (Match != null) return GmParlorInitializeResult.AlreadyInitialized;
        LastRestoreError = string.Empty;
        if (!string.IsNullOrEmpty(GmRunStore.ParlorRestoreError))
        {
            LastRestoreError = GmRunStore.ParlorRestoreError;
            Debug.LogError($"[GmParlorRules] Refusing corrupt saved match: {LastRestoreError}");
            LastInitializeResult = GmParlorInitializeResult.CorruptSavedState;
            return LastInitializeResult;
        }
        GmParlorMatchSnapshot saved = GmRunStore.GetParlorMatchSnapshot();
        if (saved != null)
        {
            if (!GmParlorMatch.TryRestore(saved, out GmParlorMatch restored, out string error))
            {
                LastRestoreError = error;
                Debug.LogError($"[GmParlorRules] Refusing corrupt saved match: {error}");
                LastInitializeResult = GmParlorInitializeResult.CorruptSavedState;
                return LastInitializeResult;
            }

            Match = restored;
            roomCompletionApplied = GmRunStore.IsRoomComplete("parlor");
            restoredOutcomesActivated = false;
            LastInitializeResult = GmParlorInitializeResult.Restored;
            return LastInitializeResult;
        }

        int runTier = corruptionTier > 0 ? corruptionTier : Math.Min(4, GmRunStore.CorruptionTier);
        int runCatches = priorCatchCount >= 0 ? priorCatchCount : GmRunStore.CheatsCaughtCount;
        bool runRead = readInitiallyUnlocked ?? GmRunStore.CheatsCaughtCount > 0;
        GmHouseRunGeneration houseRun = GmHousePersistenceCoordinator.ActiveRun;
        GmParlorAdaptivePackage frozenPackage = houseRun?.FrozenPackage;
        int frozenSeed = houseRun != null ? houseRun.Seed : seed;
        Match = new GmParlorMatch(frozenSeed, runTier, runCatches, runRead,
            frozenPackage, GmRunStore.ParlorAppliedOutcomeSequence);
        roomCompletionApplied = false;
        restoredOutcomesActivated = true;
        Match.Start();
        if (!PersistMatchQueued())
        {
            Match = null;
            LastInitializeResult = GmParlorInitializeResult.PersistenceFailed;
            return LastInitializeResult;
        }
        LastInitializeResult = GmParlorInitializeResult.StartedNew;
        return LastInitializeResult;
    }

    public GmParlorInitializeResult StartGame(int seed = 42, int corruptionTier = 1,
        int priorCatchCount = 0,
        bool readInitiallyUnlocked = false, bool forceRestart = false)
    {
        return StartGameInternal(seed, corruptionTier, priorCatchCount,
            readInitiallyUnlocked, forceRestart, null);
    }

    public GmParlorInitializeResult StartAdaptiveGame(GmParlorAdaptivePackage adaptivePackage,
        int seed = 42, int corruptionTier = 1, int priorCatchCount = 0,
        bool readInitiallyUnlocked = false, bool forceRestart = false)
    {
        if (adaptivePackage == null) throw new ArgumentNullException(nameof(adaptivePackage));
        return StartGameInternal(seed, corruptionTier, priorCatchCount,
            readInitiallyUnlocked, forceRestart, adaptivePackage);
    }

    GmParlorInitializeResult StartGameInternal(int seed, int corruptionTier,
        int priorCatchCount, bool readInitiallyUnlocked, bool forceRestart,
        GmParlorAdaptivePackage adaptivePackage)
    {
        if (!forceRestart && LastInitializeResult == GmParlorInitializeResult.Restored && Match != null)
            return GmParlorInitializeResult.Restored;
        if (!forceRestart && LastInitializeResult == GmParlorInitializeResult.CorruptSavedState)
        {
            Debug.LogError("[GmParlorRules] StartGame refused while corrupt saved state is unresolved.");
            return GmParlorInitializeResult.CorruptSavedState;
        }

        var replacement = new GmParlorMatch(seed, corruptionTier, priorCatchCount,
            readInitiallyUnlocked,
            adaptivePackage, GmRunStore.ParlorAppliedOutcomeSequence);
        if (replacement.Start() != GmParlorActionError.None)
            return GmParlorInitializeResult.PersistenceFailed;
        if (!TryPersistCandidate(replacement.ExportSnapshot(), completeRoom: false,
            out GmSaveData candidate))
            return GmParlorInitializeResult.PersistenceFailed;

        GmRunStore.CommitParlorSaveData(candidate);
        Match = replacement;
        LastResolvedOutcomeKind = GmParlorOutcomeKind.None;
        roomCompletionApplied = false;
        restoredOutcomesActivated = true;
        LastRestoreError = string.Empty;
        LastInitializeResult = GmParlorInitializeResult.StartedNew;
        OnStateChanged?.Invoke();
        return LastInitializeResult;
    }

    public GmParlorActionError PlayPlayerCard(int index) => Apply(() => Match.PlayPlayerCard(index));
    public GmParlorActionError GetPlayerCardError(int index) => Match != null
        ? Match.GetPlayerCardError(index) : GmParlorActionError.WrongPhase;

    public bool PlayPlayerCard(GmCard card)
    {
        if (Match == null) return false;
        int index = -1;
        for (int i = 0; i < Match.PlayerHand.Count; i++)
            if (Match.PlayerHand[i] == card) { index = i; break; }
        return PlayPlayerCard(index) == GmParlorActionError.None;
    }

    [Obsolete("Aldric's turns are selected deterministically by GmParlorMatch.")]
    public bool PlayHostCard(GmCard card) => false;

    public GmParlorActionError AcceptAldricPlay() => Apply(() => Match.ContinueJudgement());
    public GmParlorActionError ReadAldricPlay() => Apply(() => Match.Read());
    public GmParlorActionError ContinueResult() => Apply(() => Match.Continue());
    public GmParlorActionError StartRematch() => Apply(() =>
    {
        GmParlorActionError error = Match.StartRematch();
        if (error == GmParlorActionError.None) roomCompletionApplied = false;
        return error;
    });

    GmParlorActionError Apply(Func<GmParlorActionError> transition)
    {
        if (Match == null) return GmParlorActionError.WrongPhase;
        GmParlorMatchSnapshot beforeSnapshot = Match.ExportSnapshot();
        GmParlorMatchPhase before = Match.Phase;
        bool completionBefore = roomCompletionApplied;
        GmParlorActionError error = transition();
        if (error != GmParlorActionError.None) return error;

        bool boundaryPersisted = false;
        if (Match.TryPeekOutcome(out _, out _))
        {
            GmParlorActionError delivery = DeliverOutcome(beforeSnapshot, rollbackProducedFailure: true);
            if (delivery != GmParlorActionError.None) return delivery;
            boundaryPersisted = true;
        }

        bool enteredTrick = Match.Phase == GmParlorMatchPhase.TrickResult &&
            before != GmParlorMatchPhase.TrickResult;
        bool enteredRound = Match.Phase == GmParlorMatchPhase.RoundResult &&
            before != GmParlorMatchPhase.RoundResult;
        bool enteredMatch = Match.Phase == GmParlorMatchPhase.MatchResult &&
            before != GmParlorMatchPhase.MatchResult;
        bool boundary = enteredTrick || enteredRound || enteredMatch;

        if (boundary && !boundaryPersisted)
        {
            if (!TryPersistCandidate(Match.ExportSnapshot(),
                completeRoom: enteredMatch && !completionBefore, out GmSaveData candidate))
            {
                roomCompletionApplied = completionBefore;
                RollbackMatch(beforeSnapshot);
                return GmParlorActionError.PersistenceFailed;
            }
            GmRunStore.CommitParlorSaveData(candidate);
            roomCompletionApplied = GmRunStore.IsRoomComplete("parlor");
            boundaryPersisted = true;
        }
        else if (!boundary && !PersistMatchQueued())
        {
            roomCompletionApplied = completionBefore;
            RollbackMatch(beforeSnapshot);
            return GmParlorActionError.PersistenceFailed;
        }

        if (enteredTrick)
            OnTrickCompleted?.Invoke(Match.LastTrickWinner == GmTrickOwner.Player);
        if (enteredRound)
            OnRoundCompleted?.Invoke(Match.RoundWinner == GmTrickOwner.Player);
        if (enteredMatch)
            OnGameCompleted?.Invoke(Match.MatchWinner == GmTrickOwner.Player);
        OnStateChanged?.Invoke();
        return GmParlorActionError.None;
    }

    public bool AbandonSavedMatch()
    {
        if (!GmRunStore.TryCreateParlorAbandonSaveData(out GmSaveData candidate,
            out string error))
        {
            LastPersistenceError = error;
            Debug.LogError($"[GmParlorRules] Could not stage match abandonment: {error}");
            return false;
        }
        if (!GmSaveSystem.QueueSaveData(candidate, out _) || !FlushPersistence()) return false;

        GmRunStore.CommitParlorAbandonSaveData(candidate);
        Match = null;
        roomCompletionApplied = false;
        restoredOutcomesActivated = true;
        return true;
    }

    public GmParlorActionError DeliverPendingOutcome()
    {
        if (Match == null || !Match.TryPeekOutcome(out _, out _)) return GmParlorActionError.None;
        return DeliverOutcome(Match.ExportSnapshot(), rollbackProducedFailure: false);
    }

    public GmParlorActionError ActivateRestoredOutcomes()
    {
        if (Match == null) return GmParlorActionError.WrongPhase;
        if (restoredOutcomesActivated) return GmParlorActionError.None;

        GmParlorActionError delivery = DeliverPendingOutcome();
        if (delivery != GmParlorActionError.None) return delivery;
        if (Match.Phase == GmParlorMatchPhase.MatchResult && !roomCompletionApplied)
        {
            if (!TryPersistCandidate(Match.ExportSnapshot(), completeRoom: true,
                out GmSaveData candidate))
                return GmParlorActionError.PersistenceFailed;
            GmRunStore.CommitParlorSaveData(candidate);
            roomCompletionApplied = true;
        }
        restoredOutcomesActivated = true;
        return GmParlorActionError.None;
    }

    GmParlorActionError DeliverOutcome(GmParlorMatchSnapshot beforeSnapshot,
        bool rollbackProducedFailure)
    {
        if (!Match.TryPeekOutcome(out GmParlorOutcome outcome, out ulong sequence))
            return GmParlorActionError.None;

        if (Match.HighestDurableOutcomeSequence < sequence)
        {
            Match.MarkOutcomeDurable(sequence);
            if (!TryPersistCandidate(Match.ExportSnapshot(), completeRoom: false,
                out GmSaveData durableCandidate))
            {
                if (rollbackProducedFailure) RollbackMatch(beforeSnapshot);
                return GmParlorActionError.PersistenceFailed;
            }
            GmRunStore.CommitParlorSaveData(durableCandidate);
        }

        GmParlorMatchSnapshot durablePending = Match.ExportSnapshot();

        if (!GmRunStore.TryApplyParlorOutcome(sequence, outcome, out string applyError))
        {
            LastPersistenceError = applyError;
            Debug.LogError($"[GmParlorRules] Outcome apply failed: {applyError}");
            return GmParlorActionError.PersistenceFailed;
        }
        if (!Match.AcknowledgeOutcome(sequence) ||
            !TryPersistCandidate(Match.ExportSnapshot(), completeRoom: false,
                out GmSaveData acknowledgedCandidate))
        {
            RestoreDurablePending(durablePending);
            return GmParlorActionError.PersistenceFailed;
        }
        GmRunStore.CommitParlorSaveData(acknowledgedCandidate);
        // This is resolved public information, not Aldric's pre-judgement truth. The controller
        // uses it to tell a successful catch from a false accusation after the durable boundary.
        LastResolvedOutcomeKind = outcome.Kind;

        try { OnOutcomeReady?.Invoke(sequence, outcome); }
        catch (Exception ex)
        {
            Debug.LogError($"[GmParlorRules] Outcome observer failed after durable ack: {ex.Message}");
            return GmParlorActionError.OutcomeHandlerFailed;
        }
        return GmParlorActionError.None;
    }

    bool TryPersistCandidate(GmParlorMatchSnapshot snapshot, bool completeRoom,
        out GmSaveData candidate)
    {
        candidate = null;
        if (!GmRunStore.TryCreateParlorSaveData(snapshot, completeRoom, out candidate,
            out string error))
        {
            LastPersistenceError = error;
            Debug.LogError($"[GmParlorRules] Refusing invalid persistence candidate: {error}");
            return false;
        }
        if (!GmSaveSystem.QueueSaveData(candidate, out _))
        {
            LastPersistenceError = GmSaveSystem.LastError;
            return false;
        }
        if (!FlushPersistence()) return false;
        LastPersistenceError = string.Empty;
        return true;
    }

    void RestoreDurablePending(GmParlorMatchSnapshot snapshot)
    {
        if (GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored, out _)) Match = restored;
        GmRunStore.RestoreParlorSnapshotForTransaction(snapshot);
    }

    bool PersistMatchQueued()
    {
        if (Match == null) return false;
        if (!GmRunStore.TrySetParlorMatch(Match.ExportSnapshot(), out string error))
        {
            LastPersistenceError = error;
            Debug.LogError($"[GmParlorRules] Refusing to persist invalid match: {error}");
            return false;
        }
        if (GmSaveSystem.QueueSave(out _))
        {
            LastPersistenceError = string.Empty;
            return true;
        }
        LastPersistenceError = GmSaveSystem.LastError;
        return false;
    }

    public bool FlushPersistence()
    {
        if (GmSaveSystem.Flush())
        {
            LastPersistenceError = string.Empty;
            return true;
        }
        LastPersistenceError = GmSaveSystem.LastError;
        return false;
    }

    void RollbackMatch(GmParlorMatchSnapshot snapshot)
    {
        if (GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored, out _)) Match = restored;
        GmRunStore.RestoreParlorSnapshotForTransaction(snapshot);
    }

    void OnDisable()
    {
        if (Match != null) FlushPersistence();
    }

    void OnApplicationQuit()
    {
        if (Match != null) FlushPersistence();
    }

    static GmParlorState ToLegacyState(GmParlorMatchPhase phase)
    {
        switch (phase)
        {
            case GmParlorMatchPhase.PlayerLeads:
            case GmParlorMatchPhase.PlayerFollowsAldricLead:
                return GmParlorState.PlayerTurn;
            case GmParlorMatchPhase.AwaitingAldricJudgement:
                return GmParlorState.AccusationWindow;
            case GmParlorMatchPhase.TrickResult:
                return GmParlorState.TrickResolving;
            case GmParlorMatchPhase.RoundResult:
                return GmParlorState.HandComplete;
            case GmParlorMatchPhase.MatchResult:
                return GmParlorState.GameOver;
            default:
                return GmParlorState.NotStarted;
        }
    }
}
