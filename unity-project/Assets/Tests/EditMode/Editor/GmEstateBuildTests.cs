// Builds the real estate and asserts against the design data. Every test here exists because the
// bug it catches already shipped once and was invisible: the scene built, the log said 0 missing,
// and it rendered a magenta daylight field.
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public class GmEstateBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmEstateBuilderV2.Build();

    [Test]
    public void SerializedSceneRetainsCompositionIntelligenceAfterReopen()
    {
        // Compare this build with its serialized/reopened form. A hard-coded fingerprint only
        // proves the scene still matches one historical revision and turns every intentional art
        // addition into test maintenance; it does not prove persistence.
        string builtFingerprint = GmEstateQualityAudit.LayoutFingerprint();
        EditorSceneManager.OpenScene(GmEstateBuilderV2.ScenePath, OpenSceneMode.Single);
        Assert.AreEqual(8, Object.FindObjectsByType<GmCompositionZone>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(15, Object.FindObjectsByType<GmCompositionCluster>(FindObjectsInactive.Include).Length);
        Assert.GreaterOrEqual(Object.FindObjectsByType<GmCompositionElement>(FindObjectsInactive.Include).Length, 72);
        Assert.AreEqual(18, Object.FindObjectsByType<GmReviewCompositionClaim>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(2, Object.FindObjectsByType<GmAdaptiveSlot>(FindObjectsInactive.Include).Length);
        foreach (GmAdaptiveSlot slot in Object.FindObjectsByType<GmAdaptiveSlot>(FindObjectsInactive.Include))
            Assert.IsNull(slot.GetComponent<GmClearedPathGuide>(),
                $"adaptive slot '{slot.SlotId}' targets a negative-space path and preview geometry will repaint it");
        GmAdaptiveSlot gardenSlot = Object.FindObjectsByType<GmAdaptiveSlot>(FindObjectsInactive.Include)
            .Single(slot => slot.SlotId == "garden-detail-slot");
        Assert.AreEqual("garden-bed-detail-01", gardenSlot.GetComponent<GmCompositionElement>()?.ElementId,
            "garden variants must replace one authored detail, not hide/move the complete bed family");
        Assert.AreEqual(1, Object.FindObjectsByType<GmRepetitionIntent>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GmEnvironmentalStoryIntent>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GmStyleIntent>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GmSurfacePaletteIntent>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GmLandscapeDepthIntent>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GmSoundscapeIntent>(FindObjectsInactive.Include).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GmPacingIntent>(FindObjectsInactive.Include).Length);
        Assert.IsEmpty(GmSceneCompositionAudit.ValidateOpenScene(GmEstateBuilderV2.SceneId,
            Object.FindAnyObjectByType<GmShotTour>(), Camera.main));
        Assert.AreEqual(builtFingerprint, GmEstateQualityAudit.LayoutFingerprint(),
            "layout changed when the freshly built scene was reopened from disk");
    }

    static GameObject Find(string namePart)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.Contains(namePart)) return go;
        return null;
    }

    /// Placement names are "{kind} ({asset})" (GmEstateBuilderV2.BuildPlacements, line ~296). A plain
    /// Contains match on a kind is unsafe when one kind is a literal prefix of another: the design
    /// data has both "estateCar" (the POI car) and "estateCart" (SM_Cart, an unrelated prop), and
    /// "estateCart (SM_Cart)".Contains("estateCar") is true. Matching "{kind} (" as a prefix instead
    /// of Contains-anywhere closes that collision.
    static GameObject FindByKind(string kind)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.StartsWith(kind + " (")) return go;
        return null;
    }

    /// A bare GameObject name is not enough to find a magenta object in a 7,000-object estate.
    static string HierarchyPath(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (Transform cursor = t; cursor != null; cursor = cursor.parent) parts.Add(cursor.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// Root cause 1. The Leartes packs ship built-in twins of every HDRP asset; a built-in material
    /// renders magenta under HDRP. This failed silently for the entire estate.
    [Test]
    public void NoRendererUsesABuiltinShader()
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
            {
                // A null material or null shader renders magenta exactly like a built-in one does.
                // This guard used to `continue` past both, so it caught only half its own defect
                // class — the half that happened to be found first.
                Assert.IsNotNull(m,
                    $"{HierarchyPath(r.transform)} has a missing material — renders magenta under HDRP");
                Assert.IsNotNull(m.shader,
                    $"{HierarchyPath(r.transform)} material '{m.name}' has no shader — renders magenta under HDRP");
                var path = AssetDatabase.GetAssetPath(m.shader);
                bool builtin = string.IsNullOrEmpty(path) || path.StartsWith("Resources/") || path.StartsWith("Library/");
                Assert.IsFalse(builtin, $"{HierarchyPath(r.transform)} uses built-in shader '{m.shader.name}' — renders magenta under HDRP");
            }
        }
    }

    /// Canon fixes the floor and the ceiling even though the escalation schedule is still Nick's
    /// call: Tier 1 is the floor because he is already cheating when you meet him, and the ceiling
    /// keeps "high corruption" a reachable state that content can gate off.
    [Test]
    public void CorruptionClimbsWithinItsCanonBounds()
    {
        GmHouseProgress.BeginNewRun();
        Assert.AreEqual(GmHouseProgress.MinCorruptionTier, GmHouseProgress.CorruptionTierTotal,
            "a run must start at Tier 1 — Tier 0 would mean he was ever playing straight");

        for (int i = GmHouseProgress.MinCorruptionTier; i < GmHouseProgress.MaxCorruptionTier; i++)
            Assert.IsTrue(GmHouseProgress.RaiseCorruption("test"), $"tier stuck below the ceiling at {i}");

        Assert.AreEqual(GmHouseProgress.MaxCorruptionTier, GmHouseProgress.CorruptionTierTotal);
        Assert.IsFalse(GmHouseProgress.RaiseCorruption("test"), "tier climbed past its ceiling");

        GmHouseProgress.BeginNewRun();
        Assert.AreEqual(GmHouseProgress.MinCorruptionTier, GmHouseProgress.CorruptionTierTotal,
            "a new run did not return to the floor");
    }

    /// The one hard blocker on the true ending: until the run survives a scene change, Court's and
    /// Shut the Box's catches cannot count toward it at all. Destroying the component must NOT
    /// clear the tally, and only BeginNewRun may.
    [Test]
    public void RunStateSurvivesLosingItsComponent()
    {
        GmHouseProgress.BeginNewRun();
        var host = new GameObject("ProgressProbe");
        try
        {
            var progress = host.AddComponent<GmHouseProgress>();
            progress.CatchCheat("test-cheat-a");
            progress.CatchCheat("test-cheat-b");
            progress.CatchCheat("test-cheat-a");   // same id twice must not bank twice
            progress.FindShard("test-shard");
            Assert.AreEqual(2, progress.CheatsCaught, "a repeated clue id banked a second catch");
            Assert.AreEqual(1, progress.MirrorShards);
        }
        finally { Object.DestroyImmediate(host); }

        // The scene that owned it is gone. This is exactly the transition that used to reset the run.
        Assert.AreEqual(2, GmHouseProgress.CheatsCaughtTotal,
            "cheatsCaught did not survive losing its component — Court and Shut the Box cannot " +
            "contribute to the true ending if the tally dies with the scene");
        Assert.AreEqual(1, GmHouseProgress.MirrorShardsTotal);

        var revived = new GameObject("ProgressProbe2");
        try
        {
            Assert.AreEqual(2, revived.AddComponent<GmHouseProgress>().CheatsCaught,
                "a fresh component in a new scene did not see the existing run");
        }
        finally { Object.DestroyImmediate(revived); }

        GmHouseProgress.BeginNewRun();
        Assert.AreEqual(0, GmHouseProgress.CheatsCaughtTotal, "BeginNewRun did not clear the run");
    }

    /// The interior added on 2026-07-31 was exempt from BOTH the runtime culling and the editor perf
    /// pass, so ~12 soft-shadow point lights stayed live across all 435 outdoor metres and nothing
    /// measured it. Route p95 was 20.35ms until the exemption was replaced with a phase rule. This
    /// pins the contract that made it cheap: the interior must be culled by phase, never exempt.
    [Test]
    public void InteriorIsGatedByPhaseNotExemptFromCulling()
    {
        var culling = typeof(GmWendRuntimeCulling);
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        Assert.IsNotNull(culling.GetField("interiorRenderers", any),
            "runtime culling no longer tracks the interior separately — a blanket exemption leaves " +
            "its renderers and soft-shadow lights live across the whole outdoor route");
        Assert.IsNotNull(culling.GetField("interiorLights", any),
            "interior LIGHTS are no longer tracked; ~12 soft-shadow point lights across 435m was " +
            "the actual cost, not the renderers");
        Assert.IsNotNull(culling.GetField("InteriorPerFrame", any),
            "the interior reveal is no longer sliced — toggling ~208 objects in one frame cost a " +
            "123ms spike, the same lesson the main sweep already learned");
    }

    /// Terrain trees and details are drawn by the Terrain system, not by Renderer components, so
    /// FindObjectsByType<Renderer>() never sees them and every magenta guard we had walked straight
    /// past them. GmWendGrassTone rewrites these prototypes to owned material copies, which is
    /// exactly the kind of remap that can leave a prototype pointing at nothing.
    [Test]
    public void NoTerrainPrototypeRendersMagenta()
    {
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            var data = terrain.terrainData;
            if (data == null) continue;
            var protos = data.treePrototypes;
            for (int i = 0; i < protos.Length; i++)
            {
                var prefab = protos[i].prefab;
                Assert.IsNotNull(prefab,
                    $"{terrain.name} tree prototype {i} has no prefab — renders magenta");
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m,
                            $"{terrain.name} tree prototype {i} '{prefab.name}' / {r.name} has a " +
                            "missing material — renders magenta");
                        Assert.IsNotNull(m.shader,
                            $"{terrain.name} tree prototype {i} '{prefab.name}' / {r.name} material " +
                            $"'{m.name}' has no shader — renders magenta");
                        var path = AssetDatabase.GetAssetPath(m.shader);
                        bool builtin = string.IsNullOrEmpty(path) ||
                            path.StartsWith("Resources/") || path.StartsWith("Library/");
                        Assert.IsFalse(builtin,
                            $"{terrain.name} tree prototype {i} '{prefab.name}' / {r.name} uses " +
                            $"built-in shader '{m.shader.name}' — renders magenta under HDRP");
                    }
                }
            }
        }
    }

    /// Root cause 2. VolumeProfile.Add<T>() is memory-only; without AddObjectToAsset the profile
    /// reloads as components:[] and HDRP silently falls back to default sky + AUTOMATIC exposure,
    /// which re-brightens authored darkness and makes every lighting value a no-op.
    [Test]
    public void NightVolumeProfilePersistsItsOverrides()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsNotNull(profile, "night volume profile asset missing");
        Assert.Greater(profile.components.Count, 0, "profile persisted 0 overrides — HDRP will fall back to default sky + auto exposure");
        Assert.IsTrue(profile.Has<UnityEngine.Rendering.HighDefinition.Exposure>(), "no Exposure override — auto-exposure will re-brighten the night");
    }

    /// Fixed exposure is deliberate (see GmEstateBuilderV2 header). Automatic would undo the dark.
    [Test]
    public void ExposureIsFixedNotAutomatic()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsTrue(profile.TryGet(out UnityEngine.Rendering.HighDefinition.Exposure e), "no Exposure override");
        // Compare enum-to-enum. The old form cast the expected side to (int) while e.mode.value stays
        // an Enum, so Assert.AreEqual compared int-to-Enum and NEVER matched, even when Fixed was set.
        Assert.AreEqual(UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed, e.mode.value,
            "exposure is not Fixed — automatic re-brightens authored darkness");
    }

    [Test]
    public void DisplayCalibrationIsSharedPersistedAndCentredOnTheAuthoredGrade()
    {
        var host = GameObject.Find("NightVolume");
        Assert.IsNotNull(host, "display grade has no stable scene owner");
        Assert.IsNotNull(host.GetComponent<GmDisplayCalibration>(),
            "player cannot calibrate dark displays from keyboard or controller");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsTrue(profile.TryGet(out UnityEngine.Rendering.HighDefinition.ColorAdjustments colour));
        Assert.IsTrue(colour.postExposure.overrideState,
            "authored brightness baseline is implicit and can drift with HDRP defaults");
        Assert.AreEqual(0f, colour.postExposure.value,
            "the persisted profile must remain level 0; player calibration is a runtime offset");
    }

    [Test]
    public void NightUsesControlledGradientAndFilmicHighlightRolloff()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsTrue(profile.Has<UnityEngine.Rendering.HighDefinition.GradientSky>(),
            "no controlled GradientSky — the physical sky regresses to the cobalt daylight dome");
        Assert.IsTrue(profile.TryGet(out UnityEngine.Rendering.HighDefinition.Tonemapping tone),
            "no tonemapping — mansion windows and chapel trim hard-clip to white");
        Assert.AreEqual(UnityEngine.Rendering.HighDefinition.TonemappingMode.ACES, tone.mode.value,
            "night is not using ACES highlight rolloff");
        var moon = GameObject.Find("Moonlight");
        Assert.IsNotNull(moon);
        var moonData = moon.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        Assert.That(moonData.angularDiameter, Is.GreaterThanOrEqualTo(2f),
            "moon shadows returned to razor-edged ground wedges that masquerade as geometry");
    }

    /// The Prologue is walking toward a lit house. Without it there is no opening to judge.
    [Test]
    public void MansionExistsAtAuthoredDepth()
    {
        var m = Find("Mansion (gravyart");
        Assert.IsNotNull(m, "no mansion in the built scene");
        Assert.AreEqual(-58f, m.transform.position.z, 0.01f, "mansion is not at the authored mansionZ");
        int nightMaterials = 0;
        foreach (var renderer in m.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.name.Contains("_WendHillNight")) nightMaterials++;
        Assert.Greater(nightMaterials, 0,
            "mansion returned to its chalk-pale daylight showcase materials");
    }

    /// Threshold Refusal is locked canon: the doors never open. Cube043 is the model's door slab.
    [Test]
    public void MansionDoorSlabIsHidden()
    {
        var m = Find("Mansion (gravyart");
        Assert.IsNotNull(m);
        int matched = 0;
        foreach (var t in m.GetComponentsInChildren<Transform>(true))
        {
            // Real FBX node names carry a literal dot ("Cube.043", "Cube.043_Columns_0" — confirmed
            // via `strings` on the imported model; GmMansion.cs's own header documents this same
            // fact). GmMansion.SealTheDoors() strips dots before comparing (t.name.Replace(".", ""))
            // and this test must do the same, or the undotted literal never matches any real
            // transform and the Assert below never runs for anything — a pass that proves nothing.
            string flat = t.name.Replace(".", "");
            if (flat != "Cube043" && !flat.StartsWith("Cube043_")) continue;
            matched++;
            var r = t.GetComponent<Renderer>();
            Assert.IsFalse(r != null && r.enabled && t.gameObject.activeInHierarchy,
                "the sealed door slab is visible — Threshold Refusal is broken");
        }
        Assert.Greater(matched, 0,
            "no Cube043 door-slab transforms found under the mansion — the name match is broken and this test asserted nothing");
    }

    [Test]
    public void MansionWindowsHaveDeterministicDarkDimAndLitStates()
    {
        var mansion = Find("Mansion (gravyart");
        int dark = 0, dim = 0, lit = 0;
        foreach (var renderer in mansion.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (material.name.Contains("Gm_WindowDark")) dark++;
                else if (material.name.Contains("Gm_WindowDim")) dim++;
                else if (material.name.Contains("Gm_WindowLit")) lit++;
            }
        Assert.Greater(dark, 0, "all mansion windows are lit — no dead panes remain");
        Assert.Greater(dim, 0, "mansion windows have no dim middle state");
        Assert.Greater(lit, 0, "mansion reads as completely dead from the drive");
        Assert.GreaterOrEqual(dark + dim + lit, 6, "too few glass slots were classified to vary the facade");
    }

    [Test]
    public void WindowFigureCanBeForcedAndVanishesPermanentlyPastTheCutoff()
    {
        var rig = GameObject.Find("WindowFigureRig");
        if (rig == null)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "WindowFigureRig") rig = root;
        }
        Assert.IsNotNull(rig, "window-figure rig is absent");
        Assert.AreEqual(0, rig.GetComponentsInChildren<Collider>(true).Length, "figure VFX carries physical collision");
        Assert.IsNull(GameObject.Find("FigurePaneLight"),
            "figure owns a replacement pane, so the event can create a glowing rectangular artifact");
        Assert.IsFalse(rig.GetComponentsInChildren<Renderer>(true).Any(renderer =>
            renderer.sharedMaterials.Any(material => material != null &&
                material.name.Contains("Glow", System.StringComparison.OrdinalIgnoreCase))),
            "figure event owns an emissive backlight instead of changing only silhouette pixels");
        Assert.GreaterOrEqual(rig.GetComponentsInChildren<Renderer>(true).Length, 3,
            "window figure has no shoulder read and can disappear into the facade mullion");
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
        {
            var serialized = new SerializedObject(renderer);
            var smallMeshCulling = serialized.FindProperty("m_SmallMeshCulling");
            Assert.IsTrue(smallMeshCulling == null || !smallMeshCulling.boolValue,
                $"figure renderer '{renderer.name}' can be culled at the authored 80m reveal distance");
        }
        var rare = Object.FindFirstObjectByType<GmRareEvents>();
        rare.ForceFigureForReview();
        Assert.IsTrue(rare.FigureVisible, "forced figure is not visible from the drive");
        rare.EvaluateFigureAtZ(GmRareEvents.FigureGoneBelowZ - 0.1f);
        Assert.IsFalse(rare.FigureVisible, "figure remains after the player passes its disappearance threshold");
        rare.EvaluateFigureAtZ(GmRareEvents.FigureGoneBelowZ + 20f);
        Assert.IsFalse(rare.FigureVisible, "figure reappeared after it was permanently dismissed");
    }

    /// The gate is the point of no return and GmThreshold already locks it. It must be visible.
    [Test]
    public void GateAndCarArePlacedAtAuthoredCoordinates()
    {
        var gate = FindByKind("estateGate");
        var car = FindByKind("estateCar");
        Assert.IsNotNull(gate, "no gate — GmThreshold locks a gate the player cannot see");
        Assert.IsNotNull(car, "no car — spawn reads as an empty road and the secret ending has no exit");
        Assert.AreEqual(65f, gate.transform.position.z, 1.5f, "gate is not at gateZ");
        Assert.AreEqual(76.5f, car.transform.position.z, 1.5f, "car is not at its POI");
    }

    [Test]
    public void GatePlaqueIsMountedAndDetailedOnTheArrivalFace()
    {
        GameObject gate = FindByKind("estateGate");
        GameObject plaque = GameObject.Find("WendHillGatePlaque");
        Assert.IsNotNull(gate);
        Assert.IsNotNull(plaque);
        Renderer[] gateRenderers = gate.GetComponentsInChildren<Renderer>(true);
        Renderer[] plaqueRenderers = plaque.GetComponentsInChildren<Renderer>(true);
        Assert.GreaterOrEqual(plaqueRenderers.Length, 9,
            "gate plaque regressed to one unexplained flat slab without frame/inset/monogram");
        Bounds gateBounds = gateRenderers[0].bounds;
        foreach (Renderer renderer in gateRenderers.Skip(1)) gateBounds.Encapsulate(renderer.bounds);
        Bounds plaqueBounds = plaqueRenderers[0].bounds;
        foreach (Renderer renderer in plaqueRenderers.Skip(1)) plaqueBounds.Encapsulate(renderer.bounds);
        Assert.Greater(plaqueBounds.min.z, gateBounds.center.z,
            "gate plaque detail is mounted on the far face and cannot be read from spawn");
        Assert.That(plaqueBounds.size.x, Is.InRange(0.8f, 1.5f));
        Assert.That(plaqueBounds.size.y, Is.InRange(0.4f, 0.8f));
    }

    [Test]
    public void PlayerStartsFacingTheMansionNotTheExitCar()
    {
        var player = GameObject.Find("Player");
        var mansion = Find("Mansion (gravyart");
        Assert.IsNotNull(player);
        Assert.IsNotNull(mansion);
        Vector3 toMansion = mansion.transform.position - player.transform.position;
        toMansion.y = 0f;
        Assert.Greater(Vector3.Dot(player.transform.forward, toMansion.normalized), 0.999f,
            "spawn camera faces away from Wend Hill; W walks back to the car and fires the secret ending");
        Assert.Less(player.transform.forward.z, -0.999f, "the authored opening route runs toward -Z");
    }

    [Test]
    public void PrologueHasResponsiveRuntimeHudAndLookPath()
    {
        Assert.IsNotNull(Object.FindAnyObjectByType<GmPrologueHud>(),
            "no runtime HUD; player falls back to unreadable resolution-dependent IMGUI text");
        var player = Object.FindAnyObjectByType<GmPlayer>();
        Assert.IsNotNull(player);
        Quaternion playerBefore = player.transform.rotation;
        var camera = player.GetComponentInChildren<Camera>().transform;
        Quaternion cameraBefore = camera.localRotation;
        try
        {
            player.ApplyLookDelta(new Vector2(100f, -20f));
            Assert.Greater(Quaternion.Angle(playerBefore, player.transform.rotation), 5f,
                "mouse delta does not rotate the player camera path");
            Assert.Greater(Quaternion.Angle(cameraBefore, camera.localRotation), 1f,
                "vertical mouse delta does not pitch the camera");
        }
        finally
        {
            player.transform.rotation = playerBefore;
            camera.localRotation = cameraBefore;
        }
    }

    [Test]
    public void OpeningHasAuthoredDepthHierarchyInsteadOfFlatDemoLighting()
    {
        var camera = Object.FindAnyObjectByType<GmPlayer>().GetComponentInChildren<Camera>();
        Assert.That(camera.fieldOfView, Is.InRange(49f, 53f),
            "opening lens is too wide and flattens the mansion into the horizon");
        var cameraData = camera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        Assert.IsNotNull(cameraData, "player camera lost its HDRP settings");
        Assert.AreEqual(UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing,
            cameraData.antialiasing, "standalone player returned to jagged, unfiltered HDRP output");

        var groundMist = GameObject.Find("DriveMistLayer")?
            .GetComponent<UnityEngine.Rendering.HighDefinition.LocalVolumetricFog>();
        Assert.IsNotNull(groundMist, "low atmospheric layer is missing");
        Assert.GreaterOrEqual(groundMist.parameters.size.x, 80f,
            "local-fog box edges can cut visible pale seams through the cemetery or garden");
        Assert.LessOrEqual(groundMist.parameters.size.y, 0.7f,
            "ground mist again blankets the player view instead of staying below eye height");
        var horizonMist = GameObject.Find("HorizonMist");
        Assert.IsNotNull(horizonMist, "pale terrain/sky boundary has no atmospheric backbuffer");
        Assert.AreEqual(4,
            horizonMist.GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.LocalVolumetricFog>(true).Length,
            "horizon backbuffer must cover north, south, east and west without a single perimeter slab");

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/WendHillNight.asset");
        Assert.IsTrue(profile.Has<UnityEngine.Rendering.HighDefinition.ColorAdjustments>(),
            "night lost its blue/amber contrast grade");
        Assert.IsTrue(profile.Has<UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion>(),
            "props will float without contact/ambient occlusion");
        Assert.IsTrue(profile.Has<UnityEngine.Rendering.HighDefinition.Vignette>(),
            "opening lost its restrained frame hierarchy");
        Assert.IsNotNull(GameObject.Find("NightSkyFill"),
            "black shadow faces returned; no broad sky return remains");

        var porchCourt = GameObject.Find("PorchArrivalCourt");
        Assert.IsNotNull(porchCourt, "mansion still meets a broad empty soil apron");
        Assert.GreaterOrEqual(porchCourt.GetComponentsInChildren<Renderer>(true).Length, 15,
            "porch arrival court is present in hierarchy but has no readable drainage/leaf/growth layers");
        Assert.AreEqual(0, porchCourt.GetComponentsInChildren<Collider>(true).Length,
            "porch arrival dressing can snag the final approach");
        Assert.IsNull(GameObject.Find("MoonDisc"),
            "primitive moon sphere returned; sky depth must come from haze, acreage and woodland silhouettes");

        var practicals = GameObject.Find("PracticalLights");
        Assert.IsNotNull(practicals, "no motivated practical-light layer");
        Assert.GreaterOrEqual(practicals.GetComponentsInChildren<GmLightFlicker>(true).Length, 5,
            "gate, chapel and brazier sources are static or missing");
        Assert.IsNotNull(GameObject.Find("GateLanternLeft"));
        Assert.IsNotNull(GameObject.Find("GateLanternRight"));
        Assert.IsNotNull(GameObject.Find("ChapelDoorLantern"));
        Assert.IsNotNull(GameObject.Find("GardenWellLantern"));

        var gate = FindByKind("estateGate");
        var renderers = gate.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Assert.That(bounds.size.y, Is.InRange(3.35f, 3.85f),
            "entry gate again dominates the whole spawn frame instead of framing the route");
    }

    [Test]
    public void AuthoredTextEscapesDecodeBeforeReachingTheHud()
    {
        string decoded = GmDesignRuntime.DecodeAuthoredText(@"don\u2019t stop\nkeep walking");
        Assert.AreEqual("don’t stop\nkeep walking", decoded);
        StringAssert.DoesNotContain(@"\u", decoded,
            "raw JSON unicode escapes will render as source text in the standalone UI");
    }

    /// The estate is unbounded without these; the cemetery stops being a place you enter.
    [Test]
    public void WalkBoundsExist()
    {
        var bounds = Find("WalkBounds");
        Assert.IsNotNull(bounds, "no walk bounds — the player can walk into the void");
        Assert.Greater(bounds.GetComponentsInChildren<BoxCollider>().Length, 15, "suspiciously few bound walls");
    }

    [Test]
    public void BackdropAndSideSpacesAreComposedButDoNotChangeWalkCollision()
    {
        var backdrop = Find("TerrainBackdrop");
        var cemetery = GameObject.Find("Cemetery");
        var garden = GameObject.Find("KitchenGarden");
        Assert.IsNotNull(backdrop, "no terrain backdrop — flat ground/sky seam returns");
        Assert.AreEqual(4, backdrop.GetComponentsInChildren<MeshFilter>().Length,
            "terrain backdrop should have four perimeter ridges");
        Assert.AreEqual(0, backdrop.GetComponentsInChildren<Collider>().Length,
            "distant ridges gained colliders — playable y=0 route can be disrupted");
        Assert.IsNotNull(cemetery, "cemetery composition missing");
        Assert.IsNotNull(garden, "kitchen garden composition missing");
        Assert.Greater(cemetery.transform.childCount, 10, "cemetery regressed to a few loose props");
        Assert.Greater(garden.GetComponentsInChildren<Transform>(true).Length, 15,
            "garden regressed to an empty field");

        var cemeteryPath = cemetery.transform.Find("CemeteryCrossPath");
        var gardenPath = garden.transform.Find("GardenEntryPath");
        Assert.IsNotNull(cemeteryPath, "cemetery lost its cross-path");
        Assert.IsNotNull(gardenPath, "garden lost its entry path");
        foreach (Transform path in new[] { cemeteryPath, gardenPath })
        {
            Assert.IsNotNull(path.GetComponent<GmClearedPathGuide>(),
                $"{path.name} lost its measurable negative-space path contract");
            Assert.AreEqual(0, path.GetComponentsInChildren<Renderer>(true).Length,
                $"{path.name} returned as opaque floor geometry instead of composed negative space");
            Assert.AreEqual(0, path.GetComponentsInChildren<Collider>(true).Length,
                $"{path.name} gained collision and can alter the authored walk route");
        }

        var edgeLitter = Find("DriveEdgeLitter");
        Assert.IsNotNull(edgeLitter, "drive verges returned to undressed empty planes");
        Assert.GreaterOrEqual(edgeLitter.GetComponentsInChildren<Renderer>(true).Length, 10,
            "drive edge litter is too sparse to break the flat ground read");
        Assert.AreEqual(0, edgeLitter.GetComponentsInChildren<Collider>(true).Length,
            "decorative leaf litter gained collision and can snag the route");
    }

    [Test]
    public void EstateUsesAnAuthoredLayeredLandscapeAndBrokenEcologicalBoundaries()
    {
        var ground = GameObject.Find("Ground");
        Assert.IsNotNull(ground);
        var terrain = ground.GetComponent<Terrain>();
        Assert.IsNotNull(terrain, "estate returned to a generated flat mesh instead of the authored terrain base");
        Assert.AreEqual("Assets/Scenes/WendHill_AuthoredTerrain.asset",
            AssetDatabase.GetAssetPath(terrain.terrainData),
            "the scene must use a clone, never modify or bind directly to the purchased TerrainData");
        Assert.GreaterOrEqual(terrain.terrainData.terrainLayers.Length, 3,
            "mud, leaf litter and desaturated moss no longer form a real surface palette");
        Assert.GreaterOrEqual(terrain.terrainData.terrainLayers
            .Select(layer => layer == null ? null : layer.diffuseTexture)
            .Where(texture => texture != null).Distinct().Count(), 3,
            "terrain must retain three ecological surface textures");
        // Worked soil was added after the road layer, so array position is no longer a semantic
        // contract. Unity renames generated TerrainLayer objects to their asset filenames, so the
        // owned non-directional road texture is the stable semantic identity.
        var roadLayer = terrain.terrainData.terrainLayers.SingleOrDefault(layer =>
            layer != null && layer.diffuseTexture != null &&
            layer.diffuseTexture.name == "T_SwampMud_B");
        Assert.IsNotNull(roadLayer, "authored wet-road terrain layer is missing");
        Assert.AreEqual("T_SwampMud_B", roadLayer.diffuseTexture.name,
            "the road stopped using the owned non-directional mud texture and may show crosswise tiling again");
        Assert.AreNotSame(terrain.terrainData.terrainLayers.First(), roadLayer,
            "road paint needs an independently remapped wet-soil layer even when it shares the acreage's mud texture");
        var driveRenderer = GameObject.Find("Drive").GetComponent<MeshRenderer>();
        Assert.IsNotNull(driveRenderer);
        Assert.IsFalse(driveRenderer.enabled,
            "the semantic drive mesh is visible and will render as a hard-edged floor over the terrain");
        int mapWidth = terrain.terrainData.alphamapWidth;
        int mapHeight = terrain.terrainData.alphamapHeight;
        float[,,] fullPaint = terrain.terrainData.GetAlphamaps(0, 0, mapWidth, mapHeight);
        for (int layer = 0; layer < 4; layer++)
        {
            float maximum = 0f;
            for (int z = 0; z < mapHeight; z += 8)
                for (int x = 0; x < mapWidth; x += 8)
                    maximum = Mathf.Max(maximum, fullPaint[z, x, layer]);
            Assert.Greater(maximum, 0.02f,
                $"terrain layer {layer + 1} exists but carries no ecological paint");
        }
        float[,,] flankPaint = terrain.terrainData.GetAlphamaps(mapWidth / 2 + mapWidth / 8,
            mapHeight / 2, 1, 1);
        float strongestTrack = 0f;
        int paintedRows = 0;
        for (int z = 0; z < mapHeight; z += 8)
        {
            float rowTrack = 0f;
            for (int x = mapWidth / 2 - 7; x <= mapWidth / 2 + 7; x++)
                rowTrack = Mathf.Max(rowTrack, fullPaint[z, x, 3]);
            strongestTrack = Mathf.Max(strongestTrack, rowTrack);
            if (rowTrack > 0.12f) paintedRows++;
        }
        Assert.Greater(strongestTrack, 0.35f,
            "the owned road layer is present but never establishes a wheel track");
        Assert.GreaterOrEqual(paintedRows, mapHeight / 24,
            "the wheel tracks have so few surviving sections that the route cannot read");
        Assert.Less(flankPaint[0, 0, 3], 0.08f,
            "the road texture floods the acreage instead of feathering out at the verge");

        var verge = GameObject.Find("DriveVergeCommunities");
        Assert.IsNotNull(verge);
        Assert.GreaterOrEqual(verge.transform.childCount, 100,
            "the route lost the clustered ecological scale between leaf litter and trees");
        Assert.AreEqual(0, verge.GetComponentsInChildren<Collider>(true).Length,
            "decorative verge ecology can snag the playable lane");

        var understory = GameObject.Find("EstateUnderstory");
        Assert.IsNotNull(understory, "terrain and hero props have no player-height ecological layer");
        Assert.GreaterOrEqual(understory.GetComponentsInChildren<Renderer>(true).Length, 650,
            "understory is too sparse to stop the estate reading as objects placed in a room");
        Assert.AreEqual(0, understory.GetComponentsInChildren<Collider>(true).Length,
            "decorative grass can snag the player route");
        var roadside = understory.transform.Find("RoadsideUnderstory");
        var cemeteryGrowth = understory.transform.Find("CemeteryUnderstory");
        var gardenGrowth = understory.transform.Find("GardenUnderstory");
        Assert.GreaterOrEqual(roadside.childCount, 420);
        Assert.GreaterOrEqual(cemeteryGrowth.childCount, 145);
        Assert.GreaterOrEqual(gardenGrowth.childCount, 90);
        Assert.IsTrue(roadside.Cast<Transform>().All(item => Mathf.Abs(item.position.x) >= 2.68f),
            "roadside grass entered the central playable tread");
        Assert.IsTrue(cemeteryGrowth.Cast<Transform>().All(item =>
                Mathf.Abs(item.position.x - 16.2f) >= 1.24f && Mathf.Abs(item.position.z - 28.5f) >= 1.01f),
            "cemetery understory obscures the authored cross path or grave spine");
        Assert.IsTrue(gardenGrowth.Cast<Transform>().All(item => Mathf.Abs(item.position.z - 22.1f) >= 1.06f),
            "garden understory obscures the authored entry path");

        float[] heights =
        {
            GmExteriorTerrainComposer.GroundHeight(-48f, -14f),
            GmExteriorTerrainComposer.GroundHeight(46f, 8f),
            GmExteriorTerrainComposer.GroundHeight(-38f, 69f),
            GmExteriorTerrainComposer.GroundHeight(42f, 61f),
            GmExteriorTerrainComposer.GroundHeight(0f, 8f),
        };
        Assert.GreaterOrEqual(heights.Max() - heights.Min(), 0.30f,
            "the imported heightmap was flattened back into a room floor");

        var ecotone = GameObject.Find("RoomEdgeEcology");
        Assert.IsNotNull(ecotone, "garden and cemetery have no transition into the wider estate");
        Assert.GreaterOrEqual(ecotone.transform.childCount, 27,
            "room-edge communities are too sparse to conceal rectangular composition bounds");
        Assert.AreEqual(0, ecotone.GetComponentsInChildren<Collider>(true).Length,
            "ecological dressing changed route collision");

        var fence = GameObject.Find("FenceRun");
        Assert.IsNotNull(fence);
        Assert.LessOrEqual(fence.transform.childCount, 48,
            "the drive fence returned to a continuous two-sided corridor");
        foreach (bool left in new[] { true, false })
        {
            float[] z = fence.transform.Cast<Transform>()
                .Where(child => left ? child.position.x < 0f : child.position.x > 0f)
                .Select(child => child.position.z).OrderBy(value => value).ToArray();
            int longGaps = Enumerable.Range(1, z.Length - 1)
                .Count(index => z[index] - z[index - 1] >= 8f);
            Assert.GreaterOrEqual(longGaps, 4,
                $"{(left ? "west" : "east")} drive boundary is visually continuous instead of decayed");
        }
    }

    [Test]
    public void DonorTerrainEcologySpansTheWholeEstateInsteadOfOneProtectedProofStrip()
    {
        Terrain terrain = GameObject.Find("Ground")?.GetComponent<Terrain>();
        Assert.IsNotNull(terrain);
        TerrainData data = terrain.terrainData;
        TreeInstance[] instances = data.treeInstances;
        Assert.That(instances.Length, Is.InRange(7000, 8500),
            "estate ecology regressed to the old 760-instance gate-to-middle-drive proof strip");

        var positions = instances.Select(instance => new Vector3(
            terrain.transform.position.x + instance.position.x * data.size.x,
            0f,
            terrain.transform.position.z + instance.position.z * data.size.z)).ToArray();
        Assert.Less(positions.Min(position => position.x), -55f,
            "west acreage has no continuous terrain ecology");
        Assert.Greater(positions.Max(position => position.x), 55f,
            "east acreage has no continuous terrain ecology");
        Assert.Less(positions.Min(position => position.z), -65f,
            "porch and terminal approach ecology never received the donor transfer");
        Assert.Greater(positions.Max(position => position.z), 95f,
            "arrival acreage ecology never received the donor transfer");
        Assert.IsFalse(positions.Any(position =>
                GmExteriorTerrainComposer.IsEstateFoliageReservedAt(position.x, position.z)),
            "terrain foliage entered a real tread, room path or structure footprint");

        int cemetery = positions.Count(position => position.x > 7.8f && position.x < 25.4f &&
            position.z > 11.8f && position.z < 41.5f);
        int garden = positions.Count(position => position.x > -24.5f && position.x < -7.7f &&
            position.z > 14.8f && position.z < 35.6f);
        int lowerApproach = positions.Count(position => position.z < 0f && position.z > -78f);
        Assert.GreaterOrEqual(cemetery, 70,
            "cemetery remained a bare prop room after the estate-wide ecology transfer");
        Assert.GreaterOrEqual(garden, 55,
            "garden remained a bare prop room after the estate-wide ecology transfer");
        Assert.GreaterOrEqual(lowerApproach, 1200,
            "the final half of the approach remained outside the ecology system");

        int tall = 0, cemeteryWild = 0, cemeteryTall = 0, gardenWheat = 0, gardenTall = 0;
        for (int i = 0; i < instances.Length; i++)
        {
            bool inCemetery = positions[i].x > 7.8f && positions[i].x < 25.4f &&
                positions[i].z > 11.8f && positions[i].z < 41.5f;
            bool inGarden = positions[i].x > -24.5f && positions[i].x < -7.7f &&
                positions[i].z > 14.8f && positions[i].z < 35.6f;
            if (instances[i].prototypeIndex >= 2) tall++;
            if (inCemetery && instances[i].prototypeIndex == 0) cemeteryWild++;
            if (inCemetery && instances[i].prototypeIndex >= 2) cemeteryTall++;
            if (inGarden && instances[i].prototypeIndex == 1) gardenWheat++;
            if (inGarden && instances[i].prototypeIndex >= 2) gardenTall++;
        }
        Assert.LessOrEqual(tall, instances.Length * 0.18f,
            "estate-wide continuity regressed into one dominant tall-reed carpet");
        Assert.GreaterOrEqual(cemeteryWild, cemetery * 0.55f,
            "cemetery no longer carries a low wild-turf cohort distinct from the garden");
        Assert.LessOrEqual(cemeteryTall, cemetery * 0.14f,
            "cemetery is again dominated by tall generic reeds");
        Assert.GreaterOrEqual(gardenWheat, garden * 0.50f,
            "garden no longer carries a failed-grain cohort distinct from cemetery turf");
        Assert.LessOrEqual(gardenTall, garden * 0.18f,
            "garden is again dominated by tall generic reeds");

        int nearReviewShots = positions.Count(position =>
            GmReviewShotProtection.IsInsideHorizontalClearance(
                position, GmShotTour.AuthoringShots, 7.5f));
        Assert.Greater(nearReviewShots, 100,
            "review-camera beauty bubbles returned; evidence cameras must not reshape the estate");
    }

    [Test]
    public void EveryTerrainFoliageVariantHasAPlayerInstancableRootAndRealPopulation()
    {
        TerrainData data = GameObject.Find("Ground")?.GetComponent<Terrain>()?.terrainData;
        Assert.IsNotNull(data);
        Assert.AreEqual(6, data.treePrototypes.Length);
        int[] populations = new int[data.treePrototypes.Length];
        foreach (TreeInstance instance in data.treeInstances)
        {
            Assert.That(instance.prototypeIndex, Is.InRange(0, data.treePrototypes.Length - 1));
            populations[instance.prototypeIndex]++;
        }
        for (int i = 0; i < data.treePrototypes.Length; i++)
        {
            GameObject prefab = data.treePrototypes[i].prefab;
            Assert.IsNotNull(prefab, $"terrain prototype {i} is null");
            Assert.IsTrue(GmTerrainPrototypeAudit.HasPlayerInstancableRoot(prefab),
                $"terrain prototype {i} '{prefab.name}' has only child meshes; Unity Terrain will reject it in the player");
            Assert.GreaterOrEqual(populations[i], 50,
                $"terrain prototype {i} '{prefab.name}' is technically registered but not meaningfully represented");
            if (i >= 2)
            {
                Material material = prefab.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
                Assert.IsNotNull(material);
                Assert.AreEqual("HDRP/Lit", material.shader?.name,
                    $"reed prototype {i} returned to the un-tintable saturated source ShaderGraph");
                Assert.GreaterOrEqual(material.GetFloat("_AlphaCutoffEnable"), 0.5f,
                    $"reed prototype {i} is not alpha-cut and can render as a rectangular card");
                Assert.That(material.GetColor("_BaseColor").maxColorComponent, Is.LessThanOrEqualTo(0.22f),
                    $"reed prototype {i} exceeds the Wend Hill night-reflectance ceiling");
            }
        }
    }

    [Test]
    public void KitchenGardenHasVisibleBrokenDeadBedsWithoutProceduralSoilSlabs()
    {
        var garden = Find("KitchenGarden");
        Assert.IsNotNull(garden, "kitchen garden composition missing");
        var beds = garden.transform.Find("DeadBeds");
        Assert.IsNotNull(beds, "garden lost its authored dead-bed composition");
        Assert.GreaterOrEqual(beds.GetComponentsInChildren<Renderer>(true).Length, 18,
            "dead beds are too sparse to read at the fixed night exposure");
        Assert.AreEqual(0, beds.GetComponentsInChildren<Collider>(true).Length,
            "decorative dead beds gained collision and can snag the route");
        Assert.IsNull(garden.transform.Find("GardenSoil"),
            "rejected monolithic garden soil slab returned");
        foreach (Transform child in garden.transform)
            Assert.IsFalse(child.name.StartsWith("NeglectedBed_"),
                "procedural soil strips returned and will dominate the failed-crop silhouettes");
        foreach (float rowX in new[] { -21.0f, -18.35f, -12.85f, -10.35f })
        {
            int rowMembers = 0;
            foreach (Transform child in beds)
                if (Mathf.Abs(child.position.x - rowX) < 0.75f &&
                    child.position.z >= 25.2f && child.position.z <= 33.0f) rowMembers++;
            Assert.GreaterOrEqual(rowMembers, 5,
                $"failed-crop band at x={rowX:F2} is too sparse to read without a painted soil strip");
        }

        var stakes = garden.transform.Find("GardenRowStakes");
        Assert.IsNotNull(stakes, "fine dead foliage has no solid cultivation markers to carry the row read");
        Assert.AreEqual(8, stakes.childCount,
            "garden should retain two restrained row markers per failed crop band");
        Assert.GreaterOrEqual(stakes.GetComponentsInChildren<Renderer>(true).Length, 8,
            "row markers exist as transforms but do not contribute visible geometry");
        Assert.AreEqual(0, stakes.GetComponentsInChildren<Collider>(true).Length,
            "decorative row markers gained collision and can snag the garden route");

        var cultivationLine = garden.transform.Find("GardenDenseCultivationLine");
        Assert.IsNotNull(cultivationLine,
            "dense failed crop still reads as estate weeds without one coherent cultivation line");
        Assert.AreEqual(4, cultivationLine.childCount,
            "dense cultivation line should be three restrained stakes and one twine segment");
        LineRenderer twine = cultivationLine.GetComponentInChildren<LineRenderer>(true);
        Assert.IsNotNull(twine, "cultivation stakes are not visually tied into one former row");
        Assert.AreEqual(5, twine.positionCount, "garden twine lost its two authored sags");
        Assert.AreEqual(0, cultivationLine.GetComponentsInChildren<Collider>(true).Length,
            "dense cultivation line gained decorative collision");
        foreach (Transform section in cultivationLine)
            if (section.name.StartsWith("DenseRowTwineStake_", System.StringComparison.Ordinal))
                Assert.Less(Mathf.Abs(section.position.x + 12.85f), 0.90f,
                    $"{section.name} drifted away from the dense bed axis");

        var cemeteryGrowth = GameObject.Find("GraveBaseGrowth");
        Assert.IsNotNull(cemeteryGrowth, "cemetery returned to clean unused floor around every grave");
        Assert.GreaterOrEqual(cemeteryGrowth.GetComponentsInChildren<Renderer>(true).Length, 9,
            "grave-base growth is too sparse to visually seat the plots");
        Assert.AreEqual(0, cemeteryGrowth.GetComponentsInChildren<Collider>(true).Length,
            "grave-base dressing gained collision");
    }

    [Test]
    public void CemeteryAndGardenGroundingSupportsTheStoryWithoutReadingAsDarkFloorSlabs()
    {
        var grounding = GameObject.Find("CemeteryGrounding");
        Assert.IsNotNull(grounding);
        Assert.AreEqual(0, grounding.GetComponentsInChildren<Renderer>(true).Length,
            "old turf graves again gained rendered family benches or black plot scars");

        var mounds = GameObject.Find("FamilyPlotMounds");
        Assert.IsNull(mounds,
            "repeated family-plot earth islands returned; only the recent open grave may expose soil");

        var furrows = GameObject.Find("GardenSculptedFurrows");
        Assert.IsNotNull(furrows);
        Assert.AreEqual(0, furrows.GetComponentsInChildren<Renderer>(true).Length,
            "generated garden soil bars returned after two GPU passes rejected them");
        Assert.AreEqual(0, furrows.GetComponentsInChildren<Collider>(true).Length);

        Assert.IsNull(GameObject.Find("GardenBedTimbers"),
            "GPU-rejected plank scatter returned to the garden; failed rows should remain organic");
    }

    [Test]
    public void GardenHumanTracesAreSpecificVisibleAndClearOfWalkingLanes()
    {
        var traces = GameObject.Find("GardenHumanTraces");
        Assert.IsNotNull(traces, "garden lost the row cover and well-side tool that explain former work");
        Assert.IsNotNull(GameObject.Find("GardenRottedStrawCover"));
        Assert.IsNotNull(GameObject.Find("GardenWellShovel"));
        Assert.GreaterOrEqual(traces.GetComponentsInChildren<Renderer>(true).Length, 2);
        Assert.AreEqual(0, traces.GetComponentsInChildren<Collider>(true).Length,
            "garden narrative traces gained collision and can snag the route");

        var entry = GameObject.Find("GardenEntryPath").GetComponent<GmClearedPathGuide>().WorldBounds;
        var work = GameObject.Find("GardenWorkPath").GetComponent<GmClearedPathGuide>().WorldBounds;
        foreach (Renderer renderer in traces.GetComponentsInChildren<Renderer>(true))
        {
            Bounds footprint = renderer.bounds;
            footprint.Expand(new Vector3(-0.10f, 0f, -0.10f));
            Assert.IsFalse(entry.Intersects(footprint), $"{renderer.name} visually clutters the garden entry lane");
            Assert.IsFalse(work.Intersects(footprint), $"{renderer.name} visually clutters the garden work lane");
        }
    }

    [Test]
    public void StagMemorialUsesAnAnchoredBrokenAntlerReliefInsteadOfTheWrongAnimalAsset()
    {
        var plinth = GameObject.Find("BrokenStagPlinth");
        Assert.IsNotNull(plinth);
        // The complete memorial is now rotated as one grounded wrapper, so the crest is a nested
        // descendant rather than a direct plinth child. Find the authored hero by identity.
        Transform crest = plinth.GetComponentsInChildren<Transform>(true)
            .SingleOrDefault(child => child.name == "StagHead");
        Assert.IsNotNull(crest, "stag iconography has no mounted hero object");
        Renderer[] crestRenderers = crest.GetComponentsInChildren<Renderer>(true);
        Assert.IsTrue(crestRenderers.Any(renderer =>
                renderer.name.Contains("BrokenStagAntlers", System.StringComparison.OrdinalIgnoreCase)),
            "memorial lost the sculpted one-antler mesh built from the owned source");
        Assert.IsTrue(crestRenderers.Any(renderer =>
                renderer.name.Contains("Wood", System.StringComparison.OrdinalIgnoreCase)),
            "antlers have no physical shield backing and can read as floating decoration");
        MeshFilter antlers = crest.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(filter => filter.name.Contains("BrokenStagAntlers",
                System.StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(antlers);
        Assert.That(antlers.sharedMesh.vertexCount, Is.InRange(350, 500),
            "broken-stag intake no longer removed exactly one branch family from the 768-vertex source");
        Bounds crestBounds = crestRenderers.Select(renderer => renderer.bounds)
            .Aggregate((combined, next) => { combined.Encapsulate(next); return combined; });
        Assert.That(Mathf.Max(crestBounds.size.x, Mathf.Max(crestBounds.size.y, crestBounds.size.z)),
            Is.InRange(0.50f, 0.60f),
            "owned antler source retained millimetre authoring scale or outgrew its plinth");
        Assert.IsFalse(crestRenderers.Any(renderer =>
                renderer.name.Contains("Goat", System.StringComparison.OrdinalIgnoreCase)),
            "a goat skull is again standing in for the text's stag crest");
        Assert.AreEqual(1, plinth.GetComponentsInChildren<Collider>(true).Length,
            "stag memorial should retain only its deliberate interaction trigger");
    }

    [Test]
    public void CemeteryAndGardenCarrySpecificPerceptualStoriesNotJustPropCounts()
    {
        int plots = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,
            FindObjectsSortMode.None))
            if (go.name.StartsWith("SM_GraveCross_Plot_")) plots++;
        Assert.AreEqual(17, plots,
            "cemetery lost the 17 explicit family plots and may have returned to a scatter loop");
        Assert.IsNull(GameObject.Find("FamilyPlotMounds"),
            "old turf cemetery returned to repeated exposed-earth grave mounds");

        GmClearedPathGuide spine = GameObject.Find("CemeterySpine").GetComponent<GmClearedPathGuide>();
        Assert.IsNotNull(spine, "cemetery spine lost its negative-space guide");
        Assert.That(spine.Length, Is.EqualTo(11.5f).Within(0.01f));
        Assert.That(spine.Width, Is.EqualTo(1.45f).Within(0.01f));

        var repetition = Object.FindAnyObjectByType<GmRepetitionIntent>();
        Assert.IsNotNull(repetition, "cemetery has no repetition contract");
        Assert.AreEqual("cemetery-family-plots", repetition.IntentId);
        Assert.GreaterOrEqual(repetition.MinimumDistinctSignatures, 6);
        Assert.LessOrEqual(repetition.MaximumSignatureShare, 0.25f);
        Assert.LessOrEqual(repetition.MaximumNearIdenticalRun, 2);

        var story = Object.FindAnyObjectByType<GmEnvironmentalStoryIntent>();
        Assert.IsNotNull(story, "garden has no staged environmental-story contract");
        Assert.AreEqual("garden-work-stopped", story.IntentId);
        Assert.AreEqual("garden-well", story.AnchorElementId);
        CollectionAssert.Contains((System.Collections.ICollection)story.TraceElementIds,
            "garden-bed-anchor");
        Assert.GreaterOrEqual(story.RevealSteps.Count, 3);
        Assert.IsNotNull(GameObject.Find("GardenAbandonedBasket"));
        Assert.IsNotNull(GameObject.Find("GardenDroppedBucket"));
        Assert.IsNotNull(GameObject.Find("GardenProduceSpill"));
    }

    [Test]
    public void AcreageUsesMultipleTreeSilhouettesAndBrokenMiddleHedgerows()
    {
        var woodland = GameObject.Find("DistantWoodland");
        var hedgerows = GameObject.Find("AcreageHedgerows");
        Assert.IsNotNull(woodland);
        Assert.IsNotNull(hedgerows, "acreage again jumps directly from empty mud to ridge trees");
        Assert.GreaterOrEqual(hedgerows.GetComponentsInChildren<Renderer>(true).Length, 20,
            "middle acreage hedgerows are too sparse to form a distance band");
        var silhouettes = new HashSet<string>();
        bool hasUprightTree = false, hasDroopingWillow = false;
        foreach (Transform child in woodland.transform)
        {
            silhouettes.Add(child.name.Replace("(Clone)", "").Trim());
            hasUprightTree |= child.name.StartsWith("SM_Tree_03");
            hasDroopingWillow |= child.name.StartsWith("SM_DeadWillow");
        }
        Assert.GreaterOrEqual(silhouettes.Count, 3,
            "distant woodland is stamped from fewer than three source silhouettes");
        Assert.IsTrue(hasUprightTree && hasDroopingWillow,
            "distant woodland morphologies are only renamed copies of one source silhouette");

        var fieldBreaks = GameObject.Find("AcreageFieldBreaks");
        Assert.IsNotNull(fieldBreaks, "middle acreage lost its broken agricultural fence history");
        Assert.GreaterOrEqual(fieldBreaks.transform.childCount, 12,
            "field breaks are too sparse to establish human scale across the acreage");
        var fenceSources = new HashSet<string>();
        foreach (Transform child in fieldBreaks.transform)
        {
            string source = child.name;
            int marker = source.LastIndexOf("SM_Wood_Fence_", System.StringComparison.Ordinal);
            if (marker >= 0) fenceSources.Add(source.Substring(marker));
        }
        Assert.GreaterOrEqual(fenceSources.Count, 3,
            "acreage field breaks repeat fewer than three fence silhouettes");

        var middle = GameObject.Find("MiddleAcreage");
        Assert.IsNotNull(middle);
        foreach (Transform child in middle.transform)
        {
            if (!child.name.StartsWith("SM_Tree_") && !child.name.StartsWith("SM_DeadWillow")) continue;
            Assert.IsFalse(GmReviewShotProtection.IsInsideHorizontalClearance(
                    child.position, GmShotTour.AuthoringShots, 7.5f),
                $"{child.name} at {child.position} can put a deterministic review camera inside its crown");
        }

        var harvest = GameObject.Find("AcreageHarvestRemnants");
        Assert.IsNotNull(harvest, "acreage has no readable former-working-land layer");
        Assert.AreEqual(7, harvest.transform.childCount,
            "field-work remnants should remain sparse and explicitly composed");
        Assert.AreEqual(0, harvest.GetComponentsInChildren<Collider>(true).Length,
            "distant field-work remnants changed playable collision");
        Assert.IsFalse(harvest.transform.Cast<Transform>().Any(child => child.name.Contains("HayStack")),
            "rejected reflective hay remnants returned to the mid-drive sightline");
        var workFamilies = new HashSet<string>(harvest.transform.Cast<Transform>()
            .Select(child => child.name.Split('_').Last()));
        Assert.GreaterOrEqual(workFamilies.Count, 3,
            "middle acreage returned to one stamped work-debris silhouette");

        int readableHedgeShapes = 0;
        foreach (Renderer renderer in hedgerows.GetComponentsInChildren<Renderer>(true))
            if (renderer.bounds.size.y >= 1.20f) readableHedgeShapes++;
        Assert.GreaterOrEqual(readableHedgeShapes, 24,
            "hedgerows are present in the hierarchy but too small to survive the night-grade distance band");
    }

    [Test]
    public void ArrivalVehicleHasSemanticMaterialSeparationAndGroundedVergeEvidence()
    {
        var car = FindByKind("estateCar");
        Assert.IsNotNull(car);
        Renderer body = car.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(renderer => renderer.name.Contains("Body_LOD0"));
        Renderer glass = car.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(renderer => renderer.name.Contains("Glass_LOD0"));
        Renderer grille = car.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(renderer => renderer.name.Contains("Grill_LOD0"));
        Material frontDetails = body.sharedMaterials.FirstOrDefault(material => material != null &&
            material.name.Contains("Parts", System.StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(body, "car body renderer missing");
        Assert.IsNotNull(glass, "car glass renderer missing");
        Assert.IsNotNull(grille, "car grille renderer missing");
        Assert.IsNotNull(frontDetails, "body mesh lost its separate lamp-and-trim material slot");

        Color bodyColor = MaterialBaseColor(body.sharedMaterial);
        Color glassColor = MaterialBaseColor(glass.sharedMaterial);
        Color grilleColor = MaterialBaseColor(grille.sharedMaterial);
        Color lampColor = MaterialBaseColor(frontDetails);
        Assert.Greater(bodyColor.r, bodyColor.b * 1.8f,
            "arrival vehicle lost its deliberate warm muddy-charcoal ordinary-world contrast");
        Assert.Less(glassColor.r + glassColor.g + glassColor.b, bodyColor.r + bodyColor.g + bodyColor.b,
            "windshield is brighter than the body before lighting and will read as a blank blue card");
        Assert.Greater(grilleColor.r + grilleColor.g + grilleColor.b, glassColor.r + glassColor.g + glassColor.b,
            "grille still collapses into the same black value as the glass");
        Assert.Greater(lampColor.r + lampColor.g + lampColor.b, grilleColor.r + grilleColor.g + grilleColor.b,
            "unlit headlamp lenses are not readable as physical detail");
        Assert.GreaterOrEqual(grille.sharedMaterial.GetFloat("_Metallic"), 0.60f,
            "grille has no material response distinct from painted bodywork");
        if (frontDetails.HasProperty("_EmissiveColor"))
            Assert.LessOrEqual(frontDetails.GetColor("_EmissiveColor").maxColorComponent, 0.0001f,
                "failed car's headlamps became an unmotivated light source");

        var verge = GameObject.Find("ArrivalVergeDebris");
        Assert.IsNotNull(verge, "vehicle is again isolated on a clean showroom patch");
        Assert.GreaterOrEqual(verge.GetComponentsInChildren<Renderer>(true).Length, 4,
            "arrival verge evidence is too sparse to visually seat the tyres");
        Assert.AreEqual(0, verge.GetComponentsInChildren<Collider>(true).Length,
            "arrival dressing changed the opening route collision");

        var traces = GameObject.Find("ArrivalStoryTraces");
        Assert.IsNotNull(traces);
        Transform leftTrace = traces.transform.Find("PressedTyre_Left");
        Transform rightTrace = traces.transform.Find("PressedTyre_Right");
        Assert.IsNotNull(leftTrace);
        Assert.IsNotNull(rightTrace);
        foreach (Transform trace in new[] { leftTrace, rightTrace })
        {
            Mesh mesh = trace.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(42, mesh.vertexCount,
                $"{trace.name} returned to clean primitive blocks instead of a ground-following ribbon");
            Assert.Greater(Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z), 2.70f,
                $"{trace.name} no longer reads as an arrival path behind the vehicle");
        }
        Assert.AreEqual(0, traces.GetComponentsInChildren<Collider>(true).Length,
            "arrival traces gained decorative collision");

    }

    static Color MaterialBaseColor(Material material)
    {
        Assert.IsNotNull(material);
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        Assert.Fail($"{material.name} has no measurable base colour");
        return Color.magenta;
    }

    [Test]
    public void WeakSceneReviewShotsAreNotDominatedByAccidentalNearCameraDressing()
    {
        GmViewportOccupancyReport occupancy = GmViewportOccupancyAudit.Analyze(
            GmEstateBuilderV2.SceneId, Object.FindAnyObjectByType<GmShotTour>(), Camera.main);
        foreach (GmViewportOccupancyRow row in occupancy.rows)
        {
            if (row.shotName == "06-cem-path" || row.shotName == "13-cem-detail")
                Assert.IsFalse(row.rendererPath.Contains("TreeLines/MiddleAcreage") &&
                    row.centerDepth < 7.5f && row.clippedCoverage > 0.18f,
                    $"{row.shotName} is still blocked by {row.rendererPath} ({row.clippedCoverage:P0})");
            if (row.shotName == "14-gdn-detail")
                Assert.IsFalse(row.rendererPath.Contains("KitchenGarden/DeadBeds") &&
                    row.centerDepth < 3.5f && row.clippedCoverage > 0.40f,
                    $"garden detail is still swallowed by {row.rendererPath} ({row.clippedCoverage:P0})");
        }

        var protectedPaths = new[]
        {
            ("06-cem-path", GameObject.Find("CemeteryCrossPath")),
            ("13-cem-detail", GameObject.Find("CemeteryCrossPath")),
            ("13-cem-detail", GameObject.Find("CemeterySpine")),
            ("09-gdn-inside", GameObject.Find("GardenEntryPath")),
            ("14-gdn-detail", GameObject.Find("GardenEntryPath")),
            ("14-gdn-detail", GameObject.Find("GardenWorkPath")),
        };
        foreach (var protectedPath in protectedPaths)
        {
            GmReviewShot shot = GmShotTour.AuthoringShots.First(item => item.Name == protectedPath.Item1);
            Bounds bounds = protectedPath.Item2.GetComponent<GmClearedPathGuide>().WorldBounds;
            Assert.GreaterOrEqual(GmReviewShotProtection.HorizontalDistanceToBounds(shot.Position, bounds), 2.75f,
                $"{shot.Name} stands on {protectedPath.Item2.name}; a flat path endpoint will dominate the review frame");
        }
    }

    [Test]
    public void DisturbedGraveReadsAsARecessInsteadOfTheSourceMound()
    {
        GameObject grave = FindByKind("estateGravePit");
        Assert.IsNotNull(grave);
        Transform visual = grave.transform.Find("OpenGraveVisual");
        Assert.IsNotNull(visual, "SM_Pit mound was not replaced with authored grave geometry");
        Assert.IsNotNull(visual.Find("OpenGraveVoid"));
        Assert.IsNotNull(visual.Find("OpenGraveLipWest"));
        Assert.IsNotNull(visual.Find("OpenGraveLipEast"));
        Assert.IsNotNull(visual.Find("OpenGraveLipHead"));
        Transform innerCut = visual.Find("OpenGraveInnerCut");
        Assert.IsNotNull(innerCut, "grave opening returned to a uniform black plane");
        MeshFilter innerCutMesh = innerCut.GetComponent<MeshFilter>();
        Assert.IsNotNull(innerCutMesh);
        Assert.AreEqual(16, innerCutMesh.sharedMesh.vertexCount,
            "grave inner cut lost one of its four separately-normaled soil walls");
        Assert.Greater(innerCut.GetComponent<Renderer>().bounds.size.y, 0.09f,
            "grave cut has flattened back into the void plane");
        Transform rimStory = visual.Find("OpenGraveRimStory");
        Assert.IsNotNull(rimStory, "open grave has no excavation evidence at its rim");
        Assert.AreEqual(4, rimStory.childCount,
            "grave rim should carry three restrained spoil clusters and the authored shovel");
        Assert.IsTrue(rimStory.Cast<Transform>()
            .Any(child => child.name.StartsWith("estateShovel", System.StringComparison.Ordinal)),
            "grave shovel is not visibly tied to the excavation rim");
        Assert.AreEqual(3, rimStory.Cast<Transform>()
            .Count(child => child.name.StartsWith("OpenGraveSpoilClods_", System.StringComparison.Ordinal)));
        Assert.AreEqual(0, rimStory.GetComponentsInChildren<Collider>(true).Length,
            "grave rim story gained decorative collision");
        Assert.AreEqual(1, grave.GetComponentsInChildren<Collider>(true).Length,
            "open grave should have one deliberate route block, not source-mound collision");
        foreach (Renderer renderer in grave.GetComponentsInChildren<Renderer>(true))
            Assert.IsTrue(renderer.transform.IsChildOf(visual),
                $"source mound renderer survived at {renderer.name}");
    }

    [Test]
    public void CoachServiceWindowHasAlignedShallowArchitecturalDepth()
    {
        GameObject window = GameObject.Find("CoachServiceWindowDepth");
        Assert.IsNotNull(window);
        Transform back = window.transform.Find("ServiceWindowBack");
        Transform side = window.transform.Find("ServiceWindowSideReveal");
        Transform sill = window.transform.Find("ServiceWindowSillReveal");
        Assert.IsNotNull(back);
        Assert.IsNotNull(side);
        Assert.IsNotNull(sill);
        float depth = Vector2.Distance(new Vector2(window.transform.position.x, window.transform.position.z),
            new Vector2(back.position.x, back.position.z));
        Assert.That(depth, Is.InRange(0.30f, 0.52f));
        Assert.Greater(back.GetComponent<Renderer>().bounds.size.y, 1.20f,
            "service-window back no longer covers the measured aperture height");
        Assert.AreEqual(0, window.GetComponentsInChildren<Collider>(true).Length,
            "shallow coach service-window reveal gained decorative collision");
    }

    [Test]
    public void NightSurfacePaletteIsAppliedToTheShadersThatActuallyRenderIt()
    {
        var dryBeds = GameObject.Find("DeadBeds");
        Assert.IsNotNull(dryBeds);
        Assert.IsNull(dryBeds.GetComponent<Renderer>(),
            "failed-crop anchor itself became a monolithic rendered soil strip");
        int customTints = 0;
        foreach (Renderer renderer in dryBeds.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                foreach (string property in new[] { "_BaseColor_Value", "_Albedo_Tint", "_ColorGrass" })
                {
                    if (material == null || !material.HasProperty(property)) continue;
                    customTints++;
                    Color tint = material.GetColor(property);
                    Assert.GreaterOrEqual(tint.r + 0.015f, tint.g,
                        $"{material.name} property {property} remains green/cyan");
                    Assert.GreaterOrEqual(tint.r, tint.b,
                        $"{material.name} property {property} remains blue/cyan");
                    Assert.GreaterOrEqual(tint.r, 0.55f,
                        $"{material.name} property {property} is too dark to survive the fixed night grade");
                }
            }
        Assert.Greater(customTints, 0, "test never inspected a custom vegetation shader tint");
        int predictableCutouts = 0;
        foreach (Material material in dryBeds.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null))
        {
            if (material.name.Contains("_ReadableDryGrowthCutout", System.StringComparison.Ordinal))
            {
                predictableCutouts++;
                Assert.AreEqual("HDRP/Lit", material.shader.name,
                    $"{material.name} returned to the pack ShaderGraph that rendered as a black card");
                Assert.IsNotNull(material.GetTexture("_BaseColorMap"),
                    $"{material.name} lost the owned crop albedo while changing shaders");
                Assert.GreaterOrEqual(material.GetFloat("_AlphaCutoffEnable"), 0.99f,
                    $"{material.name} became an opaque rectangle instead of an alpha-cut crop card");
                Assert.GreaterOrEqual(material.GetFloat("_DoubleSidedEnable"), 0.99f,
                    $"{material.name} disappears from the reverse garden review angle");
                Assert.LessOrEqual(material.GetColor("_EmissiveColor").maxColorComponent, 0.0001f,
                    $"{material.name} was made readable by emission instead of reflected light");
            }
            if (!material.name.Contains("_DeadGarden", System.StringComparison.Ordinal)) continue;
            if (material.HasProperty("_DoubleSidedEnable"))
                Assert.GreaterOrEqual(material.GetFloat("_DoubleSidedEnable"), 0.99f,
                    $"{material.name} can disappear when the garden is viewed from its reverse review angle");
            if (material.HasProperty("_EmissiveColor"))
                Assert.LessOrEqual(material.GetColor("_EmissiveColor").maxColorComponent, 0.0001f,
                    $"{material.name} was made readable by emission instead of authored lighting");
        }
        Assert.GreaterOrEqual(predictableCutouts, 24,
            "too few failed-crop renderers use the predictable lit cutout; the garden can collapse back to black cards");

        var wall = GameObject.Find("LowStoneBoundary");
        Assert.IsNotNull(wall);
        int brightnessControls = 0;
        foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.HasProperty("_Brightness"))
                {
                    brightnessControls++;
                    Assert.LessOrEqual(material.GetFloat("_Brightness"), 0.42f,
                        "cemetery ShaderGraph ignored its night brightness budget");
                }
        Assert.Greater(brightnessControls, 0, "test never reached the custom stone ShaderGraph");
    }

    [Test]
    public void ReadableDryGrowthIsPresentInAuthoredRoomsButDoesNotRewriteTheWoodland()
    {
        int Count(GameObject root) => root.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Count(material => material != null && material.name.Contains(
                "_ReadableDryGrowthCutout", System.StringComparison.Ordinal));

        var garden = GameObject.Find("DeadBeds");
        var cemetery = GameObject.Find("Cemetery");
        var acreage = GameObject.Find("TreeLines/MiddleAcreage/AcreageHedgerows");
        var woodland = GameObject.Find("TreeLines/DistantWoodland");
        Assert.IsNotNull(garden);
        Assert.IsNotNull(cemetery);
        Assert.IsNotNull(acreage);
        Assert.IsNotNull(woodland);
        Assert.GreaterOrEqual(Count(garden), 24,
            "garden rows returned to the native foliage shader that rendered as black cards");
        Assert.GreaterOrEqual(Count(cemetery), 12,
            "cemetery base and boundary growth no longer separates its marker families");
        Assert.GreaterOrEqual(Count(acreage), 12,
            "middle-acreage hedgerows vanished back into the fixed night grade");
        Assert.AreEqual(0, Count(woodland),
            "the dry-growth visibility fix leaked into the distant tree palette");
    }

    [Test]
    public void AvenueFenceUsesMultipleSilhouettesInsteadOfOneStampedPanel()
    {
        var fence = GameObject.Find("FenceRun");
        Assert.IsNotNull(fence);
        var sourceNames = new HashSet<string>();
        foreach (Transform child in fence.transform)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
            if (source != null) sourceNames.Add(source.name);
        }
        Assert.GreaterOrEqual(sourceNames.Count, 3,
            "avenue fence returned to one repeated source silhouette");
    }

    [Test]
    public void EstateQualityAuditFindsNoStructuralAssetOrPlacementFailures()
    {
        var issues = GmEstateQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Wend Hill quality audit failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void SceneContractIdentityMatchesTheBuiltScene()
    {
        var identities = Object.FindObjectsByType<GmSceneIdentity>(FindObjectsInactive.Include);
        Assert.AreEqual(1, identities.Length, "Wend Hill must carry exactly one durable scene identity");
        Assert.AreEqual(GmEstateBuilderV2.SceneId, identities[0].SceneId);
        Assert.AreEqual(GmEstateBuilderV2.DisplayName, identities[0].DisplayName);
        Assert.AreEqual(1, identities[0].SchemaVersion, "preserved legacy scene retains its v1 identity");
        Assert.AreEqual(GmEstateBuilderV2.ScenePath,
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
    }

    [Test]
    public void SceneContractAuditRejectsADuplicateIdentity()
    {
        var duplicate = new GameObject("ImpostorSceneIdentity");
        duplicate.AddComponent<GmSceneIdentity>().Configure("other-scene", "Other Scene");
        try
        {
            var issues = GmSceneContractAudit.ValidateOpenScene(
                GmEstateBuilderV2.SceneId,
                GmEstateBuilderV2.DisplayName,
                GmEstateBuilderV2.ScenePath,
                new[] { "GmSystems", "Player" });
            Assert.IsTrue(issues.Exists(issue => issue.Contains("exactly one GmSceneIdentity")),
                "a copied or merged scene with two identities passed the shared contract");
        }
        finally { Object.DestroyImmediate(duplicate); }
    }

    [Test]
    public void SceneContractAuditRejectsAMissingRequiredRoot()
    {
        var issues = GmSceneContractAudit.ValidateOpenScene(
            GmEstateBuilderV2.SceneId,
            GmEstateBuilderV2.DisplayName,
            GmEstateBuilderV2.ScenePath,
            new[] { "GmSystems", "Player", "ThisRootMustNotExist" });
        Assert.IsTrue(issues.Exists(issue => issue.Contains("ThisRootMustNotExist")),
            "the shared contract accepted a missing required root");
    }

    /// Prefab source provenance becomes null after a complete unpack. The durable identity marker
    /// must still reject an unpacked copy even when its name no longer resembles "Mansion (".
    [Test]
    public void SingleMansionAuditRejectsAnUnpackedRenamedDuplicate()
    {
        var mansion = Find("Mansion (gravyart");
        Assert.IsNotNull(mansion, "canonical mansion missing before duplicate-guard test");
        Assert.IsNotNull(mansion.GetComponent<GmMansionIdentity>(),
            "canonical mansion has no durable identity marker");

        var duplicate = Object.Instantiate(mansion);
        duplicate.name = "Summer House";
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(duplicate))
                PrefabUtility.UnpackPrefabInstance(duplicate, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

            Assert.IsFalse(PrefabUtility.IsPartOfPrefabInstance(duplicate),
                "counterexample setup failed: duplicate is still prefab-connected");
            Assert.IsNotNull(duplicate.GetComponent<GmMansionIdentity>(),
                "identity marker did not survive prefab unpacking");

            var issues = GmEstateQualityAudit.ValidateOpenScene();
            Assert.IsTrue(issues.Exists(issue => issue.Contains("durable canonical mansion identity")),
                "unpacked and renamed second mansion passed the audit:\n- " + string.Join("\n- ", issues));
        }
        finally
        {
            Object.DestroyImmediate(duplicate);
        }
    }

    [Test]
    public void ExteriorAmbienceHasNoContinuousDroneOrThreeSecondLoop()
    {
        var go = new GameObject("ambience_test");
        try
        {
            var ambience = go.AddComponent<GmAmbience>();
            var initialize = typeof(GmAmbience).GetMethod("Initialize",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(initialize, "GmAmbience test initializer missing");
            initialize.Invoke(ambience, new object[] { false });
            Assert.AreEqual(GmWindReviewProfile.SparseLocalized, ambience.reviewProfile,
                "exterior does not ship in the silence-led sparse profile");
            Assert.IsFalse(ambience.useContinuousWind,
                "shipping exterior silently re-enabled the continuous bed");
            int loopCount = 0;
            foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.loop) loopCount++;
                if (source.gameObject.name == "exterior_wind_bed")
                {
                    Assert.AreEqual("amb_wind_natural", source.clip.name,
                        "review-only control bed is not the licensed natural-air candidate");
                    Assert.AreEqual(0f, source.volume, 0.0001f,
                        "stopped sparse-profile bed initialized audible");
                }
            }
            Assert.AreEqual(0, loopCount, "shipping exterior must have zero continuous beds");
            Assert.AreEqual(4, ambience.LocalizedEmitterCount,
                "wind has no authored spatial character around hedges, trees and side grounds");
            Assert.AreEqual(2, ambience.LocalizedWildlifeEmitterCount,
                "crickets and owl returned to the player's non-diegetic centre");
            foreach (string wildlifeName in new[] { "localized_crickets", "localized_owl" })
            {
                AudioSource wildlife = go.GetComponentsInChildren<AudioSource>(true)
                    .FirstOrDefault(source => source.gameObject.name == wildlifeName);
                Assert.IsNotNull(wildlife, $"{wildlifeName} source missing");
                Assert.AreEqual(1f, wildlife.spatialBlend, 0.0001f,
                    $"{wildlifeName} is not a localized 3D event");
                Assert.IsFalse(wildlife.loop, $"{wildlifeName} became an ambience bed");
                Assert.AreEqual(0f, wildlife.dopplerLevel, 0.0001f,
                    $"stationary {wildlifeName} has Doppler movement");
            }
            int spatialOneShots = 0;
            foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
                if (!source.loop && source.spatialBlend >= 0.99f && source.clip != null &&
                    source.clip.name.StartsWith("wind_local_"))
                {
                    spatialOneShots++;
                    Assert.AreEqual(1, source.clip.channels, "localized gust is not mono");
                    Assert.AreEqual(0f, source.dopplerLevel, 0.0001f,
                        "stationary vegetation wind has Doppler movement");
                }
            Assert.AreEqual(4, spatialOneShots, "localized wind graph is missing licensed mono emitters");
            Assert.AreEqual(0, ambience.ActiveLocalizedGustCount,
                "sparse scheduler initializes with overlapping gusts");
            Assert.That(ambience.LocalizedSilenceRemaining, Is.InRange(3f, 8f),
                "sparse scheduler has no initial silence");

            var updateWind = typeof(GmAmbience).GetMethod("UpdateLocalizedWind",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(updateWind, "shared localized-wind scheduler is missing");
            updateWind.Invoke(ambience, new object[] { 1f, 9f });
            Assert.AreEqual(1, ambience.ActiveLocalizedGustCount,
                "shared scheduler did not start exactly one gust after silence");
            int first = ambience.ActiveLocalizedEmitterIndex;
            updateWind.Invoke(ambience, new object[] { 1f, 13f });
            Assert.AreEqual(0, ambience.ActiveLocalizedGustCount,
                "completed gust did not return to silence");
            Assert.That(ambience.LocalizedSilenceRemaining, Is.InRange(8f, 22f),
                "completed gust is not followed by the required silence gap");
            updateWind.Invoke(ambience, new object[] { 1f, 23f });
            Assert.AreEqual(1, ambience.ActiveLocalizedGustCount,
                "second scheduler event did not remain singular");
            Assert.AreNotEqual(first, ambience.ActiveLocalizedEmitterIndex,
                "localized scheduler repeated the same vegetation emitter consecutively");
            foreach (var surface in new[] { GmSurfaceKind.PackedMud, GmSurfaceKind.WetMud,
                GmSurfaceKind.Gravel, GmSurfaceKind.DeadGrass, GmSurfaceKind.LeafLitter,
                GmSurfaceKind.Stone, GmSurfaceKind.Wood })
                Assert.AreEqual(8, ambience.FootstepClipCount(surface),
                    $"{surface} does not have a full no-repeat footstep pool");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void WindAndBellExposeBoundedReviewCandidatesWithoutChangingCanon()
    {
        var go = new GameObject("profile_test");
        try
        {
            var ambience = go.AddComponent<GmAmbience>();
            var initialize = typeof(GmAmbience).GetMethod("Initialize",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            initialize.Invoke(ambience, new object[] { false });
            var apply = typeof(GmAmbience).GetMethod("ApplyReviewProfile",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(apply);
            apply.Invoke(ambience, new object[] { GmWindReviewProfile.SparseLocalized, false });
            Assert.AreEqual("sparse", ambience.ReviewProfileId);
            foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
                if (source.gameObject.name == "exterior_wind_bed")
                {
                    Assert.IsFalse(source.loop, "sparse profile silently remained a continuous loop");
                    Assert.AreEqual(1f, source.pitch, 0.0001f,
                        "review profile reintroduced mechanical pitch wobble");
                }
            apply.Invoke(ambience, new object[] { GmWindReviewProfile.HybridBreathing, false });
            Assert.AreEqual("hybrid", ambience.ReviewProfileId);
            Assert.AreEqual("amb_wind_hybrid", ambience.ContinuousBedName);
            ambience.ApplyLaunchArguments(new[] { "player", "-gmWind=control" });
            Assert.AreEqual(GmWindReviewProfile.ControlContinuous, ambience.reviewProfile,
                "standalone wind comparison cannot be selected from launch arguments");

            var bell = go.AddComponent<GmBellSummons>();
            bell.ApplyLaunchArguments(new[] { "player", "-gmPacing=240" });
            Assert.AreEqual(GmBellReviewProfile.Middle240, bell.reviewProfile,
                "standalone pacing comparison cannot be selected from launch arguments");
            Assert.IsTrue(bell.SetReviewProfile(GmBellReviewProfile.Tight195));
            Assert.AreEqual(35f, bell.firstTollDelay);
            Assert.AreEqual(20f, bell.tollInterval);
            Assert.AreEqual(195f, bell.firstTollDelay + 8f * bell.tollInterval);
            Assert.IsTrue(bell.SetReviewProfile(GmBellReviewProfile.Middle240));
            Assert.AreEqual(240f, bell.firstTollDelay + 8f * bell.tollInterval);
            bell.Arm();
            Assert.IsFalse(bell.SetReviewProfile(GmBellReviewProfile.Control285),
                "bell cadence changed after the canonical count was armed");
            Assert.AreEqual(9, Object.FindAnyObjectByType<GmPacingIntent>().CanonicalTollCount);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void AudioMixStatesPreserveStoryReadabilityAndRemoveTheWorldAtTaken()
    {
        var go = new GameObject("mix_test");
        try
        {
            var mix = go.AddComponent<GmAudioMixController>();
            mix.Apply(GmAudioMixState.Exploration);
            Assert.AreEqual(1f, mix.Gain(GmAudioBus.Weather));
            mix.Apply(GmAudioMixState.Threshold);
            Assert.Less(mix.Gain(GmAudioBus.Wildlife), mix.Gain(GmAudioBus.Story));
            mix.Apply(GmAudioMixState.Taken);
            Assert.LessOrEqual(mix.Gain(GmAudioBus.Ambience), 0.1f);
            Assert.AreEqual(0f, mix.Gain(GmAudioBus.Wildlife));
            Assert.AreEqual(1f, mix.Gain(GmAudioBus.Story));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void LicensedWindCandidatesPassMachineDroneAnalysisAndRetainProvenance()
    {
        var natural = Resources.Load<AudioClip>("Sfx/amb_wind_natural");
        Assert.IsNotNull(natural, "licensed natural wind candidate is missing from build resources");
        Assert.GreaterOrEqual(natural.length, 40f);
        GmAudioClipMetrics naturalMetrics = GmAudioAnalysis.Measure(natural);
        Assert.IsFalse(naturalMetrics.spaceshipRisk,
            $"natural control reads machine-like: tone={naturalMetrics.persistentToneDb:F1}dB " +
            $"stationarity={naturalMetrics.stationarity:F2}");
        Assert.Less(naturalMetrics.stationarity, 0.78f,
            "natural control is too stationary for exterior air");
        Assert.Less(naturalMetrics.loopDiscontinuity, 0.05f,
            "natural control has an audible loop seam");

        var hybrid = Resources.Load<AudioClip>("Sfx/amb_wind_hybrid");
        Assert.IsNotNull(hybrid, "quarantined hybrid evidence clip is missing");
        GmAudioClipMetrics hybridMetrics = GmAudioAnalysis.Measure(hybrid);
        Assert.IsTrue(hybridMetrics.stationarity >= 0.78f || hybridMetrics.loopDiscontinuity >= 0.05f,
            "hybrid evidence unexpectedly meets the stricter continuous-exterior limits; reassess quarantine");
        Debug.Log($"[GmAudioEvidence] natural: stationarity={naturalMetrics.stationarity:F2} " +
            $"seam={naturalMetrics.loopDiscontinuity:F3}; hybrid-quarantined: " +
            $"stationarity={hybridMetrics.stationarity:F2} seam={hybridMetrics.loopDiscontinuity:F3}");
        string provenance = "Assets/Audio/BOOK-OF-THE-DEAD-SOURCE-README.md";
        Assert.IsTrue(File.Exists(provenance), "licensed audio provenance was not copied with the curated sounds");
        StringAssert.Contains("Unity Asset Store EULA", File.ReadAllText(provenance));
    }

    [Test]
    public void NinthBellUsesFinalCuratedAudioWithProvenanceAndSafeDeliveryEnvelopes()
    {
        var durations = new Dictionary<string, Vector2>
        {
            { "chapel_bell", new Vector2(6.5f, 7.5f) },
            { "clock_chime", new Vector2(5.9f, 6.1f) },
            { "heartbeat", new Vector2(7.1f, 7.3f) },
            { "ear_whine", new Vector2(7.9f, 8.1f) },
            { "whisper_bed", new Vector2(8.8f, 9.1f) }
        };

        foreach (var pair in durations)
        {
            var clip = Resources.Load<AudioClip>($"Sfx/{pair.Key}");
            Assert.IsNotNull(clip, $"final Ninth Bell cue '{pair.Key}' is missing");
            Assert.That(clip.length, Is.InRange(pair.Value.x, pair.Value.y),
                $"'{pair.Key}' length no longer matches its authored use");
            Assert.AreEqual(2, clip.channels, $"'{pair.Key}' is not the verified stereo delivery format");

            GmAudioClipMetrics metrics = GmAudioAnalysis.Measure(clip);
            Assert.Greater(metrics.peak, 0.02f, $"'{pair.Key}' is effectively silent");
            Assert.Less(metrics.peak, 0.99f, $"'{pair.Key}' clips at delivery");
            Assert.Greater(metrics.rms, 0.001f, $"'{pair.Key}' has no useful average energy");
            Debug.Log($"[GmNinthBellAudio] {pair.Key}: seconds={clip.length:F3} rms={metrics.rms:F3} " +
                $"peak={metrics.peak:F3} centroid={metrics.spectralCentroidHz:F0}Hz " +
                $"stationarity={metrics.stationarity:F2} silence={metrics.silenceShare:F2} " +
                $"windowDelta={metrics.loopDiscontinuity:F3} similarity={metrics.loopSimilarity:F2} " +
                $"boundary={metrics.boundaryJump:F4} slope={metrics.boundarySlopeJump:F4} " +
                $"stepRms={metrics.sampleStepRms:F4} jumpRatio={metrics.boundaryJumpRatio:F2}");

            if (pair.Key != "ear_whine")
                Assert.IsFalse(metrics.spaceshipRisk,
                    $"story cue '{pair.Key}' acquired the rejected machine-drone signature");
        }

        GmAudioClipMetrics clock = GmAudioAnalysis.Measure(Resources.Load<AudioClip>("Sfx/clock_chime"));
        Assert.Less(clock.silenceShare, 0.45f,
            "clock strike has a token attack followed by mostly empty padding instead of a room decay");
        Assert.Less(clock.stationarity, 0.82f,
            "clock strike reads as a stationary oscillator instead of an archival impact and decay");

        GmAudioClipMetrics heart = GmAudioAnalysis.Measure(Resources.Load<AudioClip>("Sfx/heartbeat"));
        Assert.Less(heart.spectralCentroidHz, 900f,
            "heartbeat lost its body and became a bright synthetic click");
        Assert.LessOrEqual(heart.boundaryJump, 0.03f,
            $"heartbeat loop has a sample-value jump at its beat boundary: {heart.boundaryJump:F4}");
        Assert.LessOrEqual(heart.boundarySlopeJump, 0.04f,
            $"heartbeat loop changes slope sharply at its beat boundary: {heart.boundarySlopeJump:F4}");

        GmAudioClipMetrics whine = GmAudioAnalysis.Measure(Resources.Load<AudioClip>("Sfx/ear_whine"));
        Assert.That(whine.spectralCentroidHz, Is.InRange(2500f, 7500f),
            "tinnitus moved out of the deliberately high internal-symptom band");
        Assert.LessOrEqual(whine.boundaryJumpRatio, 3f,
            $"tinnitus wrap jump exceeds its normal high-frequency sample motion: " +
            $"jump={whine.boundaryJump:F4} stepRms={whine.sampleStepRms:F4} " +
            $"ratio={whine.boundaryJumpRatio:F2}");
        Assert.LessOrEqual(whine.boundarySlopeJump, 0.12f,
            $"tinnitus loop changes slope sharply at its boundary: {whine.boundarySlopeJump:F4}");

        GmAudioClipMetrics whisper = GmAudioAnalysis.Measure(Resources.Load<AudioClip>("Sfx/whisper_bed"));
        Assert.That(whisper.spectralCentroidHz, Is.InRange(500f, 6500f),
            "whisper no longer occupies a plausible human speech band");

        const string provenance = "Assets/Audio/NINTH-BELL-SOURCE-README.md";
        Assert.IsTrue(File.Exists(provenance), "Ninth Bell provenance is absent from the Unity project");
        string sourceRecord = File.ReadAllText(provenance);
        foreach (string required in new[] { "438313", "670465", "517868", "CC0", "Project-authored" })
            StringAssert.Contains(required, sourceRecord, $"Ninth Bell provenance omits '{required}'");

        GmAssetIndex index = GmAssetIntelligence.Reindex(false);
        GmAssetProfile whineProfile = index.assets.Single(asset =>
            asset.path.EndsWith("/ear_whine.ogg", System.StringComparison.Ordinal));
        Assert.IsTrue(whineProfile.audioSpaceshipRisk,
            "raw analyser stopped exposing the deliberately narrow tinnitus signature");
        Assert.IsTrue(whineProfile.audioIntentionallyTonal,
            "asset intelligence cannot distinguish internal tinnitus from exterior ambience");
        Assert.IsFalse(whineProfile.audioEnvironmentalRisk,
            "intentional tinnitus was incorrectly approved as an environmental bed risk");
        Assert.AreEqual("likely", whineProfile.loopSuitability,
            "phase-safe internal tinnitus was rejected by an ambience-only window comparison");
    }

    [Test]
    public void CrossingFiltersTheListenerAndUsesOneNonLoopingHumanWhisper()
    {
        var crossing = Object.FindFirstObjectByType<GmCrossing>();
        Assert.IsNotNull(crossing, "Ninth Bell crossing system is missing");
        Assert.IsFalse(crossing.WhisperLoops,
            "human whisper repeats during the crossing and exposes an obvious loop");
        var whisper = Resources.Load<AudioClip>("Sfx/whisper_bed");
        Assert.IsNotNull(whisper);
        Assert.GreaterOrEqual(whisper.length, crossing.whisperTime + 1.5f,
            "whisper source ends before its authored play-plus-fade window");

        var camera = Camera.main;
        Assert.IsNotNull(camera, "crossing has no main camera for its AudioListener");
        var oldListener = camera.GetComponent<AudioListener>();
        var oldFilter = camera.GetComponent<AudioLowPassFilter>();
        try
        {
            var method = typeof(GmCrossing).GetMethod("EnsureListenerLowPass",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "crossing listener-filter authority is missing");
            var filter = method.Invoke(crossing, null) as AudioLowPassFilter;
            Assert.IsNotNull(filter, "crossing could not build its underwater hearing filter");
            Assert.AreSame(camera.gameObject, filter.gameObject,
                "low-pass is not on the AudioListener and therefore cannot filter the game mix");
            Assert.IsNotNull(camera.GetComponent<AudioListener>());
            Assert.IsNull(crossing.GetComponent<AudioLowPassFilter>(),
                "crossing filter returned to the source-less Systems object where it is a no-op");
        }
        finally
        {
            if (oldFilter == null)
            {
                var added = camera.GetComponent<AudioLowPassFilter>();
                if (added != null) Object.DestroyImmediate(added);
            }
            if (oldListener == null)
            {
                var added = camera.GetComponent<AudioListener>();
                if (added != null) Object.DestroyImmediate(added);
            }
        }
    }

    [Test]
    public void CraftLabProvesReusableDeterministicSceneContractsWithoutShipping()
    {
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(lab);
        try
        {
            var first = GmSceneCraftLabBuilder.BuildInMemory(1907);
            string fingerprint = TransformFingerprint(first);
            Assert.AreEqual(2, first.GetComponent<GmSceneCraftProfile>().Zones.Count);
            Assert.AreEqual(3, first.GetComponentsInChildren<GmAudioIntent>(true).Count(i =>
                i.IntentId.StartsWith("lab-emitter-")));
            Assert.AreEqual(1, first.GetComponentsInChildren<GmInteractable>(true).Length);
            Assert.AreEqual(1, first.GetComponentsInChildren<GmSurfaceTag>(true).Length);
            var report = new GmSceneAuditReport { sceneId = "scene-craft-lab" };
            GmCraftQualityAudit.Analyze(lab, report);
            Assert.IsTrue(report.Passed, string.Join("\n", report.findings.Select(f => f.message)));
            Object.DestroyImmediate(first);

            var second = GmSceneCraftLabBuilder.BuildInMemory(1907);
            Assert.AreEqual(fingerprint, TransformFingerprint(second),
                "same craft seed produced a different scene graph");
            Object.DestroyImmediate(second);

            Assert.IsFalse(EditorBuildSettings.scenes.Any(s => s.path == GmSceneCraftLabBuilder.ScenePath && s.enabled),
                "non-shipping craft lab entered the player build list");
        }
        finally
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(lab, true);
        }
    }

    static string TransformFingerprint(GameObject root)
    {
        return string.Join("|", root.GetComponentsInChildren<Transform>(true)
            .OrderBy(t => t.name)
            .Select(t => $"{t.name}:{t.position.x:F3},{t.position.y:F3},{t.position.z:F3}:" +
                $"{t.localScale.x:F3},{t.localScale.y:F3},{t.localScale.z:F3}"));
    }

    [Test]
    public void EstateInteractionsUseStableBoundIdsAndEveryProminentElementIsClassified()
    {
        var targets = Object.FindObjectsByType<GmInteractable>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Assert.AreEqual(13, targets.Length, "estate should expose the thirteen authored story targets");
        Assert.AreEqual(13, targets.Select(t => t.InteractionId).Distinct().Count(),
            "interaction IDs are not unique and stable");
        foreach (var target in targets)
        {
            Assert.IsTrue(target.HasContent, $"'{target.InteractionId}' was not bound to design text");
            Assert.Greater(target.GetComponentsInChildren<Collider>(true).Length, 0,
                $"'{target.InteractionId}' has no physical focus target");
            Assert.GreaterOrEqual(target.Range, 0.5f);
            Assert.LessOrEqual(target.FocusAngle, 12f);
        }

        foreach (var element in Object.FindObjectsByType<GmCompositionElement>(FindObjectsInactive.Include,
            FindObjectsSortMode.None))
            Assert.IsTrue(element.GetComponentInChildren<GmInteractable>(true) != null ||
                element.GetComponent<GmIntentionallySilent>() != null,
                $"prominent element '{element.ElementId}' has neither interaction nor an explicit silence decision");
    }

    [Test]
    public void FocusScannerRejectsWallsAndRangeButKeepsBriefAimHysteresis()
    {
        var root = new GameObject("scanner_test_root");
        root.transform.position = new Vector3(0f, 100f, 0f);
        try
        {
            var cameraObject = new GameObject("scanner_camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.AddComponent<Camera>();
            var scanner = root.AddComponent<GmInteractionScanner>();
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "visible_target";
            target.transform.SetParent(root.transform, false);
            target.transform.localPosition = new Vector3(0f, 0f, 3f);
            target.AddComponent<GmInteractable>().Configure("scanner-visible", "Examine", 4f, 7f);
            Physics.SyncTransforms();
            scanner.Scan();
            Assert.AreEqual("scanner-visible", scanner.Focused?.InteractionId,
                "centered visible target cannot receive focus");

            cameraObject.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            scanner.Scan();
            Assert.AreEqual("scanner-visible", scanner.Focused?.InteractionId,
                "one-frame aim noise immediately drops a stable target instead of applying hysteresis");

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(scanner);
            cameraObject.transform.rotation = Quaternion.identity;
            scanner = root.AddComponent<GmInteractionScanner>();
            var occluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            occluded.transform.SetParent(root.transform, false);
            occluded.transform.localPosition = new Vector3(0f, 0f, 3f);
            occluded.AddComponent<GmInteractable>().Configure("scanner-occluded", "Examine", 4f, 7f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();
            scanner.Scan();
            Assert.IsNull(scanner.Focused, "scanner targets an interactable through solid geometry");

            Object.DestroyImmediate(wall);
            occluded.transform.localPosition = new Vector3(0f, 0f, 6f);
            Physics.SyncTransforms();
            scanner.Scan();
            Assert.IsNull(scanner.Focused, "scanner targets an interactable outside its authored range");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void GameplayInputMapSupportsKeyboardMouseAndControllerFromOneAsset()
    {
        var controls = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("Input/GmControls");
        Assert.IsNotNull(controls, "Resources/Input/GmControls.inputactions is missing");
        var map = controls.FindActionMap("Gameplay", true);
        foreach (string action in new[] { "Move", "Look", "Interact", "Cancel", "Pause", "ReviewWind", "Quit",
                     "BrightnessDown", "BrightnessUp" })
            Assert.IsNotNull(map.FindAction(action, true));
        string paths = string.Join("|", map.bindings.Select(b => b.effectivePath));
        StringAssert.Contains("<Keyboard>", paths);
        StringAssert.Contains("<Mouse>/delta", paths);
        StringAssert.Contains("<Gamepad>/leftStick", paths);
        StringAssert.Contains("<Gamepad>/dpad", paths);
        StringAssert.Contains("<Gamepad>/rightStick", paths);
        StringAssert.Contains("<Gamepad>/buttonSouth", paths);
        StringAssert.Contains("<Gamepad>/buttonEast", paths);
        StringAssert.Contains("<Gamepad>/rightShoulder", paths);
        StringAssert.Contains("<Gamepad>/start", paths);
        StringAssert.Contains("<Gamepad>/buttonNorth", paths);
        StringAssert.Contains("<Gamepad>/dpad/left", paths);
        StringAssert.Contains("<Gamepad>/dpad/right", paths);
        StringAssert.Contains("<Keyboard>/leftArrow", paths);
        StringAssert.Contains("<Keyboard>/rightArrow", paths);
        StringAssert.Contains("<Keyboard>/f8", paths);
    }

    [Test]
    public void PacingMatrixCoversAllCandidateRoutePairs()
    {
        var intent = Object.FindAnyObjectByType<GmPacingIntent>();
        Assert.IsNotNull(intent);
        Assert.AreEqual(3, intent.Candidates.Count);
        Assert.AreEqual(5, intent.Scenarios.Count);
        var report = GmPacingSimulator.Simulate(GmEstateBuilderV2.SceneId, intent);
        Assert.AreEqual(15, report.rows.Length);
        foreach (var row in report.rows)
        {
            Assert.IsFalse(row.exceedsQuietBudget,
                $"{row.candidateId}/{row.scenarioId} exceeds authored quiet budget");
            Assert.GreaterOrEqual(row.opportunitiesBeforeFirstSymptom, 2,
                $"{row.candidateId}/{row.scenarioId} reaches symptoms before the estate offers exploration");
        }
    }

    /// The design data is the shared contract. If Unity's copy drifts from the web build's, the two
    /// tell different stories from the same file name.
    [Test]
    public void DesignDataHasEveryAuthoredElement()
    {
        var json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "prologue-design.json"));
        StringAssert.Contains("\"coldOpen\"", json);
        StringAssert.Contains("estateGate", json);
        StringAssert.Contains("estateCar", json);
    }

    /// The bell is the ending now. If it is not in the scene the Prologue cannot finish at all.
    [Test]
    public void BellSummonsAndCrossingExist()
    {
        Assert.IsNotNull(Object.FindFirstObjectByType<GmBellSummons>(), "no GmBellSummons — the Prologue has no ending");
        Assert.IsNotNull(Object.FindFirstObjectByType<GmCrossing>(), "no GmCrossing — toll 9 has nowhere to go");
        Assert.IsNotNull(Object.FindFirstObjectByType<GmSymptoms>(), "no GmSymptoms — the bell tolls with no consequence");
    }

    /// A shipped test cadence would ring nine bells in 18 seconds. Easy to leave behind, invisible
    /// in a log, and it would destroy the scene's entire pace.
    [Test]
    public void BellCadenceIsShippable()
    {
        var bell = Object.FindFirstObjectByType<GmBellSummons>();
        Assert.IsNotNull(bell, "no GmBellSummons — the Prologue has no ending");
        Assert.GreaterOrEqual(bell.tollInterval, 20f, "toll interval is test-speed — the real one is 30s");
        Assert.GreaterOrEqual(bell.firstTollDelay, 20f, "first toll delay is test-speed");
    }

    /// He must wake somewhere with a floor. Waking in the mud is the failure this catches.
    [Test]
    public void WakeRoomExistsWithAPose()
    {
        Assert.IsNotNull(GameObject.Find("WakeRoom/WakePose"), "no wake pose — the crossing lands nowhere");
    }
}
