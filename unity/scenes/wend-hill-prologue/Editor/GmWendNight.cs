// Converts the purchased Abandoned Village to night by EDITING the lighting it ships, never by
// deleting it.
//
// The previous attempt stripped the pack's directional lights and overrode its authored volumes on
// step one, then failed four times trying to re-author a night from nothing. This does the opposite:
// the pack's artist already lit this place, including 24 practical lights and 22 lamp props, and all
// of that survives untouched. The only things that move are the sun, the sky/fog tint and exposure.
//
// Applied as a CUMULATIVE LADDER so the contact sheet shows what each individual change did:
//   0  untouched (the shipped daylight)
//   1  + sun re-aimed and dimmed to a moon
//   2  + sky and fog retinted to night
//   3  + the pack's ungated foliage emission turned off (see GmWendFoliage)
//   4+ + exposure dropped, one EV per rung
//
// Every step asserts the lighting census is unchanged (1 directional, 24 practical, 30 volumes).
// A step that changes those counts is a bug, not a lighting choice.
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class GmWendNight
{
    const string LogTag = "GmWendNight";

    /// Dumps everything about the shipped lighting, so the night edit is made against real values
    /// instead of assumed ones. Read-only.
    [MenuItem("GamesMaster/Wend/Inspect shipped lighting")]
    public static void Inspect()
    {
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        var sb = new StringBuilder();

        foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                     .OrderBy(l => l.type))
        {
            var hd = l.GetComponent<HDAdditionalLightData>();
            sb.AppendLine($"  {l.type,-11} '{l.name}' intensity={l.intensity:0.##} " +
                          $"unit={(hd != null ? hd.lightUnit.ToString() : "n/a")} " +
                          $"colour=({l.color.r:0.##},{l.color.g:0.##},{l.color.b:0.##}) " +
                          $"euler={l.transform.rotation.eulerAngles} " +
                          $"shadows={l.shadows} active={l.gameObject.activeInHierarchy}");
        }
        Debug.Log($"[{LogTag}] LIGHTS\n{sb}");

        sb.Clear();
        foreach (Volume v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
                     .OrderByDescending(v => v.priority))
        {
            string profile = v.sharedProfile != null ? v.sharedProfile.name : "NONE";
            string overrides = v.sharedProfile != null
                ? string.Join(", ", v.sharedProfile.components.Select(c => c.GetType().Name))
                : "";
            sb.AppendLine($"  '{v.name}' global={v.isGlobal} priority={v.priority} " +
                          $"weight={v.weight} profile={profile}");
            if (!string.IsNullOrEmpty(overrides)) sb.AppendLine($"      [{overrides}]");
        }
        Debug.Log($"[{LogTag}] VOLUMES\n{sb}");

        EditorApplication.Exit(0);
    }

    public const string OwnedProfilePath = "Assets/Scenes/WendHill_NightProfile.asset";

    /// Applies the ladder cumulatively up to and including `step`. Step 0 is a no-op, which is what
    /// makes the first frame of the contact sheet the untouched control.
    public static void ApplyStep(int step)
    {
        GmWendBuilder.LightingCensus before = GmWendBuilder.TakeCensus();

        if (step >= 1) SunToMoon();
        if (step >= 2) { OwnProfile(); SkyAndFogToNight(); }

        // Rung 3 is the pack's self-lit grass and leaves. It sits BEFORE the exposure bracket on
        // purpose: emission does not care what the sun is doing, so bracketing exposure against a
        // frame that still glows only measures the glow.
        if (step >= 3)
        {
            int repointed = GmWendFoliage.Apply();
            if (repointed == 0)
                throw new InvalidOperationException(
                    "foliage step repointed nothing. The pack's ungated emissive materials were not " +
                    "found, so the frame would still glow and the cause would look like something else.");
        }

        // Rung 4 brings the pack's practicals and lamp glass down from daylight strength. It sits before
        // the bracket because exposure cannot fix a scene that holds a 3.4 lux sky and a 63000 lumen spot
        // at the same time, and three brackets were spent proving that the hard way.
        if (step >= 4) PracticalsToNight();

        // Rungs 5 and up are the exposure bracket, one EV value each.
        if (step >= 5) { OwnProfile(); SetExposure(ExposureBracket[Mathf.Min(step - 5, ExposureBracket.Length - 1)]); }

        GmWendBuilder.LightingCensus after = GmWendBuilder.TakeCensus();
        if (!before.Equals(after))
            throw new InvalidOperationException(
                $"night step {step} changed the lighting census: {before} -> {after}. " +
                "Nothing here may add or remove a light or a volume.");

        Debug.Log($"[{LogTag}] applied step {step} (census {after})");
    }

    /// STEP 1. The pack's sun becomes the moon. Same light object, re-aimed and re-valued -- it keeps
    /// its shadow settings, its angular diameter and whatever else the artist configured.
    static void SunToMoon()
    {
        Light sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) { Debug.LogWarning($"[{LogTag}] no directional light to convert"); return; }

        var hd = sun.GetComponent<HDAdditionalLightData>();
        float wasIntensity = sun.intensity;

        // Low and raking from the north-west. A high sun angle is what makes a dimmed daylight rig
        // still read as daytime: the giveaway is short shadows, not brightness.
        sun.transform.rotation = Quaternion.Euler(MoonElevation, -132f, 0f);
        sun.color = new Color(0.62f, 0.70f, 0.92f);
        if (hd != null)
        {
            hd.lightUnit = LightUnit.Lux;
            hd.intensity = MoonLux;
            // A moon is a small, hard source. Widening it is what turns moonlight into overcast.
            hd.angularDiameter = 1.1f;

            // The moon must NOT light the volumetric fog. This is the single finding the retired village
            // pass was most confident about and it never got carried over to this scene: with
            // affectsVolumetric on, the fog scatters moonlight and becomes a light source in its own
            // right, so distance turns into glare instead of into concealment. That pass measured the
            // frame going eight times darker from this and two related changes, at the SAME exposure.
            hd.affectsVolumetric = false;
        }
        else sun.intensity = 3.4f;

        Debug.Log($"[{LogTag}] step1 sun->moon: '{sun.name}' {wasIntensity:0.##} -> {sun.intensity:0.##}");
    }

    /// Scale applied to every practical, and the ceiling that tames the showcase spots.
    ///
    /// Measured before this existed: the moon was cut from 2000 lux to 3.4, a factor of 588, and every
    /// other light in the scene was left at full daylight strength. Twelve point lights at 1549 lumens
    /// each, which is a 100W household bulb; four more at 3173 to 3725; and two spots at 6957 and 62903,
    /// the second of which is stadium scale. A 3.4 lux sky and a 63000 lumen spotlight cannot share an
    /// exposure, which is why three separate brackets each worked at one vantage and failed everywhere
    /// else, and why one 188m walk spread 0.343 to 0.881 at a fixed EV.
    ///
    /// A uniform scale keeps the artist's relative hierarchy between fixtures, which is worth preserving.
    /// The ceiling is what the scale alone cannot fix: 62903 scaled by 0.06 is still 3774, so the two
    /// showcase fills need clamping rather than scaling. 1549 becomes 93 lumens, which is a plausible
    /// cottage window at night.
    ///
    /// Not idempotent by design: applying it twice scales twice. Every path that calls it rebuilds the
    /// scene from the purchased source first, so it always runs against the pack's original values.
    /// Bisect overrides.
    ///
    /// These exist so a rung can move ONE value and nothing else, without editing code between runs.
    /// Editing constants by hand between builds is how a campaign ends up unable to say which change
    /// produced which frame, which is the exact failure the ladder was built to prevent. The defaults
    /// reproduce the committed night byte for byte, so a run with no flags is the control.
    ///
    ///   -gmPracticalScale <f>   multiplier on every practical light, default 0.06
    ///   -gmFogDimmer <f>        volumetric fog's ambient probe dimmer, default 0
    ///   -gmSkyExposureDrop <f>  stops taken off the pack's HDRI sky, default 4.0
    ///   -gmMoonLux <f>          the moon, in lux, default 1.0
    ///   -gmIndirectDiffuse <f>  ambient bounce multiplier, default 1.0
    ///   -gmFogMeanFreePath <f>  fog density, metres, default 62
    ///   -gmSSR <f>              screen space reflections, 0 off / 1 on, default 0
    ///   -gmMoonElevation <f>    moon elevation in degrees, default 24
    ///   -gmLampEmissive <f>     lamp glass emissive target, default 1.5
    ///   -gmGapLamps <f>         fill unlit route gaps with lamps, 0 off / 1 on, default 1
    ///   -gmExposureEV <f>       fixed exposure EV, default 0.30. HIGHER is DARKER
    public const float DefaultPracticalScale = 0.06f;

    /// COMMITTED at 0 on 2026-07-25, off a four-rung bracket measured along the walk rather than at a
    /// single vantage, which is the mistake that voided two earlier values in this document.
    ///
    /// At the pack's default of 1 the volumetric fog takes full ambient, and because fog integrates
    /// along the view ray that term dominates any open view while barely touching an enclosed one. The
    /// street at 330m measured 0.556 against a 0.03 to 0.06 target and read on screen as pale blue
    /// foggy DAYLIGHT, not as a bright night. At 0 the same frame measures 0.030 and reads as a
    /// village night, with the enclosed forest unmoved. The fog still scatters DIRECT lamp light, so
    /// the halo the fog quality pass was ported for survives.
    ///
    /// Bracket, whole-frame luma at matched distances: dimmer 1 / 0.5 / 0 gives 0.556 / 0.326 / 0.030
    /// at 330m, and 0.024 / 0.019 / 0.016 at the enclosed spawn. Frames inside target went 2/26 to
    /// 7/35, near-black frames stayed at zero, and max-to-min spread improved from 23x to 14x.
    public const float DefaultFogProbeDimmer = 0.0f;

    /// The HDRI sky is the scene's ambient source, so this is the dial that lights SURFACES rather
    /// than the fog. It is a relative drop, not an absolute value, because the pack's own exposure is
    /// the starting point and is not ours to assume.
    ///
    /// COMMITTED at 4.0 on 2026-07-26, 1.5 stops brighter than the old 5.5. Eight other levers were
    /// bracketed downward chasing over-brightness and every one made the spread worse or did nothing;
    /// this is the first that ever moved p5 at all, because it ADDS a constant instead of multiplying
    /// a ratio. Paired with the exposure bump below so the mean does not just rise with it.
    ///
    /// Measured over the FULL 0-438m walk, not the 0-210m half a first attempt was voided for
    /// promoting from: against the old 5.5/-1.35 pair, this drops the daylight-band failures from
    /// 4/30 frames to 2/30, and very nearly eliminates local blowout (worst frame 3.68% -> 0.08% of
    /// pixels above 0.80 luma). p5 does not improve (0.016 -> 0.012, both already failing) and the
    /// mint-green wall at 360m is still a defect (51.09% -> 41.20% green-dominant pixels, better but
    /// not fixed). Both are geometry the sky cannot reach: a wall a metre from one practical, and a
    /// stretch nowhere near a lamp. That is placement work, not a dial, and stays open below.
    public const float DefaultSkyExposureDrop = 4.0f;

    /// The ambient lift for the ground BETWEEN the lamps, which is the problem left after the fog fix.
    ///
    /// This is HDRP's indirect diffuse multiplier and NOT a fill light, deliberately. The village
    /// recipe uses a 0.35 lux fill directional, but adding a second directional here would change the
    /// lighting census that the builder and the contract both assert on, and that census is what
    /// catches a builder deleting the pack's own lights. A volume override lifts the same bounce
    /// without adding a light to count.
    public const float DefaultIndirectDiffuse = 1.0f;

    /// Fog density as a mean free path in metres. Lower is thicker.
    public const float DefaultFogMeanFreePath = 62f;

    /// Screen space reflections, off by default because the scene ships without them at all.
    public const float DefaultSSR = 0f;

    /// Moon elevation. 24 degrees is a deliberate low rake, chosen because a high angle is what makes a
    /// dimmed daylight rig still read as daytime: the giveaway is short shadows. The cost of a low one
    /// is occlusion, and this is the lever for finding out whether the moon reaches the ground at all
    /// through a forest canopy and 38 buildings.
    public const float DefaultMoonElevation = 24f;

    /// Lamp glass emissive. This is the LOCAL lever, and it exists because the colour pass found
    /// something the luma standard was passing: 4 to 6 percent of the 285m and 300m frames sits above
    /// 0.80 luma, blown, while nothing reaches the 254 clip point a whole-frame check looks for. Those
    /// frames are lit cottage windows. Every global dial has been ruled out, so what is left is the
    /// surfaces themselves.
    public const float DefaultLampEmissive = 1.5f;

    /// Gap lamps ON by default. This is the only change measured to move p5, the darkest frames, which
    /// stayed at 0.012 through every other lever including a five times practical increase.
    public const float DefaultGapLamps = 1f;

    static float PracticalScale = DefaultPracticalScale;
    static float FogProbeDimmer = DefaultFogProbeDimmer;
    static float SkyExposureDrop = DefaultSkyExposureDrop;
    static float IndirectDiffuse = DefaultIndirectDiffuse;
    static float FogMeanFreePath = DefaultFogMeanFreePath;
    static float SSR = DefaultSSR;
    static float MoonElevation = DefaultMoonElevation;
    static float LampEmissive = DefaultLampEmissive;
    static float GapLamps = DefaultGapLamps;

    /// Parses the overrides and SAYS what it read, including when it read nothing. A bisect that
    /// silently ignored its flag would produce a frame identical to the control and get recorded as
    /// "that value does nothing".
    public static void ReadBisectOverrides()
    {
        PracticalScale = ReadFlag("-gmPracticalScale", DefaultPracticalScale);
        FogProbeDimmer = ReadFlag("-gmFogDimmer", DefaultFogProbeDimmer);
        SkyExposureDrop = ReadFlag("-gmSkyExposureDrop", DefaultSkyExposureDrop);
        MoonLux = ReadFlag("-gmMoonLux", DefaultMoonLux);
        IndirectDiffuse = ReadFlag("-gmIndirectDiffuse", DefaultIndirectDiffuse);
        FogMeanFreePath = ReadFlag("-gmFogMeanFreePath", DefaultFogMeanFreePath);
        SSR = ReadFlag("-gmSSR", DefaultSSR);
        MoonElevation = ReadFlag("-gmMoonElevation", DefaultMoonElevation);
        LampEmissive = ReadFlag("-gmLampEmissive", DefaultLampEmissive);
        GapLamps = ReadFlag("-gmGapLamps", DefaultGapLamps);
        CommittedExposureEV = ReadFlag("-gmExposureEV", DefaultExposureEV);

        Debug.Log($"[{LogTag}] bisect: PracticalScale={PracticalScale} FogProbeDimmer={FogProbeDimmer} " +
                  $"SkyExposureDrop={SkyExposureDrop} MoonLux={MoonLux} IndirectDiffuse={IndirectDiffuse} " +
                  $"FogMeanFreePath={FogMeanFreePath} SSR={SSR} MoonElevation={MoonElevation} " +
                  $"(defaults {DefaultPracticalScale}/{DefaultFogProbeDimmer}/{DefaultSkyExposureDrop}/" +
                  $"{DefaultMoonLux}/{DefaultIndirectDiffuse}/{DefaultFogMeanFreePath}/{DefaultSSR})");
    }

    static float ReadFlag(string flag, float fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, flag);
        if (at < 0 || at + 1 >= args.Length) return fallback;
        if (!float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float value))
            throw new InvalidOperationException($"{flag} was given '{args[at + 1]}', which is not a number");
        return value;
    }
    const float PracticalCeilingLumens = 200f;

    /// The lamp glass, scaled rather than zeroed, because unlike the grass it is meant to glow.
    const float LampEmissiveTarget = 1.5f;

    /// Oil and paraffin lamps sit at 1900K to 2200K. The pack ships every practical at pure white.
    const float LampKelvin = 2000f;

    /// Brings the pack's 24 practicals into a night range, editing intensities and never removing a light.
    static void PracticalsToNight()
    {
        int scaled = 0, clamped = 0;
        float was = 0f, now = 0f;

        foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type == LightType.Directional) continue;
            var hd = l.GetComponent<HDAdditionalLightData>();
            if (hd == null) continue;

            float before = hd.intensity;
            float after = before * PracticalScale;
            if (hd.lightUnit == LightUnit.Lumen && after > PracticalCeilingLumens)
            {
                after = PracticalCeilingLumens;
                clamped++;
            }
            if (Mathf.Approximately(after, before)) continue;

            hd.intensity = after;

            // Colour temperature, which had a bigger effect on the look than anything else changed today
            // and was sitting in plain sight. Every practical the pack ships reads colour=(1,1,1), pure
            // white. An oil or paraffin lamp is 1900K to 2200K. White lamps in a village at night are the
            // difference between "lit" and "electrically lit", and the inspect dump was read twice for its
            // intensity column with this column ignored.
            l.useColorTemperature = true;
            l.colorTemperature = LampKelvin;
            l.color = Color.white;   // let the temperature do the tinting, not a baked-in filter
            l.shadows = LightShadows.None; // 24 tiny practical shadow maps cannot justify their frame cost

            // Flicker, so the windows breathe. Seeded off world position inside the component so no two
            // are in sync; synchronised flicker announces itself and is worse than none.
            if (l.GetComponent<GmLightFlicker>() == null) l.gameObject.AddComponent<GmLightFlicker>();

            EditorUtility.SetDirty(l);
            was += before;
            now += after;
            scaled++;
        }

        int lampSlots = GmWendFoliage.DimLamps(LampEmissive);

        Debug.Log($"[{LogTag}] practicals to night: {scaled} light(s) scaled by {PracticalScale} " +
                  $"({clamped} clamped to {PracticalCeilingLumens}), total {was:0} -> {now:0} lumens; " +
                  $"{lampSlots} lamp material slot(s) dimmed to {LampEmissive}");
    }

    /// Takes a project-owned copy of the pack's volume profile and points the scene's volume at it.
    ///
    /// Exactly ONE of the scene's 30 volumes carries a profile; the other 29 are empty leftovers from
    /// the pack's camera rig. That one profile holds the entire authored look (Fog, HDRISky,
    /// ColorAdjustments, WhiteBalance, SSAO, Vignette and more) and it lives inside the purchased
    /// LeartesStudios folder, so it must not be edited in place.
    ///
    /// Copying it and repointing keeps the purchased asset pristine while letting every night edit
    /// happen on our own file. Note this ADDS NO VOLUME -- the census is unchanged, which is exactly
    /// the difference between this and the previous attempt's override-volume approach.
    static void OwnProfile()
    {
        Volume host = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .FirstOrDefault(v => v.sharedProfile != null);
        if (host == null) { Debug.LogWarning($"[{LogTag}] no volume with a profile to own"); return; }

        string current = AssetDatabase.GetAssetPath(host.sharedProfile);
        if (current == OwnedProfilePath) return;   // already ours

        if (AssetDatabase.LoadMainAssetAtPath(OwnedProfilePath) != null)
            AssetDatabase.DeleteAsset(OwnedProfilePath);
        if (!AssetDatabase.CopyAsset(current, OwnedProfilePath))
            throw new InvalidOperationException($"failed to copy profile {current} -> {OwnedProfilePath}");
        AssetDatabase.Refresh();

        var owned = AssetDatabase.LoadAssetAtPath<VolumeProfile>(OwnedProfilePath);
        if (owned == null) throw new InvalidOperationException($"profile copy missing at {OwnedProfilePath}");

        host.sharedProfile = owned;
        EditorUtility.SetDirty(host);
        Debug.Log($"[{LogTag}] owned profile: '{host.name}' now uses {OwnedProfilePath} " +
                  $"(purchased original untouched at {current})");
    }

    /// STEP 2. Retint the profile's existing Fog and sky overrides in place. Nothing is added,
    /// disabled or reprioritised.
    static void SkyAndFogToNight()
    {
        int fogEdited = 0, skyEdited = 0, exposureFound = 0;

        foreach (Volume v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            VolumeProfile p = v.sharedProfile;
            if (p == null) continue;

            if (p.TryGet(out Fog fog))
            {
                // Darker albedo so the fog ABSORBS distance instead of scattering daylight into it.
                // Leaving this bright is what made the last attempt glow.
                // Darker albedo so the fog ABSORBS distance instead of scattering daylight into it.
                fog.albedo.Override(new Color(0.035f, 0.040f, 0.055f));
                fog.tint.Override(new Color(0.30f, 0.34f, 0.46f));

                // Fog QUALITY, which the retired village pass worked out and this scene never received.
                // Only affectsVolumetric had been ported; these were still at the pack's defaults.
                //
                // anisotropy scatters light forward, so a lamp seen through mist gets a directional halo
                // instead of an even glow, and that halo is most of what makes fog read as air rather than
                // as a grey filter. depthExtent matters even more here: the default 64m stops volumetric
                // fog well short of a 580m street, leaving everything beyond it on flat distance fog, so
                // the far half of the walk had no atmosphere in it at all.
                fog.meanFreePath.Override(FogMeanFreePath);
                fog.anisotropy.Override(0.62f);
                fog.depthExtent.Override(110f);
                fog.sliceDistributionUniformity.Override(0.6f);
                fog.multipleScatteringIntensity.Override(0.3f);
                fog.enableVolumetricFog.Override(true);

                // How much AMBIENT the fog receives. The pack leaves this at 1 and the night never
                // touched it, so while the albedo above stops the fog scattering daylight, the fog was
                // still being lit at full strength by the ambient probe. Suspected cause of open views
                // going pale while enclosed ones stay night: fog integrates over view depth, so a
                // street running hundreds of metres accumulates far more of this term than a forest
                // with geometry a few metres away. Overridden explicitly now, default unchanged at 1.
                fog.globalLightProbeDimmer.Override(FogProbeDimmer);
                fogEdited++;
                EditorUtility.SetDirty(p);
            }

            if (p.TryGet(out GradientSky grad))
            {
                grad.top.Override(new Color(0.010f, 0.014f, 0.030f));
                grad.middle.Override(new Color(0.038f, 0.046f, 0.072f));
                grad.bottom.Override(new Color(0.026f, 0.031f, 0.046f));
                skyEdited++;
                EditorUtility.SetDirty(p);
            }

            if (p.TryGet(out HDRISky hdri))
            {
                // Do not swap the pack's sky texture; just take it down. A daytime HDRI at a night
                // exposure still reads as a photograph of a day.
                //
                // This is also the scene's AMBIENT source, so it lights every surface, not just the
                // visible sky. That makes it the remaining suspect for the mid-route frames, which sit
                // at 0.225 with a properly dark sky and warm-lit geometry and did not move when either
                // the practicals or the fog ambient were halved.
                hdri.exposure.Override(hdri.exposure.value - SkyExposureDrop);
                skyEdited++;
                EditorUtility.SetDirty(p);
            }

            if (p.TryGet(out PhysicallyBasedSky pbs))
            {
                pbs.spaceEmissionMultiplier.Override(12f);
                skyEdited++;
                EditorUtility.SetDirty(p);
            }

            // Ambient bounce. The lever for ground between the lamps, which is what is left after the
            // fog fix: the practicals light their own pools and nothing lights the gaps. Added to the
            // profile rather than as a fill light so the lighting census stays exactly as the pack
            // shipped it, which is what the contract asserts on.
            if (!p.TryGet(out IndirectLightingController indirect))
                indirect = p.Add<IndirectLightingController>(true);
            indirect.indirectDiffuseLightingMultiplier.Override(IndirectDiffuse);
            EditorUtility.SetDirty(p);

            // Reflections. The scene ships with none configured at all, which on wet cobbles and
            // window glass at night is a real absence rather than a stylistic choice. Off by default
            // so it is a measured decision rather than something that arrived with a refactor.
            if (!p.TryGet(out ScreenSpaceReflection ssr))
                ssr = p.Add<ScreenSpaceReflection>(true);
            ssr.enabled.Override(SSR > 0.5f);
            EditorUtility.SetDirty(p);

            if (p.TryGet(out Exposure _)) exposureFound++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[{LogTag}] step2 retint: {fogEdited} fog, {skyEdited} sky override(s) edited " +
                  $"({exposureFound} volume(s) already carry an Exposure override)");
    }

    /// STEP 3. Exposure last, so it is corrected against a scene that is already night rather than
    /// being used to fake night on a daylight one.
    ///
    /// EV100: the applied multiplier is 2^(-fixedExposure), so LESS negative is DARKER. Getting this
    /// backwards cost a whole session last time -- moving -2.55 to -1.1 believing it was brighter
    /// produced a fully black scene.
    /// EV100 values to bracket, DARKEST FIRST.
    ///
    /// The sign is the single most expensive mistake in this project's history and it has now been
    /// made twice: the applied multiplier is 2^(-fixedExposure), so a LARGER number is DARKER.
    /// A first attempt at -9.5 meant 2^9.5, about 724x, and rendered pure white (luma 1.000).
    /// Bracketing rather than reasoning about it is the only reliable way through.
    ///
    /// This is the SECOND bracket. The first one ran { 2, 0, -2, -4, -6 } and read 0.096 / 0.229 /
    /// 0.560 / 0.877 / 0.985, which said EV +2 was the night register. That reading was worthless: it
    /// was metering the pack's self-lit grass, not the scene. With the foliage emission off the same
    /// five values read 0.001 / 0.016 / 0.199 / 0.652 / 0.915, so the register moved about two stops
    /// and +2 is now pure black. Recorded because it is the same trap in a new costume: a bracket is
    /// only as good as the frame it meters, and this one was measuring a bug.
    ///
    /// The mean luma to aim at is roughly 0.03 to 0.06. HDRP's own automatic exposure settled the
    /// fixed-emission-free scene at 0.059, and the earlier village pass was accepted at 0.027, so the
    /// window sits between EV 0 (0.016) and EV -2 (0.199). Hence half-stops across exactly that gap
    /// rather than another eight-stop sweep.
    /// THIRD bracket. The first metered a frame whose grass was self-lit. The second metered a vantage
    /// that turned out not to be in the village: the spawn was the centroid of 57 scattered road meshes,
    /// which put it on a hillside away from the lamps, and EV -1.0 was correct there. On the actual
    /// village street, with 24 practical lights and 21 lamp posts at emissive 21.95 around the player,
    /// that same fixed exposure measures 0.343 to 0.881 across a 188m walk against a 0.03 to 0.06 target.
    /// Six to fifteen times too bright.
    ///
    /// The lesson is the same one twice: a bracket only describes the frame it metered. Fixing the route
    /// moved the frame, so the exposure has to be re-picked, and this time from where the game happens.
    /// Larger is darker, so this brackets 2 to 4.5 stops below the old pick.
    /// FOURTH bracket, and the first one metered on a scene whose light levels are night levels. The
    /// practicals dropped about 17x on rung 4, so the register moves back down from the +2 the daylight
    /// practicals forced. Larger is darker.
    /// FIFTH bracket, and the first one aimed by a published reference instead of by feel.
    ///
    /// Unity's EV100 cheat sheet puts moonless at -2, MOONLIT AT +1, interior at +4, low sun at +7 and
    /// sunlit at +14. HDRP's fixedExposure is EV100, so a moonlit village with lit windows belongs at
    /// about +1, drifting toward +2 where the lamps are. Four previous brackets wandered from -9.5 to
    /// +3.5 hunting for that, and the one shot from the village independently found +1 to +2.
    /// https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/Physical-Light-Units.html
    /// FIFTH bracket. Aimed from a measured anchor, with the reference as a sanity check rather than as
    /// the aim, because reading the cheat sheet's "moonlit +1 EV" straight off nearly sent this the wrong
    /// way for the third time.
    ///
    /// The anchor: with the practicals dimmed to night levels and the moon still at 3.4 lux, EV -0.5
    /// measured 0.033 and 0.058, dead in the 0.03 to 0.06 target. Dropping the moon to a physical 0.5 lux
    /// removes up to 2.8 stops of sky, and a DARKER scene needs a BRIGHTER exposure, which is a LOWER EV.
    /// So the window is below -0.5, not above it. How far below depends on how much of each frame is lit
    /// by the moon rather than by a lamp, which is exactly what a bracket is for.
    ///
    /// Sanity check against the reference: EV -2 sits at its "moonless" mark, which is the right
    /// neighbourhood for a 0.5 lux sky with a handful of 93 lumen windows in it.
    /// https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/Physical-Light-Units.html
    public static readonly float[] ExposureBracket = { -0.5f, -1.5f, -2f, -2.5f, -3.5f };

    /// The chosen night, picked by Nick off the fine bracket contact sheet on 2026-07-25.
    ///
    /// Defensible rather than a taste call: HDRP's own automatic exposure independently settled the
    /// corrected scene at mean luma 0.059 front and 0.074 rear, and a FIXED -1.0 reproduces both to
    /// three decimals. So this is the engine's own metering of the scene, made deterministic. Automatic
    /// exposure is not an option to ship, because it is what hid the foliage bug for four attempts: it
    /// silently re-meters around anything bright and turns a lighting mistake into a normal-looking
    /// frame.
    ///
    /// Highlight headroom measured at this value: 0.000% of pixels fully clipped front, 0.006% rear,
    /// and that 0.006% is the lamp filaments, which are meant to be the brightest thing in frame.
    /// Superseded EV -1.0 on 2026-07-25. That value was metered at a spawn which turned out to be the
    /// centroid of 57 scattered road meshes, on a hillside away from the village. It was right there, and
    /// HDRP's automatic exposure agreed with it there to three decimals, which is exactly how it survived
    /// review. On the actual street it measured 0.343 to 0.881 across a 188m walk against a 0.03 to 0.06
    /// target.
    ///
    /// +2.0 comes from the third bracket, shot from the street. It is being validated against the WHOLE
    /// WALK rather than a single frame, because two agreeing measurements of one wrong vantage is the
    /// mistake that produced -1.0.
    /// EV -1.0, from the fifth bracket. The same NUMBER as the value voided earlier today, reached for
    /// entirely different reasons, and that coincidence is a trap worth naming: the first -1.0 was metered
    /// on a hillside with daylight-strength practicals and a 3.4 lux moon. This one is metered on the
    /// village street with the practicals at 2475 total lumens and the moon at a physical 0.5 lux. Same
    /// dial, different scene.
    ///
    /// Measured: EV -0.5 gives 0.015/0.025 and EV -1.5 gives 0.064/0.102, so -1.0 lands near 0.031/0.05,
    /// inside the 0.03 to 0.06 target in both directions. The forward/rear spread also tightened from 3.4x
    /// to about 1.6x, which matters more than the mean: it means one exposure now suits more of the walk.
    ///
    /// Judged final only by the WALK distribution, not by this frame. A single vantage agreeing with a
    /// bracket is precisely what produced the void value.
    /// -1.35, a third of a stop brighter than -1.0.
    ///
    /// The fog quality port took light out of the darker frames, as intended: fog that absorbs distance
    /// instead of scattering it into a wash also stops filling the shadows. Measured across the same 340m
    /// walk, luma mean fell 0.173 to 0.141 and the darkest open views fell to 0.014 to 0.036, which is
    /// under the floor rather than atmospheric. Open views spanned 0.014 to 0.197.
    ///
    /// A third of a stop is deliberately small. The frames that need help are the dark ones and the ones
    /// that do not are already near the top of the band, so the correction has to be smaller than the
    /// spread it is fixing.
    ///
    /// SUPERSEDED. Committed at +0.30 on 2026-07-26, alongside the sky drop above dropping to 4.0.
    /// +0.45 was tried first, against a predicted mean of 0.078; it measured 0.040 over a matched
    /// section and over-darkened, so +0.30 is the value actually built and walked full-route. See the
    /// sky drop constant above for the measured before/after; the two are bracketed as a pair because
    /// raising ambient without darkening exposure to match just makes the whole frame brighter.
    public const float DefaultExposureEV = 0.30f;

    /// Static so exposure can be bracketed WITH ambient. Those two are the pair that matters: ambient
    /// adds a constant and compresses the spread, exposure multiplies and moves the whole frame, so
    /// raising ambient to close the ratio and then darkening to taste is the move. Neither alone works,
    /// which is why every single-lever rung before this scored the same 2 of 4.
    ///
    /// Higher EV is DARKER. The bracket in this document reads 0.0 -> 0.016, -1.0 -> 0.059, -2.0 -> 0.198.
    public static float CommittedExposureEV = DefaultExposureEV;

    /// Lux for the moon. Audited by GmWendSceneContract, so changing it here without rebuilding the
    /// scene makes the build refuse rather than ship a scene that no longer matches the recipe.
    /// Full moon on a clear night, from Unity's own physical light unit reference: moonlight is UNDER
    /// 1 lux, and a starry night with no moon is 0.002. This was 3.4 for most of a day, which is three to
    /// thirty times too bright, and because it is the sky it lifted every frame uniformly. Bracketing
    /// exposure against it meant compensating for a sky that was itself wrong.
    ///
    /// 0.5 lux is a bright full moon. Kept off the floor deliberately: the prologue is a walk outdoors and
    /// a moonless 0.002 lux would leave nothing but the lamps.
    /// https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/Physical-Light-Units.html
    /// 1.0 lux, the top of the reference's clear-night band, raised from 0.5.
    ///
    /// At 0.5 the moon contributed nothing you could see: comparing two ladders at matched EV, cutting it
    /// 6.8x moved the frame by a few percent, because the village is lit entirely by its practicals. The
    /// walk showed the cost, with the treeline and the hillside reading as flat near-black away from any
    /// lamp and no moonlight rake anywhere in frame. A night scene needs the moon to model the shapes the
    /// lamps do not reach.
    public const float DefaultMoonLux = 1.0f;

    /// Static rather than const so the moon can be bracketed like every other lever. The contract reads
    /// this same value, so a bracket run audits against what it actually built rather than failing on
    /// the default it did not use.
    public static float MoonLux = DefaultMoonLux;

    /// The whole night, at the committed exposure, applied to the currently open scene. This is the
    /// shipping path; the ladder is the review path.
    public static void ApplyCommitted()
    {
        ReadBisectOverrides();

        GmWendBuilder.LightingCensus before = GmWendBuilder.TakeCensus();

        SunToMoon();
        OwnProfile();
        SkyAndFogToNight();

        int repointed = GmWendFoliage.Apply();
        if (repointed == 0)
            throw new InvalidOperationException(
                "the committed night repointed no foliage. The pack's ungated emissive materials were " +
                "not found, so the scene would ship with self-lit grass setting its own exposure.");

        // Neutralises the green-biased wall tints the walk found (walk-0330m through walk-0360m).
        // Cosmetic rather than build-breaking, so unlike the foliage check above this does not throw on
        // finding none -- a future pack revision with no green-biased material is a fine outcome, not a
        // broken build.
        int wallsFixed = GmWendWallTint.Apply();

        // Neutralises the grass albedo tint -- a second, independent bug on the same materials
        // GmWendFoliage already owns for their emission, found by looking harder at frames that already
        // passed the written standard. Must run after GmWendFoliage.Apply() above so the owned grass
        // copies already exist to be found rather than re-copied.
        int grassFixed = GmWendGrassTone.Apply();

        PracticalsToNight();
        SetExposure(CommittedExposureEV);

        // Fill the unlit stretches. After PracticalsToNight so the gap measurement sees the pack's
        // lamps at their NIGHT values, and before the census check below so adding lamps is proven not
        // to disturb the pack's own lighting.
        int gapLamps = GapLamps > 0.5f ? GmWendLamps.Apply() : 0;

        // Canonical opening layer: estate arrival, locking gate, story anchors, manor, wake room and
        // the complete Ninth Bell runtime. It must precede the NavMesh so its physical gate/manor are
        // included in the bake, and precede ambience so all route-derived emitters use the final route.
        GmWendOpening.Result opening = GmWendOpening.Build();

        // The NavMesh belongs here and NOT in GmWendBuilder.BuildBase, which the ladder calls once per
        // rung, ten times a run, to render stills from a rig that never walks anywhere. Baking there
        // would pay the cost ten times over for frames nobody paths through.
        float navArea = GmWendNavMesh.Apply();

        // Ambience. Not lighting, so it sits before the census check below rather than needing its own
        // exemption from it -- the same reason gap lamps and the NavMesh are ordered here rather than
        // after. See GmWendAmbience for why this scene had zero AudioSources until now.
        (int cricketAnchors, int owlAnchors, int footstepClips) ambience = GmWendAmbience.Apply();

        GmWendBuilder.LightingCensus after = GmWendBuilder.TakeCensus();
        if (!before.Equals(after))
            throw new InvalidOperationException(
                $"the committed night changed the lighting census: {before} -> {after}");

        Debug.Log($"[{LogTag}] committed night applied at EV {CommittedExposureEV} (census {after}), " +
                  $"NavMesh {navArea:0}m^2, {gapLamps} gap lamp(s), {wallsFixed} wall slot(s) de-tinted, " +
                  $"{grassFixed} grass slot(s) de-tinted, ambience {ambience.cricketAnchors} cricket / " +
                  $"{ambience.owlAnchors} owl anchor(s), {ambience.footstepClips} footstep clip(s), " +
                  $"opening {opening.routeLength:0}m/{opening.anchors} anchors/{opening.pois} POIs");
    }

    /// STEP 3. Builds the scene and SAVES it, then reopens it from disk and audits it.
    ///
    /// The reopen is the point. Every earlier night in this project existed only in memory during a
    /// render: the ladder deliberately never saves, because each rung has to start from the purchased
    /// source. That is correct for review and useless for shipping, and it is easy to mistake a good
    /// ladder frame for a scene that is actually night on disk. Reopening and re-auditing is the only
    /// thing that tells those two apart.
    [MenuItem("GamesMaster/Wend/3. Build the committed night (saves the scene)")]
    public static void BuildCommittedNight()
    {
        GmWendBuilder.BuildBase();
        ApplyCommitted();

        if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))
            throw new InvalidOperationException($"failed to save {GmWendBuilder.ScenePath}");
        AssetDatabase.SaveAssets();
        Debug.Log($"[{LogTag}] saved {GmWendBuilder.ScenePath}");

        // Round trip. Opening the path that is ALREADY open is not a reliable reload, and a reload that
        // did not really happen would audit the in-memory scene and pass for the wrong reason. Going
        // through an empty scene first forces a genuine read from disk.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.OpenScene(GmWendBuilder.ScenePath, OpenSceneMode.Single);
        GmWendSceneContract.AssertBuilt();

        Debug.Log($"[{LogTag}] STEP 3 PASS: night persisted and verified after reopen");
        EditorApplication.Exit(0);
    }

    static void SetExposure(float ev)
    {
        Volume host = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .FirstOrDefault(v => v.sharedProfile != null);
        if (host == null) { Debug.LogWarning($"[{LogTag}] no profile to set exposure on"); return; }

        VolumeProfile p = host.sharedProfile;

        // The pack ships NO Exposure override, so the scene runs on HDRP's automatic exposure --
        // which is precisely why dimming the sun alone would not darken anything: auto-exposure
        // simply compensates and hands back the same brightness. The override has to be added.
        if (!p.TryGet(out Exposure exposure))
        {
            exposure = p.Add<Exposure>(true);
            exposure.hideFlags = HideFlags.HideInHierarchy;
            // Without this the component exists only in memory and vanishes on reload, leaving a
            // profile that silently overrides nothing and a scene back on automatic exposure.
            AssetDatabase.AddObjectToAsset(exposure, p);
            Debug.Log($"[{LogTag}] step3 added an Exposure override (pack shipped none)");
        }

        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(ev);

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        Debug.Log($"[{LogTag}] exposure: fixed {ev} EV on '{p.name}'");
    }
}
