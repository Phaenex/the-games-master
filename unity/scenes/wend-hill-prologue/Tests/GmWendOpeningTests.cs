using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class GmWendOpeningTests
{
    GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void EstateRouteStopsAtTheDryManorEndpoint()
    {
        var raw = new[] {
            Vector3.zero,
            new Vector3(0f, 0f, 300f),
            new Vector3(0f, -20f, 580f),
        };
        var estate = GmWendRoute.TakeThroughDistance(raw, GmWendRoute.EstateRouteMetres);
        Assert.That(GmWendRoute.RouteLength(estate), Is.EqualTo(435f).Within(0.01f));
        Assert.That(estate[^1].z, Is.EqualTo(435f).Within(0.01f));
        Assert.That(estate[^1].y, Is.GreaterThan(-11f), "canyon tail must not become the manor endpoint");
    }

    [Test]
    public void RouteSplineProjectsProgressWithoutDependingOnWorldAxis()
    {
        root = new GameObject("route");
        var route = root.AddComponent<GmRouteSpline>();
        route.Configure(new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, -10f) });
        Assert.That(route.Length, Is.EqualTo(20f).Within(0.01f));
        Assert.That(route.ProjectDistance(new Vector3(10f, 0f, -6f)), Is.EqualTo(16f).Within(0.01f));
        Assert.That(Vector3.Distance(route.PointAt(15f), new Vector3(10f, 0f, -5f)), Is.LessThan(0.01f));
    }

    [Test]
    public void ManorApproachCorridorKeepsTheDoorSightlineClearWithoutStrippingItsOuterTreeFrame()
    {
        root = new GameObject("rotated-manor-route");
        var route = root.AddComponent<GmRouteSpline>();
        route.Configure(new[] { Vector3.zero, new Vector3(100f, 0f, 0f) });

        Assert.That(GmWendEstateForest.IsOutsideManorApproach(
            new Vector3(78f, 0f, 5.8f), route), Is.False,
            "inner avenue trees in the final approach must not cover the door");
        Assert.That(GmWendEstateForest.IsOutsideManorApproach(
            new Vector3(78f, 0f, 10.5f), route), Is.True,
            "outer avenue trees should remain to frame the manor");
        Assert.That(GmWendEstateForest.IsOutsideManorApproach(
            new Vector3(60f, 0f, 0f), route), Is.True,
            "the clearing must not flatten the earlier avenue");
    }

    [Test]
    public void ChapelSightlineCorridorRejectsOnlyVegetationInsideTheRotatedReveal()
    {
        Vector3 roadReveal = new Vector3(10f, 0f, -5f);
        Vector3 churchDoor = new Vector3(40f, 0f, 25f);

        Assert.That(GmWendEstateForest.IsOutsideSightlineCorridor(
            new Vector3(25f, 0f, 10f), roadReveal, churchDoor, 3.2f), Is.False);
        Assert.That(GmWendEstateForest.IsOutsideSightlineCorridor(
            new Vector3(20f, 0f, -1f), roadReveal, churchDoor, 3.2f), Is.True,
            "trees outside the narrow reveal must continue framing the cemetery");
        Assert.That(GmWendEstateForest.IsOutsideSightlineCorridor(
            new Vector3(2f, 0f, -13f), roadReveal, churchDoor, 3.2f), Is.True,
            "the corridor must not extend backwards through the earlier avenue");
    }

    [Test]
    public void CemeteryReviewOriginBacksAwayAlongTheMarkerAxisWithoutDriftingSideways()
    {
        Vector3 weathered = new Vector3(3f, 2f, -4f);
        Vector3 child = new Vector3(-9f, 5f, 12f);

        Vector3 origin = GmWendEstateForest.SightlineOrigin(weathered, child, 6f);

        Assert.That(origin.y, Is.EqualTo(weathered.y));
        Assert.That(Vector2.Distance(
            new Vector2(origin.x, origin.z),
            new Vector2(weathered.x, weathered.z)), Is.EqualTo(6f).Within(0.001f));
        Assert.That(GmWendEstateForest.IsOutsideSightlineCorridor(
            origin, weathered, child, 0.01f), Is.True,
            "the backed-away camera belongs before the protected marker segment");
        Assert.That(GmWendEstateForest.IsOutsideSightlineCorridor(
            weathered, origin, child, 0.01f), Is.False,
            "the marker must remain on the camera-to-child protected axis");
    }

    [Test]
    public void CemeteryDressingSupportsTheNamedMarkerSideAndTheChapelTurn()
    {
        var slots = GmWendEstateForest.CemeteryDressingSlots;

        Assert.That(slots.Count, Is.GreaterThanOrEqualTo(12),
            "three named markers need a layered grave field, not one anonymous row");
        Assert.That(slots.Count(slot => slot.lateral >= 9f), Is.GreaterThanOrEqualTo(6),
            "the dressing never reaches the positive-lateral side occupied by the named markers");
        Assert.That(slots.Any(slot => slot.lateral <= -6f), Is.True,
            "the grave field must turn back toward the negative-lateral chapel anchor");
        Assert.That(slots.Select(slot => slot.metres).Distinct().Count(), Is.EqualTo(slots.Count),
            "stacked dressing slots make overlapping crosses that only look dense in the hierarchy");
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
            GmWendEstateForest.CemeteryMonumentPrefabPath), Is.Not.Null,
            "the authored cemetery landmark must resolve to owned mesh evidence");
        Assert.That(GmWendEstateForest.CemeteryMonumentSize.y, Is.GreaterThanOrEqualTo(2.4f),
            "a waist-high monument cannot anchor a player-height cemetery composition");
    }

    [Test]
    public void ChapelCloseupPositionStaysOnTheClearedReveal()
    {
        Vector3 routeReveal = new Vector3(10f, 4f, -5f);
        Vector3 churchDoor = new Vector3(40f, 7f, 25f);

        Vector3 position = GmWendStoryTour.PositionAlongReveal(routeReveal, churchDoor, 12f);

        Assert.That(Vector2.Distance(
            new Vector2(position.x, position.z),
            new Vector2(churchDoor.x, churchDoor.z)), Is.EqualTo(12f).Within(0.001f));
        Assert.That(GmWendEstateForest.IsOutsideSightlineCorridor(
            position, routeReveal, churchDoor, 0.01f), Is.False,
            "the closeup camera must use the same cleared reveal instead of an unrelated route point");
    }

    [Test]
    public void RouteCapsuleIntersectionIncludesLowAndChestHeightBlockers()
    {
        root = new GameObject("route-capsule-test");
        var route = root.AddComponent<GmRouteSpline>();
        route.Configure(new[] { Vector3.zero, new Vector3(10f, 0f, 0f) });

        Assert.That(GmWendPerformance.IntersectsRouteCapsule(
            new Bounds(new Vector3(5f, 0.1f, 0.4f), new Vector3(0.1f, 0.2f, 0.1f)), route), Is.True);
        Assert.That(GmWendPerformance.IntersectsRouteCapsule(
            new Bounds(new Vector3(5f, 1f, 0.4f), new Vector3(0.1f, 2f, 0.1f)), route), Is.True);
        Assert.That(GmWendPerformance.IntersectsRouteCapsule(
            new Bounds(new Vector3(5f, 1f, 3f), new Vector3(0.1f, 2f, 0.1f)), route), Is.False);
    }

    [Test]
    public void RouteBlockerOwnsSiblingRenderersThroughItsLodGroup()
    {
        root = new GameObject("placed-cliff");
        LODGroup group = root.AddComponent<LODGroup>();
        var collisionObject = new GameObject("collision");
        collisionObject.transform.SetParent(root.transform, false);
        BoxCollider blocker = collisionObject.AddComponent<BoxCollider>();
        var meshObject = new GameObject("cliff-lod0");
        meshObject.transform.SetParent(root.transform, false);
        MeshRenderer siblingRenderer = meshObject.AddComponent<MeshRenderer>();
        group.SetLODs(new[] { new LOD(0.01f, new Renderer[] { siblingRenderer }) });

        CollectionAssert.Contains(GmWendPerformance.RenderersOwnedByBlocker(blocker), siblingRenderer,
            "removing a route collider without hiding its sibling LOD mesh leaves visible geometry " +
            "wrapped around the player camera");
    }

    [Test]
    public void PurchasedSandstoneCliffHierarchyIsAFalseRouteObstacleFamily()
    {
        Assert.That(GmWendPerformance.IsFalseRouteObstacleHierarchy(
            "Prefabs/Huge_Canyon_Sandstone_Cliff_LOD0__veoneio41"), Is.True,
            "the purchased cliff wrapped the arrival camera but escaped a token list that only " +
            "recognized generic 'rock' names");
    }

    [Test]
    public void PurchasedCliffIsAVisualRouteBlockerButTheWorldScaleHillIsNot()
    {
        Assert.That(GmWendPerformance.IsFalseRouteVisualHierarchy(
            "Prefabs/Huge_Canyon_Sandstone_Cliff_LOD0__veoneio41/SM_Cliff_01_LOD0"), Is.True);
        Assert.That(GmWendPerformance.IsFalseRouteVisualHierarchy(
            "Prefabs/SM_Hill11/SM_Hill"), Is.False,
            "the terrain-scale background hill must not be removed with placed route cliffs");
    }

    [Test]
    public void RouteBlockerOwnsMeshSiblingsUnderThePlacedPrefabRoot()
    {
        var prefabs = new GameObject("Prefabs");
        root = prefabs;
        var placed = new GameObject("Huge_Canyon_Sandstone_Cliff");
        placed.transform.SetParent(prefabs.transform, false);
        var collisionObject = new GameObject("collision");
        collisionObject.transform.SetParent(placed.transform, false);
        BoxCollider blocker = collisionObject.AddComponent<BoxCollider>();
        var meshObject = new GameObject("SM_Cliff_01_LOD0");
        meshObject.transform.SetParent(placed.transform, false);
        MeshRenderer siblingRenderer = meshObject.AddComponent<MeshRenderer>();

        CollectionAssert.Contains(GmWendPerformance.RenderersOwnedByBlocker(blocker), siblingRenderer,
            "an imported placed object can keep collision and LOD meshes as plain siblings without " +
            "a shared LODGroup");
    }

    [Test]
    public void EstateTreeLodRepairCollapsesDuplicateRendererWithoutChangingFarCutoff()
    {
        root = new GameObject("tree");
        var child = new GameObject("mesh");
        child.transform.SetParent(root.transform, false);
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        LODGroup duplicate = root.AddComponent<LODGroup>();
        duplicate.SetLODs(new[]
        {
            new LOD(0.30f, new Renderer[] { renderer }),
            new LOD(0.15f, new Renderer[] { renderer }),
            new LOD(0.008f, new Renderer[] { renderer }),
        });
        LODGroup empty = child.AddComponent<LODGroup>();
        empty.SetLODs(new[]
        {
            new LOD(0.30f, System.Array.Empty<Renderer>()),
            new LOD(0.10f, System.Array.Empty<Renderer>()),
        });

        Assert.That(GmWendEstateForest.HasMalformedTreeLodGroups(root), Is.True);

        int repaired = GmWendEstateForest.NormalizeTreeLodGroups(root);

        Assert.That(repaired, Is.EqualTo(2));
        Assert.That(child.GetComponent<LODGroup>(), Is.Null, "empty nested group should be removed");
        LOD[] lods = duplicate.GetLODs();
        Assert.That(lods, Has.Length.EqualTo(1));
        Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(0.008f).Within(0.0001f));
        Assert.That(lods[0].renderers, Is.EqualTo(new Renderer[] { renderer }));
        Assert.That(GmWendEstateForest.HasMalformedTreeLodGroups(root), Is.False);
    }

    [Test]
    public void EstateTreeLodRepairPreservesARealMultiMeshLodGroup()
    {
        root = new GameObject("tree");
        MeshRenderer high = new GameObject("high").AddComponent<MeshRenderer>();
        high.transform.SetParent(root.transform, false);
        MeshRenderer low = new GameObject("low").AddComponent<MeshRenderer>();
        low.transform.SetParent(root.transform, false);
        LODGroup group = root.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(0.30f, new Renderer[] { high }),
            new LOD(0.008f, new Renderer[] { low }),
        });

        Assert.That(GmWendEstateForest.HasMalformedTreeLodGroups(root), Is.False);

        int repaired = GmWendEstateForest.NormalizeTreeLodGroups(root);

        Assert.That(repaired, Is.Zero);
        Assert.That(group.GetLODs(), Has.Length.EqualTo(2));
    }

    [Test]
    public void EstateTreeLodRepairReplacesCrossPrefabRendererWithOnlyLocalMesh()
    {
        root = new GameObject("tree");
        var localObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        localObject.name = "local-mesh";
        localObject.transform.SetParent(root.transform, false);
        MeshRenderer local = localObject.GetComponent<MeshRenderer>();
        var externalObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        externalObject.name = "renderer-from-another-prefab";
        MeshRenderer external = externalObject.GetComponent<MeshRenderer>();
        LODGroup group = root.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.008f, new Renderer[] { external }) });

        try
        {
            Assert.That(GmWendEstateForest.HasMalformedTreeLodGroups(root), Is.True);
            Assert.That(GmWendEstateForest.NormalizeTreeLodGroups(root), Is.EqualTo(1));
            Assert.That(group.GetLODs()[0].renderers, Is.EqualTo(new Renderer[] { local }));
            Assert.That(GmWendEstateForest.HasMalformedTreeLodGroups(root), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(externalObject);
        }
    }

    [Test]
    public void AnchorValidationRejectsDuplicateStableIds()
    {
        root = new GameObject("anchors");
        root.AddComponent<GmWorldAnchor>().Configure("gate", 10f);
        var duplicate = new GameObject("duplicate");
        duplicate.transform.SetParent(root.transform);
        duplicate.AddComponent<GmWorldAnchor>().Configure("gate", 20f);
        CollectionAssert.Contains(GmWorldAnchor.ValidateScene(), "duplicate anchor id 'gate'");
    }

    [Test]
    public void CanonicalDesignHasExactStoryCountsAndOnlySemanticTriggers()
    {
        string path = Path.Combine(Application.dataPath, "StreamingAssets", GmWendOpening.DesignFile);
        Assert.That(File.Exists(path), Is.True, path);
        string json = File.ReadAllText(path);
        Assert.That(Count(json, "\"anchorId\""), Is.EqualTo(25)); // 13 POIs + 5 main + 7 branch
        Assert.That(Count(json, "\"id\""), Is.EqualTo(13));
        Assert.That(Count(json, "\"main\""), Is.EqualTo(12));
        Assert.That(Count(json, "\"rect\""), Is.Zero);
        Assert.That(Count(json, "\"z\""), Is.Zero);
    }

    [Test]
    public void NegativeScaleColliderIsDetectedBeforeRepair()
    {
        root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.transform.localScale = new Vector3(-1f, 2f, 3f);
        Assert.That(GmWendColliderRepair.HasNegativeLossyScale(root.transform), Is.True);
    }

    [Test]
    public void DoubleReflectionInColliderAncestryIsStillRejected()
    {
        root = new GameObject("root");
        root.transform.localScale = new Vector3(-1f, 1f, 1f);
        var child = new GameObject("child");
        child.transform.SetParent(root.transform, false);
        child.transform.localScale = new Vector3(-1f, 1f, 1f);
        BoxCollider collider = child.AddComponent<BoxCollider>();
        Assert.That(child.transform.lossyScale.x, Is.GreaterThan(0f), "two reflections cancel in lossyScale");
        Assert.That(GmWendColliderRepair.HasUnsupportedBoxTransform(collider), Is.True);
    }

    [Test]
    public void BuiltPlayerRenderBudgetIsExplicitAndNotSub720pInternally()
    {
        Assert.That(GmWendRenderBudget.InternalRenderPercentage, Is.EqualTo(67f));
        Assert.That(Mathf.CeilToInt(GmWendRenderBudget.InternalRenderPercentage * 1080f / 100f),
            Is.GreaterThanOrEqualTo(720f));
        Assert.That(GmWendRenderBudget.UpscaleFilter,
            Is.EqualTo(DynamicResUpscaleFilter.EdgeAdaptiveScalingUpres));
    }

    [Test]
    public void DiagnosticTargetRateOverrideIsExplicitAndRejectsGarbage()
    {
        Assert.That(GmWendRenderBudget.ResolveTargetFrameRate(new[] { "player" }), Is.EqualTo(100));
        Assert.That(GmWendRenderBudget.ResolveTargetFrameRate(
            new[] { "player", "-gmWendTargetFps", "240" }), Is.EqualTo(240));
        Assert.That(GmWendRenderBudget.ResolveTargetFrameRate(
            new[] { "player", "-gmWendTargetFps", "not-a-rate" }), Is.EqualTo(100));
        Assert.That(GmWendRenderBudget.ResolveTargetFrameRate(
            new[] { "player", "-gmWendTargetFps", "20" }), Is.EqualTo(100));
    }

    [Test]
    public void DiagnosticQueuedFrameOverrideIsBoundedAndRejectsGarbage()
    {
        Assert.That(GmWendRenderBudget.ResolveMaxQueuedFrames(new[] { "player" }), Is.EqualTo(1));
        Assert.That(GmWendRenderBudget.ResolveMaxQueuedFrames(
            new[] { "player", "-gmWendQueueFrames", "2" }), Is.EqualTo(2));
        Assert.That(GmWendRenderBudget.ResolveMaxQueuedFrames(
            new[] { "player", "-gmWendQueueFrames", "not-a-count" }), Is.EqualTo(1));
        Assert.That(GmWendRenderBudget.ResolveMaxQueuedFrames(
            new[] { "player", "-gmWendQueueFrames", "4" }), Is.EqualTo(1));
    }

    [Test]
    public void DiagnosticFarClipOverrideIsBoundedAndRejectsGarbage()
    {
        Assert.That(GmWendRenderBudget.ResolveCameraFarClip(new[] { "player" }), Is.EqualTo(180f));
        Assert.That(GmWendRenderBudget.ResolveCameraFarClip(
            new[] { "player", "-gmWendFarClip", "90" }), Is.EqualTo(90f));
        Assert.That(GmWendRenderBudget.ResolveCameraFarClip(
            new[] { "player", "-gmWendFarClip", "not-a-distance" }), Is.EqualTo(180f));
        Assert.That(GmWendRenderBudget.ResolveCameraFarClip(
            new[] { "player", "-gmWendFarClip", "40" }), Is.EqualTo(180f));
    }

    [Test]
    public void PrologueRenderDistanceAndFrameCapLiveOnFeelConfig()
    {
        var config = ScriptableObject.CreateInstance<GmFeelConfig>();
        try
        {
            Assert.That(config.wendTargetFrameRate, Is.EqualTo(100));
            Assert.That(config.wendRenderCorridorMetres, Is.EqualTo(100f));
            Assert.That(config.wendCameraFarClipMetres, Is.EqualTo(180f));
            Assert.That(config.wendCameraFarClipMetres,
                Is.GreaterThanOrEqualTo(config.wendRenderCorridorMetres));
        }
        finally { Object.DestroyImmediate(config); }
    }

    [Test]
    public void StandaloneScreenshotRuleAcceptsAuthoredBlackColdOpenButNotABlankFrame()
    {
        Assert.That(GmStandaloneReviewProbe.ScreenshotEvidenceIsValid(
            "01-cold-open-ui.png", 532_074, 0, 3, 0.947f, 0f), Is.True);
        Assert.That(GmStandaloneReviewProbe.ScreenshotEvidenceIsValid(
            "controller-01-cold-open.png", 543_564, 0, 3, 0.946f, 0f), Is.True);
        Assert.That(GmStandaloneReviewProbe.ScreenshotEvidenceIsValid(
            "01-cold-open-ui.png", 532_074, 0, 0, 0.995f, 0f), Is.False);
        Assert.That(GmStandaloneReviewProbe.ScreenshotEvidenceIsValid(
            "01-cold-open-ui.png", 9_999, 0, 3, 0.947f, 0f), Is.False);
        Assert.That(GmStandaloneReviewProbe.ScreenshotEvidenceIsValid(
            "02-spawn-facing-mansion.png", 532_074, 0, 3, 0.947f, 0f), Is.False,
            "only the authored cold-open states may use the narrow black-frame contract");
    }

    [Test]
    public void HouseBeginningUsesWindowMotivatedCoolSeparationAndDoesNotBlastWakeRoomOrange()
    {
        GmHouseBeginningBuilder.Build();
        root = GameObject.Find(GmHouseBeginningBuilder.RootName);

        Transform hall = root.transform.Find("EntryHall");
        Transform parlor = root.transform.Find("Parlor");
        Light[] hallCool = hall.GetComponentsInChildren<Light>(true)
            .Where(light => light.color.b > light.color.r * 1.25f).ToArray();
        Light[] parlorCool = parlor.GetComponentsInChildren<Light>(true)
            .Where(light => light.color.b > light.color.r * 1.25f).ToArray();
        Light wakeFill = hall.GetComponentsInChildren<Light>(true)
            .Single(light => light.name == "WakeClockFill");

        Assert.That(hallCool, Has.Length.GreaterThanOrEqualTo(2));
        Assert.That(parlorCool, Has.Length.GreaterThanOrEqualTo(2));
        Assert.That(hallCool.Concat(parlorCool).All(light => light.type == LightType.Spot), Is.True,
            "cool separation must read as window shafts, not omnidirectional blue fill");
        Assert.That(wakeFill.type, Is.EqualTo(LightType.Spot));
        Assert.That(wakeFill.color.b, Is.GreaterThan(wakeFill.color.r * 1.25f),
            "the wake clock is filled by the east-window moon shaft, not another orange point light");
        Assert.That(wakeFill.intensity, Is.InRange(180f, 300f));
    }

    [Test]
    public void PerformanceAuditIncludesHouseRenderersButClassifiesThemForRuntimeCulling()
    {
        root = new GameObject(GmHouseBeginningBuilder.RootName);
        GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prop.transform.SetParent(root.transform, false);
        Renderer renderer = prop.GetComponent<Renderer>();

        Assert.That(GmWendPerformance.IsExcludedFromRendererAudit(renderer), Is.False,
            "house renderers must pass through the performance audit instead of disappearing at its boundary");
        Assert.That(GmWendPerformance.IsRuntimeCulledInterior(renderer), Is.True,
            "the audit must preserve house renderers for the runtime interior culler");
    }

    [Test]
    public void HouseBeginningWarmPracticalsHaveVisibleFixturesInsteadOfFloatingBulbs()
    {
        GmHouseBeginningBuilder.Build();
        root = GameObject.Find(GmHouseBeginningBuilder.RootName);

        Light[] practicals = root.GetComponentsInChildren<Light>(true)
            .Where(light => light.enabled && light.gameObject.activeInHierarchy)
            .Where(light => light.color.r > light.color.b * 1.12f)
            .Where(light => light.name.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) < 0)
            .Where(light => light.name.IndexOf("Ambient", System.StringComparison.OrdinalIgnoreCase) < 0)
            .ToArray();

        foreach (Light practical in practicals)
        {
            Renderer[] nearby = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                .Where(renderer => renderer.bounds.SqrDistance(practical.transform.position) < 1.2f * 1.2f)
                .Where(renderer => RendererPathContains(renderer,
                    "Lamp", "Sconce", "Chandelier", "Candle", "Fire", "Hearth", "Ember", "Lantern"))
                .ToArray();
            Assert.That(nearby, Is.Not.Empty,
                $"{practical.name} is a warm practical with no visible source");
        }
    }

    [Test]
    public void WakeRoomBalanceKeepsWarmPracticalsSubordinateToTheMoonShaft()
    {
        root = new GameObject("wake-light-test");
        Light candle = AddTestLight(root.transform, "CandleFlame", 32f, Color.red);
        Light hearth = AddTestLight(root.transform, "HearthEmberLight", 40f, Color.red);
        Light ambient = AddTestLight(root.transform, "WakeRoomAmbientFill", 10f, Color.red);
        Light moon = AddTestLight(root.transform, "SlattedMoonlightShaft", 45f, Color.white);

        GmHouseBeginningBuilder.BalanceWakeRoomLighting(root.transform);

        Assert.That(candle.intensity, Is.EqualTo(22f));
        Assert.That(hearth.intensity, Is.EqualTo(22f));
        Assert.That(ambient.intensity, Is.EqualTo(8f));
        Assert.That(moon.intensity, Is.EqualTo(75f));
        Assert.That(moon.color.b, Is.GreaterThan(moon.color.r * 1.25f));
    }

    [Test]
    public void CoachHousePlacementKeepsTheWholeShellClearOfAReturningRouteLeg()
    {
        // The real route bends back past its 338m POI. A one-leg lateral offset put the shell across
        // the earlier leg, which the controller then walked through at 315m. This synthetic hairpin
        // reproduces that geometry without depending on the purchased scene.
        var routeSamples = new[]
        {
            new Vector3(-20f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 0f),
            new Vector3(20f, 0f, 12f), new Vector3(0f, 0f, 12f), new Vector3(-20f, 0f, 12f),
        };

        GmWendOutbuildings.CoachPlacement placement = GmWendOutbuildings.SelectCoachHousePlacement(
            routeSamples, Vector3.zero, Vector3.forward);

        Assert.That(placement.minRouteClearance, Is.GreaterThanOrEqualTo(4.2f));
        Assert.That(placement.outward.z, Is.LessThan(0f),
            "the preferred side contains the returning leg, so the building must choose the safe side");
    }

    [Test]
    public void CoachHouseDressingUsesOwnedPeriodMeshesAndKeepsOnlyHiddenPrimitiveCollision()
    {
        root = new GameObject("coach-house-art-test");
        Transform room = new GameObject("CoachHouse").transform;
        room.SetParent(root.transform, false);
        room.SetPositionAndRotation(new Vector3(50f, 4f, -30f), Quaternion.Euler(0f, 37f, 0f));
        Transform interior = new GameObject("Interior").transform;
        interior.SetParent(room, false);

        string[] structuralNames =
        {
            "CoachHouseFloor", "CoachHouseCeiling", "CoachHouseWestWall", "CoachHouseEastWall",
            "CoachHouseNorthWall", "CoachHouseSouthWallWest", "CoachHouseSouthWallEast", "CoachHouseLintel",
        };
        foreach (string structuralName in structuralNames)
        {
            GameObject structural = GameObject.CreatePrimitive(PrimitiveType.Cube);
            structural.name = structuralName;
            structural.transform.SetParent(room, false);
        }

        GmWendOutbuildings.DressCoachHouse(room, interior);
        interior.gameObject.SetActive(false);

        Transform exterior = room.Find("ExteriorArt");
        Assert.That(exterior, Is.Not.Null);
        Assert.That(exterior.gameObject.activeInHierarchy, Is.True,
            "the roadside facade and roof must be visible before the player enters");
        Assert.That(exterior.GetComponentsInChildren<Renderer>(true), Has.Length.GreaterThanOrEqualTo(12));

        string[] requiredOwnedArt =
        {
            "OwnedArt_CoachRoof", "OwnedArt_CoachDoorLeft", "OwnedArt_CoachDoorRight",
            "OwnedArt_StallDivider1", "OwnedArt_StallDivider2", "OwnedArt_StallDivider3",
            "OwnedArt_FeedingTrough", "OwnedArt_HayStack", "OwnedArt_WoodenCart",
            "OwnedArt_Barrel", "OwnedArt_CrateStack",
        };
        Transform[] all = room.GetComponentsInChildren<Transform>(true);
        foreach (string requiredName in requiredOwnedArt)
            Assert.That(all.Any(candidate => candidate.name == requiredName), Is.True,
                $"coach-house dressing is missing {requiredName}");
        for (int stall = 1; stall <= 3; stall++)
        {
            Transform divider = all.Single(candidate => candidate.name == $"OwnedArt_StallDivider{stall}");
            Assert.That(Quaternion.Angle(divider.localRotation, Quaternion.identity), Is.LessThan(1f),
                $"stall divider {stall} runs along the east wall instead of projecting into a stall bay");
        }

        foreach (string structuralName in structuralNames)
        {
            Transform structural = all.Single(candidate => candidate.name == structuralName);
            Assert.That(structural.GetComponent<Renderer>().enabled, Is.False,
                $"{structuralName} is still a visible primitive");
            Assert.That(structural.GetComponent<Collider>(), Is.Not.Null,
                $"{structuralName} lost its load-bearing collision");
        }

        Assert.That(interior.GetComponentsInChildren<Renderer>(true)
            .Count(renderer => renderer.GetComponentsInParent<Transform>(true)
                .Any(candidate => candidate.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
            Is.GreaterThanOrEqualTo(8), "the enterable room still lacks period mesh dressing");
        Assert.That(room.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.GetComponentsInParent<Transform>(true)
                .Any(candidate => candidate.name.StartsWith(GmOwnedPropFactory.VisualPrefix)))
            .SelectMany(renderer => renderer.sharedMaterials)
            .All(material => material != null && material.shader != null && material.shader.name == "HDRP/Lit"),
            Is.True, "an owned coach-house mesh still uses an unproved source-pack shader");
        Assert.That(room.GetComponentsInChildren<Light>(true)
            .All(light => Vector3.Distance(light.transform.position, room.position) < 12f), Is.True,
            "coach-house lights were given local coordinates to a helper that expects world positions");
    }

    [Test]
    public void CoachHouseFootprintRejectsVegetationInsideTheRotatedStableAndApproachApron()
    {
        root = new GameObject("CoachHouse");
        root.transform.SetPositionAndRotation(new Vector3(18f, 0f, -24f), Quaternion.Euler(0f, 41f, 0f));

        Assert.That(GmWendOutbuildings.IsOutsideCoachHouseFootprint(
            root.transform.TransformPoint(Vector3.zero), root.transform, 1.5f), Is.False);
        Assert.That(GmWendOutbuildings.IsOutsideCoachHouseFootprint(
            root.transform.TransformPoint(new Vector3(0f, 0f, -5.4f)), root.transform, 1.5f), Is.False,
            "the doorway apron also needs to stay free of trunks and brush");
        Assert.That(GmWendOutbuildings.IsOutsideCoachHouseFootprint(
            root.transform.TransformPoint(new Vector3(8f, 0f, 0f)), root.transform, 1.5f), Is.True);
    }

    [Test]
    public void EstateFenceWingIsCombinedPeriodGeometryAndExtendsPastTheFormer45MetreCutoff()
    {
        root = new GameObject("gate-wing-art-test");
        GameObject wing = GmWendOpening.BuildPerimeterWingArt(root.transform, 1f, 2.7f, 72f);

        Renderer[] renderers = wing.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers, Has.Length.InRange(2, 3),
            "one renderer per picket recreates the draw-call defect; iron and stone should be combined");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        Assert.That(bounds.max.x, Is.GreaterThanOrEqualTo(71.5f));
        Assert.That(wing.GetComponentsInChildren<MeshFilter>(true)
            .All(filter => AssetDatabase.GetAssetPath(filter.sharedMesh) != "Library/unity default resources"),
            Is.True, "the player-facing perimeter still exposes Unity primitive meshes");
        Assert.That(wing.GetComponentsInChildren<Collider>(true)
            .Any(collider => collider.bounds.max.x >= 71.5f), Is.True,
            "the extended visible fence has no matching collision span");
    }

    [Test]
    public void EstateFenceWingLengthsReachTheTerrainBoundaryInBothDirections()
    {
        var world = new Bounds(Vector3.zero, new Vector3(200f, 20f, 200f));
        Vector3 gate = new Vector3(-40f, 0f, -70f);
        Vector3 right = new Vector3(0.6f, 0f, 0.8f);

        Assert.That(GmWendOpening.PerimeterWingEnd(world, gate, right, 1f),
            Is.EqualTo(212.5f).Within(0.01f),
            "the positive wing stopped before the first terrain edge hit by the gate's local-right ray");
        Assert.That(GmWendOpening.PerimeterWingEnd(world, gate, right, -1f),
            Is.EqualTo(37.5f).Within(0.01f),
            "the negative wing stopped before the first terrain edge hit by the gate's local-left ray");
    }

    [Test]
    public void EstateLampPostLightHasAVisibleLanternFixture()
    {
        root = new GameObject("estate-lamp-practical-test");
        GameObject pole = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_LightPole.prefab");
        GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Prefabs/SM_Lantern.prefab");
        MethodInfo place = typeof(GmWendEstateForest).GetMethod("PlaceLampPost",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(place, Is.Not.Null);
        place.Invoke(null, new object[]
        {
            pole, lantern, root.transform, null, Vector3.zero, Vector3.forward, 1f, 0,
        });

        Light practical = root.GetComponentInChildren<Light>(true);
        AssertVisibleLanternFixture(practical);
    }

    [TestCase("MakeGateLantern", "GateLantern")]
    [TestCase("MakeManorLantern", "PorchLanternLeft")]
    public void OpeningLanternLightsHaveVisiblePeriodFixtures(string methodName, string objectName)
    {
        root = new GameObject("opening-lantern-practical-test");
        MethodInfo make = typeof(GmWendOpening).GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(make, Is.Not.Null);
        if (methodName == "MakeGateLantern")
            make.Invoke(null, new object[] { root.transform, new Vector3(0f, 3f, 0f) });
        else
            make.Invoke(null, new object[] { root.transform, objectName, new Vector3(0f, 3f, 0f) });

        Light practical = root.GetComponentInChildren<Light>(true);
        AssertVisibleLanternFixture(practical);
    }

    [Test]
    public void ArrivalCarHeadlampsAreVisibleWarmSpotsAimedAlongTheVehicle()
    {
        root = new GameObject("arrival-car-light-test");
        var bounds = new Bounds(new Vector3(0f, 1.1f, 0f), new Vector3(2f, 1.4f, 4.6f));

        GmWendOpening.AddArrivalCarHeadlamps(root.transform, bounds, Vector3.forward);

        Light[] lamps = root.GetComponentsInChildren<Light>(true);
        Assert.That(lamps, Has.Length.EqualTo(2));
        Assert.That(lamps.All(light => light.type == LightType.Spot), Is.True);
        Assert.That(lamps.All(light => light.intensity >= 650f && light.range >= 18f), Is.True,
            "the headlamps must reveal the car and verge instead of existing as decorative pixels");
        Assert.That(lamps.All(light => Vector3.Dot(light.transform.forward, Vector3.forward) > 0.98f), Is.True);
        Assert.That(lamps.All(light => light.color.r > light.color.b * 1.25f), Is.True);
        Assert.That(lamps.Select(light => Mathf.Sign(light.transform.position.x)).Distinct().Count(),
            Is.EqualTo(2), "the two fixtures must straddle the vehicle centreline");
        foreach (Light lamp in lamps) AssertVisibleLanternFixture(lamp);
    }

    [Test]
    public void ArrivalCarStylingKeepsBodyGlassAndMetalAsSeparateReadableMaterials()
    {
        root = new GameObject("arrival-car-material-test");
        Shader shader = Shader.Find("HDRP/Lit");
        MeshRenderer body = AddNamedRenderer(root.transform, "Car03_Body_LOD0", shader);
        MeshRenderer glass = AddNamedRenderer(root.transform, "Car03_Glass_LOD0", shader);
        MeshRenderer grille = AddNamedRenderer(root.transform, "Car03_Grill_LOD0", shader);

        GmWendOpening.StyleArrivalCar(new Renderer[] { body, glass, grille });

        Color bodyColor = body.sharedMaterial.GetColor("_BaseColor");
        Color glassColor = glass.sharedMaterial.GetColor("_BaseColor");
        Color grilleColor = grille.sharedMaterial.GetColor("_BaseColor");
        Assert.That(bodyColor.r, Is.GreaterThan(bodyColor.b * 1.8f));
        Assert.That(glassColor.r + glassColor.g + glassColor.b,
            Is.LessThan(bodyColor.r + bodyColor.g + bodyColor.b));
        Assert.That(grilleColor.r + grilleColor.g + grilleColor.b,
            Is.GreaterThan(glassColor.r + glassColor.g + glassColor.b));
        Assert.That(grille.sharedMaterial.GetFloat("_Metallic"), Is.GreaterThanOrEqualTo(0.6f));
        Assert.That(new[] { body.sharedMaterial, glass.sharedMaterial, grille.sharedMaterial }.Distinct().Count(),
            Is.EqualTo(3), "semantic parts must not collapse back to one cloned material");
    }

    [Test]
    public void ArrivalCarMoonKeyIsCoolAndAimedAtTheVehicleBody()
    {
        root = new GameObject("arrival-car-moon-key-test");
        var bounds = new Bounds(new Vector3(0f, 1.1f, 0f), new Vector3(2f, 1.4f, 4.6f));

        Light key = GmWendOpening.AddArrivalCarMoonKey(root.transform, bounds, Vector3.forward);

        Assert.That(key, Is.Not.Null);
        Assert.That(key.name, Is.EqualTo("ArrivalMoonlightKey"));
        Assert.That(key.type, Is.EqualTo(LightType.Spot));
        Assert.That(key.intensity, Is.InRange(900f, 1500f));
        Assert.That(key.range, Is.GreaterThanOrEqualTo(14f));
        Assert.That(key.color.b, Is.GreaterThan(key.color.r * 1.25f));
        Vector3 expected = (bounds.center - key.transform.position).normalized;
        Assert.That(Vector3.Dot(key.transform.forward, expected), Is.GreaterThan(0.999f));
    }

    [Test]
    public void ArrivalCarRotationKeepsTheImportedWheelAxisBelowTheBody()
    {
        Vector3 routeForward = new Vector3(0.8f, 0f, 0.6f).normalized;

        Quaternion rotation = GmWendOpening.ArrivalCarRotation(routeForward);

        Assert.That(Vector3.Dot(rotation * Vector3.back, routeForward), Is.GreaterThan(0.999f),
            "the imported grille is on local back and must face up the estate route");
        Assert.That(Vector3.Dot(rotation * Vector3.up, Vector3.down), Is.GreaterThan(0.999f),
            "this FBX stores its wheel side on positive local Y; mapping it to world up flips the car");
    }

    [Test]
    public void DerivedGapLampAlsoHasAVisiblePeriodFixture()
    {
        root = new GameObject("gap-lamp-practical-test");
        MethodInfo create = typeof(GmWendLamps).GetMethod("CreateGapLamp",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(create, Is.Not.Null,
            "gap lamps need a testable construction seam so their visible source cannot disappear");
        create.Invoke(null, new object[] { root.transform, new Vector3(3f, 4f, 5f), 0 });

        Light practical = root.GetComponentInChildren<Light>(true);
        AssertVisibleLanternFixture(practical);
    }

    [Test]
    public void VictorianProofKitIsInstalledAndBuildsMappedHdrpMaterials()
    {
        GmVictorianInteriorKit.Prepare();
        Assert.That(GmVictorianInteriorKit.Ready, Is.True,
            "run `npm run unity:assets:victorian` before rebuilding the opening");
        Material material = GmVictorianInteriorKit.Surface("chair", "test-chair", Vector2.one);
        Assert.That(material.shader.name, Is.EqualTo("HDRP/Lit"));
        Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null);
        Assert.That(material.GetTexture("_NormalMap"), Is.Not.Null);
        Object.DestroyImmediate(material);
    }

    static int Count(string input, string value) =>
        input.Split(new[] { value }, System.StringSplitOptions.None).Length - 1;

    static void AssertVisibleLanternFixture(Light practical)
    {
        Assert.That(practical, Is.Not.Null);
        Renderer[] renderers = practical.transform.parent != null
            ? practical.transform.parent.GetComponentsInChildren<Renderer>(true)
            : practical.GetComponentsInChildren<Renderer>(true);
        Renderer[] lanterns = renderers.Where(renderer =>
            renderer.enabled && renderer.gameObject.activeInHierarchy &&
            renderer.GetComponentsInParent<Transform>(true)
                .Any(candidate => candidate.name.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) >= 0))
            .ToArray();
        Assert.That(lanterns, Is.Not.Empty, $"{practical.name} is still a floating light with no lantern mesh");
        Assert.That(lanterns.Min(renderer => renderer.bounds.SqrDistance(practical.transform.position)),
            Is.LessThan(0.75f * 0.75f), $"{practical.name} is too far from its visible lantern fixture");
    }

    static bool RendererPathContains(Renderer renderer, params string[] tokens)
    {
        foreach (Transform candidate in renderer.GetComponentsInParent<Transform>(true))
            foreach (string token in tokens)
                if (candidate.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
        return false;
    }

    static MeshRenderer AddNamedRenderer(Transform parent, string name, Shader shader)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(shader) { name = name + "_Source" };
        return renderer;
    }

    static Light AddTestLight(Transform parent, string name, float intensity, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Light light = go.AddComponent<Light>();
        light.intensity = intensity;
        light.color = color;
        return light;
    }
}
