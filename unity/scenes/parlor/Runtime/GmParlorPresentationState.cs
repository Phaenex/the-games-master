using System;
using System.Collections.Generic;

[Serializable]
public sealed class GmParlorObservedFactData
{
    public ulong commandId;
    public GmParlorObservedFact fact;

    public GmParlorObservedFactData() { }

    public GmParlorObservedFactData(ulong commandId, GmParlorObservedFact fact)
    {
        this.commandId = commandId;
        this.fact = fact;
    }

    public GmParlorObservedFactData DeepCopy() => new GmParlorObservedFactData(commandId, fact);
}

/// <summary>
/// Versioned player-visible Parlor memory. It is deliberately separate from canonical match truth,
/// RNG, House Memory, and accessibility preferences.
/// </summary>
[Serializable]
public sealed class GmParlorPresentationState
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public bool hasMatchIdentity;
    public int matchSeed;
    public ulong matchOutcomeNamespace;
    public List<GmParlorObservedFactData> observedFacts = new List<GmParlorObservedFactData>();

    public static GmParlorPresentationState Empty() => new GmParlorPresentationState();

    public GmParlorPresentationState DeepCopy()
    {
        var copy = Empty();
        copy.version = version;
        copy.hasMatchIdentity = hasMatchIdentity;
        copy.matchSeed = matchSeed;
        copy.matchOutcomeNamespace = matchOutcomeNamespace;
        if (observedFacts == null)
        {
            copy.observedFacts = null;
            return copy;
        }
        copy.observedFacts.Clear();
        for (int index = 0; index < observedFacts.Count; index++)
            copy.observedFacts.Add(observedFacts[index]?.DeepCopy());
        return copy;
    }

    public bool TryValidate(out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"unsupported Parlor presentation version {version}";
            return false;
        }
        if (observedFacts == null)
        {
            error = "Parlor observed facts cannot be null";
            return false;
        }
        if (observedFacts.Count > GmParlorEvidenceLog.Capacity)
        {
            error = $"Parlor observed facts exceed capacity {GmParlorEvidenceLog.Capacity}";
            return false;
        }

        var keys = new HashSet<FactKey>();
        var commandFacts = new Dictionary<ulong, HashSet<GmParlorObservedFact>>();
        for (int index = 0; index < observedFacts.Count; index++)
        {
            GmParlorObservedFactData item = observedFacts[index];
            if (item == null)
            {
                error = $"Parlor observed fact {index} is null";
                return false;
            }
            if (item.commandId == 0)
            {
                error = $"Parlor observed fact {index} has an empty command id";
                return false;
            }
            if (!Enum.IsDefined(typeof(GmParlorObservedFact), item.fact))
            {
                error = $"Parlor observed fact {index} has invalid enum value {(int)item.fact}";
                return false;
            }
            if (!keys.Add(new FactKey(item.commandId, item.fact)))
            {
                error = $"duplicate Parlor observed fact {item.commandId}/{item.fact}";
                return false;
            }
            if (!commandFacts.TryGetValue(item.commandId,
                out HashSet<GmParlorObservedFact> facts))
            {
                facts = new HashSet<GmParlorObservedFact>();
                commandFacts.Add(item.commandId, facts);
            }
            facts.Add(item.fact);
        }
        foreach (KeyValuePair<ulong, HashSet<GmParlorObservedFact>> group in commandFacts)
        {
            bool calm = group.Value.SetEquals(new[]
            {
                GmParlorObservedFact.CardPlacedWithoutPause,
            });
            bool suspicious = group.Value.SetEquals(new[]
            {
                GmParlorObservedFact.RightHandPausedAboveDeck,
                GmParlorObservedFact.CardContactBroke,
            });
            if (calm || suspicious) continue;
            bool mixesCalm = group.Value.Contains(GmParlorObservedFact.CardPlacedWithoutPause);
            error = mixesCalm
                ? $"mixed calm and suspicious Parlor facts for command {group.Key}"
                : $"partial or unsupported Parlor fact set for command {group.Key}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void BindTo(int seed, ulong outcomeNamespace)
    {
        hasMatchIdentity = true;
        matchSeed = seed;
        matchOutcomeNamespace = outcomeNamespace;
    }

    public bool Matches(int seed, ulong outcomeNamespace) => hasMatchIdentity &&
        matchSeed == seed && matchOutcomeNamespace == outcomeNamespace;

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
