using System;

public enum GmParlorPresentationAction
{
    PlayerCardToLead,
    PlayerCardToFollow,
    AldricCardToLead,
    AldricCardToFollow,
    OpenJudgement,
    ClearTable,
    ResolveTrick,
    ResolveRound,
    ResolveMatch,
    BeginRound,
    BeginRematch,
    SnapToCanonicalState,
}

/// <summary>
/// A renderer-independent instruction describing what changed at the Parlor table. It deliberately
/// carries no clip, transform, duration, probability or hidden-cheat field. Presentation code is
/// free to interpret the instruction, but only the canonical match can decide that it exists.
/// </summary>
public readonly struct GmParlorPresentationCommand : IEquatable<GmParlorPresentationCommand>
{
    public readonly ulong Id;
    public readonly GmParlorPresentationAction Action;
    public readonly GmCard? Card;
    public readonly GmTrickOwner Owner;
    public readonly GmTellObservation Observation;

    internal GmParlorPresentationCommand(ulong id, GmParlorPresentationAction action,
        GmCard? card, GmTrickOwner owner, GmTellObservation observation)
    {
        Id = id;
        Action = action;
        Card = card;
        Owner = owner;
        Observation = observation;
    }

    public bool Equals(GmParlorPresentationCommand other)
    {
        return Id == other.Id && Action == other.Action && Nullable.Equals(Card, other.Card) &&
            Owner == other.Owner && Observation == other.Observation;
    }

    public override bool Equals(object obj)
    {
        return obj is GmParlorPresentationCommand other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Id.GetHashCode();
            hash = (hash * 397) ^ (int)Action;
            hash = (hash * 397) ^ (Card.HasValue ? Card.Value.GetHashCode() : 0);
            hash = (hash * 397) ^ (int)Owner;
            return (hash * 397) ^ (int)Observation;
        }
    }

    public static bool operator ==(GmParlorPresentationCommand left,
        GmParlorPresentationCommand right) => left.Equals(right);

    public static bool operator !=(GmParlorPresentationCommand left,
        GmParlorPresentationCommand right) => !left.Equals(right);
}
