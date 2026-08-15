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
}
