// Generated minimum gates for Boot. Add tests for every regression found.
using NUnit.Framework;
using UnityEngine;

public class GmBootBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmBootBuilder.Build();

    [Test]
    public void SceneContractPasses()
    {
        var issues = GmBootQualityAudit.ValidateOpenScene();
        Assert.IsEmpty(issues, "Boot scene contract failed:\n- " + string.Join("\n- ", issues));
    }

    [Test]
    public void ReviewTourHasNoPlaceholderShotNames()
    {
        var tour = Object.FindAnyObjectByType<GmBootShotTour>();
        Assert.IsNotNull(tour, "review tour component is missing");
        Assert.IsFalse(tour.HasPlaceholderShots,
            "replace every generated '*-replace-me' waypoint before this scene can pass");
        Assert.That(tour.ShotCount, Is.EqualTo(7),
            "Boot recovery states are missing from the rendered evidence contract");
        Assert.That(tour.UsesBackbufferCaptureForAudit, Is.True,
            "camera capture cannot see the screen-space Boot UI");
    }

    [Test]
    public void AuthoredCompositionContractPasses()
    {
        var issues = GmSceneCompositionAudit.ValidateOpenScene(
            GmBootBuilder.SceneId, Object.FindAnyObjectByType<GmBootShotTour>(), Camera.main);
        Assert.IsEmpty(issues, "Boot composition failed:\n- " + string.Join("\n- ", issues));
    }
}
