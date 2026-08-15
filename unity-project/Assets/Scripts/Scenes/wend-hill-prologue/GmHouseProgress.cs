using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run state shared by the house chapter and every later game.
///
/// The model here was already complete; what was missing was surviving a scene load. This component
/// used to own its fields directly, so the moment the Prologue handed off to another scene the run
/// reset — that is what "cheatsCaught is Parlor-local" meant in the tracker, and why Court's and
/// Shut the Box's catches could not count toward the true ending at all.
///
/// The state now lives in GmRunStore and this component is a view over it. It used to live in a
/// private static store of its own, which did survive a scene load but was invisible to everything
/// that scores the run: GmEndingManager reads GmRunStore, GmSaveSystem persists GmRunStore, and the
/// later rooms write to GmRunStore. So the house chapter kept a faithful tally that no ending ever
/// saw — Shard #1 and every Parlor catch dropped out of the true-ending maths. One store, written
/// here, read there. Deliberately NOT DontDestroyOnLoad: that would drag the whole HouseBeginning
/// root — walls, lights, the Parlor — into every later scene. A static survives scene loads without
/// carrying geometry with it.
/// </summary>
public sealed class GmHouseProgress : MonoBehaviour
{
    /// The house chapter counts sanity 0-100 and the store keeps it 0-1. These are the same amounts
    /// the component applied inline before it delegated; only the scale they are expressed in moved.
    const float SanityPerCatch = 0.05f;
    const float SanityPerMiss = -0.02f;
    const float SanityPerFalseRead = -0.10f;

    /// Starts a fresh run. The ONLY thing that clears the state — a scene transition must not.
    public static void BeginNewRun()
    {
        GmRunStore.BeginNewRun();
        Debug.Log("[GmHouseProgress] new run: state cleared deliberately");
    }

    public int Sanity => SanityTotal;
    public int CorruptionTier => CorruptionTierTotal;
    public int Defiance => DefianceTotal;
    public int Compliance => ComplianceTotal;
    public int CheatsCaught => CheatsCaughtTotal;
    public int MirrorShards => MirrorShardsTotal;
    public int ClueCount => GmRunStore.DiscoveredCluesCount;
    public IEnumerable<string> Clues => GmRunStore.DiscoveredClues;

    /// Static mirrors so a scene with no GmHouseProgress component can still read the run. Court and
    /// Shut the Box need the tally without inheriting the house's hierarchy to get it.
    public static int CheatsCaughtTotal => GmRunStore.CheatsCaughtCount;
    public static int MirrorShardsTotal => GmRunStore.ShardsCount;
    public static int SanityTotal => Mathf.RoundToInt(GmRunStore.Sanity * 100f);
    public static int DefianceTotal => GmRunStore.DefianceCount;
    public static int ComplianceTotal => GmRunStore.ComplianceCount;

    /// Tier 1 is the floor: he is already cheating when you meet him, never innocent. The ceiling
    /// keeps "high corruption" a reachable state rather than an ever-climbing number, because
    /// content gates off it — Shut the Box's tile-9 tampering only appears near the top.
    ///
    /// This ceiling is the house chapter's, and it sits one below the store's: GmRunStore allows
    /// Tier 5, which is what GmEndingManager reads as the Corrupted Host ending. The house cannot
    /// push a run into that ending on its own — Parlor, Court, and Shut the Box call
    /// GmRunStore.RaiseCorruption directly, uncapped by this constant, and are what carry a run the
    /// rest of the way. Confirmed intentional pacing, not a merge artefact (Nick, 2026-08-14): the
    /// House alone must never end a run in Corrupted Host.
    public const int MinCorruptionTier = GmRunStore.MinCorruptionTier;
    public const int MaxCorruptionTier = 4;

    /// The mechanism only. WHAT raises the tier is a pacing decision that shapes all seven games
    /// and is deliberately not decided here: canon fixes the floor, the ceiling, and that frequency
    /// and visibility climb with it, but not the schedule. Callers pass a reason so the escalation
    /// is auditable once that schedule exists.
    public static bool RaiseCorruption(string reason)
    {
        if (GmRunStore.CorruptionTier >= MaxCorruptionTier) return false;
        GmRunStore.RaiseCorruption(reason);
        GmExperienceTelemetry.Record("corruption", $"{GmRunStore.CorruptionTier}:{reason}");
        Debug.Log($"[GmHouseProgress] corruption tier {GmRunStore.CorruptionTier} ({reason})");
        return true;
    }

    public static int CorruptionTierTotal => GmRunStore.CorruptionTier;

    public bool HasClue(string id) => GmRunStore.HasClue(id);

    public bool Discover(string id)
    {
        if (!GmRunStore.RecordClue(id)) return false;
        GmExperienceTelemetry.Record("clue", id);
        Debug.Log($"[GmHouseProgress] clue '{id}' ({GmRunStore.DiscoveredCluesCount} total)");
        return true;
    }

    public void FindShard(string id)
    {
        if (!Discover(id)) return;
        int slot = ShardSlotFor(id);
        // A slot already filled means the shard was banked in an earlier session or by the room that
        // owns it; the defiance that comes with it must not be banked a second time either.
        if (slot < 0 || !GmRunStore.CollectShard(slot)) return;
        GmRunStore.RecordDefiance();
    }

    /// Shard ids carry their own 1-based number — "mirror-shard-01" is Shard #1, slot 0. Court takes
    /// slot 1 and the hidden room slot 2 by calling GmRunStore.CollectShard directly, so the three
    /// never contend for the same slot. An unnumbered id takes the lowest free slot and says so in
    /// the log, rather than silently dropping a shard the player really did pick up.
    static int ShardSlotFor(string id)
    {
        int end = id.Length;
        while (end > 0 && !char.IsDigit(id[end - 1])) end--;
        int start = end;
        while (start > 0 && char.IsDigit(id[start - 1])) start--;

        if (end > start && int.TryParse(id.Substring(start, end - start), out int number))
        {
            int slot = number - 1;
            if (slot >= 0 && slot < GmRunStore.MirrorShards.Count) return slot;
        }

        for (int slot = 0; slot < GmRunStore.MirrorShards.Count; slot++)
        {
            if (GmRunStore.HasShard(slot)) continue;
            Debug.LogWarning($"[GmHouseProgress] shard id '{id}' carries no shard number; taking slot {slot}");
            return slot;
        }

        Debug.LogWarning($"[GmHouseProgress] shard id '{id}' arrived with all three slots already filled");
        return -1;
    }

    /// Clue-keyed, so the same catch cannot be banked twice by re-entering a room. The true ending
    /// needs 8+ genuine catches; a re-readable one would make that threshold meaningless. The store
    /// is keyed the same way, and is asked too: a run reloaded from disk brings its catches back
    /// without its scene state, so the clue ledger alone is not enough to stop a second banking.
    public void CatchCheat(string id)
    {
        if (!Discover(id)) return;
        if (!GmRunStore.RecordCatch(id)) return;
        GmRunStore.RecordDefiance();
        GmRunStore.ApplySanityDelta(SanityPerCatch);
    }

    public void MissCheat()
    {
        GmRunStore.RecordCompliance();
        GmRunStore.ApplySanityDelta(SanityPerMiss);
    }

    public void FalseRead()
    {
        GmRunStore.RecordCompliance();
        GmRunStore.ApplySanityDelta(SanityPerFalseRead);
    }
}
