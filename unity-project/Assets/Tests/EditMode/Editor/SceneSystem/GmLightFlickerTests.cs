using NUnit.Framework;
using UnityEngine;

public sealed class GmLightFlickerTests
{
    [Test]
    public void RuntimePassAnimatesFlamePracticalsButLeavesNavigationAndMoonlightStable()
    {
        var sconce = new GameObject("GallerySconceTest");
        var door = new GameObject("CourtDoorSconceTest");
        var moon = new GameObject("NorthMoonWindowTest");
        Light sconceLight = sconce.AddComponent<Light>();
        door.AddComponent<Light>();
        moon.AddComponent<Light>();
        sconceLight.intensity = 31f;
        try
        {
            int added = GmLightFlicker.EnsureLoadedScenePracticals();

            GmLightFlicker motion = sconce.GetComponent<GmLightFlicker>();
            Assert.That(added, Is.GreaterThanOrEqualTo(1));
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.baseIntensity, Is.EqualTo(31f));
            Assert.That(door.GetComponent<GmLightFlicker>(), Is.Null);
            Assert.That(moon.GetComponent<GmLightFlicker>(), Is.Null);

            sconceLight.intensity = 29f; // Simulate sampling the light during its animation.
            Assert.That(GmLightFlicker.EnsureLoadedScenePracticals(), Is.EqualTo(0));
            Assert.That(motion.baseIntensity, Is.EqualTo(31f),
                "revisiting another scene rebased a persistent practical from a flickered sample");
        }
        finally
        {
            Object.DestroyImmediate(sconce);
            Object.DestroyImmediate(door);
            Object.DestroyImmediate(moon);
        }
    }
}
