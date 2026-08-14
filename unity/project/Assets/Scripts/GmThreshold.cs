// Threshold Refusal + gate lock, ported from the web build, then rewritten for The Ninth Bell.
// Canon survives: the front doors never open, you never earn the civil doorway. What changes is
// what takes you -- not a hand at the collar, the hour. See
// docs/superpowers/specs/2026-07-17-the-ninth-bell.md.
//
// The porch KO is retired. Reaching the porch is now a BEAT, not an ending: the doors still do
// not open, and the player is simply left standing there while the count continues -- which is
// worse than a knockdown, because nothing happens and the hour arrives anyway.
using UnityEngine;

public class GmThreshold : MonoBehaviour
{
    public float arrivalZ = -36f, gateZ = 65f;
    public string gateAnchorId = "", porchAnchorId = "";
    GmPlayer player;
    GmDesignRuntime rt;
    GmAmbience amb;
    GmBellSummons bell;
    bool porchSaid, gateLocked, crossing;
    GmRouteSpline route;
    GmWorldAnchor gateAnchor, porchAnchor;
    float maxProgress;

    public bool GateLocked => gateLocked;
    public bool Crossing => crossing;

    void Start()
    {
        player = FindFirstObjectByType<GmPlayer>();
        rt = FindFirstObjectByType<GmDesignRuntime>();
        amb = FindFirstObjectByType<GmAmbience>();
        bell = FindFirstObjectByType<GmBellSummons>();
        route = FindFirstObjectByType<GmRouteSpline>();
        gateAnchor = GmWorldAnchor.Find(gateAnchorId);
        porchAnchor = GmWorldAnchor.Find(porchAnchorId);
        if (route != null && player != null) maxProgress = route.ProjectDistance(player.transform.position);
    }

    void Update()
    {
        if (player == null || crossing) return;
        float z = player.transform.position.z;
        bool semantic = route != null && gateAnchor != null && porchAnchor != null;
        float progress = semantic ? route.ProjectDistance(player.transform.position) : 0f;
        if (semantic && progress > maxProgress) maxProgress = progress;

        // Gate lock: crossing the gate house-ward IS the commitment. Arm the bell here -- the house
        // can only count someone it already has, and passing its gate is when it has you. Retreating
        // to the car BEFORE the gate still beats it outright; nothing after this point does.
        //
        // This used to require a backward move after passing (progress < maxProgress - 0.5f), which
        // meant the count only started if the player happened to glance back. A player who did
        // exactly what the invitation said -- walk to the house -- passed the gate, reached the
        // porch, and stood there forever: no slam, no nine tolls, no crossing, no Entry Hall. The
        // whole prologue sat behind an optional look over the shoulder. Nick's call, 2026-08-15.
        //
        // GmSecretEnding already treated this same line as the point of no return (it sets
        // passedGate on exactly this condition and then refuses to fire), so the two systems
        // disagreed about what commits you. They now agree.
        bool lockNow = semantic
            ? maxProgress >= gateAnchor.RouteMetres + 1f
            : z <= gateZ;
        if (!gateLocked && lockNow)
        {
            gateLocked = true;
            amb?.PlayOneShot("gate_slam", 1f);
            Invoke(nameof(LockTick), 0.52f);
            rt?.ShowBeat("Something slammed shut behind me.",
                "When I looked back, the gate was closed — and the lock, somehow, had already turned.");
            GmExperienceTelemetry.Record("gate-lock", "crossed house-ward");
            bell?.Arm();
            FindFirstObjectByType<GmGateLeaves>()?.Close();
        }

        // Reaching the porch is no longer an ending -- it is a discovery. The doors do not open, and
        // the hour arrives anyway, wherever you happen to be standing when it does.
        bool atPorch = semantic ? progress >= porchAnchor.RouteMetres - 3f : z <= arrivalZ;
        if (!porchSaid && atPorch)
        {
            porchSaid = true;
            rt?.ShowBeat("The doors did not open.",
                "Callers waited. Guests were taken. Warm light held in the seam, then went still. I stood there like a man with an appointment, which is what I was.");
            GmExperienceTelemetry.Record("porch-refusal", "doors remain closed");
        }
    }

    void LockTick() => amb?.PlayOneShot("gate_lock", 1f);

    /// Toll 9. Called by GmBellSummons -- the count does not taper off, it lands. Hand straight to
    /// GmCrossing, which owns the cut-to-black, dead air, whispers, iris-in and ninth chime end to
    /// end. This method must NOT draw its own overlay: GmCrossing's fade eases back to 0 when the
    /// sequence resolves, and a second overlay stuck at fade=1 with nothing to ever clear it would
    /// leave the screen black forever underneath whatever GmCrossing renders -- a permanent loading
    /// screen where the reveal is supposed to be. GmCrossing.Run() disables the player itself.
    public void BeginCrossing()
    {
        if (crossing) return;
        crossing = true;
        GmExperienceTelemetry.Record("crossing-begin", "toll-nine");

        var cross = FindFirstObjectByType<GmCrossing>();
        if (cross != null) StartCoroutine(cross.Run());
        else Debug.LogError("[GmThreshold] FAILED: no GmCrossing — toll 9 has nowhere to go");
    }
}
