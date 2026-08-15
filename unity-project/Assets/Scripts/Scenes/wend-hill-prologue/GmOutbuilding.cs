// A spatial volume for an enterable outbuilding on the estate grounds. Toggles its interior on/off
// as the player crosses the threshold, banks a persistent "entered" clue on first entry, and feeds
// capped dwell-time pressure into GmGroundsExploration. See
// docs/superpowers/specs/2026-08-13-the-reckoning.md.
//
// Interior visibility is all-or-nothing on enter/exit, the same instrument GmWendRuntimeCulling
// already uses for HouseBeginning -- not distance culling, which is the wrong tool for a volume
// this small and this binary.
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class GmOutbuilding : MonoBehaviour
{
    [SerializeField] string buildingId;
    [SerializeField] GameObject interiorRoot;

    public string BuildingId => buildingId;

    static readonly List<GmOutbuilding> registry = new List<GmOutbuilding>();

    bool playerInside;
    bool enteredOnce;
    float dwellSecondsCharged;
    GmGroundsExploration exploration;

    public void Configure(string id, GameObject interior)
    {
        buildingId = id;
        interiorRoot = interior;
        // Guaranteed at the one call site every real caller (and every test) actually uses, rather
        // than trusting Awake's timing relative to AddComponent across every context this runs in.
        if (!registry.Contains(this)) registry.Add(this);
    }

    void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        if (!registry.Contains(this)) registry.Add(this);
        SetInteriorActive(false);
    }

    void OnDestroy() => registry.Remove(this);

    void Start()
    {
        exploration = FindFirstObjectByType<GmGroundsExploration>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<GmPlayer>() == null) return;
        playerInside = true;
        SetInteriorActive(true);
        if (enteredOnce) return;
        enteredOnce = true;
        GmFeelConfig cfg = GmFeelConfig.Active;
        exploration?.AddOwed(cfg.reckoningWeightOutbuildingEntered);
        GmExperienceTelemetry.Record("outbuilding-enter", buildingId);
        if (GmRunStore.RecordClue($"outbuilding-{buildingId}-entered")) GmSaveSystem.Save();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<GmPlayer>() == null) return;
        playerInside = false;
        SetInteriorActive(false);
    }

    void Update()
    {
        if (!playerInside || exploration == null) return;
        GmFeelConfig cfg = GmFeelConfig.Active;
        if (dwellSecondsCharged >= cfg.reckoningDwellChargeCapSeconds) return;
        float step = Mathf.Min(Time.deltaTime, cfg.reckoningDwellChargeCapSeconds - dwellSecondsCharged);
        dwellSecondsCharged += step;
        exploration.AddOwed(step * cfg.reckoningWeightOutbuildingDwellPerSecond);
    }

    void SetInteriorActive(bool active)
    {
        if (interiorRoot != null) interiorRoot.SetActive(active);
    }

    /// Called by GmCrossing when toll nine lands, so a lit interior never survives into the
    /// crossing -- costing frames, or visible through a gap when the iris opens in the WakeRoom.
    public static void CloseAllForCrossing()
    {
        foreach (GmOutbuilding outbuilding in registry)
            if (outbuilding != null) outbuilding.SetInteriorActive(false);
    }

    /// Test-only: registry accumulates across domain reloads in edit-mode test runs otherwise.
    public static void ResetRegistryForTests() => registry.Clear();
}
