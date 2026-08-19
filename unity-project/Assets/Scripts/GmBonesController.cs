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
    public GmBonesMatch Match { get; private set; }
    public int FocusIndex { get; private set; }
    public string LastPersistenceError { get; private set; } = string.Empty;
    public string LastRestoreError { get; private set; } = string.Empty;

    public event Action OnStateChanged;
    public event Action<GmBonesMatchResult> OnCompleted;

    public GmBonesInitializeResult InitializeOrRestore()
    {
        if (Match != null) return GmBonesInitializeResult.AlreadyInitialized;
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
            Match = restored;
            FocusIndex = 0;
            return GmBonesInitializeResult.Restored;
        }

        ulong seed = unchecked((uint)GmRunSeed.SeedForStream("bones"));
        var started = new GmBonesMatch(seed, null);
        if (!TryPersist(started.ExportSnapshot(), out GmSaveData candidate))
            return GmBonesInitializeResult.PersistenceFailed;
        GmRunStore.CommitBonesSaveData(candidate);
        Match = started;
        FocusIndex = 0;
        return GmBonesInitializeResult.StartedNew;
    }

    public void MoveFocus(int delta)
    {
        int moved = (FocusIndex + delta) % 4;
        FocusIndex = moved < 0 ? moved + 4 : moved;
    }

    public GmBonesActionError ConfirmFocusedAction()
    {
        if (Match == null) return GmBonesActionError.NotInitialized;
        if (Match.Phase == GmBonesMatchPhase.AwaitingIntervention)
            return Apply(() => Match.TryResolveIntervention(false, out _));
        if (Match.Phase != GmBonesMatchPhase.AwaitingPlayerChoice)
            return GmBonesActionError.WrongPhase;
        GmBonesChoice choice = FocusIndex == 0 ? GmBonesChoice.Bank : GmBonesChoice.Press;
        int lockIndex = FocusIndex == 0 ? -1 : FocusIndex - 1;
        return Apply(() => Match.TryChoose(choice, lockIndex, out _));
    }

    public GmBonesActionError CallTell()
    {
        if (Match == null) return GmBonesActionError.NotInitialized;
        if (Match.Phase != GmBonesMatchPhase.AwaitingIntervention)
            return GmBonesActionError.WrongPhase;
        return Apply(() => Match.TryResolveIntervention(true, out _));
    }

    GmBonesActionError Apply(Func<bool> transition)
    {
        GmBonesMatchSnapshot before = Match.ExportSnapshot();
        int focusBefore = FocusIndex;
        bool wasComplete = Match.HasResult;
        if (!transition()) return GmBonesActionError.WrongPhase;
        if (!TryPersist(Match.ExportSnapshot(), out GmSaveData candidate))
        {
            GmBonesMatch.TryRestore(before, out GmBonesMatch restored, out _);
            Match = restored;
            FocusIndex = focusBefore;
            return GmBonesActionError.PersistenceFailed;
        }

        GmRunStore.CommitBonesSaveData(candidate);
        OnStateChanged?.Invoke();
        if (!wasComplete && Match.TryGetResult(out GmBonesMatchResult result)) OnCompleted?.Invoke(result);
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
