using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GmSceneDirectorTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void ScenePathsMatchProductionLocations()
    {
        Assert.AreEqual("Assets/Scenes/WendHill_Prologue.unity", GmSceneDirector.PrologueScenePath);
        Assert.AreEqual("Assets/Scenes/EntryHall.unity", GmSceneDirector.EntryHallScenePath);
        Assert.AreEqual("Assets/Scenes/Parlor.unity", GmSceneDirector.ParlorScenePath);
        Assert.AreEqual("Assets/Scenes/ShutTheBox.unity", GmSceneDirector.ShutTheBoxScenePath);
        Assert.AreEqual("Assets/Scenes/Court.unity", GmSceneDirector.CourtScenePath);
        Assert.AreEqual("Assets/Scenes/HiddenRoom.unity", GmSceneDirector.HiddenRoomScenePath);
        Assert.AreEqual("Assets/Scenes/Labyrinth.unity", GmSceneDirector.LabyrinthScenePath);
    }

    [Test]
    public void SceneDirectorResolvesCorrectEnding()
    {
        var directorObj = new GameObject("TestDirector");
        var director = directorObj.AddComponent<GmSceneDirector>();

        // Default run resolves TrappedLoop
        Assert.AreEqual(GmEndingType.TrappedLoop, director.ResolveAndShowEnding());

        // Full golden run resolves TrueEscape
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(1);
        GmRunStore.CollectShard(2);
        for (int i = 1; i <= 8; i++) GmRunStore.RecordCatch($"catch-{i}");

        Assert.AreEqual(GmEndingType.TrueEscape, director.ResolveAndShowEnding());

        Object.DestroyImmediate(directorObj);
    }

    [Test]
    public void SceneTransitionTriggerSetsTarget()
    {
        var triggerObj = new GameObject("TestTrigger");
        var trigger = triggerObj.AddComponent<GmSceneTransitionTrigger>();
        trigger.TargetSceneId = "parlor";
        trigger.TargetScenePath = GmSceneDirector.ParlorScenePath;

        Assert.IsFalse(trigger.IsTriggered);
        trigger.TriggerTransition();
        Assert.IsTrue(trigger.IsTriggered);

        Object.DestroyImmediate(triggerObj);
    }

    [Test]
    public void ATriggerWithNoDirectorHoldsItsExitInsteadOfDroppingIt()
    {
        var triggerObj = new GameObject("TestTrigger");
        var trigger = triggerObj.AddComponent<GmSceneTransitionTrigger>();
        trigger.TargetSceneId = "parlor";
        trigger.TargetScenePath = GmSceneDirector.ParlorScenePath;
        FieldInfo pendingField = typeof(GmSceneTransitionTrigger).GetField("pending",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(pendingField, "the trigger no longer holds a transition it could not hand off");
        try
        {
            Assert.IsFalse((bool)pendingField.GetValue(trigger));
            trigger.TriggerTransition();
            Assert.IsTrue((bool)pendingField.GetValue(trigger),
                "IsTriggered is a one-shot, so an unheld transition is the player's exit lost for good");

            // Stand in for a completed hand-off, then fire again. The one-shot guard must swallow the
            // second call; without it an overlapping trigger volume re-queues a transition already made.
            pendingField.SetValue(trigger, false);
            trigger.TriggerTransition();
            Assert.IsFalse((bool)pendingField.GetValue(trigger),
                "a second entry re-queued a transition the trigger had already handed off");
        }
        finally
        {
            Object.DestroyImmediate(triggerObj);
        }
    }

    [Test]
    public void ADirectorStartsInThePrologueAndNotMidTransition()
    {
        var directorObj = new GameObject("TestDirector");
        var director = directorObj.AddComponent<GmSceneDirector>();

        Assert.AreEqual("wend-hill-prologue", director.CurrentSceneId);
        Assert.IsFalse(director.IsTransitioning);

        Object.DestroyImmediate(directorObj);
    }

    // Not covered here, and not coverable here: TransitionTo/TransitionRoutine, the IsTransitioning
    // reentrancy guard and the OnTransitionStarted/OnTransitionCompleted pair. All of it runs inside
    // a coroutine that loads a scene, which needs the player loop. It belongs in the PlayMode suite
    // (unity/project/Assets/Tests/PlayMode), not in EditMode.
}
