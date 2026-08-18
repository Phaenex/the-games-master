using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public class GmShutTheBoxBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmShutTheBoxBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmShutTheBoxQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Shut the Box scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmShutTheBoxShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmShutTheBoxBuilder.SceneId, Object.FindAnyObjectByType<GmShutTheBoxShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Shut the Box composition failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void MatchCompletionOpensTheOrdinaryExitToTheLabyrinth()
    {
        GmSequenceExit exit = ExitTo(GmLabyrinthBuilder.SceneId);
        var trigger = exit.GetComponentInChildren<GmSceneTransitionTrigger>(true);
        Assert.AreEqual(GmLabyrinthBuilder.ScenePath, trigger.TargetScenePath);
        Assert.IsFalse(trigger.GetComponent<Collider>().enabled);

        GmRunStore.CompleteRoom(GmShutTheBoxBuilder.SceneId, countsAsTableGame: true);
        Assert.IsTrue(exit.IsUnlocked);
        Assert.IsTrue(trigger.GetComponent<Collider>().enabled);
    }

    [Test]
    public void OnlyTheTileNineCatchOpensThePhysicalHiddenRoomPassage()
    {
        GmSequenceExit exit = ExitTo(GmHiddenRoomBuilder.SceneId);
        var trigger = exit.GetComponentInChildren<GmSceneTransitionTrigger>(true);
        Assert.AreEqual(GmHiddenRoomBuilder.ScenePath, trigger.TargetScenePath);
        Assert.AreEqual(GmShutTheBoxBuilder.SceneId, trigger.RequiredCompletedRoomId,
            "the hidden-room load can discard a live, unfinished match");
        Assert.IsFalse(trigger.GetComponent<Collider>().enabled);

        // Finishing the match alone must not hand every player the secret.
        GmRunStore.CompleteRoom(GmShutTheBoxBuilder.SceneId, countsAsTableGame: true);
        Assert.IsFalse(exit.IsUnlocked);
        Assert.IsFalse(trigger.GetComponent<Collider>().enabled);

        GmRunStore.RecordCatch("stb-tile-9-door-latch");
        Assert.IsTrue(exit.IsUnlocked);
        Assert.IsTrue(trigger.GetComponent<Collider>().enabled);
    }

    [Test]
    public void FocalPropsAndTheTableLightAreActuallyRenderableInHdrp()
    {
        foreach (string name in new[] { "PanelDoor", "AlcoveBook_1", "TableCandle" })
        {
            GameObject prop = GameObject.Find(name);
            Assert.IsNotNull(prop, $"{name} is missing");
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    Assert.AreEqual("HDRP/Lit", material.shader.name,
                        $"{name}/{renderer.name} would render with an incompatible material");
        }

        GameObject lampObject = GameObject.Find("TableLampLight");
        Assert.IsNotNull(lampObject);
        Light lamp = lampObject.GetComponent<Light>();
        Assert.AreEqual("Lumen", lamp.lightUnit.ToString());
        Assert.AreEqual(GmInteriorAtmosphere.PracticalCeilingLumens, lamp.intensity, 0.01f,
            "the shared fixed-exposure atmosphere did not enforce the period practical ceiling");
        Vector3 subject = (new Vector3(0f, 0.82f, 0f) - lampObject.transform.position).normalized;
        Assert.Greater(Vector3.Dot(lampObject.transform.forward, subject), 0.995f,
            "the table spotlight illuminates the north wall instead of the game");

        GameObject onwardFixtures = GameObject.Find("OnwardDoorSconces");
        GameObject onwardLightObject = GameObject.Find("OnwardDoorSconceLight");
        Assert.IsNotNull(onwardFixtures, "the Labyrinth passage has no visible period light source");
        Assert.AreEqual(2, onwardFixtures.transform.childCount);
        Assert.IsNotNull(onwardLightObject);
        Assert.AreEqual(GmInteriorAtmosphere.PracticalCeilingLumens,
            onwardLightObject.GetComponent<Light>().intensity, 0.01f);
    }

    static GmSequenceExit ExitTo(string sceneId)
    {
        GmSequenceExit[] exits = Object.FindObjectsByType<GmSequenceExit>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        GmSequenceExit result = exits.FirstOrDefault(exit =>
            exit.GetComponentInChildren<GmSceneTransitionTrigger>(true)?.TargetSceneId == sceneId);
        Assert.IsNotNull(result, $"Shut the Box has no physical exit to {sceneId}");
        return result;
    }
}
