// One player rig, built the same way in every scene.
//
// Six rooms shipped with real, unit-tested gameplay — Court's evidence trial, the Parlor's
// trick-taking with a cheating host AI, Shut the Box's dice and tile rules, the Hidden Room's shard
// mechanic, the Labyrinth's chase — and not one of them contained a player. Every controller was
// reachable only from a test. A room nobody can walk into is not 0% built; it is 100% built and 0%
// reachable, and a phase bar cannot tell those apart, which is how it went unnoticed for months.
//
// The rig is deliberately identical everywhere so a bug found in one room is a bug found in all six,
// and so the body the player inhabits matches the body the NavMesh was baked for. Those two drifting
// apart is a real failure mode this project already documented: a controller that climbs less than
// the mesh claims it can leaves the player jammed on a route that says it is clear.
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmPlayerRig
{
    /// Eye height. One number, here, rather than per-scene: a camera that sits at a different height
    /// in each room makes every composition claim scene-specific for no reason anyone chose.
    public const float EyeHeight = 1.62f;

    public const float DefaultWalkSpeed = 3.5f;

    /// Builds the player and makes its camera and ear the only live ones in the scene.
    ///
    /// <param name="lookAt">Optional point to face on spawn. Yaw only — a rig that spawns pitched at
    /// the floor or the ceiling is disorienting in a way no one ever intends.</param>
    public static GameObject Build(Transform parent, Vector3 spawn, Vector3? lookAt = null,
        float walkSpeed = DefaultWalkSpeed)
    {
        var player = new GameObject("Player");
        if (parent != null) player.transform.SetParent(parent, true);
        player.transform.position = spawn;

        if (lookAt.HasValue)
        {
            Vector3 look = lookAt.Value - spawn;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                player.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        // The body and the NavMesh bake are one contract and read the same four numbers. In the
        // prologue these matched by coincidence of literals for a while, with slopeLimit and
        // stepOffset silently left at Unity's defaults — the exact drift described above.
        var controller = player.AddComponent<CharacterController>();
        controller.height = GmWendNavMesh.AgentHeight;
        controller.radius = GmWendNavMesh.AgentRadius;
        controller.center = new Vector3(0f, GmWendNavMesh.AgentHeight * 0.5f, 0f);
        controller.slopeLimit = GmWendNavMesh.AgentSlope;
        controller.stepOffset = GmWendNavMesh.AgentStep;

        var cameraObject = new GameObject("PlayerCamera");
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 900f;
        // Explicit depth so being the live camera does not depend on merely being the last one left.
        camera.depth = 10f;
        cameraObject.AddComponent<HDAdditionalCameraData>();
        var listener = cameraObject.AddComponent<AudioListener>();

        var controls = player.AddComponent<GmPlayer>();
        controls.walkSpeed = walkSpeed;
        // GmPlayer adds this itself at runtime, but adding it here means the built scene contains the
        // component a scene audit can actually see, rather than one that only exists once played.
        player.AddComponent<GmInteractionScanner>();
        // Every room built from this common rig gets the same reachable dossier/settings overlay.
        // The prologue attaches the same component at runtime so it cedes pause/settings ownership
        // to this common menu without requiring its generated scene to be rewritten by hand.
        player.AddComponent<GmPauseMenu>();

        int cameras = Solo(camera);
        int ears = Solo(listener);
        Debug.Log($"[GmPlayerRig] player at {spawn} — camera live ({cameras} other(s) disabled), " +
            $"ear live ({ears} other(s) disabled)");
        return player;
    }

    /// Disables every OTHER camera in the scene.
    ///
    /// In the editor a stray camera never mattered, because every review frame was rendered by one
    /// the harness created and pointed by hand. In a BUILT PLAYER it decides the whole frame: two
    /// live cameras at equal depth both render and whichever draws last owns the backbuffer. The
    /// first prologue app was caught doing exactly that, rendering a purchased pack's beauty shot
    /// instead of the player's eye and measuring as though the night had broken.
    static int Solo(Camera live)
    {
        int disabled = 0;
        foreach (Camera other in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (other == live || !other.enabled) continue;
            other.enabled = false;
            disabled++;
        }
        return disabled;
    }

    /// Same problem, one component over: two AudioListeners make Unity pick one and warn, and the
    /// one it picks is not reliably the player's.
    static int Solo(AudioListener live)
    {
        int disabled = 0;
        foreach (AudioListener other in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (other == live || !other.enabled) continue;
            other.enabled = false;
            disabled++;
        }
        return disabled;
    }
}
