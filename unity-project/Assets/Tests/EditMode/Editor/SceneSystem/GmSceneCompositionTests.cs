using System;
using System.Collections.Generic;
using UnityEditor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GmSceneCompositionTests
{
    const string SceneId = "composition-test";
    GmSceneComposition manifest;
    GmCompositionElement anchor;
    GmCompositionElement detail;

    [SetUp]
    public void BuildValidAuthoredFixture()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var composition = new GameObject("Composition");
        manifest = GmCompositionAuthoring.Begin(composition, SceneId,
            "A clear foreground route leads past human traces to one dominant trial landmark.",
            minZones: 1, minClusters: 1, minElements: 3,
            requireEveryShot: false, requireMotivatedLights: false);

        var zone = new GameObject("ArrivalZone");
        GmCompositionAuthoring.Zone(zone, "arrival", "Frames the first choice without filling its walking lane.",
            new Vector3(20f, 6f, 20f), minClusters: 1, minElements: 3);

        var cluster = new GameObject("ThresholdCluster");
        GmCompositionAuthoring.Cluster(cluster, "threshold", "arrival",
            "One dominant destination, one route cue and one trace of prior use.", "door",
            minSupports: 1, minDetails: 1, maxMembers: 6, maxRadius: 9f,
            requireVariation: false);

        anchor = MarkCube("Door", new Vector3(0f, 1f, 4f), new Vector3(2f, 2f, 0.3f),
            "door", "threshold", "architecture", "The only dominant destination on the route.",
            GmCompositionRole.Anchor);
        MarkCube("RouteStone", new Vector3(-2f, 0.25f, 1.5f), new Vector3(0.5f, 0.5f, 0.5f),
            "route-stone", "threshold", "stone", "Breaks the edge and points toward the threshold.",
            GmCompositionRole.RouteCue);
        detail = MarkCube("LostCase", new Vector3(2f, 0.3f, 2f), new Vector3(0.8f, 0.6f, 0.5f),
            "lost-case", "threshold", "luggage", "A human trace that explains why the space feels abandoned.",
            GmCompositionRole.Detail);
    }

    [TearDown]
    public void ClearFixture()
    {
        if (SceneManager.GetActiveScene().IsValid())
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void DeliberateMiniatureCompositionPasses()
    {
        Assert.IsEmpty(Validate());
    }

    [Test]
    public void NegativeSpacePathCanBeACompositionElementWithoutPaintedGeometry()
    {
        var pathObject = new GameObject("ClearedPath");
        pathObject.transform.position = new Vector3(0f, 0f, 2f);
        pathObject.AddComponent<GmClearedPathGuide>().Configure(5f, 1.2f, false);
        GmCompositionAuthoring.Element(pathObject, "cleared-path", "threshold", "negative-space-path",
            "The open lane is legible because the surrounding composition deliberately avoids it.",
            GmCompositionRole.Gameplay, GmSpatialRelation.Grounded, surfaceY: 0f, blocksRoutes: false);
        Assert.IsEmpty(Validate());
    }

    [Test]
    public void OrphanedElementIsRejected()
    {
        detail.Configure("lost-case", "missing-cluster", "luggage",
            "A human trace that explains why the space feels abandoned.", GmCompositionRole.Detail,
            GmSpatialRelation.Grounded, surfaceY: 0f);
        Assert.That(Validate(), Has.Some.Contains("missing cluster 'missing-cluster'"));
    }

    [Test]
    public void OccupiedNegativeSpaceIsRejected()
    {
        var reserved = new GameObject("DoorReadClear");
        reserved.transform.position = anchor.transform.position;
        GmCompositionAuthoring.Reserve(reserved, "door-read", "Keeps the destination silhouette readable from the entrance.",
            new Vector3(2.5f, 3f, 2f));
        Assert.That(Validate(), Has.Some.Contains("negative space 'door-read' is occupied"));
    }

    [Test]
    public void BlockedAuthoredRouteIsRejected()
    {
        var route = new GameObject("MainRoute");
        GmCompositionAuthoring.Route(route, "main-route", "Preserves a clean walk from entrance to the dominant door.",
            new[] { Vector3.zero, new Vector3(0f, 0f, 6f) }, 0.65f);
        Assert.That(Validate(), Has.Some.Contains("route 'main-route' is obstructed"));
    }

    [Test]
    public void UnmotivatedLocalLightIsRejected()
    {
        manifest.Configure(SceneId,
            "A clear foreground route leads past human traces to one dominant trial landmark.",
            minZones: 1, minClusters: 1, minElements: 3,
            requireEveryShot: false, requireMotivatedLights: true);
        var light = new GameObject("InvisibleFill").AddComponent<Light>();
        light.type = LightType.Point;
        Assert.That(Validate(), Has.Some.Contains("has no GmMotivatedLight"));
    }

    [Test]
    public void ExactOverlappingElementTransformsAreRejected()
    {
        var duplicate = MarkCube("DuplicateCase", detail.transform.position, detail.transform.localScale,
            "duplicate-case", "threshold", "luggage", "This deliberately duplicates a transform for the regression test.",
            GmCompositionRole.Detail);
        duplicate.transform.rotation = detail.transform.rotation;
        Assert.That(Validate(), Has.Some.Contains("overlapping duplicate placement suspected"));
    }

    [Test]
    public void ZoneAndClusterCentersSupportAuthoredLocalOffsets()
    {
        var offsetZone = new GameObject("OffsetZone");
        GmCompositionZone zone = GmCompositionAuthoring.Zone(offsetZone, "offset-zone",
            "Places a logical volume away from its convenient hierarchy owner.",
            new Vector3(2f, 2f, 2f), centerOffset: new Vector3(5f, 0f, 0f));
        Assert.That(zone.Contains(new Vector3(5f, 0f, 0f)), Is.True);
        Assert.That(zone.Contains(Vector3.zero), Is.False);

        // The cluster half of the same idea: its centre is an authored local offset from whatever
        // transform happens to own it, so a rotated or moved owner must carry the centre with it.
        var offsetCluster = new GameObject("OffsetClusterOwner");
        offsetCluster.transform.position = new Vector3(1f, 0f, -2f);
        offsetCluster.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        GmCompositionCluster cluster = GmCompositionAuthoring.Cluster(offsetCluster, "offset-cluster",
            "offset-zone", "Sits its radius over the anchor rather than over its hierarchy owner.",
            "offset-anchor", centerOffset: new Vector3(4f, 0f, 0f));
        Assert.That(cluster.LocalCenterOffset, Is.EqualTo(new Vector3(4f, 0f, 0f)));
        // Yaw 90 turns local +x into world -z.
        Assert.That(cluster.WorldCenter.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(cluster.WorldCenter.z, Is.EqualTo(-6f).Within(0.001f));
    }

    [Test]
    public void ClusterRadiusIsMeasuredFromItsOffsetCentreNotItsOwnerTransform()
    {
        var clusterObject = new GameObject("OffsetCluster");
        GmCompositionAuthoring.Cluster(clusterObject, "offset-cluster", "arrival",
            "Groups the far bench and its traces around a centre away from the scene origin.",
            "offset-anchor", minSupports: 1, minDetails: 1, maxMembers: 6, maxRadius: 2f,
            requireVariation: false, centerOffset: new Vector3(6f, 0f, 0f));

        MarkCube("OffsetBench", new Vector3(6f, 0.9f, 0f), new Vector3(1.6f, 1.8f, 0.4f),
            "offset-anchor", "offset-cluster", "furniture", "The dominant destination of the far group.",
            GmCompositionRole.Anchor);
        MarkCube("OffsetKerb", new Vector3(5.2f, 0.2f, 0.6f), new Vector3(0.4f, 0.4f, 0.4f),
            "offset-kerb", "offset-cluster", "stone", "Turns the walking line toward the bench.",
            GmCompositionRole.RouteCue);
        GmCompositionElement stray = MarkCube("OffsetCup", new Vector3(6.7f, 0.3f, -0.5f),
            new Vector3(0.6f, 0.6f, 0.6f), "offset-cup", "offset-cluster", "crockery",
            "A human trace left where somebody sat.", GmCompositionRole.Detail);

        List<string> gathered = Validate();
        Assert.That(gathered, Is.Empty,
            "members gathered around the authored centre were rejected:\n" + string.Join("\n", gathered));

        // Same cluster, same radius: this places the trace within 2m of the owner's origin but far
        // outside the authored centre. Only an audit that ignores the offset would accept it.
        stray.transform.position = new Vector3(0.5f, 0.3f, 0.5f);
        Assert.That(Validate(),
            Has.Some.Contains("element 'offset-cup' lies outside cluster 'offset-cluster' radius"));
    }

    [Test]
    public void CompositionFillMustNameABoundedSubject()
    {
        var lightObject = new GameObject("CompositionFill");
        lightObject.AddComponent<Light>().type = LightType.Point;
        GmAdaptiveIntentAuthoring.Light(lightObject, "fill-1", GmLightIntentKind.CompositionFill,
            "Separates the threshold silhouette without pretending to be a practical source.",
            subjectElementId: "missing-subject", maximumSubjectDistance: 8f);
        Assert.That(Validate(), Has.Some.Contains("references missing subject 'missing-subject'"));
    }

    [Test]
    public void AdaptiveSlotsCannotOwnProtectedRoles()
    {
        var slotObject = new GameObject("ForbiddenSlot");
        GmAdaptiveIntentAuthoring.Slot(slotObject, "forbidden-anchor", GmCompositionRole.Anchor,
            "arrival", "threshold", "Deliberately invalid protected-anchor variation for the audit test.",
            new[] { "architecture" }, new[] { "door" }, Vector3.one * 0.1f, 5f,
            new Vector2(0.9f, 1.1f), 0.1f, 0.05f, 7,
            new[] { new GmAdaptiveCandidate("a", "test-guid", "architecture", Vector3.zero, 0f, Vector3.one) });
        Assert.That(Validate(), Has.Some.Contains("uses forbidden role Anchor"));
    }

    [Test]
    public void VariantExceptionRestoresExactSceneAndDoesNotSaveMutation()
    {
        const string folder = "Assets/Tests/GmSceneIntelligenceTemp";
        const string prefabPath = folder + "/Candidate.prefab";
        const string scenePath = folder + "/VariantRestore.unity";
        try
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Tests")) AssetDatabase.CreateFolder("Assets", "Tests");
                AssetDatabase.CreateFolder("Assets/Tests", "GmSceneIntelligenceTemp");
            }
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var slotObject = new GameObject("DetailSlot");
            GmAdaptiveIntentAuthoring.Slot(slotObject, "detail-slot", GmCompositionRole.Detail,
                "arrival", "threshold", "Tests reversible candidate rendering on one explicitly swappable detail.",
                new[] { "luggage" }, new[] { "lost-case" }, new Vector3(0.2f, 0.2f, 0.2f), 10f,
                new Vector2(0.9f, 1.1f), 0.1f, 0.05f, 17,
                new[] { new GmAdaptiveCandidate("candidate-a", guid, "luggage", Vector3.zero, 0f, Vector3.one) });
            new GameObject("ReviewCamera").AddComponent<Camera>().tag = "MainCamera";
            Assert.That(Validate(), Is.Empty);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath), Is.True);
            string before = GmSceneFingerprint.Current();
            Assert.Throws<InvalidOperationException>(() =>
                GmGuardedVariantPreview.GenerateAll(() => throw new InvalidOperationException("injected failure")));
            Assert.That(GmSceneFingerprint.Current(), Is.EqualTo(before));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test]
    public void NickReviewPreviewJsonUsesDocumentedUnityEmptyFieldShape()
    {
        var document = new GmReviewSessionDocument {
            sessionId = "preview-only", sceneId = SceneId,
            createdAt = "2026-07-19T12:00:00.000Z",
            reviewer = new GmReviewerDocument { kind = "nick", id = "nick", displayName = "Nick" },
            evidence = new GmEvidenceDocument { buildFingerprint = "test-fingerprint" },
            verdicts = new[] {
                new GmVerdictDocument {
                    id = "preview-verdict", category = "visual", verdict = "keep",
                    context = new GmReviewContextDocument { zoneId = "arrival" },
                    tags = Array.Empty<string>(),
                },
            },
        };
        string json = JsonUtility.ToJson(document);
        Assert.That(json, Does.Contain("\"zoneId\":\"arrival\""));
        // JsonUtility emits optional null strings as empty strings. The Node ingestion boundary
        // strips only these known optional fields before schema validation and canonical storage.
        Assert.That(json, Does.Contain("\"shotName\":\"\""));
        Assert.That(json, Does.Contain("\"assetGuid\":\"\""));
    }

    static GmCompositionElement MarkCube(string name, Vector3 position, Vector3 scale,
        string id, string cluster, string family, string rationale, GmCompositionRole role)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        return GmCompositionAuthoring.Element(go, id, cluster, family, rationale, role,
            GmSpatialRelation.Grounded, surfaceY: 0f, groundTolerance: 0.02f);
    }

    // -------------------------------------------------------------------------------------------
    // The UI-only exit. It shipped with no tests at all, and the commit that introduced it claimed
    // "a room can never quietly acquire this and stop being checked" -- which was not true as
    // written: the self-refutation counted composition MARKERS, and a scene that quietly grows
    // geometry grows it without authoring a single zone. These four are the cases that claim needs.
    // -------------------------------------------------------------------------------------------

    /// Rebuilds the scene as a bare screen-space menu: no zones, no clusters, no elements.
    void BeginScreenSpaceOnlyScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var composition = new GameObject("Composition");
        GmCompositionAuthoring.Begin(composition, SceneId,
            "A title screen. Everything the player sees is drawn on the glass, not standing in a room.",
            minZones: 0, minClusters: 0, minElements: 0,
            requireEveryShot: false, requireMotivatedLights: false, uiOnly: true);
    }

    [Test]
    public void AScreenSpaceOnlySceneWithNoGeometryPasses()
    {
        // Without this direction the exit could be satisfied by a check that always fails, and the
        // boot scene -- the only real user -- would be unshippable.
        BeginScreenSpaceOnlyScene();
        Assert.IsEmpty(Validate());
    }

    [Test]
    public void AScreenSpaceOnlySceneThatGrowsAPropIsCaughtEvenWithNoMarkersOnIt()
    {
        // The real failure mode, and the one the marker count could not see. Nobody adds a zone to a
        // prop they are quietly dropping into a menu scene.
        BeginScreenSpaceOnlyScene();
        GameObject.CreatePrimitive(PrimitiveType.Cube).name = "SomeoneAddedARoom";

        List<string> issues = Validate();
        CollectionAssert.IsNotEmpty(issues, "a prop appeared in a scene exempt from every geometry " +
            "check and nothing objected");
        StringAssert.Contains("world-space renderer", string.Join("\n", issues));
    }

    [Test]
    public void AScreenSpaceOnlySceneThatGrowsALightIsCaught()
    {
        // Lights are the other half. A menu needs none; a room always has some, and light motivation
        // is one of the checks the exit turns off.
        BeginScreenSpaceOnlyScene();
        new GameObject("SomeoneLitTheRoom").AddComponent<Light>();

        List<string> issues = Validate();
        CollectionAssert.IsNotEmpty(issues, "a light appeared in a scene exempt from light motivation " +
            "checks and nothing objected");
        StringAssert.Contains("light(s)", string.Join("\n", issues));
    }

    [Test]
    public void AScreenSpaceCanvasIsNotMistakenForARoom()
    {
        // The false positive that would make the check useless: a menu's own text and images are
        // Renderers too. Only ones outside a screen-space Canvas are props.
        BeginScreenSpaceOnlyScene();
        var canvasObject = new GameObject("MenuCanvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
        label.name = "TitleArt";
        label.transform.SetParent(canvasObject.transform, false);

        Assert.IsEmpty(Validate(), "a renderer under a screen-space canvas is the menu, not a room");
    }

    static List<string> Validate()
    {
        return GmSceneCompositionAudit.ValidateOpenScene(SceneId);
    }
}
