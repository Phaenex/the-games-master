// Generated minimum gates for Entry Hall. Add tests for every regression found.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public class GmEntryHallBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce()
    {
        GmRunStore.BeginNewRun();
        GmEntryHallBuilder.Build();
    }

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [SetUp]
    public void IsolateEachTest()
    {
        GmRunStore.BeginNewRun();
        Door("LibraryDoor")?.RelockClosed();
        Door("CellarPanel")?.RelockClosed();
        Door("MarrDoor")?.RelockClosed();
        Door("AtticHatch")?.RelockClosed();
        var shelf = Object.FindAnyObjectByType<GmWeightedShelf>(FindObjectsInactive.Include);
        shelf?.ResetPuzzle();
        Physics.SyncTransforms();
    }

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmEntryHallQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Entry Hall scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmEntryHallShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void ReviewTourIncludesTheLibraryInterior()
    {
        var tour = Object.FindAnyObjectByType<GmEntryHallShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.AreEqual(23, tour.ShotCount, "registry and tour both need the cellar descent and vault");
        string[] names = tour.ShotsForAudit.Select(shot => shot.Name).ToArray();
        CollectionAssert.Contains(names, "13-library-interior");
        CollectionAssert.Contains(names, "14-weighted-shelf");
        CollectionAssert.Contains(names, "15-upper-gallery");
        CollectionAssert.Contains(names, "16-marr-study");
        CollectionAssert.Contains(names, "17-barred-guest");
        CollectionAssert.Contains(names, "18-attic-hatch");
        CollectionAssert.Contains(names, "19-attic-loft");
        CollectionAssert.Contains(names, "20-attic-shard");
        CollectionAssert.Contains(names, "21-cellar-panel");
        CollectionAssert.Contains(names, "22-cellar-descent");
        CollectionAssert.Contains(names, "23-cellar-vault");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmEntryHallBuilder.SceneId, Object.FindAnyObjectByType<GmEntryHallShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Entry Hall composition failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void TheHallOwnsTheSecondHalfOfTheNinthBellCrossing()
    {
        // This is the room the ninth bell delivers you to. GmCrossing raises the curtain and starts
        // the load, then dies with its scene -- so if this component is not HERE, the player arrives
        // inside the house behind a black that nothing will ever open. Control never returns and no
        // error is logged, because from every component's point of view it did its job.
        //
        // The same defect class as the six rooms that had no player and the transition trigger that
        // was in no scene: real, unit-tested, and connected to nothing.
        var arrival = Object.FindAnyObjectByType<GmSceneArrival>(FindObjectsInactive.Include);
        Assert.IsNotNull(arrival,
            "EntryHall has no GmSceneArrival — the ninth bell would strand the player behind black");
    }

    [Test]
    public void TheHallHasAWayToTheTable()
    {
        // Phase A's last missing link. Every scene was in the build and every room had a player, and
        // there was still no way from this room into a single game -- GmSceneTransitionTrigger existed,
        // was unit-tested, and was placed in ZERO scenes.
        var trigger = GameObject.Find("ParlorTransition")?.GetComponent<GmSceneTransitionTrigger>();
        Assert.IsNotNull(trigger, "the Entry Hall has no exit — the player wakes in the house and stays there");

        Assert.AreEqual(GmParlorBuilder.SceneId, trigger.TargetSceneId,
            "the hall's exit does not lead to the first game");
        // Compared against the builder's own constant rather than a literal, so renaming the scene
        // cannot leave a path string here that loads nothing.
        Assert.AreEqual(GmParlorBuilder.ScenePath, trigger.TargetScenePath,
            "the hall's exit names a scene path the Parlor builder does not write");

        var box = trigger.GetComponent<Collider>();
        Assert.IsNotNull(box, "the transition has no collider, so nothing can enter it");
        Assert.IsTrue(box.isTrigger, "the transition volume is SOLID — it would block the doorway it is in");
    }

    [Test]
    public void TheDoorwayVolumeIsInsideTheOpeningAndNotAcrossTheRoom()
    {
        // A transition volume big enough to catch someone crossing the hall would take the player out
        // of the room on the way to the staircase, which reads as the game hijacking a walk.
        var trigger = GameObject.Find("ParlorTransition")?.GetComponent<GmSceneTransitionTrigger>();
        Assert.IsNotNull(trigger);
        Bounds bounds = trigger.GetComponent<Collider>().bounds;

        Assert.Less(bounds.size.x, 1.5f, "the exit volume reaches out into the hall");
        Assert.Greater(bounds.center.x, 4.5f, "the exit volume is not against the east wall");
        Assert.Less(bounds.min.y, 1f, "the volume floats above the floor — a walking player passes under it");
    }

    [Test]
    public void ParlorDoorLeavesSwingFromOpposingOuterHinges()
    {
        Transform south = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "ParlorDoorLeafSouth");
        Transform north = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "ParlorDoorLeafNorth");

        Assert.IsNotNull(south, "south parlor door leaf is missing");
        Assert.IsNotNull(north, "north parlor door leaf is missing");
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, south.eulerAngles.y)), Is.GreaterThan(50f),
            "south leaf is barely open and reads as a wall-sized slab");
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, north.eulerAngles.y)), Is.GreaterThan(50f),
            "north leaf is barely open and reads as a wall-sized slab");
        Assert.That(south.position.x, Is.EqualTo(north.position.x).Within(0.05f),
            "door leaves do not swing the same distance into the hall");
        Assert.That((south.position.z + north.position.z) * 0.5f, Is.EqualTo(6f).Within(0.05f),
            "door leaves are not mirrored around the opening centre");
        Assert.That(north.position.z - south.position.z, Is.LessThan(2f),
            "door leaves were translated away from the hinges instead of swung around them");
    }

    [Test]
    public void ParlorDoorwayCutsARealOpeningThroughTheEastWall()
    {
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(5f, 1.2f, 6f), Vector3.right, 2.5f);
        string[] blockingWallHits = hits
            .Where(hit => hit.transform.GetComponentsInParent<Transform>(true)
                .Any(parent => parent.name == "RightWall"))
            .Select(hit => HierarchyPath(hit.transform))
            .ToArray();

        Assert.IsEmpty(blockingWallHits,
            "the open parlor doors are mounted over a solid east wall:\n- "
            + string.Join("\n- ", blockingWallHits));
    }

    [Test]
    public void ParlorDoorwayHasInteriorDepthBeyondTheThreshold()
    {
        Transform backing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "ParlorThresholdBacking");

        Assert.IsNotNull(backing,
            "the open doorway exposes HDRP's exterior sky instead of a dark interior threshold");
        Assert.Greater(backing.position.x, 7.5f,
            "threshold backing is flush with the doors and reads as another blocked wall");
        Assert.IsNotNull(backing.GetComponent<Renderer>(),
            "threshold backing exists only as an empty marker");
    }

    [Test]
    public void MajorRoomPropsUseOwnedVictorianMeshes()
    {
        string[] propNames =
        {
            "ConsoleTable", "GrandStaircase", "WakeSettee", "WakeTable", "LibraryReadingTable",
            "LibraryStubShelf",
        };

        foreach (string propName in propNames)
        {
            Transform prop = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.name == propName);
            Assert.IsNotNull(prop, $"{propName} logical prop root is missing");
            Assert.IsTrue(prop.GetComponentsInChildren<Renderer>(true).Any(GmVictorianInteriorKit.IsImportedVisual),
                $"{propName} is still primitive-only; place an owned mesh under the logical prop root");
        }
    }

    [Test]
    public void PerimeterDressingBreaksUpTheEmptyHallWithOwnedPeriodMeshes()
    {
        string[] dressing =
        {
            "EastConsole", "EastHallMirror", "GalleryChairSouth", "GalleryChairNorth",
            "LandingClock", "StairSideTable",
        };

        foreach (string objectName in dressing)
        {
            Transform root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.name == objectName);
            Assert.IsNotNull(root, $"Entry Hall perimeter dressing is missing {objectName}");
            Assert.IsTrue(root.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                    GmVictorianInteriorKit.IsImportedVisual(renderer) ||
                    renderer.transform.GetComponentsInParent<Transform>(true)
                        .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
                $"{objectName} is not backed by an owned period mesh");
        }
    }

    [Test]
    public void LandingClockUsesItsRealBoundsToStandUpright()
    {
        Transform clock = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .First(candidate => candidate.gameObject.scene.IsValid() && candidate.name == "LandingClock");
        Renderer[] renderers = clock.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        Assert.That(bounds.size.y, Is.GreaterThan(Mathf.Max(bounds.size.x, bounds.size.z) * 1.35f),
            "the imported grandfather clock is lying on its side beside the stair");
    }

    [Test]
    public void LandingClockIsHdrpWoodNotAWhiteUntexturedCase()
    {
        Transform clock = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .First(candidate => candidate.gameObject.scene.IsValid() && candidate.name == "LandingClock");
        Renderer[] renderers = clock.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers);
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.IsNotNull(material, "LandingClock has a missing material slot");
                Assert.That(material.shader.name, Does.Contain("HDRP").IgnoreCase,
                    $"LandingClock shader '{material.shader.name}' will render white in HDRP");
                Assert.IsTrue(material.HasProperty("_BaseColor"),
                    "LandingClock HDRP material has no _BaseColor");
                Color color = material.GetColor("_BaseColor");
                Assert.That(Mathf.Max(color.r, color.g, color.b), Is.LessThan(0.35f),
                    $"LandingClock albedo is still blown white ({color})");
            }
        }
        Assert.IsNotNull(clock.GetComponent<Collider>(),
            "LandingClock has no collider — a body walks through the case");
    }

    [Test]
    public void PercivalsBedIsDarkWoodNotDefaultWhite()
    {
        Transform bed = GameObject.Find("PercivalBed").transform;
        foreach (Renderer renderer in bed.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.IsNotNull(material, "PercivalBed has a missing material slot");
                Assert.That(material.shader.name, Does.Contain("HDRP").IgnoreCase,
                    $"PercivalBed shader '{material.shader.name}' will render white in HDRP");
                if (!material.HasProperty("_BaseColor")) continue;
                Color color = material.GetColor("_BaseColor");
                Assert.That(Mathf.Max(color.r, color.g, color.b), Is.LessThan(0.35f),
                    $"Percival's bed albedo is still blown white ({color})");
            }
        }
    }

    [Test]
    public void MoonWindowsProvideCoolSeparationBeyondTheOilLampCeiling()
    {
        Light[] shafts = Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(light => light.gameObject.scene.IsValid() && light.name.StartsWith("MoonWindowLight"))
            .ToArray();

        Assert.That(shafts, Has.Length.EqualTo(2),
            "the hall needs paired visible moon windows rather than another room-wide warm point light");
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
    public void PlayerFacingPropsDoNotUseBuiltInPrimitiveMeshes()
    {
        string[] architecturalShell =
        {
            "HallFloor", "LeftWall", "RightWall", "NorthWall", "SouthWall", "Ceiling",
            "ParlorDoorLintel",             "NorthLibrary", "SecondFloorGallery", "PercivalBedroom",
            "MarrStudy", "BarredGuestRoom", "AtticLoft", "CellarVault",
            "StairLanding",
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
            "Entry Hall still renders player-facing Unity primitives:\n- " + string.Join("\n- ", primitivePaths));
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

    [Test]
    public void TheGrandStaircaseIsClimbableInsteadOfASolidNorthWallBox()
    {
        Transform stairs = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .First(candidate => candidate.gameObject.scene.IsValid() && candidate.name == "GrandStaircase");
        BoxCollider[] colliders = stairs.GetComponentsInChildren<BoxCollider>(true);
        Assert.IsFalse(colliders.Any(box => box.size.y >= 3.2f && box.size.z >= 4.5f && box.size.x >= 4.0f),
            "GrandStaircase still carries the 4.6x3.4x5 solid that made upstairs unreachable");

        Transform treads = stairs.Find("StairTreads");
        Assert.IsNotNull(treads, "StairTreads root is missing");
        int treadCount = Enumerable.Range(0, treads.childCount)
            .Count(i => treads.GetChild(i).name.StartsWith("StairTread_"));
        Assert.That(treadCount, Is.GreaterThanOrEqualTo(12),
            "not enough treads for a 0.4m stepOffset climb to the 2F landing");

        Physics.SyncTransforms();
        bool stepped = Physics.Raycast(new Vector3(0f, 0.35f, 6.3f), Vector3.down, out RaycastHit hit, 0.5f);
        Assert.IsTrue(stepped, "the first stair tread does not exist under a climbing foot");
        Assert.That(hit.point.y, Is.GreaterThan(0.05f));
        Assert.That(hit.point.y, Is.LessThan(GmWendNavMesh.AgentStep + 0.05f));
    }

    [Test]
    public void FoyerDoorsPhysicallyMatchTheirLockStates()
    {
        GmEstateDoor library = Door("LibraryDoor");
        GmEstateDoor conservatory = Door("ConservatoryDoor");
        GmEstateDoor front = Door("FrontDoors");
        GmEstateDoor cellar = Door("CellarPanel");
        GmEstateDoor percival = Door("PercivalDoor");
        GmEstateDoor marr = Door("MarrDoor");
        GmEstateDoor barred = Door("BarredGuestDoor");
        GmEstateDoor hatch = Door("AtticHatch");

        Assert.IsTrue(library.IsLocked);
        Assert.IsTrue(library.BlocksPassage);
        Assert.IsTrue(conservatory.IsBarred);
        Assert.IsTrue(conservatory.BlocksPassage);
        Assert.IsTrue(front.IsBarred);
        Assert.IsTrue(front.BlocksPassage);
        Assert.IsTrue(cellar.IsSecretMechanism);
        Assert.IsTrue(cellar.IsLocked);
        Assert.IsTrue(cellar.BlocksPassage);
        Assert.That(Mathf.Abs(cellar.transform.position.x), Is.GreaterThan(1.6f),
            "the cellar panel sits on the stair climb instead of the under-stair flank");
        Assert.IsTrue(percival.IsOpen);
        Assert.IsFalse(percival.BlocksPassage);
        Assert.IsTrue(marr.IsLocked);
        Assert.IsTrue(marr.BlocksPassage);
        Assert.IsTrue(barred.IsBarred);
        Assert.IsTrue(barred.BlocksPassage);
        Assert.IsTrue(hatch.IsLocked);
        Assert.IsTrue(hatch.BlocksPassage);
    }

    [Test]
    public void TheLibraryKeySitsOnTheWakeTable()
    {
        GmEstateKeyItem key = KeyItem(GmEntryHallBuilder.LibraryKeyClueId);
        Assert.That(key.transform.position.z, Is.LessThan(-7f),
            "the library key is not on the wake table");
    }

    [Test]
    public void LockedAndBarredOpeningsAreSolidToARay()
    {
        Physics.SyncTransforms();
        Assert.IsTrue(RayHitsBarrier(new Vector3(-4.7f, 1.2f, 8.6f), Vector3.forward, 2.2f),
            "the locked library door does not occupy its opening");
        Assert.IsTrue(RayHitsBarrier(new Vector3(-4.7f, 1.2f, -8f), Vector3.left, 2.2f),
            "the barred conservatory door does not occupy its opening");
        Assert.IsTrue(RayHitsBarrier(new Vector3(0f, 1.3f, -8.6f), Vector3.back, 2.2f),
            "the barred front doors do not occupy their opening");
        Assert.IsFalse(RayHitsBarrier(new Vector3(-4.4f, 4.55f, 13.2f), Vector3.left, 2.0f),
            "Percival's open door still occupies the 2F opening");
        Assert.IsTrue(RayHitsBarrier(new Vector3(-2.4f, 4.55f, 14.6f), Vector3.forward, 2.2f),
            "Lady Marr's locked door does not occupy its opening");
        Assert.IsTrue(RayHitsBarrier(new Vector3(2.4f, 4.55f, 14.6f), Vector3.forward, 2.2f),
            "the barred guest door does not occupy its opening");
        Assert.IsTrue(RayHitsBarrier(new Vector3(4.2f, 6.18f, 12.55f), Vector3.forward, 2.2f),
            "the locked attic hatch does not occupy the loft well");
        BoxCollider hatchBarrier = Door("AtticHatch").Barrier;
        Assert.IsNotNull(hatchBarrier, "attic hatch has no DoorBarrier");
        Assert.That(hatchBarrier.size.y, Is.LessThan(0.4f),
            "attic hatch is a wall-door at the well lip instead of a ceiling slab");
        Assert.That(hatchBarrier.size.x, Is.GreaterThan(1.2f));
        Assert.That(hatchBarrier.size.z, Is.GreaterThan(2.0f));
    }

    [Test]
    public void ACharacterControllerCannotWalkThroughLockedOrBarredDoors()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            Physics.SyncTransforms();
            AssertDoorHolds(probe, "LibraryDoor", 0.05f);
            AssertDoorHolds(probe, "FrontDoors", 0.05f);
            AssertDoorHolds(probe, "ConservatoryDoor", 0.05f);
            AssertDoorHolds(probe, "CellarPanel", 0.05f);
            AssertDoorHolds(probe, "MarrDoor", GmEntryHallBuilder.SecondFloorY + 0.05f);
            AssertDoorHolds(probe, "BarredGuestDoor", GmEntryHallBuilder.SecondFloorY + 0.05f);
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void ACharacterControllerCanWalkIntoPercivalsOpenBedroom()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            GmEstateDoor percival = Door("PercivalDoor");
            Vector3 through = percival.transform.forward;
            Vector3 start = new Vector3(-4.2f, GmEntryHallBuilder.SecondFloorY + 0.05f, 13.2f);
            Physics.SyncTransforms();
            var result = GmPhysicalIntegrityProbe.AttemptBypass(
                probe, start, through, 3.2f, percival.transform.position, through);
            Assert.IsFalse(result.blocked && result.travelledDistance < 1.0f,
                "Percival's opening is physically closed: " + GmPhysicalIntegrityProbe.Describe(result));
            Assert.That(result.finalPosition.x, Is.LessThan(-5.9f),
                "a body never entered Percival's room: " + GmPhysicalIntegrityProbe.Describe(result));
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void ACharacterControllerCanClimbTheStairsOntoTheSecondFloor()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            probe.enabled = false;
            probe.transform.position = new Vector3(0f, 0.05f, 5.7f);
            probe.enabled = true;
            Physics.SyncTransforms();
            for (int step = 0; step < 90; step++)
                probe.Move(new Vector3(0f, -0.45f, 0.18f));
            Assert.That(probe.transform.position.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY - 0.35f),
                $"the climb stalled at {probe.transform.position} instead of reaching the 2F landing");
            Assert.That(probe.transform.position.z, Is.GreaterThan(9.2f),
                $"the climb never reached the landing (ended {probe.transform.position})");
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void ACharacterControllerCannotWalkThroughTheLandingClock()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            Transform clock = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .First(candidate => candidate.gameObject.scene.IsValid() && candidate.name == "LandingClock");
            Vector3 start = new Vector3(3.35f, 0.05f, 7.85f);
            Physics.SyncTransforms();
            var result = GmPhysicalIntegrityProbe.AttemptBypass(
                probe, start, Vector3.right, 2.6f, clock.position, Vector3.right);
            Assert.IsTrue(result.blocked,
                "the clock case is a hologram: " + GmPhysicalIntegrityProbe.Describe(result));
            Assert.IsFalse(result.penetratedBarrier,
                "a body walked through the clock: " + GmPhysicalIntegrityProbe.Describe(result));
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void FactoryDoorLeavesFillTheWallInsteadOfStabbingThroughTheOpening()
    {
        Bounds library = CombinedRendererBounds(GameObject.Find("LibraryDoor").transform);
        Assert.That(library.size.y, Is.GreaterThan(1.6f),
            $"library door is not standing in the opening (size={library.size})");
        Assert.That(library.size.x, Is.GreaterThan(library.size.z * 1.6f),
            $"the library door is a slab running through the opening instead of a leaf in the north wall (size={library.size})");
        Bounds conservatory = CombinedRendererBounds(GameObject.Find("ConservatoryDoor").transform);
        Assert.That(conservatory.size.y, Is.GreaterThan(1.6f),
            $"conservatory door is not standing in the opening (size={conservatory.size})");
        Assert.That(conservatory.size.z, Is.GreaterThan(conservatory.size.x * 1.6f),
            $"the conservatory door is not filling the west-wall opening (size={conservatory.size})");
    }

    [Test]
    public void PercivalsRoomIsAFurnishedBedroomNotAnEmptyStub()
    {
        Assert.IsNotNull(GameObject.Find("PercivalBedroom"));
        Assert.IsNotNull(GameObject.Find("PercivalBed"));
        Assert.IsNotNull(GameObject.Find("PercivalDesk"));
        Transform bed = GameObject.Find("PercivalBed").transform;
        Assert.IsTrue(bed.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                renderer.transform.GetComponentsInParent<Transform>(true)
                    .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
            "Percival's bed is not the owned GothicBed mesh");

        Bounds deskBounds = CombinedRendererBounds(GameObject.Find("PercivalDesk").transform);
        Assert.That(deskBounds.min.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY - 0.25f),
            "Percival's desk is still grounded to the foyer floor");
        Assert.That(deskBounds.min.y, Is.LessThan(GmEntryHallBuilder.SecondFloorY + 0.25f));

        Bounds bedBounds = CombinedRendererBounds(bed);
        Assert.That(bedBounds.min.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY - 0.25f),
            "Percival's bed is not sitting on the 2F floor");
        Assert.That(Mathf.Max(bedBounds.size.x, bedBounds.size.z),
            Is.GreaterThan(bedBounds.size.y * 0.85f),
            "Percival's bed is standing on end");
    }

    [Test]
    public void TheFoyerStillOwnsTheNineDebtorPortraitsAndTheShard()
    {
        Transform gallery = GameObject.Find("PortraitGallery").transform;
        Assert.That(gallery.childCount, Is.GreaterThanOrEqualTo(10),
            "moving the 2F hang must not strip the foyer wall (9 frames + shard)");
        Assert.IsNotNull(GameObject.Find("MirrorShard_1"));
        Assert.That(GameObject.Find("MirrorShard_1").transform.position.y, Is.LessThan(3f),
            "Shard #1 left the foyer Percival frame");
    }

    [Test]
    public void TheSecondFloorExtendsTheDebtorGallery()
    {
        Transform upper = GameObject.Find("UpperDebtorGallery").transform;
        Assert.That(upper.childCount, Is.EqualTo(GmEntryHallBuilder.DebtorNames.Length),
            "the 2F run is missing the extended debtor hang");
        Assert.IsNotNull(GameObject.Find("UpperGalleryRunner"));
        Assert.That(GameObject.Find("UpperGalleryRunner").transform.position.y,
            Is.GreaterThan(GmEntryHallBuilder.SecondFloorY - 0.2f));
    }

    [Test]
    public void MarrsStudyIsAFurnishedRoomBehindTheLockedDoor()
    {
        Assert.IsNotNull(GameObject.Find("MarrStudy"));
        Assert.IsNotNull(GameObject.Find("MarrDesk"));
        Assert.IsNotNull(GameObject.Find("MarrChair"));
        Assert.IsNotNull(GameObject.Find("MarrHandNote"));
        Assert.IsNotNull(GameObject.Find("MarrBooks"));
        Assert.That(GameObject.Find("MarrBooks").GetComponentsInChildren<Renderer>(true)
            .Count(renderer => renderer.transform.GetComponentsInParent<Transform>(true)
                .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
            Is.GreaterThanOrEqualTo(8),
            "Marr's bookcase still has empty shelves");
        Bounds deskBounds = CombinedRendererBounds(GameObject.Find("MarrDesk").transform);
        Assert.That(deskBounds.min.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY - 0.25f),
            "Marr's desk is still grounded to the foyer floor");
        Assert.That(deskBounds.min.z, Is.GreaterThan(16.3f),
            "Marr's desk is not inside the study north of the landing");
        GmEstateDoor marr = Door("MarrDoor");
        Assert.IsTrue(marr.IsLocked);
        Assert.IsTrue(marr.BlocksPassage);
    }

    [Test]
    public void TheMarrKeySitsOnPercivalsDesk()
    {
        GmEstateKeyItem key = KeyItem(GmEntryHallBuilder.MarrKeyClueId);
        Assert.That(key.transform.position.x, Is.LessThan(-8f),
            "Lady Marr's key is not in Percival's room");
        Assert.That(key.transform.position.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY + 0.4f),
            "Lady Marr's key is not on Percival's desk");
    }

    [Test]
    public void ACharacterControllerCanWalkIntoMarrsStudyAfterTheKey()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        GmEstateDoor marr = Door("MarrDoor");
        try
        {
            GmRunStore.RecordClue(GmEntryHallBuilder.MarrKeyClueId);
            marr.OnGmInteraction(null);
            Assert.IsTrue(marr.IsOpen);
            Assert.IsFalse(marr.BlocksPassage);

            Vector3 start = new Vector3(-2.4f, GmEntryHallBuilder.SecondFloorY + 0.05f, 14.6f);
            Physics.SyncTransforms();
            var result = GmPhysicalIntegrityProbe.AttemptBypass(
                probe, start, Vector3.forward, 5.2f, new Vector3(-2.4f, 4.6f, 18.4f), Vector3.forward);
            Assert.That(result.finalPosition.z, Is.GreaterThan(16.8f),
                "a body never entered Marr's study: " + GmPhysicalIntegrityProbe.Describe(result));
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void TheBarredGuestRoomExistsAndABodyCannotEnter()
    {
        Assert.IsNotNull(GameObject.Find("BarredGuestRoom"));
        Assert.IsNotNull(GameObject.Find("BarredGuestBed"));
        Assert.IsNotNull(GameObject.Find("BarredGuestChair"));
        Bounds bedBounds = CombinedRendererBounds(GameObject.Find("BarredGuestBed").transform);
        Assert.That(bedBounds.min.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY - 0.25f),
            "the barred guest bed is not on the 2F floor");
        Assert.That(bedBounds.min.z, Is.GreaterThan(16.3f),
            "the barred guest bed is not behind the north wall");

        GmEstateDoor barred = Door("BarredGuestDoor");
        Assert.IsTrue(barred.IsBarred);
        Assert.IsTrue(barred.BlocksPassage);

        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            Physics.SyncTransforms();
            Vector3 start = new Vector3(2.4f, GmEntryHallBuilder.SecondFloorY + 0.05f, 14.6f);
            var result = GmPhysicalIntegrityProbe.AttemptBypass(
                probe, start, Vector3.forward, 5.2f, new Vector3(2.4f, 4.6f, 18.4f), Vector3.forward);
            Assert.IsTrue(result.blocked,
                "the barred guest door is a hologram: " + GmPhysicalIntegrityProbe.Describe(result));
            Assert.That(result.finalPosition.z, Is.LessThan(16.4f),
                "a body walked into the barred guest room: " + GmPhysicalIntegrityProbe.Describe(result));
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void TheNorthLibraryIsAFurnishedRoomNotAStub()
    {
        Assert.IsNotNull(GameObject.Find("NorthLibrary"));
        Assert.IsNotNull(GameObject.Find("LibraryReadingTable"));
        Assert.IsNotNull(GameObject.Find("LibraryLadder"));
        Assert.IsNotNull(GameObject.Find("LibraryWestBayMid"));
        Assert.IsNotNull(GameObject.Find("WeightedShelfCase"));
        Assert.That(GameObject.Find("NorthLibrary").GetComponentsInChildren<Renderer>(true)
            .Count(renderer => GmVictorianInteriorKit.IsImportedVisual(renderer)),
            Is.GreaterThanOrEqualTo(6),
            "the library does not have a row of owned bookcases");

        var shelf = Object.FindAnyObjectByType<GmWeightedShelf>(FindObjectsInactive.Include);
        Assert.IsNotNull(shelf);
        CollectionAssert.AreEqual(GmWeightedShelf.StartingOrder, shelf.CurrentOrder);
        Assert.AreEqual(2, shelf.CurrentOrder[2]);
        Assert.IsFalse(shelf.IsSolved);
        Assert.That(Object.FindObjectsByType<GmInteractable>(FindObjectsInactive.Include)
            .Count(item => item.InteractionId != null &&
                item.InteractionId.StartsWith(GmWeightedShelf.BookIdPrefix)),
            Is.EqualTo(5));
    }

    [Test]
    public void ACharacterControllerCanWalkTheLibraryAisleAfterTheKey()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        GmEstateDoor library = Door("LibraryDoor");
        try
        {
            GmRunStore.RecordClue(GmEntryHallBuilder.LibraryKeyClueId);
            library.OnGmInteraction(null);
            Assert.IsTrue(library.IsOpen);
            Assert.IsFalse(library.BlocksPassage);

            Vector3 start = new Vector3(-4.7f, 0.05f, 8.4f);
            Physics.SyncTransforms();
            var result = GmPhysicalIntegrityProbe.AttemptBypass(
                probe, start, Vector3.forward, 5.5f, new Vector3(-4.7f, 1.2f, 13.2f), Vector3.forward);
            Assert.That(result.finalPosition.z, Is.GreaterThan(11.2f),
                "a body never entered the library: " + GmPhysicalIntegrityProbe.Describe(result));
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void OrderingTheWeightedShelfBanksTheCellarLever()
    {
        var shelf = Object.FindAnyObjectByType<GmWeightedShelf>(FindObjectsInactive.Include);
        Assert.IsNotNull(shelf);
        Assert.IsTrue(shelf.HandleSlot(0));
        Assert.IsTrue(shelf.HandleSlot(1));
        Assert.IsTrue(shelf.HandleSlot(1));
        Assert.IsTrue(shelf.HandleSlot(4));
        Assert.IsTrue(shelf.IsSolved);
        Assert.IsTrue(GmRunStore.HasClue(GmEntryHallBuilder.LibraryLeverClueId));

        GmEstateDoor cellar = Door("CellarPanel");
        cellar.OnGmInteraction(null);
        Assert.IsFalse(cellar.IsLocked);
        Assert.IsTrue(cellar.IsOpen);
        Assert.IsFalse(cellar.BlocksPassage);
    }

    static Bounds CombinedRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers, root.name + " has no renderer");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    [Test]
    public void TheAtticIsAFurnishedLoftNotAStub()
    {
        Assert.IsNotNull(GameObject.Find("AtticLoft"));
        Assert.IsNotNull(GameObject.Find("AtticLadder"));
        Assert.IsNotNull(GameObject.Find("AtticCrate"));
        Assert.IsNotNull(GameObject.Find("AtticCobwebs"));
        Assert.IsNotNull(GameObject.Find("AtticDormer"));
        Assert.IsNotNull(GameObject.Find("MirrorShard_2"));
        Assert.That(GameObject.Find("MirrorShard_2").transform.position.y,
            Is.GreaterThan(GmEntryHallBuilder.AtticFloorY),
            "Shard #2 is not in the loft");
        Assert.That(GameObject.Find("MirrorShard_1").transform.position.y, Is.LessThan(3f),
            "placing shard 2 must not move shard 1 off Percival's foyer frame");
        Transform treads = GameObject.Find("AtticLadder").transform;
        int treadCount = Enumerable.Range(0, treads.childCount)
            .Count(i => treads.GetChild(i).name.StartsWith("AtticLadderTread_"));
        Assert.That(treadCount, Is.GreaterThanOrEqualTo(8),
            "attic ladder has no 0.4m-legal treads");
        Assert.That(GameObject.Find("AtticCrate").GetComponentsInChildren<Renderer>(true)
            .Any(renderer => renderer.transform.GetComponentsInParent<Transform>(true)
                .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
            "attic crate is not the owned crate mesh");
    }

    [Test]
    public void TheAtticKeySitsOnMarrsDesk()
    {
        GmEstateKeyItem key = KeyItem(GmEntryHallBuilder.AtticKeyClueId);
        Assert.That(key.transform.position.z, Is.GreaterThan(16.5f),
            "the attic key is not in Marr's study");
        Assert.That(key.transform.position.y, Is.GreaterThan(GmEntryHallBuilder.SecondFloorY + 0.4f),
            "the attic key is not on Marr's desk");
        Assert.That(key.transform.position.y, Is.LessThan(GmEntryHallBuilder.AtticFloorY - 1f),
            "the attic key was placed behind the hatch it opens");
    }

    [Test]
    public void ACharacterControllerCannotEnterTheAtticWithoutTheKey()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            probe.enabled = false;
            probe.transform.position = new Vector3(4.2f, GmEntryHallBuilder.SecondFloorY + 0.05f, 12.8f);
            probe.enabled = true;
            Physics.SyncTransforms();
            for (int step = 0; step < 90; step++)
                probe.Move(new Vector3(0f, -0.45f, 0.18f));
            Assert.That(probe.transform.position.y, Is.LessThan(GmEntryHallBuilder.AtticFloorY - 0.15f),
                $"a body climbed into the locked loft (ended {probe.transform.position})");
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void ACharacterControllerCanClimbIntoTheAtticAfterTheKey()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        GmEstateDoor hatch = Door("AtticHatch");
        try
        {
            GmRunStore.RecordClue(GmEntryHallBuilder.AtticKeyClueId);
            hatch.OnGmInteraction(null);
            Assert.IsTrue(hatch.IsOpen);
            Assert.IsFalse(hatch.BlocksPassage);

            probe.enabled = false;
            probe.transform.position = new Vector3(4.2f, GmEntryHallBuilder.SecondFloorY + 0.05f, 12.8f);
            probe.enabled = true;
            Physics.SyncTransforms();
            for (int step = 0; step < 140; step++)
                probe.Move(new Vector3(0f, -0.45f, 0.18f));
            Assert.That(probe.transform.position.y, Is.GreaterThan(GmEntryHallBuilder.AtticFloorY - 0.4f),
                $"the attic climb stalled at {probe.transform.position}");
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void TheCellarIsAFurnishedVaultNotAStub()
    {
        Assert.IsNotNull(GameObject.Find("CellarVault"));
        Assert.IsNotNull(GameObject.Find("CellarStairs"));
        Assert.IsNotNull(GameObject.Find("CellarBarrel"));
        Assert.IsNotNull(GameObject.Find("CellarBrazier"));
        Assert.IsNotNull(GameObject.Find("VaultGrate"));
        Assert.IsNotNull(GameObject.Find("VaultTransition"));
        Transform treads = GameObject.Find("CellarStairs").transform;
        int treadCount = Enumerable.Range(0, treads.childCount)
            .Count(i => treads.GetChild(i).name.StartsWith("CellarStairTread_"));
        Assert.That(treadCount, Is.GreaterThanOrEqualTo(10),
            "cellar descent has no 0.4m-legal treads");
        Assert.That(GameObject.Find("CellarBarrel").GetComponentsInChildren<Renderer>(true)
            .Any(renderer => renderer.transform.GetComponentsInParent<Transform>(true)
                .Any(parent => parent.name.StartsWith(GmOwnedPropFactory.VisualPrefix))),
            "cellar barrel is not the owned barrel mesh");
        var grate = GameObject.Find("VaultTransition").GetComponent<GmSceneTransitionTrigger>();
        Assert.AreEqual(GmHiddenRoomBuilder.SceneId, grate.TargetSceneId);
        Assert.AreEqual(GmHiddenRoomBuilder.ScenePath, grate.TargetScenePath);
    }

    [Test]
    public void ACharacterControllerCannotEnterTheCellarWithoutTheLever()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        try
        {
            AssertDoorHolds(probe, "CellarPanel", 0.05f);
            probe.enabled = false;
            probe.transform.position = new Vector3(4.1f, 0.05f, GmEntryHallBuilder.CellarPanelZ);
            probe.enabled = true;
            Physics.SyncTransforms();
            for (int step = 0; step < 40; step++)
                probe.Move(new Vector3(-0.18f, -0.45f, 0f));
            Assert.That(probe.transform.position.y, Is.GreaterThan(-0.4f),
                $"a body dropped into the locked cellar (ended {probe.transform.position})");
            Assert.That(probe.transform.position.x, Is.GreaterThan(GmEntryHallBuilder.CellarPanelX - 0.35f),
                $"a body walked through the locked panel (ended {probe.transform.position})");
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    [Test]
    public void ACharacterControllerCanDescendIntoTheCellarAfterTheLever()
    {
        CharacterController probe = MakeProbe();
        CharacterController player = GameObject.Find("Player")?.GetComponent<CharacterController>();
        if (player != null) player.enabled = false;
        GmEstateDoor cellar = Door("CellarPanel");
        try
        {
            var shelf = Object.FindAnyObjectByType<GmWeightedShelf>(FindObjectsInactive.Include);
            Assert.IsTrue(shelf.HandleSlot(0));
            Assert.IsTrue(shelf.HandleSlot(1));
            Assert.IsTrue(shelf.HandleSlot(1));
            Assert.IsTrue(shelf.HandleSlot(4));
            cellar.OnGmInteraction(null);
            Assert.IsTrue(cellar.IsOpen);
            Assert.IsFalse(cellar.BlocksPassage);

            probe.enabled = false;
            probe.transform.position = new Vector3(GmEntryHallBuilder.CellarWellCenterX, 0.05f,
                GmEntryHallBuilder.CellarPanelZ);
            probe.enabled = true;
            Physics.SyncTransforms();
            for (int step = 0; step < 160; step++)
                probe.Move(new Vector3(0f, -0.45f, -0.18f));
            Assert.That(probe.transform.position.y, Is.LessThan(GmEntryHallBuilder.CellarFloorY + 0.55f),
                $"the cellar descent stalled at {probe.transform.position}");
            Assert.That(probe.transform.position.x, Is.LessThan(GmEntryHallBuilder.CellarPanelX - 0.2f),
                $"the body never passed the panel (ended {probe.transform.position})");
        }
        finally
        {
            if (player != null) player.enabled = true;
            Object.DestroyImmediate(probe.gameObject);
        }
    }

    static GmEstateDoor Door(string objectName)
    {
        Transform root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.name == objectName);
        Assert.IsNotNull(root, objectName + " is missing");
        var door = root.GetComponent<GmEstateDoor>();
        Assert.IsNotNull(door, objectName + " has no GmEstateDoor");
        return door;
    }

    static GmEstateKeyItem KeyItem(string clueId)
    {
        var item = Object.FindObjectsByType<GmEstateKeyItem>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate.KeyClueId == clueId);
        Assert.IsNotNull(item, clueId + " was never placed");
        return item;
    }

    static bool RayHitsBarrier(Vector3 origin, Vector3 direction, float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance);
        return hits.Any(hit => hit.collider != null && hit.collider.name == GmEstateDoor.BarrierName);
    }

    static CharacterController MakeProbe()
    {
        var go = new GameObject("EntryHallPhysicalProbe");
        var controller = go.AddComponent<CharacterController>();
        controller.height = GmWendNavMesh.AgentHeight;
        controller.radius = GmWendNavMesh.AgentRadius;
        controller.center = new Vector3(0f, GmWendNavMesh.AgentHeight * 0.5f, 0f);
        controller.slopeLimit = GmWendNavMesh.AgentSlope;
        controller.stepOffset = GmWendNavMesh.AgentStep;
        return controller;
    }

    static void AssertDoorHolds(CharacterController probe, string doorName, float standY)
    {
        GmEstateDoor door = Door(doorName);
        Vector3 through = door.transform.forward;
        Vector3 plane = door.transform.position;
        plane.y = standY;
        float[] offsets = { 0f, 0.25f, -0.25f };
        var results = GmPhysicalIntegrityProbe.SweepLateralBypass(
            probe, plane, through, offsets, pushDistance: 3.6f, startBackoff: 1.7f);
        foreach (GmPhysicalIntegrityProbe.BypassAttemptResult attempt in results)
        {
            Assert.IsFalse(attempt.penetratedBarrier,
                doorName + " let a body through: " + GmPhysicalIntegrityProbe.Describe(attempt));
            Assert.IsTrue(attempt.blocked,
                doorName + " was never solid: " + GmPhysicalIntegrityProbe.Describe(attempt));
        }
    }
}
