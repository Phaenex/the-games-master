using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Motivated lighting harmonization and real-time shadow budget governor.
/// Ensures consistent aesthetic color grading (Kelvin color temperatures, lumen intensities)
/// and prevents GPU render-stall regressions by strictly capping active shadow casters in the viewport.
/// </summary>
public static class GmLightingEngine
{
    // Color Palettes
    public static readonly Color LanternAmber = new Color(1.0f, 0.52f, 0.18f);   // 2100K Warm Lantern / Pier Sconce
    public static readonly Color MoonlightSlate = new Color(0.24f, 0.32f, 0.44f); // 6800K Cold Nocturnal Wash
    public static readonly Color HearthWarm = new Color(0.96f, 0.44f, 0.12f);     // 1800K Fireplace / Hearth Glow
    public static readonly Color ParlorCandle = new Color(0.98f, 0.62f, 0.28f);   // 2400K Candlelight

    public const int MaxConcurrentShadowCasters = 4;

    /// <summary>
    /// Applies an authored warm lantern preset to a point light.
    /// </summary>
    public static void ApplyLanternPreset(Light light, HDAdditionalLightData hd, float intensityLumens = 420f, float range = 18f, bool castShadows = false)
    {
        if (light == null) return;

        light.type = LightType.Point;
        light.color = LanternAmber;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

        if (hd != null)
        {
            hd.lightUnit = LightUnit.Lumen;
            hd.intensity = intensityLumens;
            hd.range = range;
            hd.affectsVolumetric = false;
        }
    }

    /// <summary>
    /// Applies a cold nocturnal moonlight preset.
    /// </summary>
    public static void ApplyMoonlightPreset(Light light, HDAdditionalLightData hd, float intensityLumens = 180f, float range = 40f)
    {
        if (light == null) return;

        light.type = LightType.Point;
        light.color = MoonlightSlate;
        light.shadows = LightShadows.None;

        if (hd != null)
        {
            hd.lightUnit = LightUnit.Lumen;
            hd.intensity = intensityLumens;
            hd.range = range;
            hd.affectsVolumetric = true;
        }
    }

    /// <summary>
    /// Dynamically enforces shadow budget around the viewer.
    /// Selects the closest N lights to cast soft shadows; demotes all distant lights to shadow-less fills.
    /// </summary>
    public static int EnforceShadowBudget(Vector3 viewerPosition, IEnumerable<Light> lights, int maxShadowCasters = MaxConcurrentShadowCasters)
    {
        if (lights == null) return 0;

        var activePointLights = lights
            .Where(l => l != null && l.enabled && l.type == LightType.Point)
            .OrderBy(l => (l.transform.position - viewerPosition).sqrMagnitude)
            .ToList();

        int shadowCastersCount = 0;

        for (int i = 0; i < activePointLights.Count; i++)
        {
            Light l = activePointLights[i];
            if (i < maxShadowCasters && l.shadows != LightShadows.None)
            {
                l.shadows = LightShadows.Soft;
                shadowCastersCount++;
            }
            else
            {
                l.shadows = LightShadows.None;
            }
        }

        return shadowCastersCount;
    }
}
