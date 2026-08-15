using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmParlorShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmParlorBuilder.SceneId, Object.FindAnyObjectByType<GmParlorShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Parlor composition failed:\n- " + string.Join("\n- ", issues));
    }
}
