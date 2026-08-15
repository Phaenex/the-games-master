using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cross-scene run state store. Manages catches, corruption, sanity, and ending metrics across
/// all seven game rooms. Persists in static memory without requiring DontDestroyOnLoad hierarchy drags.
/// </summary>
public static class GmRunStore
{
    public const int MinCorruptionTier = 1;
    public const int MaxCorruptionTier = 5;

    public static event Action OnStateChanged;

    static readonly HashSet<string> cheatsCaught = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly bool[] mirrorShards = new bool[3];

    // Everything the player has learned, catches included. Kept apart from cheatsCaught because the
    // true ending counts catches and only catches: a ledger line or a shard is progress, not a catch,
    // and folding the two sets together would inflate the 8-catch threshold with reading material.
    static readonly HashSet<string> discoveredClues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static int CorruptionTier { get; private set; } = MinCorruptionTier;
    public static float Sanity { get; private set; } = 1.0f;
    public static int Defiance { get; private set; } = 0;
    public static int Compliance { get; private set; } = 0;
    public static string CurrentSceneId { get; set; } = "wend-hill-prologue";
    public static string LastCheckpoint { get; set; } = "spawn";

    public static int CheatsCaughtCount => cheatsCaught.Count;
    public static IReadOnlyCollection<string> CheatsCaught => cheatsCaught;
    public static int DiscoveredCluesCount => discoveredClues.Count;
    public static IReadOnlyCollection<string> DiscoveredClues => discoveredClues;
    public static IReadOnlyList<bool> MirrorShards => mirrorShards;
    public static int DefianceCount => Defiance;
    public static int ComplianceCount => Compliance;
    public static bool AllShardsCollected => ShardsCount >= 3;

    public static int ShardsCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < mirrorShards.Length; i++)
                if (mirrorShards[i]) count++;
            return count;
        }
    }

    public static bool HasShard(int shardIndex)
    {
        if (shardIndex < 0 || shardIndex >= mirrorShards.Length) return false;
        return mirrorShards[shardIndex];
    }

    public static bool CollectShard(int shardIndex)
    {
        if (shardIndex < 0 || shardIndex >= mirrorShards.Length) return false;
        if (mirrorShards[shardIndex]) return false;
        mirrorShards[shardIndex] = true;
        OnStateChanged?.Invoke();
        return true;
    }

    public static bool RecordCatch(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        bool added = cheatsCaught.Add(clueId.Trim());
        if (added) OnStateChanged?.Invoke();
        return added;
    }

    public static bool HasClue(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        return discoveredClues.Contains(clueId.Trim());
    }

    public static bool RecordClue(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        bool added = discoveredClues.Add(clueId.Trim());
        if (added) OnStateChanged?.Invoke();
        return added;
    }

    public static void RecordMiss()
    {
        // ApplySanityDelta raises OnStateChanged itself; a second invoke here would double-fire
        // every subscriber for one miss.
        ApplySanityDelta(-0.05f);
    }

    public static void RaiseCorruption(string reason)
    {
        if (CorruptionTier < MaxCorruptionTier)
        {
            CorruptionTier++;
            Debug.Log($"[GmRunStore] Corruption raised to Tier {CorruptionTier} ({reason})");
            OnStateChanged?.Invoke();
        }
    }

    public static void LowerCorruption(string reason)
    {
        if (CorruptionTier > MinCorruptionTier)
        {
            CorruptionTier--;
            Debug.Log($"[GmRunStore] Corruption lowered to Tier {CorruptionTier} ({reason})");
            OnStateChanged?.Invoke();
        }
    }

    public static void ApplySanityDelta(float delta)
    {
        Sanity = Mathf.Clamp01(Sanity + delta);
        OnStateChanged?.Invoke();
    }

    public static void RecordDefiance()
    {
        Defiance++;
        OnStateChanged?.Invoke();
    }

    public static void RecordCompliance()
    {
        Compliance++;
        OnStateChanged?.Invoke();
    }

    public static void BeginNewRun()
    {
        cheatsCaught.Clear();
        discoveredClues.Clear();
        for (int i = 0; i < mirrorShards.Length; i++) mirrorShards[i] = false;
        CorruptionTier = MinCorruptionTier;
        Sanity = 1.0f;
        Defiance = 0;
        Compliance = 0;
        CurrentSceneId = "wend-hill-prologue";
        LastCheckpoint = "spawn";
        OnStateChanged?.Invoke();
        Debug.Log("[GmRunStore] Began fresh run (Tier 1 floor, full sanity, 0 catches, 0 shards)");
    }

    public static GmSaveData ToSaveData()
    {
        return new GmSaveData
        {
            corruptionTier = CorruptionTier,
            sanity = Sanity,
            defiance = Defiance,
            compliance = Compliance,
            cheatsCaught = new List<string>(cheatsCaught),
            discoveredClues = new List<string>(discoveredClues),
            mirrorShards = new List<bool>(mirrorShards),
            currentSceneId = CurrentSceneId,
            lastCheckpoint = LastCheckpoint,
            timestampUtc = DateTime.UtcNow.ToString("o")
        };
    }

    public static void LoadFromSaveData(GmSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        cheatsCaught.Clear();
        if (data.cheatsCaught != null)
        {
            foreach (string clue in data.cheatsCaught)
                if (!string.IsNullOrWhiteSpace(clue)) cheatsCaught.Add(clue.Trim());
        }

        discoveredClues.Clear();
        if (data.discoveredClues != null)
        {
            foreach (string clue in data.discoveredClues)
                if (!string.IsNullOrWhiteSpace(clue)) discoveredClues.Add(clue.Trim());
        }

        for (int i = 0; i < mirrorShards.Length; i++)
            mirrorShards[i] = data.mirrorShards != null && i < data.mirrorShards.Count && data.mirrorShards[i];

        CorruptionTier = Mathf.Clamp(data.corruptionTier, MinCorruptionTier, MaxCorruptionTier);
        Sanity = Mathf.Clamp01(data.sanity);
        Defiance = Mathf.Max(0, data.defiance);
        Compliance = Mathf.Max(0, data.compliance);
        CurrentSceneId = !string.IsNullOrEmpty(data.currentSceneId) ? data.currentSceneId : "wend-hill-prologue";
        LastCheckpoint = !string.IsNullOrEmpty(data.lastCheckpoint) ? data.lastCheckpoint : "spawn";
        OnStateChanged?.Invoke();
    }
}

[Serializable]
public sealed class GmSaveData
{
    public int corruptionTier = 1;
    public float sanity = 1.0f;
    public int defiance = 0;
    public int compliance = 0;
    public List<string> cheatsCaught = new List<string>();
    public List<string> discoveredClues = new List<string>();
    public List<bool> mirrorShards = new List<bool> { false, false, false };
    public string currentSceneId = "wend-hill-prologue";
    public string lastCheckpoint = "spawn";
    public string timestampUtc = "";
}
