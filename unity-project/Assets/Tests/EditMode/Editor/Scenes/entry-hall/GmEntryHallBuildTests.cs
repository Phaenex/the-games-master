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

    [Test]
    public void TheHallHasAWayToTheTable()
    {
        // Phase A's last missing link. Every scene was in the build and every room had a player, and
        // there was still no way from this room into a single game -- GmSceneTransitionTrigger existed,
        // was unit-tested, and was placed in ZERO scenes.
        var trigger = Object.FindAnyObjectByType<GmSceneTransitionTrigger>(FindObjectsInactive.Include);
        Assert.IsNotNull(trigger, "the Entry Hall has no exit — the player wakes in the house and stays there");

        Assert.AreEqual(GmParlorBuilder.SceneId, trigger.TargetSceneId,
            "the hall's exit does not lead to the first game");
        // Compared against the builder's own constant rather than a literal, so renaming the scene
        // cannot leave a path string here that loads nothing.
        Assert.AreEqual(GmParlorBuilder.ScenePath, trigger.TargetScenePath,
            "the hall's exit names a scene path the Parlor builder does not write");

        var box = trigger.GetComponent<Collider>();
        Assert.IsNotNull(box, "the transition has no collider, so nothing can enter it");
        Assert.IsTrue(box.isTrigger, "the transition volume is SOLID — it would block the doorway it is in");
    }

    [Test]
    public void TheDoorwayVolumeIsInsideTheOpeningAndNotAcrossTheRoom()
    {
        // A transition volume big enough to catch someone crossing the hall would take the player out
        // of the room on the way to the staircase, which reads as the game hijacking a walk.
        var trigger = Object.FindAnyObjectByType<GmSceneTransitionTrigger>(FindObjectsInactive.Include);
        Assert.IsNotNull(trigger);
        Bounds bounds = trigger.GetComponent<Collider>().bounds;

        Assert.Less(bounds.size.x, 1.5f, "the exit volume reaches out into the hall");
        Assert.Greater(bounds.center.x, 4.5f, "the exit volume is not against the east wall");
        Assert.Less(bounds.min.y, 1f, "the volume floats above the floor — a walking player passes under it");
    }
}
