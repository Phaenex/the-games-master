using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmBonesBuildTests
{
    GmBonesSceneHost fixtureHost;
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
        GmBonesBuilder.Build();
        fixtureHost = Object.FindAnyObjectByType<GmBonesSceneHost>();
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
            GmBonesBuilder.Build();
            Assert.That(GmSaveSystem.SavePath, Is.EqualTo(productionSavePath),
                "builder must restore the production backend after its isolated review scope");
            AssertProductionSaveUnchanged();

            fixtureHost = Object.FindAnyObjectByType<GmBonesSceneHost>();
            fixtureHost.ActivateDirectReviewPersistence();
            Assert.That(GmSaveSystem.SavePath, Is.Not.EqualTo(productionSavePath));
            Assert.That(GmBonesQualityAudit.ValidateOpenScene(), Is.Empty);
            GmBonesSceneHost host = fixtureHost;
            GmBonesInput input = Object.FindAnyObjectByType<GmBonesInput>();
            GmBonesShotTour tour = Object.FindAnyObjectByType<GmBonesShotTour>();
            host.RestartForReview(1);
            tour.ReplayToPendingForReview(input);
            Assert.That(host.Controller.Phase, Is.EqualTo(GmBonesMatchPhase.AwaitingIntervention));
            AssertProductionSaveUnchanged();
        }
        finally
        {
            fixtureHost?.ReleaseDirectReviewPersistence();
            AssertProductionSaveUnchanged();
            fixtureHost = Object.FindAnyObjectByType<GmBonesSceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void PhysicalSceneContractPasses()
    {
        Assert.That(GmBonesQualityAudit.ValidateOpenScene(), Is.Empty);
    }

    [Test]
    public void TableHasExactlyThreeIndependentAuthoredDiceAndSixtyThreePips()
    {
        GmBonesDieView[] dice = Object.FindObjectsByType<GmBonesDieView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(dice, Has.Length.EqualTo(3));
        Assert.That(dice.Distinct().Count(), Is.EqualTo(3));
        Assert.That(dice.Sum(die => die.GetComponentsInChildren<GmPhysicalDiePip>(true).Length),
            Is.EqualTo(63));
        Assert.That(dice.All(die => die.GetComponentsInChildren<MeshFilter>(true)
            .Any(filter => filter.sharedMesh != null && filter.sharedMesh.name == "GmAuthoredPhysicalDie")), Is.True,
            "every die body must be an authored mesh, not a primitive hierarchy");
    }

    [Test]
    public void StandardFacesHaveOppositePairsSummingToSeven()
    {
        Assert.That(GmPhysicalDieFaces.ValueForNormal(Vector3.up), Is.EqualTo(1));
        Assert.That(GmPhysicalDieFaces.ValueForNormal(Vector3.down), Is.EqualTo(6));
        Assert.That(GmPhysicalDieFaces.ValueForNormal(Vector3.forward), Is.EqualTo(2));
        Assert.That(GmPhysicalDieFaces.ValueForNormal(Vector3.back), Is.EqualTo(5));
        Assert.That(GmPhysicalDieFaces.ValueForNormal(Vector3.right), Is.EqualTo(3));
        Assert.That(GmPhysicalDieFaces.ValueForNormal(Vector3.left), Is.EqualTo(4));
        for (int face = 1; face <= 6; face++)
            Assert.That(GmPhysicalDieFaces.Opposite(face) + face, Is.EqualTo(7));
    }

    [Test]
    public void EveryFaceRotationPlacesItsMappedNormalOnTop()
    {
        for (int face = 1; face <= 6; face++)
        {
            Vector3 top = GmBonesDieView.RotationForFace(face) * GmPhysicalDieFaces.NormalForValue(face);
            Assert.That(Vector3.Dot(top.normalized, Vector3.up), Is.GreaterThan(0.999f));
        }
    }

    [Test]
    public void ChangedDieHasPersistentNonColorEvidence()
    {
        GmBonesDieView die = Object.FindObjectsByType<GmBonesDieView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        die.SetFace(6, true, true);
        Assert.That(die.ChangedEvidenceRendererCount, Is.GreaterThanOrEqualTo(4));
        Assert.That(die.IsChangedEvidenceVisible, Is.True);
        Assert.That(die.gameObject.name, Does.Not.Contain("Primitive"));
    }

    [Test]
    public void MajorVisiblePropsComeFromImportedAssetsAndAldricsChairIsEmpty()
    {
        foreach (string name in new[] { "BonesTable", "PlayerChair", "AldricChair", "BonesCarpet", "TableCandles" })
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
        foreach (string name in new[] { "BonesTable", "PlayerChair", "AldricChair", "BonesCarpet", "TableCandles" })
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
        foreach (string roomSurface in new[] { "BonesFloor", "NorthWall", "SouthWall", "WestWall", "EastWall", "BonesCeiling" })
        {
            Material material = GameObject.Find(roomSurface).GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null, roomSurface);
            Assert.That(material.GetTexture("_NormalMap"), Is.Not.Null, roomSurface);
        }
    }

    [Test]
    public void DustStaysOutOfTheReadableDiceConeAndTableLightNeverFlickers()
    {
        GmBonesDustField[] dust = Object.FindObjectsByType<GmBonesDustField>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(dust.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(dust.All(field => field.IsOutsideDiceCone), Is.True);
        GameObject tableLight = GameObject.Find("BonesTableTaskLight");
        Assert.That(tableLight, Is.Not.Null);
        Assert.That(tableLight.GetComponent<GmPeriodLampFlicker>(), Is.Null);
        Assert.That(tableLight.GetComponent<GmLightIntent>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<GmPeriodLampFlicker>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.GreaterThan(0));
    }

    [Test]
    public void HostInputHudPresenterAudioAndTourAreConfigured()
    {
        GmBonesSceneHost host = Object.FindAnyObjectByType<GmBonesSceneHost>();
        Assert.That(host, Is.Not.Null);
        Assert.That(host.IsConfigured, Is.True);
        Assert.That(Object.FindAnyObjectByType<GmBonesInput>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmBonesHud>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmBonesPresenter>(), Is.Not.Null);
        GmBonesAudio audio = Object.FindAnyObjectByType<GmBonesAudio>();
        Assert.That(audio, Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmAudioManager>().GetComponent<GmAudioIntent>(), Is.Not.Null);
        Assert.That(audio.UsesSharedAudioManager, Is.True);
        Assert.That(Object.FindAnyObjectByType<GmAudioManager>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GmBonesPresenter>().SupportsAccessibility, Is.True);
        GmBonesShotTour tour = Object.FindAnyObjectByType<GmBonesShotTour>();
        Assert.That(tour, Is.Not.Null);
        Assert.That(tour.ShotCount, Is.EqualTo(10));
        Assert.That(tour.HasPlaceholderShots, Is.False);
    }

    [Test]
    public void TourAbortsWhenDeterministicReplayDoesNotReachPendingIntervention()
    {
        GmBonesSceneHost host = Object.FindAnyObjectByType<GmBonesSceneHost>();
        GmBonesInput input = Object.FindAnyObjectByType<GmBonesInput>();
        GmBonesShotTour tour = Object.FindAnyObjectByType<GmBonesShotTour>();
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
            fixtureHost = Object.FindAnyObjectByType<GmBonesSceneHost>();
            fixtureHost?.ActivateDirectReviewPersistence();
        }
    }

    [Test]
    public void DestroyingDirectReviewHostRestoresExactProductionBackendAndBytes()
    {
        fixtureHost?.ReleaseDirectReviewPersistence();
        GmSaveSystem.ResetTestConfiguration();
        var owner = new GameObject("BonesReviewLifetimeProof");
        GmBonesSceneHost host = owner.AddComponent<GmBonesSceneHost>();
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
            "GmSceneIntelligence", "bones-review-prior", "prior-save.json");
        GmSaveSystem.ConfigureForTests(priorPath, backend);
        var owner = new GameObject("BonesReviewBackendProof");
        GmBonesSceneHost host = owner.AddComponent<GmBonesSceneHost>();
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
            Assert.That(GmBonesQualityAudit.ValidateOpenScene(),
                Has.Some.Contains("player-facing primitive"));
        }
        finally { Object.DestroyImmediate(primitive); }
    }

    [Test]
    public void PublicReviewActionsReachBothLoadedSixOutcomesAndChallengeCorrectionIsSilent()
    {
        GmBonesSceneHost host = Object.FindAnyObjectByType<GmBonesSceneHost>();
        GmBonesInput input = Object.FindAnyObjectByType<GmBonesInput>();
        GmBonesAudio audio = Object.FindAnyObjectByType<GmBonesAudio>();
        Assert.That(host.RestartForReview(1), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        input.FocusThenConfirm(0); input.FocusThenConfirm(0); input.FocusThenConfirm(0);
        Assert.That(host.Controller.Phase, Is.EqualTo(GmBonesMatchPhase.AwaitingIntervention));
        Assert.That(GmBonesPresentationModel.Project(host.Controller).HasLoadedSixMarker, Is.True);
        Assert.That(audio.VisibleThrowRevision, Is.EqualTo(3));
        input.ChallengeAction();
        Assert.That(host.Controller.Phase, Is.EqualTo(GmBonesMatchPhase.Complete));
        Assert.That(audio.VisibleThrowRevision, Is.EqualTo(3),
            "Challenge correction must not replay the dice-roll sound");

        Assert.That(host.RestartForReview(1), Is.EqualTo(GmBonesInitializeResult.StartedNew));
        input.FocusThenConfirm(0); input.FocusThenConfirm(0); input.FocusThenConfirm(0);
        input.ConfirmAction();
        Assert.That(host.Controller.Phase, Is.EqualTo(GmBonesMatchPhase.Complete));
        Assert.That(host.Controller.Result, Is.EqualTo(GmBonesMatchResult.AldricWin));
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
