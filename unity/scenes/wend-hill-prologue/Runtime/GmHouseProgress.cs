using System.Collections.Generic;
using UnityEngine;

/// <summary>Session state shared by the house chapter and every later game.</summary>
public sealed class GmHouseProgress : MonoBehaviour
{
    readonly HashSet<string> clues = new HashSet<string>();

    public int Sanity { get; private set; } = 100;
    public int CorruptionTier { get; private set; } = 1;
    public int Defiance { get; private set; }
    public int Compliance { get; private set; }
    public int CheatsCaught { get; private set; }
    public int MirrorShards { get; private set; }
    public int ClueCount => clues.Count;
    public IEnumerable<string> Clues => clues;

    public bool HasClue(string id) => !string.IsNullOrWhiteSpace(id) && clues.Contains(id);

    public bool Discover(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !clues.Add(id)) return false;
        GmExperienceTelemetry.Record("clue", id);
        Debug.Log($"[GmHouseProgress] clue '{id}' ({clues.Count} total)");
        return true;
    }

    public void FindShard(string id)
    {
        if (!Discover(id)) return;
        MirrorShards++;
        Defiance++;
    }

    public void CatchCheat(string id)
    {
        if (!Discover(id)) return;
        CheatsCaught++;
        Defiance++;
        Sanity = Mathf.Min(100, Sanity + 5);
    }

    public void MissCheat()
    {
        Compliance++;
        Sanity = Mathf.Max(0, Sanity - 2);
    }

    public void FalseRead()
    {
        Compliance++;
        Sanity = Mathf.Max(0, Sanity - 10);
    }
}
