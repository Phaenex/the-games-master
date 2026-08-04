// The night look as DATA, in one place, so the lighting lab sweeps exactly what the builder ships.
// Keeping this a plain struct (rather than constants inside the builder) is what makes an empirical
// dial-in honest: GmVillageLightingLab renders variants of this type, and GmVillageBuilder bakes the
// chosen one. There is no second copy of the recipe to drift out of sync with the tested one.
//
// Why the shipped village needs a recipe with PRACTICAL lights at all: GmVillageSurvey found the
// purchased scene carries exactly two directional lights and zero point/spot lights. Lit only by a
// moon, a village is a field of same-value silhouettes -- which is precisely what the first M1 render
// looked like (village-spawn-preview.png: one flat blue wash, no depth). Warm pools from a few lit
// windows supply the three things that render was missing: value contrast, warm/cool colour contrast,
// and depth layering from light sources at different distances.
using System.Collections.Generic;
using UnityEngine;

public struct GmVillageNightRecipe
{
    public string label;

    // Exposure is EV100: applied multiplier is 2^(-fixedExposure), so MORE NEGATIVE is BRIGHTER.
    // An earlier pass moved -2.55 -> -1.1 believing that was brighter; it is roughly a third as
    // bright, and rendered fully black. The sign is counter-intuitive enough to be worth stating.
    public float exposureEV;

    public float moonLux;
    public float fillLux;
    public Vector3 moonEuler;
    public Color moonColor;

    // Whether the moon lights the volumetric fog. On, the fog SCATTERS moonlight and becomes a
    // glowing haze that lifts the whole frame and flattens depth -- which is what made the first
    // shipped look read as a bright winter evening instead of a night to be afraid of. Off, the fog
    // still occludes distance but stays dark, so it hides things rather than announcing them.
    public bool moonVolumetric;

    public float fogMeanFreePath;   // metres; larger = thinner fog = more distance readable
    public float fogMaxHeight;
    public Color fogAlbedo;

    public float contrast;
    public float saturation;

    public Color skyTop, skyMiddle, skyBottom;
    public float skyExposure;

    // Swaps the flat three-colour GradientSky for a baked starfield cubemap (GmVillageSky). The
    // gradient is a fine ambient source and a dead thing to look at, which matters in a game where
    // the player spends the prologue outdoors looking up at rooflines, a church tower and a bell.
    public bool useStarSky;
    public float starSkyExposure;
    public float starSkyRotation;

    // 0 disables that family. Interior lights only read if the building meshes have window openings;
    // exterior lights always read. The lab exists to settle which is true for these assets rather
    // than assuming, so both are independently switchable.
    public float practicalInteriorLumens;
    public float practicalExteriorLumens;
    public float practicalRange;
    public Color practicalColor;

    // Whether practicals light the volumetric fog. At 900 lumens with this on, each lamp inflated the
    // fog into a white halo that swallowed the village (Screens/VillageLab/e-full__C-eye.png). Kept
    // switchable because a SMALL amount of the same effect is exactly the haze-around-a-lit-window
    // look this setting is worth having at all.
    public bool practicalVolumetric;

    public float aoIntensity;
    public float vignetteIntensity;
    public float bloomIntensity;

    // Which buildings burn. Deliberately a subset: an abandoned village with EVERY window lit reads
    // cosy, and one with none reads as an unlit prop field. A few uneven lights reads inhabited by
    // something -- which is the register this game wants, and it doubles as the player's landmark.
    public string[] litBuildings;

    // Derived from rendered frames, not from argument. Sweep 1 (Screens/VillageLab) established:
    //   * with the spawn corrected, this atmosphere already reads as a real village at night --
    //     variant "a" is composed, legible and moody (a-m1-lighting-no-practicals__C-eye.png);
    //   * thinning fog to 115 and pushing contrast to 12 overshoots into blue-graded dusk, with the
    //     ground going near-white (b-thin-fog-no-practicals__C-eye.png), so those sit lower here;
    //   * 900/260-lumen practicals blow the frame to white (e-full__C-eye.png), so these are an order
    //     of magnitude lower and read as accents rather than floodlights.
    public static GmVillageNightRecipe Base() => new GmVillageNightRecipe
    {
        label = "base",
        // EV100: 2^(-fixedExposure), so LESS negative is DARKER.
        //
        // The first dread pass moved this to -5.4 while ALSO cutting the moon, the fill, the fog
        // albedo, the sky and raising contrast. Those multiply: mean frame luminance fell from 0.211
        // to 0.004, about fifty times too dark to play. The character changes were right; the
        // magnitude was not. This keeps every one of them and backs the total off to roughly half
        // the old brightness, which is a night rather than a blackout.
        // Chosen from the rendered bracket in Screens/ExposureBracket, not from description.
        //
        // Worth noting because the number is misleading: the look Nick rejected as overexposed was
        // ALSO -7.0. It is not the same image. With moon-volumetric off, a dark fog albedo and the
        // starfield sky, this frame measures 0.027 mean luminance against the old 0.027-vs-0.211 --
        // about eight times darker at an identical exposure value. The brightness problem was the
        // glowing fog, not the stop.
        exposureEV = -7.0f,
        // The moon shapes silhouettes; it is not there to light the ground.
        moonLux = 4.6f,
        fillLux = 0.35f,
        moonEuler = new Vector3(34f, -128f, 0f),
        moonColor = new Color(0.66f, 0.72f, 0.92f),
        moonVolumetric = false,
        fogMeanFreePath = 62f,
        fogMaxHeight = 20f,
        // Darker fog. A bright albedo makes fog a light SOURCE once anything scatters through it,
        // which is what turned distance into glare instead of into concealment.
        fogAlbedo = new Color(0.028f, 0.032f, 0.044f),
        contrast = 11f,
        saturation = -9f,
        skyTop = new Color(0.005f, 0.007f, 0.019f),
        skyMiddle = new Color(0.026f, 0.032f, 0.054f),
        skyBottom = new Color(0.018f, 0.022f, 0.036f),
        skyExposure = -0.55f,
        useStarSky = true,
        starSkyExposure = 0f,
        starSkyRotation = 22f,
        practicalInteriorLumens = 74f,
        // Exteriors are a grounding touch only. At 40 they produced the brightest thing in frame --
        // a shapeless ground wash (l-nominal-no-volumetric__C-eye.png, right third) that competed
        // with the lit windows for the eye. The interiors do the real storytelling: they escape the
        // window openings, so the light reads as coming from inside a building rather than from a
        // lamp floating in a yard.
        practicalExteriorLumens = 9f,
        // Tighter as well as dimmer. A wide pool spills across the road and lifts the whole
        // foreground; a short one keeps the light where its source is and leaves the walk dark.
        practicalRange = 11f,
        // Deeper amber. Under the old near-white exposure this read as ordinary lamplight; against a
        // properly black night it should look like fire behind glass.
        practicalColor = new Color(1f, 0.60f, 0.26f),
        // OFF after direct comparison: with fog interaction on, each practical inflated a soft halo
        // that bled warm haze into the sky and softened every lit edge. Off, the same lamps give
        // crisp lit window panes against a clean deep-blue night.
        practicalVolumetric = false,
        aoIntensity = 0.62f,
        vignetteIntensity = 0.26f,
        bloomIntensity = 0.06f,
        // Every building now, not four of seven. With only four lit, the three dark ones read as
        // unfinished rather than as deliberately empty, and the walk had long stretches with nothing
        // to aim at. The unevenness that keeps it from looking cosy comes from the footprint-scaled
        // intensity and the per-source flicker, not from leaving buildings out.
        litBuildings = new[]
        {
            "SM_House_02", "SM_House_04", "SM_House_06", "SM_House_09",
            "SM_House_11", "SM_House_12", "SM_Church",
        },
    };

    public GmVillageNightRecipe With(string newLabel)
    {
        GmVillageNightRecipe c = this;
        c.label = newLabel;
        return c;
    }

    /// Variants the lab renders. Each isolates ONE question so a contact sheet answers it directly,
    /// rather than shifting several dials at once and leaving the cause of a change ambiguous.
    ///
    /// Sweep 2. Sweep 1 answered the big ones -- the spawn was the real defect, and practicals at
    /// 900 lumens destroy the frame. What is still open is how much warm accent this scene wants and
    /// whether those accents should touch the volumetric fog.
    /// EXPOSURE BRACKET on the finished atmosphere.
    ///
    /// Everything else -- starfield sky, ground mist, fog anisotropy, flicker, sway, practical
    /// colour and intensity -- is settled and identical across all six. Only exposureEV moves, in
    /// third-of-a-stop steps around the current -6.7. The point is to turn the one remaining
    /// subjective decision into a pick-a-frame rather than another round of describing brightness in
    /// words, which is what caused both the black scene and the washed-out one.
    ///
    /// Remember the sign: EV100 applies 2^(-fixedExposure), so the LOWER numbers here are DARKER.
    public static List<GmVillageNightRecipe> LabSweep()
    {
        var list = new List<GmVillageNightRecipe>();
        float[] stops = { -6.1f, -6.4f, -6.7f, -7.0f, -7.3f, -7.6f };
        string[] names =
        {
            "exp1-darkest", "exp2-darker", "exp3-current", "exp4-lighter", "exp5-lighter2", "exp6-lightest",
        };

        for (int i = 0; i < stops.Length; i++)
        {
            GmVillageNightRecipe r = Base().With($"{names[i]}-EV{stops[i]:0.0}");
            r.exposureEV = stops[i];
            list.Add(r);
        }
        return list;
    }
}

