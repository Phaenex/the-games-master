using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class GmLabyrinthMazeTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void MazeGeneratorProduces7x7GridWithOpenEntranceShrineExit()
    {
        var mazeObj = new GameObject("TestMaze");
        var gen = mazeObj.AddComponent<GmLabyrinthGenerator>();

        gen.GenerateDeterministicMaze(42);

        // Entrance (0,0), Center Shrine (3,3), and Exit (6,6) must always be open (false)
        Assert.IsFalse(gen.WallGrid[0, 0], "Entrance (0,0) must be open");
        Assert.IsFalse(gen.WallGrid[3, 3], "Center Shrine (3,3) must be open");
        Assert.IsFalse(gen.WallGrid[6, 6], "Exit (6,6) must be open");

        // Internal obstructions must exist
        Assert.IsTrue(gen.WallGrid[1, 1], "Wall (1,1) should be present");
        Assert.IsTrue(gen.WallGrid[5, 5], "Wall (5,5) should be present");

        Object.DestroyImmediate(mazeObj);
    }

    [Test]
    public void HuntsmanLanternTellFlashesTriggerState()
    {
        var huntsmanObj = new GameObject("TestHuntsman");
        var ai = huntsmanObj.AddComponent<GmHuntsmanAI>();

        Assert.AreEqual(GmHuntsmanState.Patrolling, ai.State);
        Assert.IsFalse(ai.TellFlickerActive);

        ai.TriggerLanternTell();
        Assert.AreEqual(GmHuntsmanState.TellWindowOpen, ai.State);
        Assert.IsTrue(ai.TellFlickerActive);

        Object.DestroyImmediate(huntsmanObj);
    }

    [Test]
    public void EvadeOrDefendWithoutMirrorDodgesCharge()
    {
        var huntsmanObj = new GameObject("TestHuntsman");
        var ai = huntsmanObj.AddComponent<GmHuntsmanAI>();

        ai.TriggerLanternTell();
        bool ok = ai.EvadeOrDefend(hadMirror: false);

        Assert.IsTrue(ok);
        Assert.AreEqual(GmHuntsmanState.Patrolling, ai.State);
        Assert.IsTrue(GmRunStore.CheatsCaught.Contains("labyrinth-huntsman-evaded"));

        Object.DestroyImmediate(huntsmanObj);
    }

    [Test]
    public void EvadeOrDefendWithMirrorStunsHuntsmanAndAwardsSanity()
    {
        var huntsmanObj = new GameObject("TestHuntsman");
        var ai = huntsmanObj.AddComponent<GmHuntsmanAI>();

        // BeginNewRun() resets Sanity to 1.0 (its max, per Clamp01), leaving no room for a
        // positive delta to register. Spend some down first so the mirror-stun gain is observable.
        GmRunStore.ApplySanityDelta(-0.5f);
        float initialSanity = GmRunStore.Sanity;
        ai.TriggerLanternTell();
        bool ok = ai.EvadeOrDefend(hadMirror: true);

        Assert.IsTrue(ok);
        Assert.AreEqual(GmHuntsmanState.Stunned, ai.State);
        Assert.IsTrue(GmRunStore.CheatsCaught.Contains("labyrinth-huntsman-mirrored"));
        Assert.Greater(GmRunStore.Sanity, initialSanity);

        Object.DestroyImmediate(huntsmanObj);
    }
}
