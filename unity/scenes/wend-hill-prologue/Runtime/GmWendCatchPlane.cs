// The failsafe under the world: if the player ends up below the terrain, put them back on the road
// and say so loudly.
//
// This is the second half of the map-edge fix. The first half is GmWendBounds' invisible walls, which
// are what should actually stop anyone leaving. This exists for the case where the walls have a gap,
// because an early walk found exactly that and fell 690m, and the two frames it captured on the way
// down read mean luma 0.005 -- which a luma check alone calls a darker night rather than an absent
// world. A fall that is silently survivable is worse than one that is not, so this rescues the player
// AND leaves an error in the log.
//
// WHY A Y THRESHOLD AND NOT A TRIGGER COLLIDER: a falling body accelerates, and a trigger thin enough
// to place is thin enough to tunnel straight through at terminal speed. Unity's physics would need
// continuous detection and a deep volume to be reliable. Comparing a float against a float every frame
// cannot tunnel and cannot be defeated by a fast fall.
//
// The loud log is deliberate and it is load bearing. GmWendWalkProbe reports a fall as a defect and
// standalone-proof fails on any runtime error, so a hole in the world cannot be rescued into looking
// like a clean run. Rescuing quietly would hide the very thing this was built to find.
using System.Collections.Generic;
using UnityEngine;

public sealed class GmWendCatchPlane : MonoBehaviour
{
    [SerializeField] float catchY;
    [SerializeField] Vector3[] respawnPoints = new Vector3[0];

    CharacterController controller;
    Transform player;
    Vector3 lastGrounded;
    int catches;

    /// Set by the builder. Serialized into the scene, so the built player carries the same values the
    /// editor computed rather than recomputing them from a scene it would have to search again.
    public void Configure(float catchHeight, IList<Vector3> route)
    {
        catchY = catchHeight;
        respawnPoints = new Vector3[route?.Count ?? 0];
        for (int i = 0; i < respawnPoints.Length; i++) respawnPoints[i] = route[i];
    }

    public float CatchHeight => catchY;
    public int Catches => catches;

    void Start()
    {
        player = GameObject.Find(GmWendBuilder_PlayerName)?.transform;
        if (player == null)
        {
            Debug.LogError("[GmWendCatchPlane] no Player in the scene; the world has no floor guard");
            enabled = false;
            return;
        }
        controller = player.GetComponent<CharacterController>();
        lastGrounded = player.position;
    }

    // The builder's constant lives in an Editor-only class, so the name is repeated rather than
    // referenced. Kept adjacent to its one use so the duplication is visible instead of buried.
    const string GmWendBuilder_PlayerName = "Player";

    void LateUpdate()
    {
        Vector3 here = player.position;

        // Remember the last place the ground actually held the player. Nearest-waypoint is measured
        // from here rather than from wherever the fall ended, because a long fall drifts a long way
        // horizontally and the nearest waypoint to the bottom of a ravine is not where they left.
        if (controller != null && controller.isGrounded) lastGrounded = here;

        if (here.y >= catchY) return;

        catches++;
        Vector3 to = Recover(lastGrounded);
        Debug.LogError($"[GmWendCatchPlane] FELL OUT OF THE WORLD at {here} " +
                       $"({catchY - here.y:0}m below the catch height {catchY:0}). " +
                       $"Recovered to {to}. This is catch {catches}: the boundary has a gap here and " +
                       "the walls are what should have stopped it, not this.");

        // A CharacterController writes the transform itself every frame, so assigning position while it
        // is enabled is overwritten before it takes effect. Disabling it for the teleport is the
        // documented way to move one.
        if (controller != null)
        {
            controller.enabled = false;
            player.position = to;
            controller.enabled = true;
        }
        else player.position = to;

        lastGrounded = to;
    }

    Vector3 Recover(Vector3 from)
    {
        Vector3 target = NearestPoint(respawnPoints, from, from);
        // Lift clear of the surface so the controller settles onto the ground rather than starting
        // inside it, which pops the player through and straight back down to the catch height.
        return target + Vector3.up * 1.5f;
    }

    /// Nearest point measured in XZ only, ignoring height. Public and static so the choice can be
    /// tested without a scene, a terrain or a fall.
    ///
    /// Height is excluded on purpose: the route runs over a hill, so a waypoint 40m up the slope is
    /// closer in 3D to a falling player than the one they actually walked past, and recovering to it
    /// would teleport them somewhere they had never been.
    public static Vector3 NearestPoint(IList<Vector3> points, Vector3 to, Vector3 fallback)
    {
        if (points == null || points.Count == 0) return fallback;

        Vector3 best = points[0];
        float bestSqr = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float dx = points[i].x - to.x;
            float dz = points[i].z - to.z;
            float sqr = dx * dx + dz * dz;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = points[i];
        }
        return best;
    }
}
