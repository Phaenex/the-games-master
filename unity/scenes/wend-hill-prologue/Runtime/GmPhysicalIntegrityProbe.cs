// Adversarial probe for the defect class Nick found by hand: a barrier that is "locked" in narrative
// state but has no matching collision, so the player simply walks around it.
//
// The estate gate passed every automated check while being bypassable, because every check walked the
// scripted centreline. A barrier is only proven when something actively TRIES to defeat it -- head-on
// and, more importantly, laterally, off the path, where nobody authored geometry because nobody
// expected a player to go there. That is what this probe does.
//
// Read the plane test carefully before trusting a result. Penetration is measured along the barrier's
// OWN forward axis, not world Z: BuildGate orients the gate rig to the route tangent
// (Quaternion.LookRotation(forward)), and the route curves, so a world-Z comparison silently reports
// nonsense the moment a barrier is not axis-aligned. A probe that lies is worse than no probe.
using System.Collections.Generic;
using UnityEngine;

public sealed class GmPhysicalIntegrityProbe : MonoBehaviour
{
    /// Step size for the push. Small enough that a CharacterController cannot tunnel a 0.4m-thick
    /// barrier at the collision-detection Unity gives a Move() sweep.
    public const float StepMetres = 0.2f;

    /// Progress below this per step means the controller is being held by something solid rather
    /// than merely scraping along it.
    const float StalledStepMetres = 0.01f;

    /// Consecutive stalled steps before the push is called blocked. One stalled step can be a single
    /// frame's skin-width settle; four in a row is a wall.
    const int StalledStepsToBlock = 4;

    public struct BypassAttemptResult
    {
        public Vector3 startPosition;
        public Vector3 attemptedDirection;
        public float attemptedDistance;
        public Vector3 finalPosition;
        public bool penetratedBarrier;

        /// Straight-line distance actually covered, which is what separates "stopped by a wall" from
        /// "walked the whole way unobstructed". attemptedDistance alone cannot tell those apart.
        public float travelledDistance;

        /// True when the controller stopped making progress -- something solid held it. A push can
        /// end un-penetrated WITHOUT being blocked (it simply ran out of distance), and calling that
        /// a pass is how a barrier that does not exist gets certified as working.
        public bool blocked;

        /// Name of the collider that held it, or "None" when nothing was touched. Sourced from the
        /// real ControllerColliderHit callback, so a result naming a collider is evidence that
        /// specific object did the stopping.
        /// EDIT-MODE CAVEAT: OnControllerColliderHit is a play-mode message and does not fire in an
        /// EditMode test, so this reads "None" there even when the controller was genuinely stopped.
        /// In EditMode judge blocking by `blocked` and `travelledDistance`; `obstacleHit` is only
        /// evidence in play mode. Reading a bare "None" as "nothing stopped it" would invert the
        /// result -- the estate-house sweep reports exactly that shape while being firmly blocked.
        public string obstacleHit;
    }

    /// Records what the CharacterController actually collided with. Unity only reports this through
    /// the OnControllerColliderHit message, which requires a component on the controller's own
    /// GameObject -- hence this listener rather than a return value.
    sealed class HitListener : MonoBehaviour
    {
        public string LastHit = "None";
        public int HitCount;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider == null) return;
            LastHit = hit.collider.name;
            HitCount++;
        }

        public void Reset() { LastHit = "None"; HitCount = 0; }
    }

    /// <summary>
    /// Pushes a CharacterController along <paramref name="direction"/> and reports whether it got
    /// past the barrier plane.
    /// </summary>
    /// <param name="barrierPoint">Any point on the barrier plane.</param>
    /// <param name="barrierNormal">The barrier's own forward axis -- the direction a player crosses
    /// it. Pass the gate rig's transform.forward, never a world axis.</param>
    public static BypassAttemptResult AttemptBypass(
        CharacterController controller,
        Vector3 start,
        Vector3 direction,
        float distance,
        Vector3 barrierPoint,
        Vector3 barrierNormal)
    {
        var listener = controller.GetComponent<HitListener>()
            ?? controller.gameObject.AddComponent<HitListener>();
        listener.Reset();

        controller.transform.position = start;
        Physics.SyncTransforms();

        Vector3 normal = barrierNormal.normalized;
        // Signed distance from the plane at the start. Crossing means this value changes sign.
        float startSide = Vector3.Dot(start - barrierPoint, normal);

        Vector3 step = direction.normalized * StepMetres;
        float attempted = 0f;
        int stalledSteps = 0;
        bool blocked = false;
        Vector3 previous = controller.transform.position;

        while (attempted < distance)
        {
            controller.Move(step);
            attempted += StepMetres;

            Vector3 current = controller.transform.position;
            if (Vector3.Distance(current, previous) < StalledStepMetres)
            {
                if (++stalledSteps >= StalledStepsToBlock) { blocked = true; break; }
            }
            else stalledSteps = 0;
            previous = current;
        }

        Vector3 finalPos = controller.transform.position;
        float endSide = Vector3.Dot(finalPos - barrierPoint, normal);
        // Penetrated only if it started on one side of the plane and finished on the other. Using a
        // sign change rather than an absolute coordinate keeps this correct for a barrier at any
        // orientation, anywhere in the world.
        bool penetrated = Mathf.Sign(endSide) != Mathf.Sign(startSide) && Mathf.Abs(endSide) > 0.01f;

        return new BypassAttemptResult
        {
            startPosition = start,
            attemptedDirection = direction.normalized,
            attemptedDistance = distance,
            finalPosition = finalPos,
            penetratedBarrier = penetrated,
            travelledDistance = Vector3.Distance(start, finalPos),
            blocked = blocked,
            obstacleHit = listener.LastHit,
        };
    }

    /// <summary>
    /// Walks the controller at a barrier from a fan of lateral offsets. This is the test that matters:
    /// offset 0 is the scripted path everything already covers, and every non-zero offset is a place
    /// a real player wanders and an author never looked.
    /// </summary>
    /// <param name="lateralOffsets">Metres left(-)/right(+) of the barrier centre. Include offsets
    /// BEYOND the barrier's own width -- that is where a bypass lives, and an offset set that stops
    /// at the barrier edge can only ever confirm what is already known.</param>
    public static List<BypassAttemptResult> SweepLateralBypass(
        CharacterController controller,
        Vector3 barrierCenter,
        Vector3 forward,
        float[] lateralOffsets,
        float pushDistance,
        float startBackoff = 2f)
    {
        var results = new List<BypassAttemptResult>();
        Vector3 normal = forward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;

        foreach (float offset in lateralOffsets)
        {
            Vector3 start = barrierCenter + right * offset - normal * startBackoff;
            results.Add(AttemptBypass(controller, start, normal, pushDistance, barrierCenter, normal));
        }

        return results;
    }

    /// Convenience for the assertion every caller actually wants to make: nothing got through.
    public static bool AnyPenetrated(List<BypassAttemptResult> results)
    {
        foreach (BypassAttemptResult r in results) if (r.penetratedBarrier) return true;
        return false;
    }

    /// Human-readable failure detail, so a red test names the offset and the collider rather than
    /// just reporting that a boolean was true.
    public static string Describe(BypassAttemptResult r) =>
        $"from {r.startPosition} heading {r.attemptedDirection}: travelled {r.travelledDistance:F2}m " +
        $"of {r.attemptedDistance:F2}m, blocked={r.blocked}, hit='{r.obstacleHit}', " +
        $"penetrated={r.penetratedBarrier}, ended {r.finalPosition}";
}
