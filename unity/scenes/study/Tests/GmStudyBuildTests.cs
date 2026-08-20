using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmStudyBuildTests
{
    GmStudySceneHost fixtureHost;
    string productionSavePath;
    bool productionSaveExisted;
    byte[] productionSaveBytes;

    [OneTimeSetUp]
    public void BuildOnce()
    {
        GmSaveSystem.ResetTestConfiguration();
        productionSavePath = GmSaveSystem.SavePath;
        Assert.That(Path.GetDirectoryName(productionSavePath),
            Is.EqualTo(Application.persistentDataPath));
        productionSaveExisted = File.Exists(productionSavePath);
        productionSaveBytes = productionSaveExisted ? File.ReadAllBytes(productionSavePath) : null;
        GmStudyBuilder.Build();
        fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
        fixtureHost.ActivateDirectReviewPersistence();
    }

    [OneTimeTearDown]
    public void TearDownOnce()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void RebuildAuditAndTourReplayNeverTouchProductionSave()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        Assert.That(GmSaveSystem.SavePath, Is.EqualTo(productionSavePath));
        try
        {
            GmStudyBuilder.Build();
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(productionSavePath),
                "builder must restore the production backend after its isolated review scope");
            AssertProductionSaveUnchanged();

            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost.ActivateDirectReviewPersistence();
            Assert.That(GmSaveSystem.SavePath, Is.Not.EqualTo(productionSavePath));
            Assert.That(GmStudyQualityAudit.ValidateOpenScene(), Is.Empty);
            GmStudySceneHost host = fixtureHost;
            GmStudyInput input = Object.FindAnyObjectByType<GmStudyInput>();
            GmStudyShotTour tour = Object.FindAnyObjectByType<GmStudyShotTour>();
            host.RestartForReview(1);
            tour.ReplayToPendingForReview(input);
            Assert.That(host.Controller.Phase, Is.EqualTo(GmStudyMatchPhase.AwaitingIntervention));
            AssertProductionSaveUnchanged();
        }
        finally
        {
            fixtureHost?.ReleaseDirectReviewPersistence();
            AssertProductionSaveUnchanged();
            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void OpeningSavedStudyInEditModeDoesNotActivateReviewPersistence()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        GmSaveSystem.ResetTestConfiguration();
        GmStudySceneHost openedHost = null;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Study.unity", OpenSceneMode.Single);
            openedHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            Assert.That(openedHost, Is.Not.Null);
            Assert.That(openedHost.IsDirectReviewOnly, Is.True);
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(productionSavePath),
                "EditMode OnEnable must not acquire the direct-review persistence scope");
            AssertProductionSaveUnchanged();
        }
        finally
        {
            openedHost?.ReleaseDirectReviewPersistence();
            GmSaveSystem.ResetTestConfiguration();
            GmStudyBuilder.Build();
            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void RebuildingWhileSavedStudyIsOpenRestoresExactCustomBackend()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        var backend = new CountingBackend();
        string priorPath = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "study-open-rebuild-prior", "prior-save.json");
        GmSaveSystem.ConfigureForTests(priorPath, backend);
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Study.unity", OpenSceneMode.Single);
            GmStudyBuilder.Build();
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(priorPath));
            Assert.That(backend.WriteCount, Is.Zero,
                "an EditMode scene host must not unwind the builder's nested review scope");
            AssertProductionSaveUnchanged();
        }
        finally
        {
            Object.FindAnyObjectByType<GmStudySceneHost>()?.ReleaseDirectReviewPersistence();
            GmSaveSystem.ResetTestConfiguration();
            GmStudyBuilder.Build();
            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void PhysicalSceneContractPasses()
    {
        Assert.That(GmStudyQualityAudit.ValidateOpenScene(), Is.Empty);
    }

    [Test]
    public void BoardHasExactlySixtyFourTilesAndFiveAuthoredPieces()
    {
        GmStudyBoardTile[] tiles = Object.FindObjectsByType<GmStudyBoardTile>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(tiles, Has.Length.EqualTo(64));
        Assert.That(tiles.Select(tile => (tile.File, tile.Rank)).Distinct().Count(), Is.EqualTo(64));

        GmStudyPieceView[] pieces = Object.FindObjectsByType<GmStudyPieceView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(pieces, Has.Length.EqualTo(5));
        Assert.That(pieces.Count(piece => piece.PieceType == GmChessPieceType.King), Is.EqualTo(2));
        Assert.That(pieces.Count(piece => piece.PieceType == GmChessPieceType.Queen), Is.EqualTo(1));
        Assert.That(pieces.Count(piece => piece.PieceType == GmChessPieceType.Rook), Is.EqualTo(1));
        Assert.That(pieces.Count(piece => piece.PieceType == GmChessPieceType.Pawn), Is.EqualTo(1));
        string[] primitiveNames = { "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad" };
        Assert.That(pieces.All(piece => piece.GetComponentsInChildren<MeshFilter>(true).Length >= 2 &&
            piece.GetComponentsInChildren<MeshFilter>(true).All(filter =>
                filter.sharedMesh != null && !primitiveNames.Contains(filter.sharedMesh.name))), Is.True,
            "every piece body must be a composed authored mesh hierarchy, not a bare primitive");
    }

    [Test]
    public void EveryBoardTileSitsOnTheTableSurfaceNotNearTheFloor()
    {
        // A prior build placed all 64 tiles near world y=0 (CreateRoundedProp treats its position
        // argument as world space; board-local-looking offsets landed at the floor instead of the
        // table). No prior test caught it -- only the rendered tour screenshot did.
        GameObject boardAnchor = GameObject.Find("StudyBoard");
        Assert.That(boardAnchor, Is.Not.Null);
        float tableTop = boardAnchor.transform.position.y;
        Assert.That(tableTop, Is.GreaterThan(0.5f), "board anchor itself must be at table height");
        GmStudyBoardTile[] tiles = Object.FindObjectsByType<GmStudyBoardTile>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(tiles, Is.Not.Empty);
        foreach (GmStudyBoardTile tile in tiles)
            Assert.That(tile.transform.position.y, Is.InRange(tableTop - 0.02f, tableTop + 0.05f),
                $"tile ({tile.File},{tile.Rank}) sits at y={tile.transform.position.y}, expected within a few cm of the table surface (y={tableTop})");
    }

    [Test]
    public void FreshBuildPlacesEveryPieceOnTheBoardSurfaceAtItsFenSquare()
    {
        // A prior build put every piece ~0.87m above the board (boardOrigin.y and pieceSurfaceY
        // were both absolute TableTop-based heights, added together). No prior test caught it --
        // only the rendered tour screenshot did. This pins the board-surface height contract.
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        GmStudyPresentationState state = GmStudyPresentationModel.Project(host.Controller);
        var byTypeAndSide = Object.FindObjectsByType<GmStudyPieceView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .ToDictionary(piece => (piece.PieceType, piece.IsWhite));

        var expected = GmChessFen.ParsePlacement(state.ShownFen);
        Assert.That(expected.Count, Is.EqualTo(5));
        GameObject boardAnchor = GameObject.Find("StudyBoard");
        float tableTop = boardAnchor.transform.position.y;
        foreach (GmChessPiece piece in expected)
        {
            Assert.That(byTypeAndSide.ContainsKey((piece.Type, piece.IsWhite)), Is.True,
                $"no physical view exists for {(piece.IsWhite ? "white" : "black")} {piece.Type}");
            GmStudyPieceView view = byTypeAndSide[(piece.Type, piece.IsWhite)];
            Assert.That(view.IsOnBoard, Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.transform.position.y, Is.InRange(tableTop, tableTop + 0.05f),
                $"{piece.Type} sits at y={view.transform.position.y}, expected within 5cm of the board surface (y={tableTop})");
        }
        var placedTypes = new HashSet<(GmChessPieceType, bool)>(expected.Select(p => (p.Type, p.IsWhite)));
        foreach (var entry in byTypeAndSide)
            if (!placedTypes.Contains(entry.Key))
                Assert.That(entry.Value.IsOnBoard, Is.False,
                    $"{entry.Key} is not in the current position but is still marked on the board");
    }

    [Test]
    public void ChangedPieceHasPersistentNonColorEvidence()
    {
        GmStudyPieceView pawn = Object.FindObjectsByType<GmStudyPieceView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(piece => piece.PieceType == GmChessPieceType.Pawn);
        pawn.PlaceAtSquare(pawn.transform.position, true);
        Assert.That(pawn.ChangedEvidenceRendererCount, Is.GreaterThanOrEqualTo(6));
        Assert.That(pawn.IsChangedEvidenceVisible, Is.True);
        Assert.That(pawn.gameObject.name, Does.Not.Contain("Primitive"));
    }

    [Test]
    public void MajorVisiblePropsComeFromImportedAssetsAndAldricsChairIsEmpty()
    {
        foreach (string name in new[] { "StudyTable", "PlayerChair", "AldricChair", "StudyCarpet", "TableCandles" })
        {
            GameObject prop = GameObject.Find(GmOwnedPropFactory.VisualPrefix + name);
            Assert.That(prop, Is.Not.Null, name + " must be a real imported asset instance");
        }
        GameObject chair = GameObject.Find(GmOwnedPropFactory.VisualPrefix + "AldricChair");
        Assert.That(chair.GetComponentsInChildren<Animator>(true), Is.Empty);
        Assert.That(chair.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
    }

    [Test]
    public void MajorPropsAndRoomUseTexturedHdrpSurfaces()
    {
        foreach (string name in new[] { "StudyTable", "PlayerChair", "AldricChair", "StudyCarpet", "TableCandles" })
        {
            GameObject prop = GameObject.Find(GmOwnedPropFactory.VisualPrefix + name);
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, name);
            Assert.That(renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null)
                .All(material => material.shader != null &&
                    !material.shader.name.Contains("InternalErrorShader")), Is.True, name);
            Assert.That(renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null)
                .Any(material => material.HasProperty("_BaseColorMap") &&
                    material.GetTexture("_BaseColorMap") != null), Is.True,
                name + " must retain a real PBR albedo texture");
        }
        foreach (string roomSurface in new[] { "StudyFloor", "NorthWall", "SouthWall", "WestWall", "EastWall", "StudyCeiling" })
        {
            Material material = GameObject.Find(roomSurface).GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null, roomSurface);
            Assert.That(material.GetTexture("_NormalMap"), Is.Not.Null, roomSurface);
        }
    }

    [Test]
    public void DustStaysOutOfTheReadableBoardConeAndTableLightNeverFlickers()
    {
        GmStudyDustField[] dust = Object.FindObjectsByType<GmStudyDustField>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(dust.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(dust.All(field => field.IsOutsideBoardCone), Is.True);
        GameObject tableLight = GameObject.Find("StudyTableTaskLight");
        Assert.That(tableLight, Is.Not.Null);
        Assert.That(tableLight.GetComponent<GmPeriodLampFlicker>(), Is.Null);
        Assert.That(tableLight.GetComponent<GmLightIntent>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<GmPeriodLampFlicker>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.GreaterThan(0));
    }

    [Test]
    public void HostInputHudPresenterAudioAndTourAreConfigured()
    {
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        Assert.That(host, Is.Not.Null);
        Assert.That(host.IsConfigured, Is.True);
        Assert.That(Object.FindAnyObjectByType<GmStudyInput>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmStudyHud>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmStudyPresenter>(), Is.Not.Null);
        GmStudyAudio audio = Object.FindAnyObjectByType<GmStudyAudio>();
        Assert.That(audio, Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmAudioManager>().GetComponent<GmAudioIntent>(), Is.Not.Null);
        Assert.That(audio.UsesSharedAudioManager, Is.True);
        Assert.That(Object.FindAnyObjectByType<GmAudioManager>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmStudyPresenter>().SupportsAccessibility, Is.True);
        GmStudyShotTour tour = Object.FindAnyObjectByType<GmStudyShotTour>();
        Assert.That(tour, Is.Not.Null);
        Assert.That(tour.ShotCount, Is.EqualTo(10));
        Assert.That(tour.HasPlaceholderShots, Is.False);
    }

    [Test]
    public void TourAbortsWhenDeterministicReplayDoesNotReachPendingIntervention()
    {
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        GmStudyInput input = Object.FindAnyObjectByType<GmStudyInput>();
        GmStudyShotTour tour = Object.FindAnyObjectByType<GmStudyShotTour>();
        fixtureHost?.ReleaseDirectReviewPersistence();
        GmSaveSystem.ResetTestConfiguration();
        try
        {
            host.ActivateDirectReviewPersistence();
            Assert.That(GmSaveSystem.SavePath, Is.Not.EqualTo(productionSavePath));
            host.RestartForReview(1);
            Assert.Throws<InvalidOperationException>(() => tour.ReplayToPendingForReview(input, 0));
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(productionSavePath),
                "failed deterministic tour staging must release its review override");
            AssertProductionSaveUnchanged();
        }
        finally
        {
            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void FailedTourReplayCannotWriteToRestoredCustomBackendThroughLiveInput()
    {
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        GmStudyInput input = Object.FindAnyObjectByType<GmStudyInput>();
        GmStudyHud hud = Object.FindAnyObjectByType<GmStudyHud>();
        GmStudyShotTour tour = Object.FindAnyObjectByType<GmStudyShotTour>();
        fixtureHost?.ReleaseDirectReviewPersistence();
        var backend = new CountingBackend();
        string priorPath = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "study-review-failure-prior", "prior-save.json");
        GmSaveSystem.ConfigureForTests(priorPath, backend);
        try
        {
            host.ActivateDirectReviewPersistence();
            host.RestartForReview(1);
            Assert.Throws<InvalidOperationException>(() => tour.ReplayToPendingForReview(input, 0));
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(priorPath));
            Assert.That(host.MutationSurfacesSuspended, Is.True);
            Assert.That(input.InteractionsEnabled, Is.False);

            input.ConfirmAction();
            input.ChallengeAction();
            hud.InvokeButtonActionForTests("StudyCard1");
            hud.InvokeButtonActionForTests("StudyChallenge");

            Assert.That(backend.WriteCount, Is.Zero,
                "a failed tour must close input before restoring the prior backend");
            AssertProductionSaveUnchanged();
        }
        finally
        {
            GmSaveSystem.ResetTestConfiguration();
            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void DisablingHostCannotLeaveInputWritingToRestoredBackend()
    {
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        GmStudyInput input = Object.FindAnyObjectByType<GmStudyInput>();
        GmStudyHud hud = Object.FindAnyObjectByType<GmStudyHud>();
        fixtureHost?.ReleaseDirectReviewPersistence();
        var backend = new CountingBackend();
        string priorPath = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "study-review-disable-prior", "prior-save.json");
        GmSaveSystem.ConfigureForTests(priorPath, backend);
        try
        {
            host.ActivateDirectReviewPersistence();
            host.RestartForReview(1);
            host.enabled = false;
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(priorPath));
            Assert.That(host.MutationSurfacesSuspended, Is.True);
            Assert.That(input.InteractionsEnabled, Is.False);

            input.ConfirmAction();
            input.ChallengeAction();
            hud.InvokeButtonActionForTests("StudyCard1");
            hud.InvokeButtonActionForTests("StudyChallenge");

            Assert.That(backend.WriteCount, Is.Zero,
                "disabled host siblings must be inert before the prior backend is restored");
            AssertProductionSaveUnchanged();

            host.enabled = true;
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(priorPath),
                "EditMode re-enable must remain inert until review code explicitly activates it");
            Assert.That(host.MutationSurfacesSuspended, Is.True);
            Assert.That(input.InteractionsEnabled, Is.False);

            host.ActivateDirectReviewPersistence();
            Assert.That(GmSaveSystem.SavePath, Is.Not.EqualTo(priorPath),
                "explicit EditMode activation must acquire isolation before controls resume");
            Assert.That(host.MutationSurfacesSuspended, Is.False);
            Assert.That(input.InteractionsEnabled, Is.True);
        }
        finally
        {
            if (!host.enabled) host.enabled = true;
            host.ReleaseDirectReviewPersistence();
            GmSaveSystem.ResetTestConfiguration();
            fixtureHost = Object.FindAnyObjectByType<GmStudySceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void DestroyingDirectReviewHostRestoresExactProductionBackendAndBytes()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        GmSaveSystem.ResetTestConfiguration();
        var owner = new GameObject("StudyReviewLifetimeProof");
        GmStudySceneHost host = owner.AddComponent<GmStudySceneHost>();
        host.ConfigureForDirectReview();
        try
        {
            host.ActivateDirectReviewPersistence();
            Assert.That(GmSaveSystem.SavePath, Is.Not.EqualTo(productionSavePath));
            Object.DestroyImmediate(owner);
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(productionSavePath));
            AssertProductionSaveUnchanged();
        }
        finally
        {
            if (owner != null) Object.DestroyImmediate(owner);
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void DestroyingDirectReviewHostRestoresPriorCustomBackendAndPath()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        var backend = new CountingBackend();
        string priorPath = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "study-review-prior", "prior-save.json");
        GmSaveSystem.ConfigureForTests(priorPath, backend);
        var owner = new GameObject("StudyReviewBackendProof");
        GmStudySceneHost host = owner.AddComponent<GmStudySceneHost>();
        host.ConfigureForDirectReview();
        try
        {
            host.ActivateDirectReviewPersistence();
            Assert.That(GmSaveSystem.SavePath, Is.Not.EqualTo(priorPath));
            Object.DestroyImmediate(owner);
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(priorPath));
            GmRunStore.BeginNewRun();
            Assert.That(GmSaveSystem.Save(), Is.True);
            Assert.That(backend.WriteCount, Is.EqualTo(1),
                "disposing the review scope must restore the exact prior backend instance");
            AssertProductionSaveUnchanged();
        }
        finally
        {
            if (owner != null) Object.DestroyImmediate(owner);
            GmSaveSystem.ResetTestConfiguration();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void AuditRejectsAPlayerFacingPrimitive()
    {
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        primitive.name = "ForbiddenPlayerFacingPrimitive";
        try
        {
            Assert.That(GmStudyQualityAudit.ValidateOpenScene(),
                Has.Some.Contains("player-facing primitive"));
        }
        finally { Object.DestroyImmediate(primitive); }
    }

    [Test]
    public void PublicReviewActionsReachBothArbiterOverrideOutcomesAndChallengeCorrectionIsSilent()
    {
        GmStudySceneHost host = Object.FindAnyObjectByType<GmStudySceneHost>();
        GmStudyInput input = Object.FindAnyObjectByType<GmStudyInput>();
        GmStudyAudio audio = Object.FindAnyObjectByType<GmStudyAudio>();
        Assert.That(host.RestartForReview(1), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        input.FocusThenConfirm(0); input.FocusThenConfirm(1); input.FocusThenConfirm(0);
        Assert.That(host.Controller.Phase, Is.EqualTo(GmStudyMatchPhase.AwaitingIntervention));
        Assert.That(GmStudyPresentationModel.Project(host.Controller).HasArbiterMarker, Is.True);
        Assert.That(audio.VisibleMoveRevision, Is.EqualTo(3));
        input.ChallengeAction();
        Assert.That(host.Controller.Phase, Is.EqualTo(GmStudyMatchPhase.Complete));
        Assert.That(host.Controller.Result, Is.EqualTo(GmStudyMatchResult.PlayerWin));
        Assert.That(audio.VisibleMoveRevision, Is.EqualTo(3),
            "Challenge correction must not replay the piece-move sound");

        Assert.That(host.RestartForReview(1), Is.EqualTo(GmStudyInitializeResult.StartedNew));
        input.FocusThenConfirm(0); input.FocusThenConfirm(1); input.FocusThenConfirm(0);
        input.ConfirmAction();
        Assert.That(host.Controller.Phase, Is.EqualTo(GmStudyMatchPhase.Complete));
        Assert.That(host.Controller.Result, Is.EqualTo(GmStudyMatchResult.AldricWin));
    }

    [Test]
    public void OnePlayerCameraAndListenerAndClearTableApproach()
    {
        Assert.That(Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None), Has.Length.EqualTo(1));
        GameObject clearance = GameObject.Find("PlayerTableClearance");
        Assert.That(clearance, Is.Not.Null);
        Assert.That(Physics.OverlapBox(clearance.transform.position,
            clearance.transform.localScale * 0.49f, Quaternion.identity)
            .All(collider => collider.transform.IsChildOf(clearance.transform)), Is.True);
    }


    void AssertProductionSaveUnchanged()
    {
        Assert.That(File.Exists(productionSavePath), Is.EqualTo(productionSaveExisted));
        if (productionSaveExisted)
            CollectionAssert.AreEqual(productionSaveBytes, File.ReadAllBytes(productionSavePath));
    }

    sealed class CountingBackend : IGmAtomicSaveBackend
    {
        public int WriteCount { get; private set; }
        public void WriteAtomic(string target, string json) => WriteCount++;
    }
}
