using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Simulates organic Victorian gas / paraffin lantern flame flickering with multi-frequency
/// noise and occasional draft dips.
/// </summary>
public sealed class GmPeriodLampFlicker : MonoBehaviour
{
    public float baseIntensity = 180f;
    public float flickerSpeed = 3.5f;
    public float flickerAmount = 0.15f;
    public float dipChance = 0.04f;

    Light targetLight;
    HDAdditionalLightData hdLight;
    float seed;
    float currentDip = 0f;

    void Awake()
    {
        targetLight = GetComponent<Light>();
        hdLight = GetComponent<HDAdditionalLightData>();
        if (targetLight != null && baseIntensity <= 0f)
            baseIntensity = targetLight.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (targetLight == null && hdLight == null) return;

        float time = Time.time * flickerSpeed + seed;
        float noise = Mathf.PerlinNoise(time, 0f) * 0.7f + Mathf.PerlinNoise(time * 2.3f, 10f) * 0.3f;
        float fluctuation = (noise - 0.5f) * 2f * flickerAmount;

        if (Random.value < dipChance * Time.deltaTime * 60f)
            currentDip = Random.Range(0.1f, 0.25f);
        currentDip = Mathf.MoveTowards(currentDip, 0f, Time.deltaTime * 1.5f);

        float intensity = baseIntensity * (1f + fluctuation - currentDip);
        intensity = Mathf.Max(0.1f, intensity);

        if (targetLight != null) targetLight.intensity = intensity;
        if (hdLight != null) hdLight.intensity = intensity;
    }
}
