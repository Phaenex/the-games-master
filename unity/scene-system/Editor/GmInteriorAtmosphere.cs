// The reason the rooms did not look like they belonged in the same game as the prologue.
//
// Counted 2026-08-15: GmWendNight carries 31 references to Volume/Exposure/VolumeProfile. Every one
// of the six room builders carried ZERO. Not a different style -- no atmosphere at all. With no
// Exposure override HDRP falls back to automatic exposure, which compensates for a dim room by
// opening up until it is bright, so the Entry Hall rendered as a white box with faint wallpaper on
// it while the drive outside was a moonlit night at a fixed 0.3 EV.
//
// That is the same trap the prologue already documented in GmWendNight: "dimming the sun alone would
// not darken anything: auto-exposure simply compensates and hands back the same brightness. The
// override has to be added." The rooms never got the override.
//
// One profile per scene, written beside the scene so it is build output rather than a hand-edited
// asset, and every room asks for the same numbers so a lamp reads the same brightness in the Parlor
// as it does in the hall.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmInteriorAtmosphere
{
    /// The same fixed exposure the committed night uses outdoors.
    ///
    /// Deliberately identical rather than "tuned for interiors": the player walks from the drive into
    /// the hall, and if the two were exposed differently the crossing would read as a cut to a
    /// different film. Rooms get their mood from their lamps, not from the camera being opened up.
    public const float InteriorExposureEV = 0.30f;

    /// Distance at which interior fog fully occludes. Long enough that a 20m hall keeps its far end,
    /// short enough that the air in the room is visible at all -- a lamp with no medium to scatter in
    /// looks like a decal, which is most of what made these rooms read as boxes.
    public const float FogMeanFreePath = 28f;

    /// The prologue's own ceiling for a practical, reused rather than re-picked.
    public const float PracticalCeilingLumens = 200f;

    /// Adds a global volume to the open scene and returns it.
    public static GameObject Apply(Transform parent, string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId)) throw new ArgumentException("sceneId is required", nameof(sceneId));

        string directory = "Assets/Scenes/Generated";
        Directory.CreateDirectory(directory);
        string path = $"{directory}/GmInterior_{sceneId}.asset";

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        Configure(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Half a fix is worse than none here. Pinning exposure without touching the lights blew the
        // Entry Hall to p05=250 / p95=255 -- pure white -- because those intensities were chosen
        // implicitly against AUTOMATIC exposure, which had been quietly compensating for them. The
        // prologue does both halves and says so: practicals scaled by 0.06 and clamped, THEN a fixed
        // EV. Rooms get the same ceiling so a lamp reads the same indoors as it does on the drive.
        int clamped = 0;
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type == LightType.Directional) continue;
            // Light.intensity, not HDAdditionalLightData.intensity. The HD properties are deprecated
            // from 2023.3 and the first version of this read lightUnit off the deprecated one, matched
            // nothing, and clamped zero lights while reporting success. The builders set Light
            // .intensity directly, so this compares like with like.
            if (light.intensity <= PracticalCeilingLumens) continue;
            Debug.Log($"[GmInteriorAtmosphere] '{light.name}' {light.intensity:0} -> {PracticalCeilingLumens}");
            light.intensity = PracticalCeilingLumens;
            EditorUtility.SetDirty(light);
            clamped++;
        }

        var host = new GameObject("Atmosphere");
        if (parent != null) host.transform.SetParent(parent, false);
        var volume = host.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
        Debug.Log($"[GmInteriorAtmosphere] '{sceneId}' fixed {InteriorExposureEV} EV, fog {FogMeanFreePath}m, {clamped} light(s) clamped -> {path}");
        return host;
    }

    static void Configure(VolumeProfile profile)
    {
        // Without this the component lives only in memory and vanishes on reload, leaving a profile
        // that silently overrides nothing. Learned outdoors first; it costs a whole rebuild to spot.
        if (!profile.TryGet(out Exposure exposure))
        {
            exposure = profile.Add<Exposure>(true);
            exposure.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(exposure, profile);
        }
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(InteriorExposureEV);

        if (!profile.TryGet(out Fog fog))
        {
            fog = profile.Add<Fog>(true);
            fog.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(fog, profile);
        }
        fog.enabled.Override(true);
        fog.meanFreePath.Override(FogMeanFreePath);
        // Constant, NOT the default SkyColor. These rooms have no sky override, so sky-coloured fog
        // samples HDRP's default bright sky and floods a windowless interior with daylight -- the
        // first version of this file did exactly that and took the Entry Hall from washed-out to
        // p05=250, p95=255, a frame that is pure white with 13.6% clipped. Indoors there is no sky
        // to take a colour from, so the fog has to be told one.
        fog.colorMode.Override(FogColorMode.ConstantColor);
        fog.color.Override(new Color(0.055f, 0.062f, 0.080f));
        // Cool, and quietly. This is the one cool element in a room lit entirely by 2000K lamps, so
        // warm light finally has something to be warm against -- the same fix the drive needed, at
        // the scale of a room rather than a night. It is not a substitute for the moonlit window
        // that #39 calls for; it is the medium that window will eventually shine through.
        fog.tint.Override(new Color(0.42f, 0.47f, 0.58f));
        fog.albedo.Override(new Color(0.52f, 0.57f, 0.66f));
    }
}
