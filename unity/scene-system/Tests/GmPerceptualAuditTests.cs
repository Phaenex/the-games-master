using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

sealed class GmPerceptualTestTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots = {
        new GmReviewShot("entry", new Vector3(0f, 1f, 0f), 0f, 0f),
        new GmReviewShot("detail", new Vector3(0f, 1f, 2f), 0f, 0f),
    };
    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
}

public sealed class GmPerceptualAuditTests
{
    Scene scene;
    Camera camera;
    GmPerceptualTestTour tour;

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Camera");
        camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        var tourObject = new GameObject("Tour");
        tour = tourObject.AddComponent<GmPerceptualTestTour>();
    }

    [TearDown]
    public void TearDown()
    {
        if (scene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void RepetitionRejectsRenamedButPerceptuallyIdenticalRows()
    {
        var ids = new string[6];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = $"grave-{i}";
            MarkCube(ids[i], new Vector3(i * 2f - 5f, 0.5f, 8f), Vector3.one);
        }
        var owner = new GameObject("RepetitionIntent");
        GmPerceptualAuthoring.Repetition(owner, "grave-rhythm",
            "Graves must read as accumulated family plots, not a duplicated kit row.",
            ids, 4, 0.34f, 2);

        GmSceneAuditReport report = Analyze();
        Assert.That(report.findings.Any(item => item.category == "repetition" &&
            item.severity == GmAuditSeverity.Error), Is.True);
    }

    [Test]
    public void RepetitionAcceptsDistinctSilhouettesAndIrregularPlotSpacing()
    {
        var ids = new string[6];
        float[] x = { -5.2f, -3.7f, -0.8f, 1.4f, 4.9f, 7.1f };
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = $"grave-{i}";
            GmCompositionElement element = MarkCube(ids[i],
                new Vector3(x[i], 0.5f, 7f + i * 0.17f),
                new Vector3(0.7f + i * 0.07f, 0.8f + i * 0.13f, 0.5f));
            element.transform.rotation = Quaternion.Euler(i * 5f, 180f, i * 7f);
        }
        var owner = new GameObject("RepetitionIntent");
        GmPerceptualAuthoring.Repetition(owner, "grave-rhythm",
            "Graves must read as accumulated family plots, not a duplicated kit row.",
            ids, 6, 0.25f, 2);

        GmSceneAuditReport report = Analyze();
        Assert.That(report.findings.Where(item => item.category == "repetition" &&
            item.severity == GmAuditSeverity.Error), Is.Empty,
            string.Join("\n", report.findings.Select(item => item.message)));
    }

    [Test]
    public void StoryRejectsPropDensityWithoutThreeNamedTraces()
    {
        MarkCube("well", new Vector3(0f, 0.5f, 8f), Vector3.one);
        for (int i = 0; i < 12; i++)
            GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position =
                new Vector3((i % 4) - 2f, 0.25f, 6f + i / 4);
        var owner = new GameObject("StoryIntent");
        GmPerceptualAuthoring.Story(owner, "garden-interruption", "garden",
            "Productive kitchen-garden work stopped abruptly and was never resumed.", "well",
            new[] { "missing-basket", "missing-tool" },
            new[] { new GmStoryRevealStep("entry", "well") });

        GmSceneAuditReport report = Analyze();
        Assert.That(report.findings.Count(item => item.category == "story" &&
            item.severity == GmAuditSeverity.Error), Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void StyleRejectsUnexplainedEraConflict()
    {
        MarkCube("car", new Vector3(0f, 0.75f, 7f), new Vector3(2f, 1.5f, 4f));
        var owner = new GameObject("StyleIntent");
        GmPerceptualAuthoring.Style(owner, "arrival-car-style", "car",
            GmEra.Contemporary, GmEra.Victorian, false, "", 1f,
            new[] { new GmStyleShotBudget("entry", 0.8f) });

        GmSceneAuditReport report = Analyze();
        Assert.That(report.findings.Any(item => item.category == "style" &&
            item.message.Contains("without an exception")), Is.True);
    }

    [Test]
    public void SurfacePaletteRejectsUntreatedFluorescentFoliageAndAcceptsDryTint()
    {
        GmCompositionElement foliage = MarkCube("dry-growth", new Vector3(0f, 0.5f, 7f), Vector3.one);
        var material = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
        foliage.GetComponent<Renderer>().sharedMaterial = material;
        string colourProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        material.SetColor(colourProperty, new Color(0.10f, 0.92f, 0.16f));
        var owner = new GameObject("SurfacePaletteIntent");
        GmPerceptualAuthoring.SurfacePalette(owner, "dry-garden-surfaces",
            "Failed crops must remain warm, restrained and visible instead of glowing green or disappearing black.",
            new[] { new GmSurfacePaletteRule("dry-growth", 0.08f, 0.45f, 0.82f, true) });

        Assert.That(Analyze().findings.Any(item => item.category == "surface-palette" &&
            item.severity == GmAuditSeverity.Error), Is.True);

        material.SetColor(colourProperty, new Color(0.32f, 0.22f, 0.12f));
        Assert.That(Analyze().findings.Where(item => item.category == "surface-palette" &&
            item.severity == GmAuditSeverity.Error), Is.Empty);
    }

    [Test]
    public void LandscapeRejectsMissingMiddleDistanceAndFlatHorizon()
    {
        MarkCube("near", new Vector3(0f, 1f, 5f), new Vector3(2f, 2f, 1f));
        MarkCube("far", new Vector3(0f, 2f, 24f), new Vector3(2f, 4f, 1f));
        var owner = new GameObject("LandscapeIntent");
        GmPerceptualAuthoring.Landscape(owner, "acreage-depth",
            "The property must continue through three irregular distance bands.",
            new[] { new GmLandscapeShotRequirement("entry", new[] { "near" },
                new[] { "missing-middle" }, new[] { "far" }, 0.55f, 0.85f) });

        GmSceneAuditReport report = Analyze();
        Assert.That(report.findings.Any(item => item.category == "landscape" &&
            item.severity == GmAuditSeverity.Error), Is.True);
    }

    [Test]
    public void ViewportOccupancyNamesDominantSubjectAndShotClearanceProtectsItsCamera()
    {
        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "AccidentalNearCameraBlocker";
        blocker.transform.position = new Vector3(0f, 1f, 4f);
        blocker.transform.localScale = new Vector3(4f, 3f, 0.5f);

        GmViewportOccupancyReport occupancy = GmViewportOccupancyAudit.Analyze(
            "perceptual-test", tour, camera, 4);
        GmViewportOccupancyRow entry = occupancy.rows.First(item =>
            item.shotName == "entry" && item.rendererPath.Contains(blocker.name));
        Assert.That(entry.clippedCoverage, Is.GreaterThan(0.25f));
        Assert.That(occupancy.focusHits.Any(hit => hit.shotName == "entry" &&
            hit.colliderPath.Contains(blocker.name)), Is.True);
        Assert.That(occupancy.focusHits.Any(hit => hit.shotName == "entry" &&
            hit.rendererBoundsPath.Contains(blocker.name)), Is.True);
        Assert.That(occupancy.focusHits.Any(hit => hit.shotName == "entry" &&
            hit.meshSurfacePath.Contains(blocker.name)), Is.True,
            "actual mesh-surface evidence should identify the visible blocker, not just an overlapping bounds box");
        Assert.That(GmReviewShotProtection.IsInsideHorizontalClearance(
            blocker.transform.position, tour.ShotsForAudit, 5f), Is.True);
        Assert.That(GmReviewShotProtection.IsInsideHorizontalClearance(
            new Vector3(20f, 0f, 20f), tour.ShotsForAudit, 5f), Is.False);
    }

    [Test]
    public void ClearedPathGuideRejectsPaintedFloorGeometryAndAcceptsNegativeSpace()
    {
        var guideObject = new GameObject("GardenNegativeSpace");
        GmClearedPathGuide guide = guideObject.AddComponent<GmClearedPathGuide>();
        guide.Configure(12f, 1.4f, false);
        GameObject paintedStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paintedStrip.transform.SetParent(guideObject.transform);

        GmSceneAuditReport rejected = Analyze();
        Assert.That(rejected.findings.Any(item => item.category == "cleared-path" &&
            item.severity == GmAuditSeverity.Error), Is.True);

        UnityEngine.Object.DestroyImmediate(paintedStrip);
        GmSceneAuditReport accepted = Analyze();
        Assert.That(accepted.findings.Where(item => item.category == "cleared-path" &&
            item.severity == GmAuditSeverity.Error), Is.Empty);
        Assert.That(guide.WorldBounds.size.z, Is.EqualTo(12f).Within(0.001f));
        Assert.That(guide.WorldBounds.size.x, Is.EqualTo(1.4f).Within(0.001f));
    }

    [Test]
    public void AudioAnalysisSeparatesStationaryToneFromSparseWindLikeNoise()
    {
        const int frequency = 8000, samples = 16000;
        AudioClip drone = AudioClip.Create("synthetic-drone", samples, 1, frequency, false);
        var droneData = new float[samples];
        for (int i = 0; i < samples; i++)
            droneData[i] = 0.35f * Mathf.Sin(2f * Mathf.PI * 250f * i / frequency);
        drone.SetData(droneData, 0);

        AudioClip sparse = AudioClip.Create("sparse-noise", samples, 1, frequency, false);
        var sparseData = new float[samples];
        uint state = 17;
        for (int i = 0; i < samples; i++)
        {
            state = state * 1664525u + 1013904223u;
            float noise = ((state >> 8) / 16777216f) * 2f - 1f;
            sparseData[i] = i % 4000 < 2200 ? 0f : noise * 0.12f;
        }
        sparse.SetData(sparseData, 0);
        try
        {
            GmAudioClipMetrics droneMetrics = GmAudioAnalysis.Measure(drone);
            GmAudioClipMetrics sparseMetrics = GmAudioAnalysis.Measure(sparse);
            Assert.That(droneMetrics.spaceshipRisk, Is.True,
                $"tone={droneMetrics.persistentToneDb:F1} stationary={droneMetrics.stationarity:F2}");
            Assert.That(sparseMetrics.spaceshipRisk, Is.False);
            Assert.That(sparseMetrics.silenceShare, Is.GreaterThan(0.2f));
            Assert.That(droneMetrics.boundaryJumpRatio, Is.LessThanOrEqualTo(3f),
                "an integer-cycle tone was falsely classified as a click seam merely because its " +
                "last and first analysis windows carry different phase");

            float previousEnd = droneData[droneData.Length - 1];
            droneData[droneData.Length - 1] = 1f;
            drone.SetData(droneData, 0);
            GmAudioClipMetrics brokenBoundary = GmAudioAnalysis.Measure(drone);
            Assert.That(brokenBoundary.boundaryJumpRatio, Is.GreaterThan(3f),
                "a true sample-boundary discontinuity passed the loop seam evidence");
            droneData[droneData.Length - 1] = previousEnd;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(drone);
            UnityEngine.Object.DestroyImmediate(sparse);
        }
    }

    [Test]
    public void PacingRejectsBrokenCanonAndAcceptsThreeReviewCadences()
    {
        var brokenOwner = new GameObject("BrokenPacing");
        GmPerceptualAuthoring.Pacing(brokenOwner, "bell", "A deliberately invalid fixture.",
            8, 3, 8, 35f, new[] { new GmPacingCandidate("test-speed", 2f, 2f) });
        Assert.That(Analyze().findings.Any(item => item.category == "pacing" &&
            item.severity == GmAuditSeverity.Error), Is.True);

        UnityEngine.Object.DestroyImmediate(brokenOwner);
        var validOwner = new GameObject("ValidPacing");
        GmPerceptualAuthoring.Pacing(validOwner, "bell",
            "Nine fixed tolls compare tension without changing the canonical story order.",
            9, 4, 9, 35f, new[] {
                new GmPacingCandidate("tight-195", 35f, 20f),
                new GmPacingCandidate("middle-240", 40f, 25f),
                new GmPacingCandidate("control-285", 45f, 30f),
            }, new[] { new GmPacingScenario("direct", 2.5f,
                new[] { Vector3.zero, new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 70f) },
                new[] { 2f, 3f }) });
        Assert.That(Analyze().findings.Where(item => item.category == "pacing" &&
            item.severity == GmAuditSeverity.Error), Is.Empty);
        GmPacingSimulationReport simulation = GmPacingSimulator.Simulate("fixture",
            validOwner.GetComponent<GmPacingIntent>());
        Assert.That(simulation.rows.Length, Is.EqualTo(3));
        Assert.That(simulation.rows.All(row => row.routeCompletesBeforeBlackout), Is.True);
    }

    GmSceneAuditReport Analyze()
    {
        var report = new GmSceneAuditReport { sceneId = "perceptual-test", fingerprint = "test" };
        GmPerceptualAudit.Analyze(scene, tour, camera, report);
        return report;
    }

    static GmCompositionElement MarkCube(string id, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = id;
        go.transform.position = position;
        go.transform.localScale = scale;
        return GmCompositionAuthoring.Element(go, id, "fixture", "fixture-family",
            "A deliberate test fixture element with stable authored meaning.", GmCompositionRole.Detail,
            GmSpatialRelation.Grounded, surfaceY: position.y - scale.y * 0.5f,
            groundTolerance: 0.1f, blocksRoutes: false);
    }
}
