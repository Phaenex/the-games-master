using NUnit.Framework;
using UnityEngine;

public sealed class GmEndingTriggerTests
{
    [SetUp]
    public void SetUp() => GmRunStore.BeginNewRun();

    [Test]
    public void CrossingTheLabyrinthGateBanksTheRoomBlocksControlAndShowsTheResolvedEnding()
    {
        var curtainHost = new GameObject("Curtain");
        var curtain = curtainHost.AddComponent<GmSceneCurtain>();
        var exit = new GameObject("EndingExit");
        var playerObject = new GameObject("Player");
        var player = playerObject.AddComponent<GmPlayer>();
        try
        {
            GmRunStore.RecordDefiance();
            var trigger = exit.AddComponent<GmEndingTrigger>();
            GmEndingType result = trigger.TriggerEnding(player);

            Assert.AreEqual(GmEndingType.DefiantSacrifice, result);
            Assert.AreEqual(result, trigger.ResolvedEnding);
            Assert.IsTrue(trigger.IsTriggered);
            Assert.IsTrue(GmRunStore.IsRoomComplete("labyrinth"));
            Assert.AreEqual("ending", GmRunStore.LastCheckpoint);
            Assert.IsTrue(player.ControlBlocked);
            Assert.IsTrue(curtain.IsRaised);
            StringAssert.Contains(GmEndingManager.GetEndingTitle(result), curtain.Card);
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(exit);
            Object.DestroyImmediate(curtainHost);
        }
    }

    [Test]
    public void EndingTriggerIsOneShot()
    {
        var exit = new GameObject("EndingExit");
        try
        {
            var trigger = exit.AddComponent<GmEndingTrigger>();
            GmEndingType first = trigger.TriggerEnding(null);
            GmRunStore.RecordDefiance();
            GmEndingType second = trigger.TriggerEnding(null);
            Assert.AreEqual(first, second, "standing in the volume re-resolved a different ending");
        }
        finally { Object.DestroyImmediate(exit); }
    }
}
