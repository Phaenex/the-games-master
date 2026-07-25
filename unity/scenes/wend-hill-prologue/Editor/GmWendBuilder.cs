// Builds the prologue inside the purchased Abandoned Village scene, one verifiable layer at a time.
//
// This replaces GmVillageBuilder, which failed for a reason worth writing down: its very first step
// deleted the pack's directional lights and overrode its authored sky/fog volumes, then tried to
// re-author a night look from nothing. Every judgement after that was about lighting I had written
// rather than lighting the pack shipped, and it took four attempts to get wrong in four different
// ways (black, washed out, too dark, blown white).
//
// The measured reason it was so hard: Haunted Village ships ZERO practical lights. There was nothing
// to build a night on. Abandoned Village ships 1 directional, 24 practical lights and 22 lamp/fire
// props across 38 buildings and 57 road meshes -- an artist already lit this place, and the job is
// to move it to night, not to invent it.
//
// RULES FOR THIS FILE:
//   1. Never delete a light or a volume the pack shipped. Edit them.
//   2. One layer per step, each verifiable by a render before the next is added.
//   3. The purchased scene is only ever read. All writes go to Assets/Scenes/WendHill_Prologue.unity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendBuilder
{
    const string LogTag = "GmWend";

    public const string SourceScenePath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity";
    public const string ScenePath = "Assets/Scenes/WendHill_Prologue.unity";

    public const string PlayerName = "Player";
    const float PlayerEyeHeight = 1.7f;

    static readonly Regex RoadPattern =
        new Regex(@"Road|Path|Street|Track|Lane|Trail|Cobble", RegexOptions.IgnoreCase);
    static readonly Regex LodSuffix = new Regex(@"_LOD[1-9]$");

    /// STEP 1. Copy the purchased scene and add a player. Nothing else. No lighting is touched, so
    /// the result must look identical to the untouched baseline in Screens/Baseline -- that is the
    /// check, and it is the one the previous attempt never made.
    [MenuItem("GamesMaster/Wend/1. Copy scene + player only")]
    public static void BuildBase()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building");

        RecopyFromSource();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        LightingCensus before = TakeCensus();
        Vector3 spawn = PlacePlayer();

        // The edge of the map, before the census is re-taken, so the census assert below also proves
        // the boundary added no light and no volume. The pack ships no boundary of any kind and an
        // early walk fell 690m off it.
        int walls = GmWendBounds.Apply();

        LightingCensus after = TakeCensus();

        // The whole point of this step is that lighting is UNCHANGED. Assert it rather than trust it.
        if (!before.Equals(after))
            throw new InvalidOperationException(
                $"lighting changed during a step that must not touch it: {before} -> {after}");

        if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))
            throw new InvalidOperationException($"failed to save {ScenePath}");

        Debug.Log($"[{LogTag}] STEP 1 PASS: spawn={spawn} lighting untouched ({after}) " +
                  $"boundary={walls} walls");
    }

    static void RecopyFromSource()
    {
        if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null &&
            !AssetDatabase.DeleteAsset(ScenePath))
            throw new InvalidOperationException($"failed to delete stale {ScenePath}");

        if (AssetDatabase.LoadMainAssetAtPath(SourceScenePath) == null)
            throw new InvalidOperationException($"source scene missing: {SourceScenePath}");
        if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            throw new InvalidOperationException($"failed to copy {SourceScenePath} -> {ScenePath}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[{LogTag}] fresh copy: {SourceScenePath} -> {ScenePath}");
    }

    /// A cheap fingerprint of the scene's lighting, so any step that claims not to touch it can prove
    /// it. Counting is enough: the failure being guarded against is wholesale deletion, which is what
    /// happened last time.
    public struct LightingCensus : IEquatable<LightingCensus>
    {
        public int directional, practical, volumes;

        public bool Equals(LightingCensus o) =>
            directional == o.directional && practical == o.practical && volumes == o.volumes;

        public override bool Equals(object o) => o is LightingCensus c && Equals(c);
        public override int GetHashCode() => (directional, practical, volumes).GetHashCode();
        public override string ToString() =>
            $"{directional} directional, {practical} practical, {volumes} volumes";
    }

    public static LightingCensus TakeCensus()
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        return new LightingCensus
        {
            directional = lights.Count(l => l.type == LightType.Directional),
            practical = lights.Count(l => l.type != LightType.Directional),
            volumes = UnityEngine.Object
                .FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include).Length,
        };
    }

    /// Spawns ON A ROAD. The pack has 57 road meshes and the prologue is a walk down one, so the
    /// player belongs on the road surface rather than at a bounding-box centre. The previous attempt
    /// derived spawn from terrain bounds and put the player 100m outside the village in an empty
    /// field, which is the single mistake that cost the most time.
    static Vector3 PlacePlayer()
    {
        GameObject stale = GameObject.Find(PlayerName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        // The player starts at the BEGINNING of the prologue's route, from the shared definition in
        // GmWendRoute, so the spawn and the walk can never describe different places.
        //
        // What this replaces, because it is worth not repeating: the spawn used to be the CENTROID of all
        // 57 road meshes, snapped to the nearest piece. The pack's roads are scattered clusters spread
        // over about 1.5km, so their centroid is not a location. It put the player on a slope running
        // downhill into the pack's water plane, and every review frame of the night was shot from there
        // before anyone walked away from it.
        List<Vector3> route = GmWendRoute.Build(out string routeReport);
        Debug.Log($"[{LogTag}] route\n{routeReport}");

        Vector3 at;
        if (route.Count > 0)
        {
            at = route[0];
            Debug.Log($"[{LogTag}] spawn at the start of the route: {at}");
        }
        else
        {
            Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.gameObject.activeInHierarchy && r.GetComponent<Terrain>() == null)
                .ToArray();
            Vector3 sum = Vector3.zero;
            foreach (Renderer r in all) sum += r.bounds.center;
            at = all.Length > 0 ? sum / all.Length : Vector3.zero;
            Debug.LogWarning($"[{LogTag}] no road meshes matched; spawning at content centroid {at}");
        }

        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        float groundY = terrain != null
            ? terrain.SampleHeight(at) + terrain.transform.position.y
            : at.y;
        Vector3 spawn = new Vector3(at.x, groundY + 0.1f, at.z);

        var player = new GameObject(PlayerName);
        // Face down the route, so the first thing the prologue shows is where it is going.
        if (route.Count > 1)
        {
            Vector3 look = route[1] - route[0];
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                player.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        player.transform.position = spawn;

        var camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 900f;
        camGo.AddComponent<HDAdditionalCameraData>();
        camGo.AddComponent<AudioListener>();

        int silenced = SoloPlayerCamera(cam);
        Debug.Log($"[{LogTag}] player camera is now the only live one ({silenced} pack camera(s) disabled)");

        int deafened = SoloPlayerListener(camGo.GetComponent<AudioListener>());
        Debug.Log($"[{LogTag}] player ear is now the only live one ({deafened} pack listener(s) disabled)");

        player.AddComponent<GmPlayer>();
        return spawn;
    }

    /// Makes the player's camera the ONLY one that renders.
    ///
    /// The purchased scene ships THIRTY cameras: a showcase camera plus animation rigs. One of them,
    /// 'Camera06', is left enabled and active at depth 0, which is exactly the depth a new camera
    /// inherits. In the editor that never mattered, because every review frame in this project was
    /// rendered by a camera the harness created and pointed by hand, so no editor frame ever went
    /// through a scene camera at all. In a BUILT PLAYER it decides the entire frame: two live cameras at
    /// equal depth both render, and whichever draws last owns the backbuffer.
    ///
    /// The first prologue app was caught doing exactly this. It rendered the pack's beauty shot of a
    /// lamp-lit street instead of the player's eye, measured at whole-frame luma 0.477 against the
    /// editor's 0.026 to 0.074, which reads as "the night broke" when the night was fine and the camera
    /// was wrong. Nothing in the editor could have shown it, which is the whole reason the app has to be
    /// launched and looked at rather than trusted because it compiled.
    ///
    /// An explicit depth as well, so the player's camera does not depend on being the only one left.
    /// Makes the player's ear the ONLY AudioListener.
    ///
    /// Exactly the camera bug, one component over, and it survives the camera fix. SoloPlayerCamera
    /// disables the Camera COMPONENT on the pack's 30 cameras; an AudioListener on that same GameObject
    /// is a different component and stays enabled. Unity picks one listener when several are active and
    /// warns about the rest, so the scene would mix its audio from a showcase rig parked somewhere in
    /// the village rather than from the player's head, and every spatial cue would come from the wrong
    /// place. The camera version of this shipped once already, and it was invisible until the built app
    /// was looked at.
    static int SoloPlayerListener(AudioListener player)
    {
        int silenced = 0;
        foreach (AudioListener other in
                 UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (other == player || !other.enabled) continue;
            other.enabled = false;
            EditorUtility.SetDirty(other);
            silenced++;
        }
        return silenced;
    }

    static int SoloPlayerCamera(Camera player)
    {
        int silenced = 0;
        foreach (Camera other in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (other == player || !other.enabled) continue;
            // Disabled even when its GameObject is currently inactive: something enabling that parent
            // later would silently take the frame back.
            other.enabled = false;
            EditorUtility.SetDirty(other);
            silenced++;
        }

        player.depth = 10f;
        EditorUtility.SetDirty(player);
        return silenced;
    }
}
