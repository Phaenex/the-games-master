using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GmHiddenRoomBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmHiddenRoomBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmHiddenRoomQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Hidden Room scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmHiddenRoomShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmHiddenRoomBuilder.SceneId, Object.FindAnyObjectByType<GmHiddenRoomShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Hidden Room composition failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void TheSecretRoomHasAnOpenPhysicalWayBackIntoTheNight()
    {
        var trigger = Object.FindAnyObjectByType<GmSceneTransitionTrigger>(FindObjectsInactive.Include);
        Assert.IsNotNull(trigger, "entering the Hidden Room is a one-way soft lock");
        Assert.AreEqual(GmLabyrinthBuilder.SceneId, trigger.TargetSceneId);
        Assert.AreEqual(GmLabyrinthBuilder.ScenePath, trigger.TargetScenePath);
        Assert.AreEqual(GmHiddenRoomBuilder.SceneId, trigger.CompleteRoomOnTransitionId);
        Assert.IsTrue(trigger.GetComponent<Collider>().enabled);
        Assert.IsTrue(trigger.GetComponent<Collider>().isTrigger);
    }

    [Test]
    public void EveryAuthoredRoomLightCarriesItsHdrpLumenValue()
    {
        foreach (string name in new[] { "DoorSconceLight", "DeskLanternLight", "MirrorColdLight", "ShelfSconceLight" })
        {
            GameObject lightObject = GameObject.Find(name);
            Assert.IsNotNull(lightObject, $"{name} is missing");
            Light light = lightObject.GetComponent<Light>();
            Assert.AreEqual("Lumen", light.lightUnit.ToString(), $"{name} fell back to an implicit HDRP unit");
            Assert.Greater(light.intensity, 0f, $"{name} has no authored intensity");
        }

        GameObject mirrorLightObject = GameObject.Find("MirrorColdLight");
        Assert.AreEqual(8f, mirrorLightObject.GetComponent<Light>().intensity, 0.01f,
            "the supernatural mirror accent is bright enough to blow out the mirror at fixed exposure");
        Assert.Greater(Vector3.Distance(mirrorLightObject.transform.position,
            GameObject.Find("StandingMirrorFrame").transform.position), 0.8f,
            "the mirror accent is sitting inside the reflective frame");
        Assert.AreEqual(GmInteriorAtmosphere.PracticalCeilingLumens,
            GameObject.Find("DoorSconceLight").GetComponent<Light>().intensity, 0.01f,
            "the return passage has no period-limited practical");
    }
}
