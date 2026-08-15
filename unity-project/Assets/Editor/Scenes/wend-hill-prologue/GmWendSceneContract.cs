// Verifies that WendHill_Prologue.unity ON DISK is actually the committed night.
//
// The existing GmVillageStandaloneBuild guard checks that required ROOT OBJECTS are present, because
// the failure it was written for was a crashed builder leaving a raw copy of the purchased showcase
// with no estate and no player. That check does not transfer to this scene. GmWendBuilder's night is
// made of VALUES, not roots: it re-aims the pack's own sun, retints the pack's own volume profile and
// repoints the pack's own materials. A crashed run leaves a scene that has every root the purchased
// pack ships, opens fine, builds fine, and is broad daylight.
//
// So this audits the night itself. Seventeen checks, each one a thing that a stale, half-built or
// reverted-on-reload scene fails. Checks 1-10 are the night; 11-17 arrived with the canonical opening
// and the first house chapter, and went undocumented here for long enough that the header said ten,
// the PASS line said fifteen and the code numbered sixteen:
//
//   1. the player exists and has an eye to render from
//   2. the lighting census still matches what the pack shipped
//   3. the directional light is the moon and not the 2000 lux sun
//   4. the scene's volume points at OUR profile copy, not the purchased one
//   5. that profile carries a Fixed exposure at the committed EV
//   6. exactly one live camera, and it is the player's
//   7. nothing in the scene still uses an ungated emissive foliage material
//   8. the map edge is closed, by four walls and a configured catch height
//   9. exactly one live AudioListener, and it is the player's
//  10. the player actually has something wired into that listener
//  11. the canonical estate opening is present, at route length, with a clear walk lane
//  12. world anchors are unique, the required five resolve, and thirteen POIs are anchored
//  13. the story runtime and the eight-shot review tour are wired into this scene
//  14. one manor, a gate that can close, and a wake destination
//  15. no negative-scale BoxCollider survives, and no purchased collider seals the route
//  16. the first house chapter: portraits, clues, interactions and textured Victorian art
//  17. every story POI carries owned rendered evidence, and the arrival car is the authored car
//
// The numbered comments in Audit() run in build order rather than in this order, because each check
// runs where the scene state it reads is cheapest to gather.
//
// Check 7 is the one that catches the specific way this could silently regress. The foliage fix
// repoints scene renderers and terrain tree prototypes in memory; if either failed to serialize, the
// scene would reopen with self-lit grass, and because HDRP's automatic exposure re-meters around
// anything bright, a reverted scene can still produce a frame that looks broadly plausible. That is
// exactly how the bug survived four attempts.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendSceneContract
{
    const string LogTag = "GmWendContract";

    /// Throws with EVERY failure listed, not just the first. A build guard that reports one problem at
    /// a time turns one wrong run into several.
    public static void AssertBuilt()
    {
        List<string> failures = Audit();
        if (failures.Count > 0)
            throw new System.InvalidOperationException(
                $"{GmWendBuilder.ScenePath} is not the committed night. " +
                $"{failures.Count} check(s) failed:\n  - {string.Join("\n  - ", failures)}\n" +
                "Re-run GamesMaster/Wend/3. Build the committed night.");
    }

    [MenuItem("GamesMaster/Wend/Audit the saved scene")]
    public static void AuditMenu()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            GmWendBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        List<string> failures = Audit();
        if (failures.Count == 0) Debug.Log($"[{LogTag}] PASS: all 17 checks green");
        else Debug.LogError($"[{LogTag}] FAIL:\n  - {string.Join("\n  - ", failures)}");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    /// Audits the currently open scene. Returns an empty list when it is the committed night.
    public static List<string> Audit()
    {
        // Pick up any bracket overrides FIRST, so the audit compares the scene against the recipe this
        // run actually used rather than against the defaults.
        //
        // Without this a bracket is unbuildable: the night build and the app build are separate Unity
        // processes, the statics reset in between, and the contract correctly reports a 4 lux moon
        // against a 1 lux recipe and refuses to build. That is the guard doing its job, so the fix
        // belongs here rather than in the guard's strictness.
        GmWendNight.ReadBisectOverrides();

        var failures = new List<string>();

        // 1. Player.
        GameObject player = GameObject.Find(GmWendBuilder.PlayerName);
        if (player == null) failures.Add($"no '{GmWendBuilder.PlayerName}' root");
        else if (player.transform.Find("PlayerCamera") == null)
            failures.Add($"'{GmWendBuilder.PlayerName}' has no PlayerCamera child to render from");

        // 2. Census. The pack's lighting must still be all there.
        GmWendBuilder.LightingCensus census = GmWendBuilder.TakeCensus();
        if (census.directional != 1 || census.practical != 24 || census.volumes != 30)
            failures.Add($"lighting census is {census}, expected 1 directional, 24 practical, 30 volumes");

        // 3. The moon. A daylight sun here means the night never applied, or applied and was lost.
        Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) failures.Add("no directional light");
        else
        {
            var hd = sun.GetComponent<HDAdditionalLightData>();
            if (hd == null) failures.Add($"directional '{sun.name}' has no HDAdditionalLightData");
            else if (hd.lightUnit != LightUnit.Lux)
                failures.Add($"moon is in {hd.lightUnit}, expected Lux");
            else if (Mathf.Abs(hd.intensity - GmWendNight.MoonLux) > 0.01f)
                failures.Add($"moon is {hd.intensity:0.##} lux, expected {GmWendNight.MoonLux:0.##}. " +
                             "Either the scene predates the current recipe or the recipe changed without a rebuild");
        }

        // 11. The canonical estate opening exists as one coherent layer.
        GameObject opening = GameObject.Find(GmWendOpening.RootName);
        GmRouteSpline route = Object.FindAnyObjectByType<GmRouteSpline>();
        if (opening == null) failures.Add($"no '{GmWendOpening.RootName}' root: this is still the environment-only walk");
        if (route == null) failures.Add("no semantic route spline");
        else if (Mathf.Abs(route.Length - GmWendRoute.EstateRouteMetres) > 2f)
            failures.Add($"canonical route is {route.Length:0}m, expected {GmWendRoute.EstateRouteMetres:0}m before the canyon");
        else
        {
            int terrainObstructions = 0;
            foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            {
                TerrainData data = terrain.terrainData;
                if (data == null) continue;
                foreach (TreeInstance tree in data.treeInstances)
                {
                    Vector3 world = terrain.transform.position + Vector3.Scale(tree.position, data.size);
                    float metres = route.ProjectDistance(world);
                    Vector3 onRoute = route.PointAt(metres);
                    float distance = Vector2.Distance(new Vector2(world.x, world.z),
                        new Vector2(onRoute.x, onRoute.z));
                    if (distance <= (metres <= 30f ? 6.5f : 3.6f)) terrainObstructions++;
                }
            }
            if (terrainObstructions > 0)
                failures.Add($"{terrainObstructions} terrain vegetation instance(s) still intersect the walk lane");
        }

        // 12. Stable anchors are unique and every required story/world endpoint resolves.
        failures.AddRange(GmWorldAnchor.ValidateScene());
        foreach (string id in new[] { "arrival-car", "gate", "chapel", "manor-porch", "wake-pose" })
            if (GmWorldAnchor.Find(id) == null) failures.Add($"required world anchor '{id}' is missing");
        if (Object.FindObjectsByType<GmWorldAnchor>(FindObjectsInactive.Include)
                .Count(a => a.name.StartsWith("POI_")) != 13)
            failures.Add("canonical opening does not contain exactly 13 anchored POIs");

        // 13. Full story runtime and deterministic review tour are wired into this scene.
        GmDesignRuntime story = Object.FindAnyObjectByType<GmDesignRuntime>();
        if (story == null || story.designFile != GmWendOpening.DesignFile)
            failures.Add($"story runtime is missing or does not use {GmWendOpening.DesignFile}");
        if (Object.FindAnyObjectByType<GmBellSummons>() == null ||
            Object.FindAnyObjectByType<GmThreshold>() == null ||
            Object.FindAnyObjectByType<GmCrossing>() == null ||
            Object.FindAnyObjectByType<GmSecretEnding>() == null ||
            Object.FindAnyObjectByType<GmColdOpen>() == null ||
            Object.FindAnyObjectByType<GmPrologueHud>() == null ||
            Object.FindAnyObjectByType<GmDisplayCalibration>() == null ||
            Object.FindAnyObjectByType<GmWendRuntimeCulling>() == null ||
            Object.FindAnyObjectByType<GmWendRenderBudget>() == null)
            failures.Add("one or more canonical opening systems are missing");
        GmWendStoryTour review = Object.FindAnyObjectByType<GmWendStoryTour>();
        if (review == null || review.ShotCount != 8) failures.Add("canonical review tour is missing or is not 8 shots");

        // 14. One manor, a real closing gate and a wake destination complete the route.
        if (Object.FindObjectsByType<GmMansionIdentity>(FindObjectsInactive.Include).Length != 1)
            failures.Add("expected exactly one authored manor");
        if (Object.FindAnyObjectByType<GmGateLeaves>() == null) failures.Add("estate gate has no closable leaves");
        if (GameObject.Find("WakeRoom/WakePose") == null) failures.Add("crossing wake room/pose is missing");

        // 16. The crossing now continues into the complete first house chapter.
        GameObject house = GameObject.Find(GmHouseBeginningBuilder.RootName);
        if (house == null) failures.Add("house beginning root is missing");
        else
        {
            if (house.GetComponent<GmHouseBeginning>() == null || house.GetComponent<GmHouseHud>() == null ||
                house.GetComponent<GmHouseProgress>() == null)
                failures.Add("house beginning does not carry its flow, HUD and shared progress engine");
            Transform portraits = house.transform.Find("EntryHall/Portraits");
            int namedPortraits = portraits == null ? 0 : portraits.Cast<Transform>()
                .Count(child => child.name.StartsWith("Portrait_"));
            if (namedPortraits != 9) failures.Add($"entry hall has {namedPortraits}/9 named guest portraits");
            foreach (string path in new[]
            {
                "EntryHall/LedgerDesk/Ledger",
                "EntryHall/Portraits/Portrait_Percival/MirrorShard",
                "EntryHall/ParlorDoor",
                "Parlor/AldricVoss",
                "Parlor/FirstGameTable",
                "Parlor/HostIntroductionThreshold",
            })
                if (house.transform.Find(path) == null) failures.Add($"house beginning is missing '{path}'");
            GmInteractable[] houseInteractions = house.GetComponentsInChildren<GmInteractable>(true);
            string[] interactionIds = houseInteractions.Select(item => item.InteractionId).ToArray();
            if (interactionIds.Length < 20) failures.Add($"entry/parlor has only {interactionIds.Length} authored interactions, expected at least 20");
            if (interactionIds.Any(string.IsNullOrWhiteSpace) || interactionIds.Distinct().Count() != interactionIds.Length)
                failures.Add("house interaction IDs are missing or duplicated");

            Renderer[] importedVictorian = house.GetComponentsInChildren<Renderer>(true)
                .Where(GmVictorianInteriorKit.IsImportedVisual).ToArray();
            if (importedVictorian.Length < 30)
                failures.Add($"house beginning has only {importedVictorian.Length} imported Victorian renderer(s), expected at least 30");
            int texturedVictorian = importedVictorian.Count(renderer => renderer.sharedMaterials.Any(material =>
                material != null && material.shader != null && material.shader.name == "HDRP/Lit" &&
                material.HasProperty("_BaseColorMap") && material.GetTexture("_BaseColorMap") != null &&
                material.HasProperty("_NormalMap") && material.GetTexture("_NormalMap") != null));
            if (texturedVictorian != importedVictorian.Length)
                failures.Add($"{importedVictorian.Length - texturedVictorian}/{importedVictorian.Length} imported Victorian renderers lack HDRP albedo/normal materials");
        }

        // 15. Purchased-prefab reflections must not reach BoxCollider. Positive-scale scene-owned
        // proxies preserve the collision while avoiding Unity's undefined negative-size warning.
        string[] negative = GmWendColliderRepair.ActiveNegativeColliderPaths();
        if (negative.Length > 0)
            failures.Add($"{negative.Length} active negative-scale BoxCollider(s): {string.Join(", ", negative.Take(5))}");
        string[] serializedNegative = GmWendColliderRepair.AllNegativeColliderPaths();
        if (serializedNegative.Length > 0)
            failures.Add($"{serializedNegative.Length} serialized negative-scale BoxCollider(s): " +
                         string.Join(", ", serializedNegative.Take(5)));
        foreach (GmWendColliderProxy marker in Object.FindObjectsByType<GmWendColliderProxy>(FindObjectsInactive.Include))
        {
            BoxCollider proxy = marker.GetComponent<BoxCollider>();
            if (proxy == null || Vector3.Distance(proxy.bounds.center, marker.SourceWorldCenter) > 0.05f ||
                Vector3.Distance(proxy.bounds.size, marker.SourceWorldSize) > 0.1f)
                failures.Add($"collider proxy '{marker.name}' does not preserve source world bounds");
        }
        if (route != null)
        {
            int falseDoorBlockers = 0;
            int falseRouteObstacles = 0;
            string[] obstacleTokens = { "wall", "fence", "door", "wood", "barrel", "crate", "cart",
                "wagon", "bench", "table", "chair", "rock", "debris", "prop" };
            foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                if (!GmWendPerformance.IntersectsRouteCapsule(collider.bounds, route)) continue;
                if (collider.name.IndexOf("Wall_Door", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    falseDoorBlockers++;
                string path = collider.name.ToLowerInvariant();
                for (Transform current = collider.transform.parent; current != null; current = current.parent)
                    path += "/" + current.name.ToLowerInvariant();
                if (collider is not TerrainCollider && collider.transform.root.name != GmWendOpening.RootName &&
                    collider.transform.root.name != GmWendBounds.RootName &&
                    obstacleTokens.Any(path.Contains)) falseRouteObstacles++;
            }
            if (falseDoorBlockers > 0)
                failures.Add($"{falseDoorBlockers} purchased doorway collider(s) still seal the semantic route");
            if (falseRouteObstacles > 0)
                failures.Add($"{falseRouteObstacles} purchased wall/prop collider(s) still seal the semantic route");
        }

        // 17. Every story POI carries owned rendered evidence, and the arrival car is the authored one.
        foreach (GmWorldAnchor poi in Object.FindObjectsByType<GmWorldAnchor>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None).Where(anchor => anchor.name.StartsWith("POI_")))
        {
            if (poi.AnchorId == "arrival-car" || poi.AnchorId == "chapel-door") continue;
            Transform prop = poi.transform.Cast<Transform>().FirstOrDefault(child => child.name.StartsWith("Prop_"));
            if (prop == null || prop.GetComponentsInChildren<Renderer>(true).Length == 0)
                failures.Add($"story POI '{poi.AnchorId}' has no owned rendered evidence prop");
            if (poi.transform.Cast<Transform>().Any(child => child.name.StartsWith("Evidence_")))
                failures.Add($"story POI '{poi.AnchorId}' regressed to a primitive placeholder");
        }
        GameObject arrivalCar = GameObject.Find($"{GmWendOpening.RootName}/ArrivalCar");
        if (arrivalCar == null)
            failures.Add("arrival car is missing");
        else
        {
            Renderer[] carRenderers = arrivalCar.GetComponentsInChildren<Renderer>(true);
            if (carRenderers.Length == 0) failures.Add("arrival car has no renderers");
            else
            {
                Bounds carBounds = carRenderers[0].bounds;
                foreach (Renderer renderer in carRenderers.Skip(1)) carBounds.Encapsulate(renderer.bounds);
                float longest = Mathf.Max(carBounds.size.x, Mathf.Max(carBounds.size.y, carBounds.size.z));
                if (longest < 4.4f || longest > 4.8f)
                    failures.Add($"arrival car longest dimension is {longest:0.00}m instead of 4.6m");
                GmWorldAnchor carAnchor = GmWorldAnchor.Find("arrival-car");
                if (carAnchor == null || Vector2.Distance(new Vector2(carBounds.center.x, carBounds.center.z),
                        new Vector2(carAnchor.transform.position.x, carAnchor.transform.position.z)) > 0.15f)
                    failures.Add("arrival car visual bounds are not centred on the arrival-car anchor");
            }
        }

        // 4 and 5. The owned profile, and the exposure on it.
        Volume host = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .FirstOrDefault(v => v.sharedProfile != null);
        if (host == null) failures.Add("no volume carries a profile");
        else
        {
            string path = AssetDatabase.GetAssetPath(host.sharedProfile);
            if (path != GmWendNight.OwnedProfilePath)
                failures.Add($"volume '{host.name}' uses profile {path}, expected {GmWendNight.OwnedProfilePath}. " +
                             "A purchased path here means the scene reverted to the pack's own look on reload");

            if (!host.sharedProfile.TryGet(out Exposure exposure))
                failures.Add($"profile {path} has no Exposure override, so the scene is on automatic exposure");
            else if (exposure.mode.value != ExposureMode.Fixed)
                failures.Add($"exposure mode is {exposure.mode.value}, expected Fixed");
            else if (Mathf.Abs(exposure.fixedExposure.value - GmWendNight.CommittedExposureEV) > 0.001f)
                failures.Add($"exposure is EV {exposure.fixedExposure.value:0.##}, " +
                             $"expected the committed {GmWendNight.CommittedExposureEV:0.##}");
        }

        // 6. Exactly one live camera, and it is the player's.
        //
        // This check exists because its absence shipped a wrong app. The original version of this audit
        // checked that PlayerCamera EXISTS, which it did, while the pack's 'Camera06' sat enabled at the
        // same depth and owned the built player's backbuffer. Presence is not the same as being the one
        // that renders, and no editor frame can tell the difference, because the review rigs render
        // cameras they create themselves.
        Camera[] live = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy)
            .ToArray();
        if (live.Length != 1)
            failures.Add($"{live.Length} live camera(s): {string.Join(", ", live.Select(c => $"'{c.name}' depth={c.depth}"))}. " +
                         "Expected exactly 1. The pack ships 30 cameras, and a second live one at equal " +
                         "depth can own the backbuffer in a built player while the editor looks fine");
        else if (live[0].transform.parent == null || live[0].transform.parent.name != GmWendBuilder.PlayerName)
            failures.Add($"the only live camera is '{live[0].name}', which is not under " +
                         $"'{GmWendBuilder.PlayerName}'");
        else if (live[0].GetComponent<HDAdditionalCameraData>() == null ||
                 !live[0].GetComponent<HDAdditionalCameraData>().allowDynamicResolution)
            failures.Add("the player HDRP camera does not allow the explicit built-player render budget");

        // 7. Foliage emission, by both routes.
        Material[] emitters = GmWendFoliage.RemainingEmitters();
        if (emitters.Length > 0)
            failures.Add($"{emitters.Length} foliage material(s) still emit: " +
                         $"{string.Join(", ", emitters.Select(m => m.name))}. " +
                         "The scene would light its own grass and re-meter the whole frame around it");

        // 8. The map edge.
        //
        // Audited by VALUE like everything else here, not by the root merely existing. A boundary root
        // with no walls under it, or a catch plane left at its default height of zero, is a scene that
        // opens fine and still lets the player walk off the world. Zero is called out specifically
        // because it is what an unconfigured component serializes as, and on this terrain it sits
        // above the ground rather than under it, so it would fire constantly instead of never.
        GameObject bounds = GameObject.Find(GmWendBounds.RootName);
        if (bounds == null)
            failures.Add($"no '{GmWendBounds.RootName}' root: the map edge is open and a walk has " +
                         "already fallen 690m off it");
        else
        {
            int walls = bounds.GetComponentsInChildren<BoxCollider>(true).Length;
            if (walls != 4) failures.Add($"boundary has {walls} wall(s), expected 4");

            var catcher = bounds.GetComponent<GmWendCatchPlane>();
            if (catcher == null)
                failures.Add($"'{GmWendBounds.RootName}' has no GmWendCatchPlane, so a gap in the " +
                             "walls would be an unrecoverable fall");
            else if (Mathf.Approximately(catcher.CatchHeight, 0f))
                failures.Add("catch height is 0, which is the unconfigured default rather than a " +
                             "height derived from the terrain");
        }

        // 9. Exactly one live ear, and it is the player's.
        //
        // The same failure as check 6, one component over. Disabling a Camera does not disable an
        // AudioListener sitting on the same GameObject, so the pack's 30 rigs can leave a second ear
        // enabled after the camera fix has run and reported success. Unity chooses one listener and
        // warns about the others, which means the mix would come from a showcase rig parked in the
        // village while every frame still rendered correctly from the player's eye.
        AudioListener[] ears = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include)
            .Where(a => a.enabled && a.gameObject.activeInHierarchy)
            .ToArray();
        if (ears.Length != 1)
            failures.Add($"{ears.Length} live AudioListener(s): " +
                         $"{string.Join(", ", ears.Select(a => $"'{a.name}'"))}. Expected exactly 1, " +
                         "or the scene mixes its audio from somewhere that is not the player's head");
        else if (ears[0].GetComponentInParent<Transform>() == null ||
                 ears[0].transform.parent == null ||
                 ears[0].transform.parent.name != GmWendBuilder.PlayerName)
            failures.Add($"the only live AudioListener is '{ears[0].name}', which is not under " +
                         $"'{GmWendBuilder.PlayerName}'");

        // 10. Ambience actually exists, wired to the player, by value after a genuine reload.
        //
        // The same shape of failure as check 7: a runtime-constructed audio graph that failed to
        // serialize would still open, still build, and still pass everything above it, while the
        // scene shipped exactly as silent as it started. GmWendAmbience builds its AudioSources and
        // assigns their clips at EDIT time for exactly this reason -- so there is something on disk
        // for this check to find rather than something a Start() method would have to construct.
        GmWendAmbienceSource ambience =
            player != null ? player.GetComponentInChildren<GmWendAmbienceSource>(true) : null;
        if (ambience == null)
            failures.Add($"'{GmWendBuilder.PlayerName}' has no GmWendAmbienceSource: the scene would " +
                         "ship as silent as it started");
        else
        {
            if (ambience.windBed == null || ambience.windBed.clip == null || !ambience.windBed.loop)
                failures.Add("ambience wind bed has no looping clip assigned");
            if (ambience.footstepPool == null || ambience.footstepPool.Length == 0)
                failures.Add("ambience footstep pool is empty");
            if (ambience.cricketAnchors == null || ambience.cricketAnchors.Length == 0)
                failures.Add("ambience has no cricket anchors derived from the route");
        }

        if (failures.Count == 0)
            Debug.Log($"[{LogTag}] 17/17 checks pass: player, census ({census}), moon " +
                      $"{GmWendNight.MoonLux} lux, owned profile, fixed EV " +
                      $"{GmWendNight.CommittedExposureEV}, one live camera, zero foliage emitters, " +
                      "closed map edge, one live ear, ambience wired " +
                      $"({ambience.footstepPool.Length} footstep clips, " +
                      $"{ambience.cricketAnchors.Length} cricket anchors), canonical estate opening, " +
                      "semantic anchors, story runtime, manor/gate/wake, collider proxies, " +
                      "house beginning chapter, POI evidence props");

        return failures;
    }
}
