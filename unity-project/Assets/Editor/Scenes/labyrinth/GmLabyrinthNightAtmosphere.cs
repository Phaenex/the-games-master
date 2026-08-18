using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmLabyrinthNightAtmosphere
{
    public const float ExposureEV = 0.30f;
    public const float MoonLux = 10f;
    public const float FogMeanFreePath = 38f;

    public static GameObject Apply(Transform parent, GmLabyrinthSceneParts parts)
    {
        const string directory = "Assets/Scenes/Generated";
        const string path = directory + "/GmNight_labyrinth.asset";
        Directory.CreateDirectory(directory);

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        Exposure exposure = GetOrAdd<Exposure>(profile);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(ExposureEV);

        VisualEnvironment environment = GetOrAdd<VisualEnvironment>(profile);
        environment.skyType.Override((int)SkyType.Gradient);
        environment.cloudType.Override(0);
        environment.skyAmbientMode.Override(SkyAmbientMode.Static);

        GradientSky sky = GetOrAdd<GradientSky>(profile);
        sky.top.Override(new Color(0.010f, 0.014f, 0.030f));
        sky.middle.Override(new Color(0.038f, 0.046f, 0.072f));
        sky.bottom.Override(new Color(0.026f, 0.031f, 0.046f));
        sky.gradientDiffusion.Override(3.5f);
        sky.exposure.Override(0f);

        IndirectLightingController indirect = GetOrAdd<IndirectLightingController>(profile);
        indirect.indirectDiffuseLightingMultiplier.Override(0.82f);
        indirect.reflectionLightingMultiplier.Override(0.4f);

        Fog fog = GetOrAdd<Fog>(profile);
        fog.enabled.Override(true);
        fog.meanFreePath.Override(FogMeanFreePath);
        fog.colorMode.Override(FogColorMode.ConstantColor);
        fog.color.Override(new Color(0.025f, 0.032f, 0.052f));
        fog.tint.Override(new Color(0.36f, 0.43f, 0.62f));
        fog.albedo.Override(new Color(0.36f, 0.43f, 0.58f));
        fog.baseHeight.Override(0f);
        fog.maximumHeight.Override(8f);
        fog.anisotropy.Override(0.5f);
        fog.depthExtent.Override(60f);

        Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments colour = GetOrAdd<ColorAdjustments>(profile);
        colour.postExposure.Override(0f);
        colour.contrast.Override(12f);
        colour.saturation.Override(-8f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var host = new GameObject("Atmosphere");
        host.transform.SetParent(parent, false);
        Volume volume = host.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;

        var moonObject = new GameObject("LabyrinthMoon");
        moonObject.transform.SetParent(parent, false);
        moonObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        Light moon = moonObject.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.44f, 0.56f, 0.82f);
        moon.lightUnit = LightUnit.Lux;
        moon.intensity = MoonLux;
        moon.shadows = LightShadows.Soft;
        moonObject.AddComponent<HDAdditionalLightData>();
        parts.EnvironmentMoonLight = moon;

        return host;
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;
        component = profile.Add<T>(true);
        component.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }
}
