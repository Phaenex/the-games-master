// Guards the save system's restore decision.
//
// SCOPE, stated because it is easy to misread this file's presence as "the prologue has saves": it
// does not. GmVillageSave is added only by GmVillageBuilder, the retired village builder, so no save
// component exists in WendHill_Prologue at all. What is tested here is the decision logic, which is
// shared and which is what can silently go wrong. Wiring saves into the prologue is a separate call.
//
// Both refusals fail silently by nature. Restoring a save from another scene drops the player at
// coordinates that mean something else here, usually inside geometry. Restoring one taken at the
// spawn moves nobody while every log line reads like a successful load. Neither raises an error, so
// neither would be noticed without a test.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendSaveRestoreTests
{
    const float MinDistance = 3f;

    [Test]
    public void ASaveFromAnotherSceneIsRefused()
    {
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.WrongScene,
            GmVillageSave.Decide("WendHillVillage", "WendHill_Prologue",
                new Vector3(100f, 0f, 100f), Vector3.zero, MinDistance));
    }

    [Test]
    public void AMissingOrEmptySceneNameIsRefusedRatherThanMatchingByAccident()
    {
        // A truncated or hand-edited save file deserialises to empty strings rather than null fields.
        // Treating "" as a match would restore any save into any scene.
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.WrongScene,
            GmVillageSave.Decide(null, "WendHill_Prologue", Vector3.one * 50f, Vector3.zero, MinDistance));
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.WrongScene,
            GmVillageSave.Decide("", "WendHill_Prologue", Vector3.one * 50f, Vector3.zero, MinDistance));
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.WrongScene,
            GmVillageSave.Decide("", "", Vector3.one * 50f, Vector3.zero, MinDistance));
    }

    [Test]
    public void ASaveTakenAtTheSpawnIsNotRestored()
    {
        // Teleporting the player two metres sideways on load is worse than doing nothing: it looks
        // like the save worked while losing wherever they actually were.
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.AtTheSpawn,
            GmVillageSave.Decide("WendHill_Prologue", "WendHill_Prologue",
                new Vector3(1f, 0f, 1f), Vector3.zero, MinDistance));
    }

    [Test]
    public void ASaveFurtherThanTheThresholdIsRestored()
    {
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.Restore,
            GmVillageSave.Decide("WendHill_Prologue", "WendHill_Prologue",
                new Vector3(0f, 0f, 200f), Vector3.zero, MinDistance));
    }

    [Test]
    public void TheSceneGuardIsCheckedBeforeTheDistanceGuard()
    {
        // Order matters. A save from another scene that happens to sit near this scene's spawn must
        // still be refused for being the wrong scene, not waved through as "nothing to restore" and
        // then treated as a valid session.
        Assert.AreEqual(
            GmVillageSave.RestoreVerdict.WrongScene,
            GmVillageSave.Decide("SomewhereElse", "WendHill_Prologue",
                Vector3.zero, Vector3.zero, MinDistance));
    }
}
