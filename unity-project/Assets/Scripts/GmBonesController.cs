using System;

public enum GmBonesInitializeResult
{
    StartedNew,
    Restored,
    CorruptSavedState,
    AlreadyInitialized,
    PersistenceFailed
}

public enum GmBonesActionError
{
    None,
    NotInitialized,
    WrongPhase,
    PersistenceFailed
}

/// <summary>Visual-free durable command adapter for the Bones table.</summary>
public sealed class GmBonesController
{
    GmBonesMatch match;

    public bool IsInitialized => match != null;
    public GmBonesMatchSnapshot Snapshot => match?.ExportSnapshot();
    public GmBonesMatchPhase Phase => match != null
        ? match.Phase
        : throw new InvalidOperationException("the Bones controller is not initialized");
    public bool HasResult => match != null && match.HasResult;
    public GmBonesMatchResult Result => match != null
        ? match.Result
        : throw new InvalidOperationException("the Bones controller is not initialized");
    public int PlayerDecisionCount => match?.PlayerDecisionCount ?? 0;
    public int FocusIndex { get; private set; }
    public string LastPersistenceError { get; private set; } = string.Empty;
    public string LastRestoreError { get; private set; } = string.Empty;

    public event Action OnStateChanged;
    public event Action OnFocusChanged;
    public event Action<GmBonesMatchResult> OnCompleted;

    public GmBonesInitializeResult InitializeOrRestore()
    {
        if (match != null) return GmBonesInitializeResult.AlreadyInitialized;
        LastRestoreError = string.Empty;
        if (!string.IsNullOrEmpty(GmRunStore.BonesRestoreError))
        {
            LastRestoreError = GmRunStore.BonesRestoreError;
            return GmBonesInitializeResult.CorruptSavedState;
        }

        GmBonesMatchSnapshot saved = GmRunStore.GetBonesMatchSnapshot();
        if (saved != null)
        {
            if (!GmBonesMatch.TryRestore(saved, out GmBonesMatch restored, out string error))
            {
                LastRestoreError = error;
                return GmBonesInitializeResult.CorruptSavedState;
            }
            match = restored;
            FocusIndex = 0;
            return GmBonesInitializeResult.Restored;
        }

        ulong seed = unchecked((uint)GmRunSeed.SeedForStream("bones"));
        var started = new GmBonesMatch(seed, null);
        if (!TryPersist(started.ExportSnapshot(), out GmSaveData candidate))
            return GmBonesInitializeResult.PersistenceFailed;
        GmRunStore.CommitBonesSaveData(candidate);
        match = started;
        FocusIndex = 0;
        return GmBonesInitializeResult.StartedNew;
    }

    public void MoveFocus(int delta)
    {
        int moved = (FocusIndex + delta) % 4;
        moved = moved < 0 ? moved + 4 : moved;
        if (moved == FocusIndex) return;
        FocusIndex = moved;
        OnFocusChanged?.Invoke();
    }

    public GmBonesActionError ConfirmFocusedAction()
    {
        if (match == null) return GmBonesActionError.NotInitialized;
        if (match.Phase == GmBonesMatchPhase.AwaitingIntervention)
            return Apply(() => match.TryResolveIntervention(false, out _));
        if (match.Phase != GmBonesMatchPhase.AwaitingPlayerChoice)
            return GmBonesActionError.WrongPhase;
        GmBonesChoice choice = FocusIndex == 0 ? GmBonesChoice.Bank : GmBonesChoice.Press;
        int lockIndex = FocusIndex == 0 ? -1 : FocusIndex - 1;
        return Apply(() => match.TryChoose(choice, lockIndex, out _));
    }

    public GmBonesActionError CallTell()
    {
        if (match == null) return GmBonesActionError.NotInitialized;
        if (match.Phase != GmBonesMatchPhase.AwaitingIntervention)
            return GmBonesActionError.WrongPhase;
        return Apply(() => match.TryResolveIntervention(true, out _));
    }

    GmBonesActionError Apply(Func<bool> transition)
    {
        GmBonesMatchSnapshot before = match.ExportSnapshot();
        int focusBefore = FocusIndex;
        bool wasComplete = match.HasResult;
        if (!transition()) return GmBonesActionError.WrongPhase;
        if (!TryPersist(match.ExportSnapshot(), out GmSaveData candidate))
        {
            GmBonesMatch.TryRestore(before, out GmBonesMatch restored, out _);
            match = restored;
            FocusIndex = focusBefore;
            return GmBonesActionError.PersistenceFailed;
        }

        GmRunStore.CommitBonesSaveData(candidate);
        OnStateChanged?.Invoke();
        if (!wasComplete && match.TryGetResult(out GmBonesMatchResult result)) OnCompleted?.Invoke(result);
        return GmBonesActionError.None;
    }

    bool TryPersist(GmBonesMatchSnapshot snapshot, out GmSaveData candidate)
    {
        candidate = null;
        if (!GmRunStore.TryCreateBonesSaveData(snapshot, out candidate, out string error))
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
