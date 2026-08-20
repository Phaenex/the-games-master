using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GmBonesBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmBonesBuilder.Build();

    [OneTimeTearDown]
    public void TearDownOnce() =>
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
        Assert.That(audio.GetComponent<GmAudioIntent>(), Is.Not.Null);
        GmBonesShotTour tour = Object.FindAnyObjectByType<GmBonesShotTour>();
        Assert.That(tour, Is.Not.Null);
        Assert.That(tour.ShotCount, Is.EqualTo(10));
        Assert.That(tour.HasPlaceholderShots, Is.False);
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
}
