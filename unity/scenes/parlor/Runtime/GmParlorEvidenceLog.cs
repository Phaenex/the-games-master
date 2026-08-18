using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public enum GmParlorObservedFact
{
    RightHandPausedAboveDeck,
    CardContactBroke,
    SleeveBrushedTable,
    CardPlacedWithoutPause,
}

public readonly struct GmParlorEvidenceFact
{
    public readonly ulong CommandId;
    public readonly GmParlorObservedFact Fact;
    public readonly string Text;

    internal GmParlorEvidenceFact(ulong commandId, GmParlorObservedFact fact, string text)
    {
        CommandId = commandId;
        Fact = fact;
        Text = text;
    }
}

/// <summary>
/// A bounded journal of what the player could perceive. Callers choose from a fixed fact vocabulary
/// so presentation cannot smuggle hidden truth into a caption or focus view.
/// </summary>
public sealed class GmParlorEvidenceLog : MonoBehaviour
{
    public const int Capacity = 8;

    readonly List<GmParlorEvidenceFact> facts = new List<GmParlorEvidenceFact>(Capacity);
    readonly HashSet<FactKey> recorded = new HashSet<FactKey>();
    ReadOnlyCollection<GmParlorEvidenceFact> readOnlyFacts;

    public IReadOnlyList<GmParlorEvidenceFact> Facts =>
        readOnlyFacts ??= facts.AsReadOnly();
    public bool DurablePersistenceEnabled { get; private set; }
    public string LastPersistenceError { get; private set; } = string.Empty;
    public event Action OnChanged;

    public bool Record(ulong commandId, GmParlorObservedFact fact)
    {
        var key = new FactKey(commandId, fact);
        if (commandId == 0 || !Enum.IsDefined(typeof(GmParlorObservedFact), fact) ||
            recorded.Contains(key)) return false;

        GmParlorPresentationState candidate = ExportState();
        if (candidate.observedFacts.Count == Capacity)
            candidate.observedFacts.RemoveAt(0);
        candidate.observedFacts.Add(new GmParlorObservedFactData(commandId, fact));
        if (DurablePersistenceEnabled && !TryPersist(candidate)) return false;

        recorded.Add(key);
        if (facts.Count == Capacity)
        {
            GmParlorEvidenceFact removed = facts[0];
            facts.RemoveAt(0);
            recorded.Remove(new FactKey(removed.CommandId, removed.Fact));
        }
        facts.Add(new GmParlorEvidenceFact(commandId, fact, TextFor(fact)));
        OnChanged?.Invoke();
        return true;
    }

    public bool TryRecordObservation(ulong commandId, GmTellObservation observation)
    {
        if (commandId == 0 || !Enum.IsDefined(typeof(GmTellObservation), observation))
            return false;
        GmParlorObservedFact[] observed = observation == GmTellObservation.Suspicious
            ? new[]
            {
                GmParlorObservedFact.RightHandPausedAboveDeck,
                GmParlorObservedFact.CardContactBroke,
            }
            : new[] { GmParlorObservedFact.CardPlacedWithoutPause };
        bool complete = true;
        for (int index = 0; index < observed.Length; index++)
            if (!recorded.Contains(new FactKey(commandId, observed[index]))) complete = false;
        if (complete) return true;

        GmParlorPresentationState candidate = ExportState();
        for (int candidateIndex = candidate.observedFacts.Count - 1;
             candidateIndex >= 0; candidateIndex--)
        {
            GmParlorObservedFactData item = candidate.observedFacts[candidateIndex];
            if (item.commandId != commandId) continue;
            for (int observedIndex = 0; observedIndex < observed.Length; observedIndex++)
                if (item.fact == observed[observedIndex])
                {
                    candidate.observedFacts.RemoveAt(candidateIndex);
                    break;
                }
        }
        while (candidate.observedFacts.Count > Capacity - observed.Length)
            RemoveOldestCommand(candidate.observedFacts);
        for (int index = 0; index < observed.Length; index++)
            candidate.observedFacts.Add(new GmParlorObservedFactData(commandId, observed[index]));
        if (DurablePersistenceEnabled && !TryPersist(candidate)) return false;
        return TryImport(candidate, out _);
    }

    static void RemoveOldestCommand(List<GmParlorObservedFactData> candidate)
    {
        if (candidate.Count == 0) return;
        ulong oldestCommandId = candidate[0].commandId;
        candidate.RemoveAll(item => item != null && item.commandId == oldestCommandId);
    }

    public void EnableDurablePersistence() => DurablePersistenceEnabled = true;

    public GmParlorPresentationState ExportState()
    {
        var state = GmParlorPresentationState.Empty();
        for (int index = 0; index < facts.Count; index++)
            state.observedFacts.Add(new GmParlorObservedFactData(
                facts[index].CommandId, facts[index].Fact));
        return state;
    }

    public bool TryRestoreFromRunStore(out string error)
    {
        if (!string.IsNullOrEmpty(GmRunStore.ParlorPresentationRestoreError))
        {
            error = GmRunStore.ParlorPresentationRestoreError;
            return false;
        }
        return TryImport(GmRunStore.GetParlorPresentationState(), out error);
    }

    public bool TryImport(GmParlorPresentationState state, out string error)
    {
        if (state == null)
        {
            error = "Parlor presentation state cannot be null";
            return false;
        }
        if (!state.TryValidate(out error)) return false;
        facts.Clear();
        recorded.Clear();
        for (int index = 0; index < state.observedFacts.Count; index++)
        {
            GmParlorObservedFactData item = state.observedFacts[index];
            var key = new FactKey(item.commandId, item.fact);
            recorded.Add(key);
            facts.Add(new GmParlorEvidenceFact(item.commandId, item.fact, TextFor(item.fact)));
        }
        LastPersistenceError = string.Empty;
        OnChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    public void Clear()
    {
        if (DurablePersistenceEnabled && !TryPersist(GmParlorPresentationState.Empty())) return;
        facts.Clear();
        recorded.Clear();
        OnChanged?.Invoke();
    }

    bool TryPersist(GmParlorPresentationState state)
    {
        if (!GmRunStore.TryCreateParlorPresentationSaveData(state, out GmSaveData candidate,
            out string error))
        {
            LastPersistenceError = error;
            return false;
        }
        if (!GmSaveSystem.QueueSaveData(candidate, out _) || !GmSaveSystem.Flush())
        {
            LastPersistenceError = GmSaveSystem.LastError;
            return false;
        }
        GmRunStore.CommitParlorPresentationSaveData(candidate);
        LastPersistenceError = string.Empty;
        return true;
    }

    static string TextFor(GmParlorObservedFact fact)
    {
        return fact switch
        {
            GmParlorObservedFact.RightHandPausedAboveDeck =>
                "His right hand stopped above the deck.",
            GmParlorObservedFact.CardContactBroke =>
                "The card left the baize, then touched it again.",
            GmParlorObservedFact.SleeveBrushedTable =>
                "His sleeve brushed the table edge.",
            GmParlorObservedFact.CardPlacedWithoutPause =>
                "He set the card down without pausing.",
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact, null),
        };
    }

    readonly struct FactKey : IEquatable<FactKey>
    {
        readonly ulong commandId;
        readonly GmParlorObservedFact fact;

        public FactKey(ulong commandId, GmParlorObservedFact fact)
        {
            this.commandId = commandId;
            this.fact = fact;
        }

        public bool Equals(FactKey other) => commandId == other.commandId && fact == other.fact;
        public override bool Equals(object obj) => obj is FactKey other && Equals(other);
        public override int GetHashCode() => unchecked((commandId.GetHashCode() * 397) ^ (int)fact);
    }
}
