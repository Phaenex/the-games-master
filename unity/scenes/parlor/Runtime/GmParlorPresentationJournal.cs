using System;
using System.Collections.Generic;

/// <summary>
/// Converts one canonical Parlor transition into stable semantic presentation commands. This is a
/// journal, not an authority: unsupported state pairs fail closed instead of guessing what moved.
/// </summary>
public static class GmParlorPresentationJournal
{
    public static GmParlorPresentationCommand[] Build(GmParlorMatchSnapshot before,
        GmParlorMatchSnapshot after)
    {
        if (!TryBuild(before, after, out GmParlorPresentationCommand[] commands, out string error))
            throw new InvalidOperationException(error);
        return commands;
    }

    public static bool TryBuild(GmParlorMatchSnapshot before, GmParlorMatchSnapshot after,
        out GmParlorPresentationCommand[] commands, out string error)
    {
        commands = Array.Empty<GmParlorPresentationCommand>();
        if (before == null || after == null)
            return Fail("Parlor presentation snapshots cannot be null", out error);
        if (before.version != GmParlorMatchSnapshot.CurrentVersion ||
            after.version != GmParlorMatchSnapshot.CurrentVersion)
            return Fail("Parlor presentation snapshots must use the current version", out error);

        var built = new List<GmParlorPresentationCommand>(3);
        int ordinal = 0;
        if (before.phase == after.phase)
        {
            error = string.Empty;
            return true;
        }

        switch (before.phase)
        {
            case GmParlorMatchPhase.NotStarted when after.phase == GmParlorMatchPhase.PlayerLeads:
                Add(built, after, ref ordinal, GmParlorPresentationAction.BeginRound);
                break;

            case GmParlorMatchPhase.PlayerLeads when
                after.phase == GmParlorMatchPhase.AwaitingAldricJudgement:
                if (!after.hasCurrentLeadCard || !after.hasCurrentFollowCard)
                    return MissingTableCards(before, after, out error);
                Add(built, after, ref ordinal, GmParlorPresentationAction.PlayerCardToLead,
                    after.currentLeadCard);
                Add(built, after, ref ordinal, GmParlorPresentationAction.AldricCardToFollow,
                    after.currentFollowCard);
                Add(built, after, ref ordinal, GmParlorPresentationAction.OpenJudgement,
                    observation: after.tellObservation);
                break;

            case GmParlorMatchPhase.PlayerFollowsAldricLead when
                after.phase == GmParlorMatchPhase.TrickResult:
                if (!after.hasCurrentFollowCard)
                    return MissingTableCards(before, after, out error);
                Add(built, after, ref ordinal, GmParlorPresentationAction.PlayerCardToFollow,
                    after.currentFollowCard);
                Add(built, after, ref ordinal, GmParlorPresentationAction.ResolveTrick,
                    owner: after.lastTrickWinner);
                break;

            case GmParlorMatchPhase.AwaitingAldricJudgement when
                after.phase == GmParlorMatchPhase.TrickResult:
                Add(built, after, ref ordinal, GmParlorPresentationAction.ResolveTrick,
                    owner: after.lastTrickWinner);
                break;

            case GmParlorMatchPhase.TrickResult when
                after.phase == GmParlorMatchPhase.PlayerLeads:
                Add(built, after, ref ordinal, GmParlorPresentationAction.ClearTable);
                break;

            case GmParlorMatchPhase.TrickResult when
                after.phase == GmParlorMatchPhase.PlayerFollowsAldricLead:
                if (!after.hasCurrentLeadCard)
                    return MissingTableCards(before, after, out error);
                Add(built, after, ref ordinal, GmParlorPresentationAction.ClearTable);
                Add(built, after, ref ordinal, GmParlorPresentationAction.AldricCardToLead,
                    after.currentLeadCard);
                break;

            case GmParlorMatchPhase.TrickResult when
                after.phase == GmParlorMatchPhase.RoundResult:
                Add(built, after, ref ordinal, GmParlorPresentationAction.ResolveRound,
                    owner: after.roundWinner);
                break;

            case GmParlorMatchPhase.RoundResult when
                after.phase == GmParlorMatchPhase.PlayerLeads:
                Add(built, after, ref ordinal, GmParlorPresentationAction.BeginRound);
                break;

            case GmParlorMatchPhase.RoundResult when
                after.phase == GmParlorMatchPhase.MatchResult:
                Add(built, after, ref ordinal, GmParlorPresentationAction.ResolveMatch,
                    owner: after.matchWinner);
                break;

            case GmParlorMatchPhase.MatchResult when
                after.phase == GmParlorMatchPhase.PlayerLeads:
                Add(built, after, ref ordinal, GmParlorPresentationAction.BeginRematch);
                break;

            default:
                return Fail($"Unsupported Parlor presentation transition: {before.phase} -> {after.phase}",
                    out error);
        }

        commands = built.ToArray();
        error = string.Empty;
        return true;
    }

    public static GmParlorPresentationCommand SnapToCanonicalState(GmParlorMatchSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        int ordinal = 0;
        var commands = new List<GmParlorPresentationCommand>(1);
        Add(commands, snapshot, ref ordinal, GmParlorPresentationAction.SnapToCanonicalState);
        return commands[0];
    }

    public static GmParlorPresentationCommand RestoreOpenJudgement(
        GmParlorMatchSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.version != GmParlorMatchSnapshot.CurrentVersion ||
            snapshot.phase != GmParlorMatchPhase.AwaitingAldricJudgement ||
            !snapshot.hasCurrentLeadCard || !snapshot.hasCurrentFollowCard)
            throw new ArgumentException(
                "Restore judgement requires a current canonical judgement snapshot",
                nameof(snapshot));
        return new GmParlorPresentationCommand(
            StableId(snapshot, GmParlorPresentationAction.OpenJudgement, 2, null,
                GmTrickOwner.Player, snapshot.tellObservation),
            GmParlorPresentationAction.OpenJudgement, null, GmTrickOwner.Player,
            snapshot.tellObservation);
    }

    static void Add(List<GmParlorPresentationCommand> commands, GmParlorMatchSnapshot after,
        ref int ordinal, GmParlorPresentationAction action, GmCard? card = null,
        GmTrickOwner owner = GmTrickOwner.Player,
        GmTellObservation observation = GmTellObservation.Calm)
    {
        ulong id = StableId(after, action, ordinal, card, owner, observation);
        ordinal++;
        commands.Add(new GmParlorPresentationCommand(id, action, card, owner, observation));
    }

    static ulong StableId(GmParlorMatchSnapshot snapshot, GmParlorPresentationAction action,
        int ordinal, GmCard? card, GmTrickOwner owner, GmTellObservation observation)
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        ulong hash = Offset;
        Mix(ref hash, Prime, unchecked((ulong)snapshot.version));
        Mix(ref hash, Prime, unchecked((ulong)(uint)snapshot.seed));
        Mix(ref hash, Prime, snapshot.outcomeSequence);
        Mix(ref hash, Prime, unchecked((ulong)(uint)snapshot.roundNumber));
        Mix(ref hash, Prime, unchecked((ulong)(uint)snapshot.trickNumber));
        Mix(ref hash, Prime, unchecked((ulong)snapshot.phase));
        Mix(ref hash, Prime, unchecked((ulong)action));
        Mix(ref hash, Prime, unchecked((ulong)(uint)ordinal));
        Mix(ref hash, Prime, unchecked((ulong)owner));
        Mix(ref hash, Prime, unchecked((ulong)observation));
        Mix(ref hash, Prime, card.HasValue ? unchecked((ulong)(uint)((int)card.Value.Suit + 1)) : 0UL);
        Mix(ref hash, Prime, card.HasValue ? unchecked((ulong)(uint)card.Value.Rank) : 0UL);
        return hash == 0 ? 1UL : hash;
    }

    static void Mix(ref ulong hash, ulong prime, ulong value)
    {
        unchecked
        {
            for (int byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            {
                hash ^= (byte)(value >> (byteIndex * 8));
                hash *= prime;
            }
        }
    }

    static bool MissingTableCards(GmParlorMatchSnapshot before, GmParlorMatchSnapshot after,
        out string error)
    {
        return Fail($"Parlor transition {before.phase} -> {after.phase} is missing required table cards",
            out error);
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
