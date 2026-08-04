// Small deterministic practical-light motion. This is deliberately restrained: the light should
// feel flame-fed when the player walks past, never pulse like an alarm or advertise a game effect.
using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class GmLightFlicker : MonoBehaviour
{
    [Min(0f)] public float baseIntensity = 20f;
    [Range(0f, 0.45f)] public float variation = 0.15f;
    [Min(0.05f)] public float speed = 2.2f;

    Light source;
    float phase;

    void Awake()
    {
        source = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = source.intensity;
        phase = Mathf.Abs(transform.position.x * 0.731f + transform.position.z * 0.193f);
    }

    void Update()
    {
        if (source == null) return;
        float slow = Mathf.PerlinNoise(phase, Time.unscaledTime * speed);
        float fast = Mathf.PerlinNoise(phase + 13.7f, Time.unscaledTime * speed * 2.9f);
        float signed = ((slow * 0.72f + fast * 0.28f) - 0.5f) * 2f;
        source.intensity = baseIntensity * (1f + signed * variation);
    }
}

