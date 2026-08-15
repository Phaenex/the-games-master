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

    /// How far under the terrain SURFACE counts as having gone through it.
    ///
    /// The absolute catch height alone is not enough, and a walk proved it: the player went under the
    /// pack's water plane at y=-24 near the end of the route, while the catch height sat at -102
    /// because that is 30m below the terrain's LOWEST point. Being above the lowest point of a
    /// landscape says nothing about being above the ground you are standing on. A terrain that drops
    /// 70m somewhere else cannot be the reference for whether you fell through it here.
    const float BelowSurface = 10f;

    CharacterController controller;
    Transform player;
    Terrain terrain;
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
        terrain = Terrain.activeTerrain;
        lastGrounded = player.position;
    }

    /// The terrain surface under a point, or null when there is no terrain or the point is off it.
    /// Off the terrain there is no surface to be under, so only the absolute catch height applies.
    float? SurfaceUnder(Vector3 at)
    {
        if (terrain == null || terrain.terrainData == null) return null;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (at.x < origin.x || at.x > origin.x + size.x ||
            at.z < origin.z || at.z > origin.z + size.z) return null;

        return terrain.SampleHeight(at) + origin.y;
    }

    /// Whether a position is under the world. Pure and public so both conditions can be tested
    /// without a terrain, a fall or a build.
    public static bool IsBelowWorld(float playerY, float catchY, float? surfaceY, float belowSurface)
    {
        if (playerY < catchY) return true;
        return surfaceY.HasValue && playerY < surfaceY.Value - belowSurface;
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

        float? surface = SurfaceUnder(here);
        if (!IsBelowWorld(here.y, catchY, surface, BelowSurface)) return;

        catches++;
        Vector3 to = Recover(lastGrounded);
        string why = surface.HasValue && here.y < surface.Value - BelowSurface
            ? $"{surface.Value - here.y:0}m under the terrain surface ({surface.Value:0})"
            : $"below the catch height {catchY:0}";
        Debug.LogError($"[GmWendCatchPlane] FELL OUT OF THE WORLD at {here}, {why}. " +
                       $"Recovered to {to}. This is catch {catches}: something here has no floor, and " +
                       "the walls only close the map edge, not a hole in the middle of it.");

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
