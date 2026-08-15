// Every room the player is supposed to enter must contain a player.
//
// This is the test that would have caught Phase A2's whole reason for existing. Six scenes shipped
// with real, unit-tested gameplay -- Court's trial, the Parlor's cheating host AI, Shut the Box's
// rules engine, the Hidden Room's shard mechanic, the Labyrinth's chase -- and none of them
// contained a GmPlayer, so none of it was reachable by a human. Every one of those controllers had
// passing tests. Passing tests are why nobody noticed.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GmPlayerRigTests
{
    /// Scenes a player is meant to stand in. Boot is deliberately absent (a title screen has no
    /// body), and so is wend-hill-prologue, which builds its own rig through GmWendBuilder against
    /// the purchased pack's thirty cameras.
    static readonly (string Id, string Path)[] PlayableScenes =
    {
        ("entry-hall", "Assets/Scenes/EntryHall.unity"),
        ("parlor", "Assets/Scenes/Parlor.unity"),
        ("court", "Assets/Scenes/Court.unity"),
        ("shut-the-box", "Assets/Scenes/ShutTheBox.unity"),
        ("hidden-room", "Assets/Scenes/HiddenRoom.unity"),
        ("labyrinth", "Assets/Scenes/Labyrinth.unity"),
    };

    [Test]
    public void EveryPlayableSceneContainsExactlyOneReachablePlayer()
    {
        var missing = new List<string>();
        foreach ((string id, string path) in PlayableScenes)
        {
            if (!System.IO.File.Exists(path))
            {
                missing.Add($"{id}: scene file '{path}' does not exist — rebuild it");
                continue;
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var players = Object.FindObjectsByType<GmPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (players.Length == 0)
            {
                missing.Add($"{id}: no GmPlayer — this room's gameplay cannot be reached by a human");
                continue;
            }
            if (players.Length > 1)
            {
                missing.Add($"{id}: {players.Length} GmPlayers — input and camera ownership are ambiguous");
                continue;
            }

            GmPlayer player = players[0];
            if (player.GetComponent<CharacterController>() == null)
                missing.Add($"{id}: the player has no CharacterController and cannot move or be blocked");
            if (player.GetComponent<GmInteractionScanner>() == null)
                missing.Add($"{id}: the player has no interaction scanner, so nothing in the room can be examined");

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null) missing.Add($"{id}: the player has no camera");
            else if (!camera.enabled) missing.Add($"{id}: the player's camera is disabled — the room renders from something else");
        }

        Assert.IsEmpty(missing, "playable scenes are missing a usable player:\n- " + string.Join("\n- ", missing));
    }

    [Test]
    public void EveryPlayableSceneRendersAndHearsFromExactlyOneSource()
    {
        // Two live cameras at equal depth both render and whichever draws last owns the backbuffer.
        // The first prologue build shipped exactly that way, rendering a purchased pack's beauty shot
        // of a lamp-lit street instead of the player's eye, and measuring as though the night had
        // broken. Nothing in the editor could show it, because every editor frame was rendered by a
        // camera the harness created and pointed by hand.
        var faults = new List<string>();
        foreach ((string id, string path) in PlayableScenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            int liveCameras = 0;
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.enabled && camera.gameObject.activeInHierarchy) liveCameras++;

            int liveEars = 0;
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (listener.enabled && listener.gameObject.activeInHierarchy) liveEars++;

            if (liveCameras != 1) faults.Add($"{id}: {liveCameras} live camera(s), expected exactly 1");
            if (liveEars != 1) faults.Add($"{id}: {liveEars} live AudioListener(s), expected exactly 1");
        }

        Assert.IsEmpty(faults, "scenes render or hear from the wrong number of sources:\n- " + string.Join("\n- ", faults));
    }

    [Test]
    public void ThePlayerBodyMatchesTheNavMeshAgentItWasBakedFor()
    {
        // A controller that climbs less than the mesh says it can leaves the player jammed on a route
        // the mesh insists is clear. These drifted apart once already in the prologue: the capsule
        // matched on radius and height by coincidence of literals while slopeLimit and stepOffset sat
        // at Unity's defaults and the bake used the project agent instead of either.
        var host = new GameObject("RigContractFixture");
        try
        {
            GameObject player = GmPlayerRig.Build(host.transform, Vector3.zero);
            var controller = player.GetComponent<CharacterController>();
            Assert.AreEqual(GmWendNavMesh.AgentHeight, controller.height, 0.001f, "body height drifted from the bake");
            Assert.AreEqual(GmWendNavMesh.AgentRadius, controller.radius, 0.001f, "body radius drifted from the bake");
            Assert.AreEqual(GmWendNavMesh.AgentSlope, controller.slopeLimit, 0.001f, "slope limit drifted from the bake");
            Assert.AreEqual(GmWendNavMesh.AgentStep, controller.stepOffset, 0.001f, "step offset drifted from the bake");

            var camera = player.GetComponentInChildren<Camera>();
            Assert.AreEqual(GmPlayerRig.EyeHeight, camera.transform.localPosition.y, 0.001f,
                "eye height drifted; every scene's composition claims assume one height");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    /// Full hierarchy path, because "Cube" alone names nothing in a room built from primitives.
    static string HierarchyPath(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    /// The skin the CharacterController itself keeps between its capsule and the world. Insetting the
    /// probe by the same amount means resting cleanly ON the floor is not reported as being IN it.
    const float SkinWidth = 0.02f;

    [Test]
    public void NoPlayerSpawnsInsideTheGeometryOfItsOwnRoom()
    {
        // The whole reason this is a physics query and not arithmetic: five props this session were
        // found standing off the floor they were declared to stand on, and the sixth -- the dice --
        // got buried by hand-arithmetic done while fixing the fifth. Spawns were placed the same way
        // and never checked at all. Ask the engine where the capsule actually is.
        var faults = new List<string>();
        foreach ((string id, string path) in PlayableScenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var player = Object.FindAnyObjectByType<GmPlayer>(FindObjectsInactive.Include);
            if (player == null) continue;   // already reported by the test above
            var body = player.GetComponent<CharacterController>();
            if (body == null) continue;

            Vector3 foot = player.transform.TransformPoint(body.center - Vector3.up * (body.height * 0.5f - body.radius));
            Vector3 head = player.transform.TransformPoint(body.center + Vector3.up * (body.height * 0.5f - body.radius));

            foreach (Collider hit in Physics.OverlapCapsule(foot, head, body.radius - SkinWidth,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit == body) continue;
                faults.Add($"{id}: the player spawns inside '{HierarchyPath(hit.transform)}' " +
                    $"— the first frame of this room is the inside of a prop");
            }

            // The mirror defect, and the one the dice had: not overlapping anything, but not standing
            // on anything either. A spawn floating above the floor drops on frame one; a spawn below it
            // is inside the slab with its head poking out, which the capsule test above can miss when
            // the floor is thin.
            Vector3 from = player.transform.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit ground, 5f, ~0, QueryTriggerInteraction.Ignore))
                faults.Add($"{id}: nothing solid under the spawn within 5m — the player falls out of the room");
            else
            {
                float drop = player.transform.position.y - ground.point.y;
                if (Mathf.Abs(drop) > 0.05f)
                    faults.Add($"{id}: spawn sits {drop:F3}m {(drop > 0 ? "above" : "below")} the surface " +
                        $"under it ('{HierarchyPath(ground.transform)}')");
            }
        }

        Assert.IsEmpty(faults, "players spawn inside or off the geometry of their rooms:\n- " + string.Join("\n- ", faults));
    }

    [Test]
    public void TheSpawnProbeItselfReportsAnOverlapWhenThereIsOne()
    {
        // The test above is only worth its runtime if it can go red. A physics query in EditMode is
        // exactly the kind of thing that silently returns an empty array -- colliders unregistered,
        // transforms unsynced -- and an empty array reads identically to a clean room.
        var host = new GameObject("SpawnProbeFixture");
        try
        {
            GameObject player = GmPlayerRig.Build(host.transform, Vector3.zero);
            var body = player.GetComponent<CharacterController>();

            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetParent(host.transform, true);
            obstacle.transform.position = new Vector3(0f, 0.9f, 0f);   // straight through the capsule
            Physics.SyncTransforms();

            Vector3 foot = player.transform.TransformPoint(body.center - Vector3.up * (body.height * 0.5f - body.radius));
            Vector3 head = player.transform.TransformPoint(body.center + Vector3.up * (body.height * 0.5f - body.radius));
            Collider[] hits = Physics.OverlapCapsule(foot, head, body.radius - SkinWidth, ~0, QueryTriggerInteraction.Ignore);

            CollectionAssert.Contains(hits, obstacle.GetComponent<BoxCollider>(),
                "the capsule probe found nothing inside a box placed deliberately through the player — " +
                "EditMode physics is not answering, so every clean result above proves nothing");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
