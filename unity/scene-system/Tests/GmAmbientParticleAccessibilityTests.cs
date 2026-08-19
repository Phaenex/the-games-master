using NUnit.Framework;
using UnityEngine;

public class GmAmbientParticleAccessibilityTests
{
    [SetUp]
    public void SetUp() => GmAccessibilitySettings.SetReducedMotion(false);

    [TearDown]
    public void TearDown() => GmAccessibilitySettings.SetReducedMotion(false);

    [Test]
    public void ReducedMotionStopsAndClearsDecorativeParticles()
    {
        var host = new GameObject("DustTest");
        try
        {
            ParticleSystem particles = host.AddComponent<ParticleSystem>();
            GmAmbientParticleAccessibility accessibility =
                host.AddComponent<GmAmbientParticleAccessibility>();
            particles.Play();

            GmAccessibilitySettings.SetReducedMotion(true);
            accessibility.Apply();

            Assert.IsFalse(particles.emission.enabled);
            Assert.IsFalse(particles.isPlaying);

            GmAccessibilitySettings.SetReducedMotion(false);
            accessibility.Apply();
            Assert.IsTrue(particles.emission.enabled);
            Assert.IsTrue(particles.isPlaying);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
