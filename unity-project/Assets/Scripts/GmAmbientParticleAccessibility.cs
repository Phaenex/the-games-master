using UnityEngine;

/// <summary>
/// Keeps decorative room particles subordinate to the player's reduced-motion setting.
/// Gameplay VFX do not use this component.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmAmbientParticleAccessibility : MonoBehaviour
{
    ParticleSystem[] particles;

    void OnEnable()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
        GmAccessibilitySettings.OnChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        GmAccessibilitySettings.OnChanged -= Apply;
    }

    public void Apply()
    {
        if (particles == null) particles = GetComponentsInChildren<ParticleSystem>(true);
        bool enabled = !GmAccessibilitySettings.ReducedMotion;
        foreach (ParticleSystem particle in particles)
        {
            var emission = particle.emission;
            emission.enabled = enabled;
            if (enabled)
            {
                if (!particle.isPlaying) particle.Play();
            }
            else
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
