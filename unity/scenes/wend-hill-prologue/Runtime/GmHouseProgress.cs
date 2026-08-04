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
/// The state now lives in a static store and this component is a view over it. Deliberately NOT
/// DontDestroyOnLoad: that would drag the whole HouseBeginning root — walls, lights, the Parlor —
/// into every later scene. A static survives scene loads without carrying geometry with it.
/// </summary>
public sealed class GmHouseProgress : MonoBehaviour
{
    /// One run's accumulated state. Cleared explicitly at new-game, never implicitly by a scene load.
    sealed class Run
    {
        public readonly HashSet<string> Clues = new HashSet<string>();
        public int Sanity = 100;
        public int CorruptionTier = 1;
        public int Defiance;
        public int Compliance;
        public int CheatsCaught;
        public int MirrorShards;
    }

    static Run run = new Run();

    /// Starts a fresh run. The ONLY thing that clears the state — a scene transition must not.
    public static void BeginNewRun()
    {
        run = new Run();
        Debug.Log("[GmHouseProgress] new run: state cleared deliberately");
    }

    public int Sanity => run.Sanity;
    public int CorruptionTier => run.CorruptionTier;
    public int Defiance => run.Defiance;
    public int Compliance => run.Compliance;
    public int CheatsCaught => run.CheatsCaught;
    public int MirrorShards => run.MirrorShards;
    public int ClueCount => run.Clues.Count;
    public IEnumerable<string> Clues => run.Clues;

    /// Static mirrors so a scene with no GmHouseProgress component can still read the run. Court and
    /// Shut the Box need the tally without inheriting the house's hierarchy to get it.
    public static int CheatsCaughtTotal => run.CheatsCaught;
    public static int MirrorShardsTotal => run.MirrorShards;
    public static int SanityTotal => run.Sanity;
    public static int DefianceTotal => run.Defiance;
    public static int ComplianceTotal => run.Compliance;

    /// Tier 1 is the floor: he is already cheating when you meet him, never innocent. The ceiling
    /// keeps "high corruption" a reachable state rather than an ever-climbing number, because
    /// content gates off it — Shut the Box's tile-9 tampering only appears near the top.
    public const int MinCorruptionTier = 1;
    public const int MaxCorruptionTier = 4;

    /// The mechanism only. WHAT raises the tier is a pacing decision that shapes all seven games
    /// and is deliberately not decided here: canon fixes the floor, the ceiling, and that frequency
    /// and visibility climb with it, but not the schedule. Callers pass a reason so the escalation
    /// is auditable once that schedule exists.
    public static bool RaiseCorruption(string reason)
    {
        if (run.CorruptionTier >= MaxCorruptionTier) return false;
        run.CorruptionTier++;
        GmExperienceTelemetry.Record("corruption", $"{run.CorruptionTier}:{reason}");
        Debug.Log($"[GmHouseProgress] corruption tier {run.CorruptionTier} ({reason})");
        return true;
    }

    public static int CorruptionTierTotal => run.CorruptionTier;

    public bool HasClue(string id) => !string.IsNullOrWhiteSpace(id) && run.Clues.Contains(id);

    public bool Discover(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !run.Clues.Add(id)) return false;
        GmExperienceTelemetry.Record("clue", id);
        Debug.Log($"[GmHouseProgress] clue '{id}' ({run.Clues.Count} total)");
        return true;
    }

    public void FindShard(string id)
    {
        if (!Discover(id)) return;
        run.MirrorShards++;
        run.Defiance++;
    }

    /// Clue-keyed, so the same catch cannot be banked twice by re-entering a room. The true ending
    /// needs 8+ genuine catches; a re-readable one would make that threshold meaningless.
    public void CatchCheat(string id)
    {
        if (!Discover(id)) return;
        run.CheatsCaught++;
        run.Defiance++;
        run.Sanity = Mathf.Min(100, run.Sanity + 5);
    }

    public void MissCheat()
    {
        run.Compliance++;
        run.Sanity = Mathf.Max(0, run.Sanity - 2);
    }

    public void FalseRead()
    {
        run.Compliance++;
        run.Sanity = Mathf.Max(0, run.Sanity - 10);
    }
}
