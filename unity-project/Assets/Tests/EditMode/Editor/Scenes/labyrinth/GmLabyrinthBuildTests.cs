using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GmLabyrinthBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmLabyrinthBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmLabyrinthQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Labyrinth scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmLabyrinthShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmLabyrinthBuilder.SceneId, Object.FindAnyObjectByType<GmLabyrinthShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Labyrinth composition failed:\n- " + string.Join("\n- ", issues));
    }
}
