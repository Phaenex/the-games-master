using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

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
}
