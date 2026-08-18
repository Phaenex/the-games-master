// The seventh state (story bible §7) -- outside the six endings, which are all reached by sitting
// at the table. This one is reached by never sitting down: turn back and walk all the way back to
// the car before ever crossing the gate, and you leave.
//
// Trigger condition and copy matched against the shipped web build's triggerSecretEnding() /
// checkWalk() ("The Games Master - Prologue.dc.html", ~line 1237-1248 and ~line 1480), not invented:
//   - track the lowest z reached so far (the web build's _minZReached, seeded at spawn)
//   - "moving backward" means the current z has drifted back past that low point by more than 0.5
//     (small tolerance, not every frame's float jitter -- same as the web build)
//   - this fires only if that backward drift happens before the player has ever dropped to gateZ
//     or below (never passed the gate) AND the player has actually made it back within 1 unit of
//     the car (carZ - 1). Requiring the real trip back, not just a few units of drift from spawn,
//     is what stopped this firing by accident in the first seconds of a fresh walk (the web build
//     hit that exact regression in playtest).
//   - once the player has ever passed the gate this exit is closed for good; GmThreshold owns what
//     happens on a retreat after that point (the gate lock), so this script has nothing further to do.
//
// Writes nothing anywhere: no portrait, no ledger line, no shared state. The absence is the point --
// declining the invitation costs nothing and changes nothing for the house, which will simply find
// someone else.
using UnityEngine;

public class GmSecretEnding : MonoBehaviour
{
    public float carZ = 78f, gateZ = 65f;
    public string carAnchorId = "", gateAnchorId = "";
    GmPlayer player;
    GmDesignRuntime rt;
    bool passedGate, fired;
    float minZReached;
    GmRouteSpline route;
    GmWorldAnchor carAnchor, gateAnchor;
    float maxProgress;

    public bool Fired => fired;
    public bool PassedGate => passedGate;

    void Start()
    {
        player = FindAnyObjectByType<GmPlayer>();
        rt = FindAnyObjectByType<GmDesignRuntime>();
        route = FindAnyObjectByType<GmRouteSpline>();
        carAnchor = GmWorldAnchor.Find(carAnchorId);
        gateAnchor = GmWorldAnchor.Find(gateAnchorId);
        if (player != null) minZReached = player.transform.position.z;
    }

    void Update()
    {
        if (player == null || fired) return;
        float z = player.transform.position.z;
        bool semantic = route != null && carAnchor != null && gateAnchor != null;
        float progress = semantic ? route.ProjectDistance(player.transform.position) : 0f;
        if (semantic && progress > maxProgress) maxProgress = progress;

        if (z < minZReached) minZReached = z;
        if (!passedGate && (semantic ? maxProgress >= gateAnchor.RouteMetres + 1f : z <= gateZ))
        { passedGate = true; return; }
        if (passedGate) return;

        bool movingBackward = semantic ? progress < maxProgress - 0.5f : z > minZReached + 0.5f;
        bool atCar = semantic ? progress <= carAnchor.RouteMetres + 1f : z >= carZ - 1f;
        if (!movingBackward || !atCar) return;

        if (GmHousePersistenceCoordinator.IsEnabled)
        {
            if (!GmHousePersistenceCoordinator.TryAbandonActiveRun(out string houseError))
            {
                Debug.LogError($"[GmSecretEnding] could not abandon House run: {houseError}");
                return;
            }
            if (!GmSaveSystem.DeleteSave())
            {
                Debug.LogError($"[GmSecretEnding] could not erase pre-gate Continue save: {GmSaveSystem.LastError}");
                return;
            }
        }

        fired = true;
        player.SetControlBlocked(true);
        rt?.ShowAftermath(
            "I got back in the car.\n" +
            "Whatever was waiting up that drive would have to wait for someone else — there would always be someone else, hungrier or more desperate than I was tonight.\n\n" +
            "The house didn't need me specifically. It only needed someone to say yes.\n\n" +
            "— you left —\n" +
            "the road only turns one way.");
        Debug.Log("[GmSecretEnding] fired — left before the gate, nothing written");
        GmExperienceTelemetry.Record("secret-ending", "left before the gate");
    }
}
