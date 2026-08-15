// The pressure the grounds put on the ninth bell's cadence. Owns exactly one number the bell
// reads: how much the player has explored, monotonically. Nothing lowers it -- leaving a building
// doesn't give time back, and there is no cure. See
// docs/superpowers/specs/2026-08-13-the-reckoning.md.
//
// Free signals only, in this phase: the 7 authored branch rects and the 13 grounds POIs, both
// already shipping. Polls GmDesignRuntime.branchBeats[].fired and GmInteractable.Uses -- existing
// public state -- rather than adding new event wiring to files this pass doesn't own.
using System.Collections.Generic;
using UnityEngine;

public class GmGroundsExploration : MonoBehaviour
{
    float owed;
    public float Owed => owed;
    public float Pressure { get; private set; }

    GmDesignRuntime rt;
    readonly List<GmInteractable> interactables = new List<GmInteractable>();
    readonly HashSet<GmDesignRuntime.BranchBeat> countedBranches = new HashSet<GmDesignRuntime.BranchBeat>();
    readonly HashSet<GmInteractable> countedExamines = new HashSet<GmInteractable>();

    void Start()
    {
        rt = FindFirstObjectByType<GmDesignRuntime>();
        interactables.AddRange(FindObjectsByType<GmInteractable>(FindObjectsSortMode.None));
    }

    void Update()
    {
        GmFeelConfig cfg = GmFeelConfig.Active;

        if (rt != null)
        {
            foreach (GmDesignRuntime.BranchBeat beat in rt.branchBeats)
            {
                if (!beat.fired || countedBranches.Contains(beat)) continue;
                countedBranches.Add(beat);
                AddOwed(cfg.reckoningWeightBranchEntered);
            }
        }

        foreach (GmInteractable interactable in interactables)
        {
            if (interactable.Uses <= 0 || countedExamines.Contains(interactable)) continue;
            countedExamines.Add(interactable);
            AddOwed(cfg.reckoningWeightPoiExamined);
        }

        Pressure = cfg.reckoningOwedForFullPressure > 0f
            ? Mathf.Clamp01(owed / cfg.reckoningOwedForFullPressure)
            : 0f;
    }

    /// Monotonic by construction: rejects any delta that would lower Owed (negative or NaN), so
    /// nothing the player does can ever buy time back once spent.
    public void AddOwed(float delta)
    {
        if (float.IsNaN(delta) || delta < 0f) return;
        owed += delta;
    }
}
