// A clue inside an enterable outbuilding. Banks a persistent discovery via GmRunStore the first
// time it's read; each successive discovery in the same building costs more than the last, so
// restraint is a real, legible choice rather than a flat cost per line. See
// docs/superpowers/specs/2026-08-13-the-reckoning.md.
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GmInteractable))]
public class GmOutbuildingClue : MonoBehaviour, IGmInteractionReceiver
{
    [SerializeField] string clueId;
    [SerializeField] string buildingId;

    static readonly Dictionary<string, int> discoveriesPerBuilding = new Dictionary<string, int>();

    public void Configure(string clue, string building)
    {
        clueId = clue;
        buildingId = building ?? "";
    }

    public void OnGmInteraction(GmInteractable source)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return;
        if (!GmRunStore.RecordClue(clueId)) return; // already banked; no repeat charge for re-reading
        GmSaveSystem.Save();
        GmExperienceTelemetry.Record("outbuilding-clue", clueId);

        int priorDiscoveries = discoveriesPerBuilding.TryGetValue(buildingId, out int n) ? n : 0;
        discoveriesPerBuilding[buildingId] = priorDiscoveries + 1;

        GmGroundsExploration exploration = FindFirstObjectByType<GmGroundsExploration>();
        if (exploration == null) return;
        GmFeelConfig cfg = GmFeelConfig.Active;
        float escalation = Mathf.Pow(Mathf.Max(1f, cfg.reckoningDiscoveryEscalation), priorDiscoveries);
        exploration.AddOwed(cfg.reckoningWeightOutbuildingDiscoveryBase * escalation);
    }

    /// Test-only: the per-building discovery count is a static run-lifetime counter and would
    /// otherwise leak between edit-mode test cases.
    public static void ResetRegistryForTests() => discoveriesPerBuilding.Clear();
}
