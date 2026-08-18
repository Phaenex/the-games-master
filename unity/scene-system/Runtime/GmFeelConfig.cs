// One asset for every number the player can feel.
//
// The project's feel boundary says a constant that changes how the game FEELS -- how fast the walk
// is, how bright a lamp burns, how long a clock runs, how much sanity a catch pays back -- belongs
// on a ScriptableObject with a [Range] and a [Tooltip], never as a literal in a .cs file. That is not
// tidiness. Feel is Nick's gate, and a dial he cannot turn without a recompile is a dial he cannot
// turn during a walk.
//
// Most defaults below are the exact literals that originally sat at their call sites. Values changed
// by a measured tuning pass say so beside the field, including the prologue render corridor, far clip
// and no-vsync frame ceiling. That keeps the asset honest: it is the authored source of current feel,
// not a museum of the first literals the project happened to use.
//
// HOW IT RESOLVES. Call sites read `GmFeelConfig.Active`, which is the asset at
// `Resources/GmFeelConfig` when the project has one and an in-memory instance carrying the defaults
// below when it does not. The fallback is the point: a scene with no asset assigned behaves exactly
// as it did before this file existed, and no call site can null-reference. Author one with
// Assets > Create > Games Master > Feel Config, drop it in a Resources folder, and it takes over.
//
// WHAT DOES NOT BELONG HERE: geometry the scene derives (route spacing, agent radius), thresholds a
// gate asserts against, and anything a builder measures rather than chooses. Those are contracts, and
// a config that lets them drift silently would turn a failing gate into a tuning knob.
using UnityEngine;

[CreateAssetMenu(fileName = "GmFeelConfig", menuName = "Games Master/Feel Config")]
public sealed class GmFeelConfig : ScriptableObject
{
    /// Where the shipped asset lives, relative to any Resources folder.
    public const string ResourceName = "GmFeelConfig";

    static GmFeelConfig resolved;

    /// The config every call site reads. Never null: with no authored asset this hands back an
    /// instance of the defaults below, which are the literals the call sites used to carry.
    ///
    /// Unity refuses Resources.Load from a MonoBehaviour constructor or instance field initializer,
    /// so read this from Awake/Start or a method -- never from a field initializer.
    public static GmFeelConfig Active
    {
        get
        {
            if (resolved != null) return resolved;
            resolved = Resources.Load<GmFeelConfig>(ResourceName);
            if (resolved == null)
            {
                resolved = CreateInstance<GmFeelConfig>();
                resolved.name = "GmFeelConfig (defaults)";
                // Survives a scene load rather than being collected and rebuilt on every transition.
                resolved.hideFlags = HideFlags.HideAndDontSave;
            }
            return resolved;
        }
    }

    [Header("Walk")]

    [Range(0.5f, 6f)]
    [Tooltip("Prologue walk speed in metres per second, overriding GmPlayer's own 3.5 default. Both " +
             "build entry points read this one value; they used to carry the literal separately, " +
             "where editing either alone left the walk running at two speeds depending on which " +
             "path built the scene.")]
    public float walkSpeedMetresPerSecond = 2.1f;

    [Header("Prologue rendering")]

    [Range(60, 360)]
    [Tooltip("Frame cap used by the vSync-off prologue performance contract. The command-line " +
             "diagnostic override can perturb this without changing the authored default.")]
    // 📝 DECISION: cap the no-vsync prologue at 100 fps | WHY: a 1,800-frame profiler capture put
    // the 240-fps tail in Metal present synchronization, not game work; the same release route at
    // 100 fps held 11.99ms p95 with full draw distance and one queued frame | ALT: 120 fps remained
    // quantized at 16.76-16.87ms p95 on the 120Hz presentation path and had no safety margin.
    public int wendTargetFrameRate = 100;

    [Range(40f, 200f)]
    [Tooltip("Lateral world-space band retained around the full 435m prologue route. Renderers " +
             "outside it are removed by the editor performance pass before the player is built.")]
    public float wendRenderCorridorMetres = 100f;

    [Range(100f, 400f)]
    [Tooltip("Far clip distance for the prologue player camera after the editor performance pass.")]
    public float wendCameraFarClipMetres = 180f;

    [Header("Gap lamps")]

    [Range(5f, 80f)]
    [Tooltip("How far a lamp is treated as carrying. Beyond this the route is unlit, which the walk " +
             "measures as a frame around 0.012 against a lit frame's 0.1 to 0.2.")]
    public float gapLampReachMetres = 30f;

    [Range(10f, 600f)]
    [Tooltip("Lumens per added gap lamp. The pack's own practicals land near 93 lumens after the " +
             "night scale and its brightest are clamped to 200, so the top of the pack's own range " +
             "keeps these consistent rather than introducing a brighter species of lamp.")]
    public float gapLampLumens = 200f;

    [Range(1000f, 6500f)]
    [Tooltip("Colour temperature of an added gap lamp, matching the paraffin temperature the pack's " +
             "practicals were retinted to. A different temperature reads as a different light source.")]
    public float gapLampKelvin = 2000f;

    [Range(0.5f, 8f)]
    [Tooltip("Gap lamp height above the ground, roughly a lamp bracket on a wall or a post.")]
    public float gapLampHeightMetres = 3.2f;

    [Header("Ambience placement")]

    [Range(10f, 300f)]
    [Tooltip("How far apart cricket anchors sit along the route. Crickets read as a near, local call, " +
             "so spacing them tighter than the owl gives more than one patch of insects across a " +
             "long walk instead of one source serving the whole route.")]
    public float cricketSpacingMetres = 70f;

    [Range(20f, 600f)]
    [Tooltip("How far apart owl anchors sit. Owls are rare and read as distant, so they can sit much " +
             "sparser without the walk ever crossing two anchors close enough to sound like a chorus.")]
    public float owlSpacingMetres = 180f;

    [Range(0f, 30f)]
    [Tooltip("How far off the walked line a wildlife anchor sits. On the route itself a spatial " +
             "source is loudest standing still on top of it, which reads as walking INTO the sound.")]
    public float ambienceLateralOffsetMetres = 8f;

    [Range(0f, 4f)]
    [Tooltip("Cricket anchor height above the ground.")]
    public float cricketHeightMetres = 0.4f;

    [Range(0f, 20f)]
    [Tooltip("Owl anchor height. Owls sit up in whatever canopy is nearby rather than at head " +
             "height, because every call in real use comes from a tree and not from the ground.")]
    public float owlHeightMetres = 6f;

    [Header("Ambience mix")]

    [Range(0f, 1f)]
    [Tooltip("Quietest the wind bed drifts to. The whole min-to-max band is deliberately narrow: " +
             "three looping beds stacked loud is what Nick called 'like being on a spaceship'.")]
    public float windVolumeMin = 0.07f;

    [Range(0f, 1f)]
    [Tooltip("Loudest the wind bed drifts to.")]
    public float windVolumeMax = 0.12f;

    [Range(0f, 1f)]
    [Tooltip("Volume of one cricket one-shot.")]
    public float cricketVolume = 0.032f;

    [Range(0f, 1f)]
    [Tooltip("Volume of one owl call.")]
    public float owlVolume = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Volume of one footstep.")]
    public float footstepVolume = 0.30f;

    [Range(0.4f, 4f)]
    [Tooltip("Metres walked between footsteps. Driven by distance rather than time so the stride " +
             "stays with the legs when the walk speed changes.")]
    public float footstepStrideMetres = 1.68f;

    [Range(1f, 120f)]
    [Tooltip("Shortest gap between cricket one-shots, in seconds.")]
    public float cricketIntervalMinSeconds = 14f;

    [Range(0f, 180f)]
    [Tooltip("Random seconds added on top of the shortest cricket gap, so the calls never fall into " +
             "a metronome.")]
    public float cricketIntervalSpanSeconds = 24f;

    [Range(1f, 300f)]
    [Tooltip("Shortest gap between owl calls, in seconds.")]
    public float owlIntervalMinSeconds = 58f;

    [Range(0f, 300f)]
    [Tooltip("Random seconds added on top of the shortest owl gap.")]
    public float owlIntervalSpanSeconds = 64f;

    [Header("Audio mix")]

    [Range(0f, 1f)]
    [Tooltip("Starting master volume. Nothing binds a slider to it yet; it scales both channels.")]
    public float masterVolume = 1.0f;

    [Range(0f, 1f)]
    [Tooltip("Starting ambience-channel volume, multiplied by master.")]
    public float ambienceVolume = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Starting SFX-channel volume, multiplied by master.")]
    public float sfxVolume = 0.9f;

    [Range(100f, 22000f)]
    [Tooltip("Low-pass cutoff while the Read dilates time. The filter sits on the AudioListener, so " +
             "this closes down the whole mix and not just one source. The Read ends by handing " +
             "hearing back open at 22 kHz.")]
    public float readLowPassCutoffHz = 450f;

    [Range(0f, 1f)]
    [Tooltip("Relative volume of the ninth bell toll.")]
    public float bellTollVolume = 1.0f;

    [Range(0f, 1f)]
    [Tooltip("Relative volume of the sanity heartbeat.")]
    public float heartbeatVolume = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Relative volume of a parlour card snap.")]
    public float cardSnapVolume = 0.75f;

    [Range(0f, 1f)]
    [Tooltip("Relative volume of a Shut-the-Box dice roll.")]
    public float diceRollVolume = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Relative volume of a court gavel strike.")]
    public float gavelStrikeVolume = 0.95f;

    [Header("Display calibration")]

    [Range(-6, 0)]
    [Tooltip("Darkest calibration step the player can select. The authored fixed exposure is level 0 " +
             "and the range either side is deliberately narrow.")]
    public int displayMinimumLevel = -2;

    [Range(0, 6)]
    [Tooltip("Brightest calibration step the player can select.")]
    public int displayMaximumLevel = 2;

    [Range(0.05f, 1f)]
    [Tooltip("Stops of post-exposure per calibration step.")]
    public float displayStopsPerLevel = 0.25f;

    [Header("Pause menu")]

    [Range(0.5f, 1f)]
    [Tooltip("Smallest text scale the accessibility slider allows.")]
    public float minTextScale = 0.8f;

    [Range(1f, 3f)]
    [Tooltip("Largest text scale the accessibility slider allows.")]
    public float maxTextScale = 2.0f;

    [Header("Sanity rewards")]

    [Range(0f, 1f)]
    [Tooltip("Sanity returned the first time the run reads Aldric's original invitation.")]
    public float hiddenRoomInvitationSanityGain = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Sanity returned by a correct Hold accusation in Shut the Box.")]
    public float shutTheBoxCatchSanityGain = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Sanity returned for blinding the Huntsman with the assembled mirror.")]
    public float huntsmanMirrorSanityGain = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Sanity returned for correctly accusing Aldric of a cheat during the Read.")]
    public float parlorReadCatchSanityGain = 0.15f;

    [Header("Court hearing")]

    [Range(1, 9)]
    [Tooltip("Wax seals a hearing opens with. Cracking the last one wins the hearing, so this is the " +
             "length of the whole scene.")]
    public int courtWaxSeals = 3;

    [Range(15f, 300f)]
    [Tooltip("Seconds on the pressure clock. Running it out loses the hearing and raises corruption.")]
    public float courtPressureSeconds = 90.0f;

    [Header("Labyrinth")]

    [Range(2f, 60f)]
    [Tooltip("How close the Huntsman tracks before the stalk becomes a pursuit.")]
    public float huntsmanStalkDistanceMetres = 15.0f;

    [Header("The Reckoning — contextual bell cadence")]
    // docs/superpowers/specs/2026-08-13-the-reckoning.md. Every default below reproduces the
    // shipped fixed cadence exactly (45 + 8x30 = 285s) -- the feature ships dark until Nick turns
    // reckoningPressureAuthority on a walk. None of these numbers are a recommendation.

    [Range(0f, 1f)]
    [Tooltip("How much the grounds reckoning is allowed to move the bell's cadence at all. 0 " +
             "reproduces the shipped fixed 285s cadence exactly, unaffected by anything the player " +
             "does. 1 lets a maximal reckoning pull every interval down to its floor. This is the " +
             "single dial that turns the whole feature on; it ships at 0 on purpose.")]
    public float reckoningPressureAuthority = 0f;

    [Range(0f, 0.35f)]
    [Tooltip("Seeded random wobble applied to each toll, as a fraction of that toll's interval. 0 " +
             "is a metronome, as shipped. Above roughly a fifth the bell stops reading as a clock " +
             "at all. Positive jitter can never push a toll later than its own base interval -- " +
             "the schedule's ceiling is the unpressured cadence, not the wobble.")]
    public float reckoningTollJitterFraction = 0f;

    [Range(20f, 90f)]
    [Tooltip("The fastest the first toll can ever arrive, however much pressure and jitter push it. " +
             "Kept at or above 20s so no legal value here can read as the test-speed pacing " +
             "GmPerceptualAudit rejects.")]
    public float reckoningFirstTollFloorSeconds = 20f;

    [Range(15f, 60f)]
    [Tooltip("The fastest any toll after the first can ever arrive. Kept at or above 15s for the " +
             "same reason as the first-toll floor above.")]
    public float reckoningTollFloorSeconds = 15f;

    [Range(1f, 200f)]
    [Tooltip("Total weighted 'owed' units that count as fully explored -- the denominator pressure " +
             "is normalised against. Lower makes a single detour decisive; higher makes it take a " +
             "whole evening's wandering to matter.")]
    public float reckoningOwedForFullPressure = 20f;

    [Range(0f, 10f)]
    [Tooltip("Pressure units added the first time the player enters each of the estate's authored " +
             "branch rects. Free signal: the rects and their fired-once flags already ship in " +
             "GmDesignRuntime. 0 means entering a branch does nothing to the count, as shipped.")]
    public float reckoningWeightBranchEntered = 0f;

    [Range(0f, 10f)]
    [Tooltip("Pressure units added the first time each grounds POI is examined. 0 means examining " +
             "a POI does nothing to the count, as shipped.")]
    public float reckoningWeightPoiExamined = 0f;

    [Header("The Reckoning — outbuildings")]

    [Range(0f, 20f)]
    [Tooltip("Pressure units added the first time the player steps inside an outbuilding (coach " +
             "house, and later slices). 0 means entering does nothing to the count, as shipped.")]
    public float reckoningWeightOutbuildingEntered = 0f;

    [Range(0f, 2f)]
    [Tooltip("Pressure units per second spent inside an outbuilding, up to the dwell cap below. 0 " +
             "means lingering does nothing to the count, as shipped.")]
    public float reckoningWeightOutbuildingDwellPerSecond = 0f;

    [Range(10f, 300f)]
    [Tooltip("The most dwell-time seconds that can ever be charged per building visit. Without a " +
             "cap, simply standing still could push a run onto its floor by itself.")]
    public float reckoningDwellChargeCapSeconds = 60f;

    [Range(0f, 6f)]
    [Tooltip("Base pressure units for the first clue read inside an outbuilding. 0 means reading a " +
             "clue does nothing to the count, as shipped.")]
    public float reckoningWeightOutbuildingDiscoveryBase = 0f;

    [Range(1f, 2.5f)]
    [Tooltip("Multiplier applied per successive clue discovered in the same building. 1 means every " +
             "clue costs the same (no escalation), as shipped. Above 1, each further clue in one " +
             "building costs more than the last, so restraint becomes a real choice.")]
    public float reckoningDiscoveryEscalation = 1f;
}
