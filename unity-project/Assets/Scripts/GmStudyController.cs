using System;

public enum GmStudyInitializeResult
{
    StartedNew,
    Restored,
    CorruptSavedState,
    AlreadyInitialized,
    PersistenceFailed
}

public enum GmStudyActionError
{
    None,
    NotInitialized,
    WrongPhase,
    PersistenceFailed
}

/// <summary>Visual-free durable command adapter for the Study table.</summary>
public sealed class GmStudyController
{
    GmStudyMatch match;

    public bool IsInitialized => match != null;
    public GmStudyMatchSnapshot Snapshot => match?.ExportSnapshot();
    public GmStudyMatchPhase Phase => match != null
        ? match.Phase
        : throw new InvalidOperationException("the Study controller is not initialized");
    public bool HasResult => match != null && match.HasResult;
    public GmStudyMatchResult Result => match != null
        ? match.Result
        : throw new InvalidOperationException("the Study controller is not initialized");
    public int PlayerDecisionCount => match?.PlayerDecisionCount ?? 0;
    public int FocusIndex { get; private set; }
    public string LastPersistenceError { get; private set; } = string.Empty;
    public string LastRestoreError { get; private set; } = string.Empty;

    public event Action OnStateChanged;
    public event Action OnFocusChanged;
    public event Action<GmStudyMatchResult> OnCompleted;

    public GmStudyInitializeResult InitializeOrRestore()
    {
        if (match != null) return GmStudyInitializeResult.AlreadyInitialized;
        LastRestoreError = string.Empty;
        if (!string.IsNullOrEmpty(GmRunStore.StudyRestoreError))
        {
            LastRestoreError = GmRunStore.StudyRestoreError;
            return GmStudyInitializeResult.CorruptSavedState;
        }

        GmStudyMatchSnapshot saved = GmRunStore.GetStudyMatchSnapshot();
        if (saved != null)
        {
            if (!GmStudyMatch.TryRestore(saved, out GmStudyMatch restored, out string error))
            {
                LastRestoreError = error;
                return GmStudyInitializeResult.CorruptSavedState;
            }
            match = restored;
            FocusIndex = 0;
            return GmStudyInitializeResult.Restored;
        }

        ulong seed = unchecked((uint)GmRunSeed.SeedForStream("study"));
        var started = new GmStudyMatch(seed);
        if (!TryPersist(started.ExportSnapshot(), out GmSaveData candidate))
            return GmStudyInitializeResult.PersistenceFailed;
        GmRunStore.CommitStudySaveData(candidate);
        match = started;
        FocusIndex = 0;
        return GmStudyInitializeResult.StartedNew;
    }

    public void MoveFocus(int delta)
    {
        int moved = (FocusIndex + delta) % 3;
        moved = moved < 0 ? moved + 3 : moved;
        if (moved == FocusIndex) return;
        FocusIndex = moved;
        OnFocusChanged?.Invoke();
    }

    public GmStudyActionError ConfirmFocusedAction()
    {
        if (match == null) return GmStudyActionError.NotInitialized;
        if (match.Phase == GmStudyMatchPhase.AwaitingIntervention)
            return Apply(() => match.TryResolveIntervention(false, out _));
        if (match.Phase != GmStudyMatchPhase.AwaitingMove)
            return GmStudyActionError.WrongPhase;
        GmStudyMoveCard[] cards = match.CurrentMoveCards;
        if (FocusIndex < 0 || FocusIndex >= cards.Length)
            return GmStudyActionError.WrongPhase;
        string actionId = cards[FocusIndex].actionId;
        return Apply(() => match.TryChoose(actionId, out _));
    }

    public GmStudyActionError Challenge()
    {
        if (match == null) return GmStudyActionError.NotInitialized;
        if (match.Phase != GmStudyMatchPhase.AwaitingIntervention)
            return GmStudyActionError.WrongPhase;
        return Apply(() => match.TryResolveIntervention(true, out _));
    }

    GmStudyActionError Apply(Func<bool> transition)
    {
        GmStudyMatchSnapshot before = match.ExportSnapshot();
        int focusBefore = FocusIndex;
        bool wasComplete = match.HasResult;
        if (!transition()) return GmStudyActionError.WrongPhase;
        if (!TryPersist(match.ExportSnapshot(), out GmSaveData candidate))
        {
            GmStudyMatch.TryRestore(before, out GmStudyMatch restored, out _);
            match = restored;
            FocusIndex = focusBefore;
            return GmStudyActionError.PersistenceFailed;
        }

        GmRunStore.CommitStudySaveData(candidate);
        OnStateChanged?.Invoke();
        if (!wasComplete && match.TryGetResult(out GmStudyMatchResult result)) OnCompleted?.Invoke(result);
        return GmStudyActionError.None;
    }

    bool TryPersist(GmStudyMatchSnapshot snapshot, out GmSaveData candidate)
    {
        candidate = null;
        if (!GmRunStore.TryCreateStudySaveData(snapshot, out candidate, out string error))
        {
            LastPersistenceError = error;
            return false;
        }
        if (!GmSaveSystem.QueueSaveData(candidate, out _) || !GmSaveSystem.Flush())
        {
            LastPersistenceError = GmSaveSystem.LastError;
            return false;
        }
        LastPersistenceError = string.Empty;
        return true;
    }
}
