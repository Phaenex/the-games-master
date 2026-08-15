// Generated minimum gates for Entry Hall. Add tests for every regression found.
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GmEntryHallBuildTests
{
    [OneTimeSetUp]
    public void BuildOnce() => GmEntryHallBuilder.Build();

    // Build() leaves its built scene active (NewSceneMode.Single) with no teardown of its own.
    // Without this, its colliders/renderers survive into every EditMode fixture that runs after
    // it in the same batch, corrupting unrelated raycasts (e.g. GmWendRouteGroundTests).
    [OneTimeTearDown]
    public void TearDownOnce() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
}
