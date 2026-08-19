using NUnit.Framework;
using UnityEngine;

public class GmInteriorDustRuntimeTests
{
    [TestCase("EntryHall")]
    [TestCase("Parlor")]
    [TestCase("ShutTheBox")]
    [TestCase("Court")]
    [TestCase("HiddenRoom")]
    public void EveryInteriorSceneHasABoundedDustPlacement(string sceneName)
    {
        Assert.IsTrue(GmInteriorDustRuntime.TryGetPlacement(sceneName,
            out Vector3 position, out Vector3 size));
        Assert.That(position.y, Is.InRange(0.8f, 1.5f));
        Assert.That(size.x, Is.InRange(1f, 6f));
        Assert.That(size.y, Is.InRange(1f, 3f));
        Assert.That(size.z, Is.InRange(1f, 6f));
    }

    [Test]
    public void ExteriorAndBootDoNotReceiveInteriorDust()
    {
        Assert.IsFalse(GmInteriorDustRuntime.TryGetPlacement("WendHill_Prologue", out _, out _));
        Assert.IsFalse(GmInteriorDustRuntime.TryGetPlacement("Boot", out _, out _));
    }
}
