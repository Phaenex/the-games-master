// Builds the project-owned, walkable, night-lit copy of the purchased Haunted Village scene.
//
// Design: FULL DETERMINISTIC REGENERATION. Every run starts from a fresh copy of the purchased source
// and re-applies every layer in order. Nothing is ever hand-placed in the editor, so nothing can be
// lost by re-running, and there is no "preserve existing placements" problem to solve when M2 adds
// the mansion and gate -- those become another layer in this same pipeline. The purchased source
// scene is only ever read; all writes go to Assets/Scenes/WendHillVillage.unity.
//
// Placement here is measured, not assumed. GmVillageSurvey (Screens/DemoScenes/village-survey.json)
// established the facts this file depends on:
//   * the dressed village is a ~35m-wide strip through x 67..130, z -143..-50, NOT the whole 500x600
//     terrain -- the first build derived spawn from terrain-bounds centre and dropped the player
//     ~100m south of town in an empty field, which is exactly the "bare and lack life" failure;
//   * the source ships 21 ground-level showcase cameras -- the asset author's own framings of this
//     environment, and therefore the best available evidence of where it looks composed;
//   * the source ships ZERO practical lights, so night has to author its own;
//   * 91 objects (348 renderers) sit unplaced at world origin, ~148m above the village floor, and
//     would hang in the sky over gameplay.
//
// Runs headless: editing and saving a scene needs no HDRP rendering.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmVillageBuilder
{
    public const string SourceScenePath = "Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity";
    public const string VillageScenePath = "Assets/Scenes/WendHillVillage.unity";
    const string NightProfilePath = "Assets/Scenes/WendHillVillageNight.asset";
    const string TerrainDataPath = "Assets/Scenes/WendHillVillageTerrain.asset";

    const string VolumeName = "WendHillVillageNightVolume";
    const string MoonName = "VillageMoonlight";
    const string FillName = "VillageSkyFill";
    const string PracticalsName = "VillagePracticals";
    const string PlayerName = "Player";
    public const string RoadWaypointsName = "RoadWaypoints";
    const string SystemsName = "GmSystems";
    const string GroundMistName = "VillageGroundMist";

    const float PlayerEyeHeight = 1.7f;
    const float OriginJunkRadius = 5f;
    // Footprint of a typical village house here (SM_House_09 is 14.9 x 13.6m); practical intensity
    // is scaled relative to this so every lit building reads at a comparable brightness.
    const float ReferenceFootprint = 150f;

    static readonly Regex BuildingPattern =
        new Regex(@"House|Church|Barn|Shed|Hut|Cabin|Chapel|Tower|Mill", RegexOptions.IgnoreCase);
    static readonly Regex LodSuffix = new Regex(@"_LOD[1-9]$");

    /// A showcase camera's transform, harvested before the rig is stripped.
    public struct Vantage
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 walkCentroid;   // average of all 21 camera positions: where the player belongs
        public bool valid;
    }

    [MenuItem("GamesMaster/Village/Build Night Walk (M1)")]
    public static void BuildNightWalk()
    {
        Build(GmVillageNightRecipe.Base());
    }

    /// Returns the harvested showcase vantage so callers that re-light an already-built scene (the
    /// lighting lab) can re-place practicals without recopying the source scene per variant.
    public static Vantage Build(GmVillageNightRecipe recipe)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("exit Play mode before building the village walk");

        RecopyFromSource();
        EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);

        Vantage vantage = CaptureShowcaseVantage();
        int camerasRemoved = StripShowcaseCameras();
        int lightsRemoved = StripShowcaseLights();
        int junkRemoved = StripOriginJunk();
        // After the strips: the road is measured from what is actually left standing, so the origin
        // junk (which includes five full-size trees) cannot influence where the road is judged clear.
        int waypoints = BuildRoadWaypoints();
        int swayed = BuildCanopySway();
        int prototypesDropped = OwnTerrainData();

        ApplyNight(recipe);
        int practicals = BuildPracticals(recipe, vantage);

        // M2: the estate lands on the village BEFORE the player is placed, because the player's
        // cold-open spawn is defined by the arrival car, not by the showcase camera M1 used.
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) throw new InvalidOperationException("village scene has no Terrain");
        int mist = BuildGroundMist(terrain);
        GmVillageEstate.Site site = GmVillageEstate.Build(terrain);
        Vector3 spawn = PlacePlayer(vantage, site);

        // M3. Export first so scripts/remap-village-design.py has the current transform, then place
        // whatever markers the last remap produced. On a first-ever run the marker file does not
        // exist yet and PlacePoiMarkers reports 0 -- rerun the build after the remapper and it fills
        // in. The build stays deterministic either way, so running it twice is safe.
        GmVillageDesign.ExportTransform();
        int poiMarkers = GmVillageDesign.PlacePoiMarkers(null, terrain);
        BuildSystems();

        // The crossing's landing spot. Without it toll nine cuts to black and then logs
        // "no WakeRoom/WakePose -- the player wakes in the mud", which is the prologue ending into
        // nothing. GmWakeRoom builds at world z=+400, far outside the village's z -281..+4 extent,
        // and GmCrossing finds it at runtime via GameObject.Find("WakeRoom/WakePose") -- which only
        // resolves from a scene ROOT, so it must not be parented under anything here.
        GameObject staleWake = GameObject.Find("WakeRoom");
        if (staleWake != null) UnityEngine.Object.DestroyImmediate(staleWake);
        GmWakeRoom.Build();

        if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))
            throw new InvalidOperationException("failed to save WendHillVillage.unity");

        Debug.Log($"[GmVillageBuilder] BUILD PASS: recipe={recipe.label} " +
                  $"camerasRemoved={camerasRemoved} lightsRemoved={lightsRemoved} " +
                  $"originJunkRemoved={junkRemoved} roadWaypoints={waypoints} swayTrees={swayed} " +
                  $"treePrototypesDropped={prototypesDropped} " +
                  $"practicalLights={practicals} groundMist={mist} poiMarkers={poiMarkers} " +
                  $"spawn={spawn} EV={recipe.exposureEV} moon={recipe.moonLux}lux");
        return vantage;
    }

    static void RecopyFromSource()
    {
        if (AssetDatabase.LoadMainAssetAtPath(VillageScenePath) != null &&
            !AssetDatabase.DeleteAsset(VillageScenePath))
            throw new InvalidOperationException($"failed to delete stale {VillageScenePath}");
        if (AssetDatabase.LoadMainAssetAtPath(NightProfilePath) != null)
            AssetDatabase.DeleteAsset(NightProfilePath);
        if (AssetDatabase.LoadMainAssetAtPath(TerrainDataPath) != null)
            AssetDatabase.DeleteAsset(TerrainDataPath);

        if (AssetDatabase.LoadMainAssetAtPath(SourceScenePath) == null)
            throw new InvalidOperationException($"source scene missing: {SourceScenePath}");
        if (!AssetDatabase.CopyAsset(SourceScenePath, VillageScenePath))
            throw new InvalidOperationException($"failed to copy {SourceScenePath} -> {VillageScenePath}");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // Harvested BEFORE the rig is destroyed. Camera01 is the one camera the pack ships enabled -- it
    // is the framing behind the store render (Screens/DemoScenes/haunted-village-showcase.png), so
    // standing the player there starts them in a composition a human already approved. The centroid
    // of all 21 cameras marks the corridor the village was built to be seen from, which is a far
    // better "where does the player belong" than the terrain's geometric centre.
    static Vantage CaptureShowcaseVantage()
    {
        Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        if (cams.Length == 0) return new Vantage { valid = false };

        Vector3 sum = Vector3.zero;
        foreach (Camera c in cams) sum += c.transform.position;

        Camera hero = cams.FirstOrDefault(c => c.name == "Camera01") ??
                      cams.FirstOrDefault(c => c.isActiveAndEnabled) ?? cams[0];

        return new Vantage
        {
            position = hero.transform.position,
            rotation = hero.transform.rotation,
            walkCentroid = sum / cams.Length,
            valid = true,
        };
    }

    // Builds the drivable road as the CLEAREST lateral corridor at each step along the spine.
    //
    // Two earlier attempts were wrong, both caught by evidence rather than reasoning:
    //   * the straight spine (a principal-axis fit through the buildings) is right for choosing where
    //     the mansion and gate go, since those sit in open ground at either end, but it cuts through
    //     the buildings the road bends around -- the walkthrough camera passed through a shed;
    //   * the 21 showcase cameras looked like the road, being ground-level stances in the village,
    //     but they are vantage points scattered AROUND it, several shooting back from the verge.
    //     Ordering them produced a path zig-zagging 20m across the street. Smoothing hid it from a
    //     free camera, and GmVillageWalkTest then drove a real CharacterController into a wall.
    //
    // Measuring clearance directly answers the actual question -- "where is there room to walk" --
    // instead of inferring it from something correlated with the answer.
    static int BuildRoadWaypoints()
    {
        GameObject stale = GameObject.Find(RoadWaypointsName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return 0;

        // Only things tall enough to actually stop a walker. Ground scatter and low scrub are
        // walk-through clutter, and treating them as obstacles would push the road into the fields.
        var obstacles = new List<(Vector2 at, float radius)>();
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.gameObject.activeInHierarchy || r.GetComponent<Terrain>() != null) continue;
            if (LodSuffix.IsMatch(r.name)) continue;
            Bounds b = r.bounds;
            if (b.size.y < 2.5f) continue;
            obstacles.Add((new Vector2(b.center.x, b.center.z), Mathf.Max(b.size.x, b.size.z) * 0.5f));
        }

        const float StepT = 5f, FromT = 92f, ToT = -96f, MaxLateral = 13f;
        var samples = new List<(float t, float lateral)>();
        for (float t = FromT; t >= ToT; t -= StepT)
        {
            float bestLateral = 0f, bestClear = float.MinValue;
            for (float lateral = -MaxLateral; lateral <= MaxLateral; lateral += 1f)
            {
                Vector2 p = GmVillageEstate.SpinePoint(t) + GmVillageEstate.LateralAxis * lateral;
                float clear = float.MaxValue;
                foreach (var (at, radius) in obstacles)
                    clear = Mathf.Min(clear, Vector2.Distance(p, at) - radius);
                // Nudge toward the road's centre so ties do not drift the path out into open field.
                clear -= Mathf.Abs(lateral) * 0.05f;
                if (clear > bestClear) { bestClear = clear; bestLateral = lateral; }
            }
            samples.Add((t, bestLateral));
        }

        // Smooth the lateral profile. Per-step maxima jump between equally-clear gaps, and a walker
        // cannot sidestep 8m between one stride and the next.
        for (int pass = 0; pass < 3; pass++)
            for (int i = 1; i < samples.Count - 1; i++)
                samples[i] = (samples[i].t,
                    (samples[i - 1].lateral + samples[i].lateral * 2f + samples[i + 1].lateral) * 0.25f);

        var root = new GameObject(RoadWaypointsName);
        for (int i = 0; i < samples.Count; i++)
        {
            Vector2 xz = GmVillageEstate.SpinePoint(samples[i].t) +
                         GmVillageEstate.LateralAxis * samples[i].lateral;
            var wp = new GameObject($"WP_{i:D2}");
            wp.transform.SetParent(root.transform, true);
            wp.transform.position = new Vector3(
                xz.x, terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y, xz.y);
        }
        return samples.Count;
    }

    // A still village is a diorama. GmCanopySway moves a whole tree by a fraction of a degree at its
    // root -- centimetres at crown height -- which is enough that the treeline is never quite still
    // in peripheral vision without anything reading as animation.
    //
    // Scoped to trees near the walked road. The scene holds hundreds of trees and most are distant
    // silhouettes in fog where sub-degree motion is invisible, so swaying them would be pure cost.
    static int BuildCanopySway()
    {
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        GameObject meshes = GameObject.Find("Meshes");
        if (meshes == null) return 0;

        var rng = new System.Random(90210);
        int swayed = 0;

        foreach (Transform child in meshes.transform)
        {
            if (!child.name.StartsWith("SM_Tree", StringComparison.OrdinalIgnoreCase)) continue;
            if (child.GetComponent<GmCanopySway>() != null) continue;

            // Distance from the road, measured along the spine's cross axis.
            Vector2 p = new Vector2(child.position.x, child.position.z);
            float t = Vector2.Dot(p - GmVillageEstate.SpineCentroid, GmVillageEstate.SpineAxis);
            float lateral = Vector2.Dot(p - GmVillageEstate.SpineCentroid, GmVillageEstate.LateralAxis);
            if (t > 100f || t < -110f || Mathf.Abs(lateral) > 42f) continue;

            var sway = child.gameObject.AddComponent<GmCanopySway>();
            // Spread amplitude and frequency across the range so the treeline does not breathe as
            // one organism, which is what a single shared value looks like.
            sway.Configure(
                Mathf.Lerp(0.06f, 0.20f, (float)rng.NextDouble()),
                Mathf.Lerp(0.038f, 0.105f, (float)rng.NextDouble()));
            swayed++;
        }
        return swayed;
    }

    static int StripShowcaseCameras()
    {
        int removed = 0;
        foreach (string label in new[] { "Static Cameras", "DynamicCameras" })
        {
            GameObject root = GameObject.Find(label);
            if (root == null) continue;
            removed += root.GetComponentsInChildren<Camera>(true).Length;
            UnityEngine.Object.DestroyImmediate(root);
        }
        return removed;
    }

    // Both daylight directionals go, including the inactive one: leaving a disabled 1.7-intensity
    // key in the scene is a trap for anyone who later toggles it looking for "why is this dark".
    static int StripShowcaseLights()
    {
        int removed = 0;
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type != LightType.Directional) continue;
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            removed++;
        }
        return removed;
    }

    // The purchased scene leaves 91 unplaced objects (planks, buckets, shovels, benches, five trees)
    // stacked at world origin. The village floor is at y ~ -148, so origin is ~148m UP and ~90m
    // lateral: in-game that is a pile of lumber and trees hanging in the night sky. Cutting them also
    // removes 348 renderers from the frustum for free.
    static int StripOriginJunk()
    {
        GameObject meshes = GameObject.Find("Meshes");
        if (meshes == null) return 0;

        var doomed = new List<GameObject>();
        foreach (Transform child in meshes.transform)
            if (child.position.magnitude < OriginJunkRadius) doomed.Add(child.gameObject);

        foreach (GameObject go in doomed) UnityEngine.Object.DestroyImmediate(go);
        return doomed.Count;
    }

    // The built player logs "The tree SM_Tree_07 couldn't be instanced because one of its LODs
    // contains renderer of type other than MeshRenderer and BillboardRenderer." The terrain carries
    // that prototype while placing ZERO tree instances, so nothing is lost visually -- but
    // GmRuntimeIntegrityPolicy classifies "couldn't be instanced" as a render failure, which would
    // fail the standalone integrity gate on a purely cosmetic leftover.
    //
    // The prototype list lives on the TerrainData ASSET, which is purchased content shared with the
    // untouched source scene, so it must not be edited in place. Instead the scene gets its own copy
    // of the TerrainData under Assets/Scenes and the unused prototypes are dropped from that.
    static int OwnTerrainData()
    {
        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return 0;

        string sourcePath = AssetDatabase.GetAssetPath(terrain.terrainData);
        if (string.IsNullOrEmpty(sourcePath)) return 0;

        if (AssetDatabase.LoadMainAssetAtPath(TerrainDataPath) != null)
            AssetDatabase.DeleteAsset(TerrainDataPath);
        if (!AssetDatabase.CopyAsset(sourcePath, TerrainDataPath))
            throw new InvalidOperationException($"failed to copy TerrainData {sourcePath} -> {TerrainDataPath}");
        AssetDatabase.Refresh();

        var owned = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (owned == null) throw new InvalidOperationException($"TerrainData copy missing at {TerrainDataPath}");

        int dropped = 0;
        if (owned.treeInstanceCount == 0 && owned.treePrototypes.Length > 0)
        {
            dropped = owned.treePrototypes.Length;
            owned.treePrototypes = Array.Empty<TreePrototype>();
        }

        terrain.terrainData = owned;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null) collider.terrainData = owned;

        EditorUtility.SetDirty(owned);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();
        return dropped;
    }

    /// Idempotent: destroys any previously authored night objects first, so the lighting lab can
    /// re-apply variants to one open scene without stacking duplicate volumes and lights.
    public static void ApplyNight(GmVillageNightRecipe recipe)
    {
        foreach (string name in new[] { VolumeName, MoonName, FillName })
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        }

        var volGo = new GameObject(VolumeName);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        // The source scene carries its own daylight sky/fog volumes. Rather than hunt and retune each
        // one (and risk losing deliberate local variation), this sits above all of them at a dominant
        // priority and overrides only the components that define night.
        vol.priority = 1000f;

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NightProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, NightProfilePath);
        }
        else
        {
            foreach (VolumeComponent c in profile.components.ToArray())
                UnityEngine.Object.DestroyImmediate(c, true);
            profile.components.Clear();
        }
        vol.sharedProfile = profile;

        var fog = AddOverride<Fog>(profile);
        fog.enabled.Override(true);
        fog.enableVolumetricFog.Override(true);
        fog.meanFreePath.Override(recipe.fogMeanFreePath);
        fog.albedo.Override(recipe.fogAlbedo);
        fog.baseHeight.Override(0f);
        fog.maximumHeight.Override(recipe.fogMaxHeight);

        // Volumetric quality, previously left at defaults. The HDRP pipeline asset itself is
        // purchased Leartes content shared with other scenes, so it is deliberately not edited;
        // these are the per-volume controls that actually govern how the fog resolves.
        //
        // Anisotropy is the one that matters most for mood: positive values scatter light FORWARD,
        // so a lamp seen through mist gets a halo shaped by its direction instead of an even glow,
        // which is what makes fog read as air rather than as a screen filter.
        fog.anisotropy.Override(0.62f);
        // Volumetric fog is only computed within this distance; the default 64m would stop it well
        // short of the 180m walk, making the far half of the road fall back to flat distance fog.
        fog.depthExtent.Override(110f);
        // Push froxel resolution toward the near field, where the player actually reads the mist.
        fog.sliceDistributionUniformity.Override(0.55f);
        fog.multipleScatteringIntensity.Override(0.22f);

        var exposure = AddOverride<Exposure>(profile);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(recipe.exposureEV);

        var tonemapping = AddOverride<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var colour = AddOverride<ColorAdjustments>(profile);
        colour.postExposure.Override(0f);
        colour.contrast.Override(recipe.contrast);
        colour.saturation.Override(recipe.saturation);
        colour.colorFilter.Override(new Color(0.97f, 0.98f, 1f));

        var ao = AddOverride<ScreenSpaceAmbientOcclusion>(profile);
        ao.intensity.Override(recipe.aoIntensity);

        var vignette = AddOverride<Vignette>(profile);
        vignette.intensity.Override(recipe.vignetteIntensity);
        vignette.smoothness.Override(0.6f);
        vignette.rounded.Override(true);

        var bloom = AddOverride<Bloom>(profile);
        bloom.intensity.Override(recipe.bloomIntensity);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.6f);

        var vis = AddOverride<VisualEnvironment>(profile);
        Cubemap stars = recipe.useStarSky
            ? GmVillageSky.EnsureStarfield(recipe.skyTop, recipe.skyBottom)
            : null;

        if (stars != null)
        {
            var hdri = AddOverride<HDRISky>(profile);
            hdri.hdriSky.Override(stars);
            hdri.exposure.Override(recipe.starSkyExposure);
            hdri.rotation.Override(recipe.starSkyRotation);
            vis.skyType.Override((int)SkyType.HDRI);
        }
        else
        {
            // Kept as the fallback so the scene still builds if the starfield ever fails to import.
            var sky = AddOverride<GradientSky>(profile);
            sky.top.Override(recipe.skyTop);
            sky.middle.Override(recipe.skyMiddle);
            sky.bottom.Override(recipe.skyBottom);
            sky.gradientDiffusion.Override(3.5f);
            sky.exposure.Override(recipe.skyExposure);
            vis.skyType.Override((int)SkyType.Gradient);
        }

        MakeDirectional(MoonName, recipe.moonEuler, recipe.moonColor, recipe.moonLux,
            shadows: true, volumetric: recipe.moonVolumetric);
        // Fill is an ambient lift, NOT a second key. The first recipe ran it at 1.6 lux from an
        // opposing angle, which cancelled the moon's shadow rake and was a direct cause of the
        // "everything is one flat value" look.
        MakeDirectional(FillName, new Vector3(20f, 44f, 0f), new Color(0.30f, 0.32f, 0.40f),
            recipe.fillLux, shadows: false, volumetric: false);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        if (profile.components.Count == 0)
            throw new InvalidOperationException("night volume profile persisted 0 overrides");
    }

    // Knee-high mist lying in the ground, laid along the walked road.
    //
    // The global Fog override is uniform: it thins everything equally with distance, which reads as
    // haze rather than as weather. Real night fog pools -- it sits in hollows and along a road and
    // stops at your waist. These local volumes give the walk something that moves past the player at
    // ankle height and lets a lit window or a gravestone cut out of it, which is where the depth in
    // a night scene actually comes from.
    static int BuildGroundMist(Terrain terrain)
    {
        GameObject stale = GameObject.Find(GroundMistName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        if (terrain == null) return 0;

        var root = new GameObject(GroundMistName);
        var rng = new System.Random(4242);
        int made = 0;

        // Along the road from the car to past the mansion, spaced so neighbours overlap and blend
        // instead of showing as a row of discrete boxes.
        for (float t = 84f; t >= -104f; t -= 26f)
        {
            float lateral = (float)(rng.NextDouble() * 14.0 - 7.0);
            Vector2 xz = GmVillageEstate.SpinePoint(t) + GmVillageEstate.LateralAxis * lateral;
            float groundY = terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;

            var go = new GameObject($"Mist_{made:D2}");
            go.transform.SetParent(root.transform, true);
            // Centred just above the ground so the slab's lower half is buried and only the top of
            // it is visible; a box centred at eye height reads as a floating cube of smoke.
            go.transform.position = new Vector3(xz.x, groundY + 0.9f, xz.y);
            go.transform.rotation = Quaternion.Euler(0f, GmVillageEstate.SpineYawDeg, 0f);

            var fog = go.AddComponent<LocalVolumetricFog>();
            fog.parameters.albedo = new Color(0.10f, 0.11f, 0.135f);
            // Denser than the global fog (62m) so it actually reads against it, but not so dense it
            // becomes a wall the player walks into.
            fog.parameters.meanFreePath = Mathf.Lerp(11f, 20f, (float)rng.NextDouble());
            fog.parameters.size = new Vector3(
                Mathf.Lerp(46f, 74f, (float)rng.NextDouble()), 3.4f,
                Mathf.Lerp(40f, 66f, (float)rng.NextDouble()));
            // Soft edges on every axis, so entering a patch is a gradient rather than a pop.
            fog.parameters.positiveFade = new Vector3(0.45f, 0.85f, 0.45f);
            fog.parameters.negativeFade = new Vector3(0.45f, 0.35f, 0.45f);
            made++;
        }
        return made;
    }

    static void MakeDirectional(string name, Vector3 euler, Color color, float lux,
        bool shadows, bool volumetric)
    {
        var go = new GameObject(name);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        go.transform.rotation = Quaternion.Euler(euler);

        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.affectsVolumetric = volumetric;
        if (volumetric)
        {
            hd.angularDiameter = 2.2f;
            hd.softnessScale = 1f;
        }
        hd.lightUnit = LightUnit.Lux;
        light.intensity = lux;
    }

    /// Warm light in a handful of windows. This is the layer that separates "a field of silhouettes"
    /// from "somewhere with something in it", and the village ships none of its own.
    public static int BuildPracticals(GmVillageNightRecipe recipe, Vantage vantage)
    {
        GameObject existing = GameObject.Find(PracticalsName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        if (recipe.practicalInteriorLumens <= 0f && recipe.practicalExteriorLumens <= 0f) return 0;

        var root = new GameObject(PracticalsName);
        int made = 0;

        foreach (Renderer building in FindBuildings())
        {
            if (recipe.litBuildings != null &&
                !recipe.litBuildings.Any(n => building.name.StartsWith(n, StringComparison.Ordinal)))
                continue;

            Bounds b = building.bounds;
            float groundY = b.min.y;
            float lampY = groundY + Mathf.Min(2.0f, Mathf.Max(1.2f, b.size.y * 0.35f));

            // One fixed lumen value across buildings of different sizes makes the SMALL ones blaze:
            // a 9.5x8.1m open shed given the same lamp as a 14.9x13.6m house became the brightest
            // thing in frame and pulled the eye off the church. Scaling to footprint keeps a lit
            // window looking like a lit window at any building size. Clamped so the range stays
            // deliberate rather than tracking mesh bounds off to extremes.
            float footprint = b.size.x * b.size.z;
            float sizeScale = Mathf.Clamp(footprint / ReferenceFootprint, 0.5f, 1.25f);

            if (recipe.practicalInteriorLumens > 0f)
            {
                MakePoint(root.transform, $"Interior_{building.name}",
                    new Vector3(b.center.x, lampY, b.center.z),
                    recipe.practicalInteriorLumens * sizeScale, recipe.practicalRange,
                    recipe.practicalColor, recipe.practicalVolumetric);
                made++;
            }

            if (recipe.practicalExteriorLumens > 0f)
            {
                // Pushed just clear of the wall on the side the player will approach from, so the
                // pool lands where it can be walked into rather than behind the building.
                Vector3 toPlayerSide = vantage.valid
                    ? new Vector3(vantage.walkCentroid.x - b.center.x, 0f, vantage.walkCentroid.z - b.center.z)
                    : Vector3.forward;
                if (toPlayerSide.sqrMagnitude < 0.01f) toPlayerSide = Vector3.forward;
                toPlayerSide.Normalize();
                float clearance = Mathf.Max(b.size.x, b.size.z) * 0.5f + 0.6f;

                MakePoint(root.transform, $"Exterior_{building.name}",
                    new Vector3(b.center.x, groundY + 2.4f, b.center.z) + toPlayerSide * clearance,
                    recipe.practicalExteriorLumens * sizeScale, recipe.practicalRange * 0.75f,
                    recipe.practicalColor, recipe.practicalVolumetric);
                made++;
            }
        }
        return made;
    }

    static void MakePoint(Transform parent, string name, Vector3 pos, float lumens, float range,
        Color color, bool volumetric)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        // Shadows off on practicals: a dozen shadow-casting point lights is a real cost on this
        // scene's 3,300 renderers, and the pools read as glow rather than as a shaped key anyway.
        light.shadows = LightShadows.None;

        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.affectsVolumetric = volumetric;
        hd.lightUnit = LightUnit.Lumen;
        light.intensity = lumens;

        // Flame, not a bulb. A perfectly steady window in an abandoned village reads as a light
        // fixture; an unsteady one reads as something burning, which is the whole point of these
        // being lit at all. Speed is seeded off world position so no two windows breathe in sync --
        // synchronised flicker is worse than none, it announces itself as an effect.
        var flicker = go.AddComponent<GmLightFlicker>();
        flicker.baseIntensity = lumens;
        flicker.variation = 0.19f;
        flicker.speed = 1.5f + Mathf.Abs(pos.x * 0.037f + pos.z * 0.021f) % 1.3f;
    }

    static IEnumerable<Renderer> FindBuildings() =>
        UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.gameObject.activeInHierarchy)
            .Where(r => !LodSuffix.IsMatch(r.name) && BuildingPattern.IsMatch(r.name))
            .OrderBy(r => r.bounds.center.z);

    // Spawn AT the pack's own hero camera, on the ground, facing the way it faced. The first build
    // computed this from Terrain bounds instead and put the player in the empty field south of the
    // village -- the bottom half of that frame was bare dirt, which is the exact complaint this
    // whole environment pivot exists to fix.
    static Vector3 PlacePlayer(Vantage vantage, GmVillageEstate.Site site)
    {
        GameObject stale = GameObject.Find(PlayerName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) throw new InvalidOperationException("village scene has no Terrain");
        if (!vantage.valid && !site.valid)
            throw new InvalidOperationException("no showcase camera or estate site to derive spawn from");

        // Prefer the estate's arrival spawn: the prologue opens at the car, north of the gate,
        // looking south down the road. The showcase vantage stays as the fallback so the scene is
        // still walkable if the estate layer is ever skipped.
        Vector3 spawn;
        Quaternion facing;
        if (site.valid)
        {
            spawn = site.spawn;
            facing = site.spawnRotation;
        }
        else
        {
            Vector3 at = vantage.position;
            spawn = new Vector3(at.x, terrain.SampleHeight(at) + terrain.transform.position.y + 0.1f, at.z);
            Vector3 flat = vantage.rotation * Vector3.forward;
            flat.y = 0f;
            facing = Quaternion.LookRotation(
                flat.sqrMagnitude < 0.001f ? Vector3.forward : flat.normalized, Vector3.up);
        }

        var player = new GameObject(PlayerName);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        player.transform.position = spawn;
        // Yaw only. The showcase camera carries a deliberate downward tilt for its still, which
        // would start the player staring at their own boots.
        player.transform.rotation = facing;

        var camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 800f;
        camGo.AddComponent<HDAdditionalCameraData>();
        camGo.AddComponent<AudioListener>();

        player.AddComponent<GmPlayer>();
        return spawn;
    }

    // Mirrors the estate scene's GmSystems object. The Z-threshold systems are re-pointed at the
    // village's own world-Z values, all derived from the single estate->village map in
    // GmVillageEstate, so the gate lock and porch refusal fire where the gate and porch actually
    // are rather than at the estate's coordinates.
    static void BuildSystems()
    {
        GameObject stale = GameObject.Find(SystemsName);
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        var systems = new GameObject(SystemsName);
        systems.AddComponent<GmExperienceTelemetry>();
        systems.AddComponent<GmAudioMixController>();

        var design = systems.AddComponent<GmDesignRuntime>();
        design.designFile = GmVillageDesign.VillageDesignFile;

        systems.AddComponent<GmAmbience>();

        // The bell is diegetic and locatable. Its source position was a const authored for the
        // ESTATE chapel, 200m from the village church, so left alone the nine tolls would have come
        // from an empty field. Pinned to the chapel POI, which is itself anchored to the church.
        var bell = systems.AddComponent<GmBellSummons>();
        GameObject chapel = GameObject.Find("POI_chapel-door");
        if (chapel != null)
            bell.ChapelPosition = chapel.transform.position + Vector3.up * 6f;
        else
            Debug.LogWarning("[GmVillageBuilder] no POI_chapel-door; bell will toll from the estate's coordinate");

        systems.AddComponent<GmSymptoms>();

        var threshold = systems.AddComponent<GmThreshold>();
        threshold.gateZ = GmVillageEstate.VillageGateZ;
        threshold.arrivalZ = GmVillageEstate.VillageArrivalZ;

        systems.AddComponent<GmCrossing>();

        // The window figure's disappear threshold is another world-Z coordinate authored for the
        // estate. Estate z=+18 is off the north end of the village entirely, so left alone the
        // figure would blink out the moment it armed.
        var rare = systems.AddComponent<GmRareEvents>();
        rare.FigureGoneBelow = GmVillageEstate.EstateZToVillageZ(GmRareEvents.FigureGoneBelowZ);

        systems.AddComponent<GmColdOpen>();

        var secret = systems.AddComponent<GmSecretEnding>();
        secret.carZ = GmVillageEstate.VillageCarZ;
        secret.gateZ = GmVillageEstate.VillageGateZ;

        systems.AddComponent<GmPrologueHud>();
        systems.AddComponent<GmVillageSave>();

        Debug.Log($"[GmVillageBuilder] systems: gateZ={threshold.gateZ:0.#} " +
                  $"arrivalZ={threshold.arrivalZ:0.#} carZ={secret.carZ:0.#} " +
                  $"figureGoneBelowZ={rare.FigureGoneBelow:0.#} " +
                  $"bell={bell.ChapelPosition} design={design.designFile}");
    }

    static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T c = profile.Add<T>(true);
        c.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }
}

