using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public class GmParlorBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmParlorBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmParlorQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Parlor scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void HearthAndLampAreRuntimeAtmosphereNotJustCompositionCopy()
    {
        Assert.That(GameObject.Find("BankerLampLight")?.GetComponent<GmLightFlicker>(), Is.Not.Null,
            "the table lamp is described as flame-fed but has no runtime flicker");
        Assert.That(GameObject.Find("FireplaceLight")?.GetComponent<GmLightFlicker>(), Is.Not.Null,
            "the dying hearth has no runtime flame movement");
        Assert.That(GameObject.Find("EntrySconceLight")?.GetComponent<GmLightFlicker>(), Is.Null,
            "entry/navigation light must remain stable");
    }

    [Test]
    public void ShippingTableHasOneBoundPhysicalViewForEveryCanonicalCard()
    {
        GmParlorCardView[] cards = Object.FindObjectsByType<GmParlorCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(cards, Has.Length.EqualTo(GmParlorCore.TotalCards));
        Assert.That(cards.Select(card => card.PhysicalCard).Distinct().Count(),
            Is.EqualTo(GmParlorCore.TotalCards));
        Assert.That(Object.FindAnyObjectByType<GmParlorPropBinder>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmParlorPresentationCoordinator>(), Is.Not.Null);
        GmParlorAldricPresenter presenter = Object.FindAnyObjectByType<GmParlorAldricPresenter>();
        Assert.That(presenter, Is.Not.Null, "semantic Aldric presenter is missing");
        Assert.That(presenter.IsConfigured, Is.True, "semantic Aldric presenter is not bound");
        Assert.That(presenter.UsesSubstituteProxy, Is.True,
            "the current authored hand proxy must not be mislabeled as final character art");
        Assert.That(presenter.HasCharacterMotionChannel, Is.False,
            "a disconnected rehearsal glove must stay hidden until the full Aldric rig is imported");
        Assert.That(Object.FindAnyObjectByType<GmParlorEvidenceLog>(), Is.Not.Null,
            "observed-fact evidence log is missing");
        Assert.That(Object.FindAnyObjectByType<GmParlorFocusView>(), Is.Not.Null,
            "equivalent focus view is missing");
        Assert.That(Object.FindAnyObjectByType<GmParlorHud>(), Is.Not.Null,
            "Parlor HUD is missing");
        Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Any(component => component != null &&
                component.GetType().Name == "GmTheReadController"), Is.False,
            "legacy Read authority applies run deltas outside GmParlorRules");
        Assert.That(Object.FindAnyObjectByType<GmHostAI>(), Is.Null,
            "shipping Aldric choices come from GmParlorMatch, not the legacy component");
        Assert.That(cards.SelectMany(card => card.GetComponentsInChildren<Transform>(true))
            .Count(child => child.name.StartsWith("Rank_")), Is.EqualTo(GmParlorCore.TotalCards * 2),
            "every physical face needs two readable inset rank marks");
        Assert.That(cards.SelectMany(card => card.GetComponentsInChildren<Transform>(true))
            .Count(child => child.name.StartsWith("Suit_")), Is.EqualTo(GmParlorCore.TotalCards),
            "every physical face needs one modeled suit emblem");

        Bounds physicalBounds = CombinedBounds(FindRoot("PhysicalCards"));
        Assert.That(physicalBounds.size.x, Is.LessThanOrEqualTo(1.10f),
            $"physical card presentation is too wide for the baize: {physicalBounds.size}");
        Assert.That(physicalBounds.size.y, Is.LessThanOrEqualTo(0.20f),
            $"card glyph or shell escapes vertically: {physicalBounds.size}");
        Assert.That(physicalBounds.size.z, Is.LessThanOrEqualTo(0.80f),
            $"physical card presentation is too deep for the baize: {physicalBounds.size}");
    }

    [Test]
    public void SavedParlorPlayerContainsOneReachablePauseSettingsMenu()
    {
        GmPlayer player = Object.FindAnyObjectByType<GmPlayer>(FindObjectsInactive.Include);
        Assert.That(player, Is.Not.Null, "saved Parlor has no player");
        GmPauseMenu[] menus = Object.FindObjectsByType<GmPauseMenu>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(menus, Has.Length.EqualTo(1),
            "saved Parlor does not contain exactly one shipping pause menu");
        Assert.That(menus[0].GetComponent<GmPlayer>(), Is.SameAs(player));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmParlorShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void ReviewTourUsesBackbufferAndNamesTheCompletePublicStateSequenceHonestly()
    {
        GmParlorShotTour tour = Object.FindAnyObjectByType<GmParlorShotTour>();
        string[] names = tour.ShotsForAudit.Select(shot => shot.Name).ToArray();

        Assert.That(tour.UsesBackbufferCaptureForAudit, Is.True,
            "UI Toolkit evidence cannot come from Camera.Render into a RenderTexture");
        Assert.That(names, Is.EqualTo(new[]
        {
            "01-player-hand-ready", "02-empty-host-chair-framing",
            "03-player-lead-and-aldric-follow", "04-aldric-lead-player-follow",
            "05-true-suspicious-contact", "06-false-suspicious-contact",
            "07-focus-true-observed-facts", "08-focus-false-observed-facts",
            "09-correct-read-result", "10-false-read-result", "11-missed-cheat-result",
            "12-locked-read-feedback", "13-late-read-feedback", "14-trick-result",
            "15-round-result", "16-player-match-win", "17-aldric-match-win",
            "18-rematch-ready", "19-pause-journal", "20-settings-default",
            "21-settings-high-contrast-200",
            "22-restore-before", "23-restore-after", "24-restore-focus",
        }));
        Assert.That(names.Any(name => name.Contains("hands") || name.Contains("portrait")), Is.False,
            "the current scene has an empty Aldric chair and a disabled proxy, not host hands");
    }

    [Test]
    public void SavedParlorContainsOneConfiguredButUnarmedReviewProbe()
    {
        GmParlorReviewProbe[] probes = Object.FindObjectsByType<GmParlorReviewProbe>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(probes, Has.Length.EqualTo(1));
        Assert.That(probes[0].IsConfigured, Is.True);
        Assert.That(probes[0].IsArmed, Is.False,
            "ordinary Parlor launches must never run review automation");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmParlorBuilder.SceneId, Object.FindAnyObjectByType<GmParlorShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Parlor composition failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void MajorRoomPropsUseOwnedVictorianMeshes()
    {
        string[] propNames =
        {
            "CardTable", "PlayerChair", "AldricChair", "StoneMantel", "BarCabinet",
        };

        foreach (string propName in propNames)
        {
            Transform prop = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.name == propName);
            Assert.IsNotNull(prop, $"{propName} logical prop root is missing");
            Assert.IsTrue(prop.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                    GmVictorianInteriorKit.IsImportedVisual(renderer) ||
                    renderer.transform.GetComponentsInParent<Transform>(true)
                        .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
                $"{propName} is still primitive-only; place an owned mesh under the logical prop root");
        }
    }

    [Test]
    public void PerimeterDressingCreatesACompleteVictorianRoomInsteadOfACentralPropIsland()
    {
        string[] dressing =
        {
            "NorthBookcaseWest", "NorthBookcaseEast", "NorthSettee",
            "HearthChair", "ReadingSideTable", "WestMirror",
        };

        foreach (string objectName in dressing)
        {
            Transform root = FindRoot(objectName);
            Assert.IsTrue(root.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                    GmVictorianInteriorKit.IsImportedVisual(renderer) ||
                    renderer.transform.GetComponentsInParent<Transform>(true)
                        .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
                $"{objectName} is not backed by an owned period mesh");
        }
    }

    [Test]
    public void CabinetDressingRestsOnTheMeasuredCabinetTop()
    {
        Transform cabinet = FindRoot("BarCabinet");
        Transform decanter = FindRoot("CrystalDecanter");
        Transform letters = FindRoot("SealedLetters");
        Bounds cabinetBounds = CombinedBounds(cabinet);

        Assert.That(Mathf.Abs(CombinedBounds(decanter).min.y - cabinetBounds.max.y), Is.LessThan(0.05f),
            "decanter floats above or tunnels into the cabinet top");
        Assert.That(Mathf.Abs(CombinedBounds(letters).min.y - cabinetBounds.max.y), Is.LessThan(0.05f),
            "sealed letters float above or tunnel into the cabinet top");
    }

    [Test]
    public void MoonWindowsProvideCoolSeparationBeyondTheOilLampCeiling()
    {
        Light[] shafts = Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(light => light.gameObject.scene.IsValid() &&
                light.name.StartsWith("MoonWindowLight_North"))
            .ToArray();

        Assert.That(shafts, Has.Length.EqualTo(2),
            "the parlor needs paired visible moon windows rather than more orange fill");
        foreach (Light shaft in shafts)
        {
            Assert.That(shaft.type, Is.EqualTo(LightType.Spot));
            Assert.That(shaft.color.b, Is.GreaterThan(shaft.color.r * 1.35f));
            Assert.That(shaft.intensity, Is.GreaterThan(GmInteriorAtmosphere.PracticalCeilingLumens));
            Assert.That(shaft.GetComponent<GmLightIntent>()?.Kind, Is.EqualTo(GmLightIntentKind.Environmental));
        }
        Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Count(renderer => renderer.gameObject.scene.IsValid() &&
                renderer.GetComponentsInParent<Transform>(true)
                    .Any(parent => parent.name.StartsWith("NorthMoonWindow"))),
            Is.GreaterThanOrEqualTo(6), "the cool shafts have no modeled window frames or glass");
    }

    [Test]
    public void CourtDoorAxisHasAModeledMoonlitTransomAimedBackIntoTheRoom()
    {
        Light transom = Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .SingleOrDefault(light => light.gameObject.scene.IsValid() &&
                light.name == "MoonWindowLight_SouthCourtTransom");

        Assert.IsNotNull(transom, "the south exit remains an isolated orange pool with no cool source");
        Assert.That(transom.transform.forward.z, Is.GreaterThan(0.45f),
            "the south transom shines out through the wall instead of back into the Parlor");
        Assert.That(transom.intensity, Is.GreaterThan(GmInteriorAtmosphere.PracticalCeilingLumens));
        Assert.That(transom.GetComponent<GmLightIntent>()?.Kind, Is.EqualTo(GmLightIntentKind.Environmental));
        Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Count(renderer => renderer.gameObject.scene.IsValid() &&
                renderer.GetComponentsInParent<Transform>(true)
                    .Any(parent => parent.name == "SouthCourtTransom")),
            Is.GreaterThanOrEqualTo(3), "the south shaft is another floating light with no transom geometry");
    }

    [Test]
    public void PlayerFacingPropsDoNotUseBuiltInPrimitiveMeshes()
    {
        string[] architecturalShell =
        {
            "ParlorFloor", "NorthWall", "SouthWall", "SouthWall_CourtBoundary", "EastWall_Fireplace", "WestWall", "Ceiling",
        };

        string[] primitivePaths = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(filter => filter.gameObject.scene.IsValid() && filter.GetComponent<Renderer>() != null)
            .Where(filter => AssetDatabase.GetAssetPath(filter.sharedMesh) == "Library/unity default resources")
            .Where(filter => !filter.GetComponentsInParent<Transform>(true)
                .Any(parent => architecturalShell.Contains(parent.name)))
            .Select(filter => HierarchyPath(filter.transform))
            .OrderBy(path => path)
            .ToArray();

        Assert.IsEmpty(primitivePaths,
            "Parlor still renders player-facing Unity primitives:\n- " + string.Join("\n- ", primitivePaths));
    }

    [Test]
    public void CompletingTheMatchOpensARealExitToCourtThroughTheSouthWall()
    {
        var exit = Object.FindAnyObjectByType<GmSequenceExit>(FindObjectsInactive.Include);
        Assert.IsNotNull(exit, "Parlor can finish a match but has no way onward");
        var trigger = exit.GetComponentInChildren<GmSceneTransitionTrigger>(true);
        Assert.IsNotNull(trigger);
        Assert.AreEqual(GmCourtBuilder.SceneId, trigger.TargetSceneId);
        Assert.AreEqual(GmCourtBuilder.ScenePath, trigger.TargetScenePath);

        // Fresh run: the physical leaves hold and the trigger cannot steal an unfinished game.
        var triggerCollider = trigger.GetComponent<Collider>();
        Assert.IsFalse(triggerCollider.enabled);

        GmRunStore.CompleteRoom(GmParlorBuilder.SceneId, countsAsTableGame: true);
        Physics.SyncTransforms();
        Assert.IsTrue(exit.IsUnlocked);
        Assert.IsTrue(triggerCollider.enabled);

        // Ignore the trigger itself and prove the wall was actually cut. Decorative doors mounted
        // on a solid wall already shipped once in the Entry Hall; an open animation is not an exit.
        bool sealedByWall = Physics.Raycast(new Vector3(0f, 1.1f, -3.1f), Vector3.back,
            out _, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Assert.IsFalse(sealedByWall, "the open Court doors are still mounted on a solid south wall");
    }

    static string HierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

    static Transform FindRoot(string name)
    {
        Transform result = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.name == name);
        Assert.IsNotNull(result, $"{name} logical prop root is missing");
        return result;
    }

    static Bounds CombinedBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers, $"{root.name} has no renderers");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
