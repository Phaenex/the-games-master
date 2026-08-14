// The house's clock. The invitation says nine; the chapel bell tolls nine; on the ninth you are
// taken wherever you stand. See docs/superpowers/specs/2026-07-17-the-ninth-bell.md and its
// 2026-08-13 addendum (docs/superpowers/specs/2026-08-13-the-reckoning.md).
//
// Single authority ON PURPOSE: symptoms, the crossing and the wake all read Toll from here rather
// than running their own timers, which would drift out of sync with the audio and kill the effect.
//
// The COUNT is still fixed and inevitable -- nine, always, no matter what the player does. What
// changed 2026-08-13 is the SPACING: each toll's interval is now drawn from GmReckoningSchedule,
// which reads GmFeelConfig.reckoningPressureAuthority and the grounds exploration pressure. At the
// shipped default (authority 0, jitter 0) the schedule collapses to exactly the old fixed cadence
// -- 45 + 8x30 = 285s -- so this file changed, but nothing about how it plays did, until that dial
// moves. A real bell does not take five minutes to ring nine; nobody says so, and the spacing is
// deniable right up until it isn't.
using System;
using UnityEngine;

public enum GmBellReviewProfile
{
    Control285,
    Middle240,
    Tight195
}

public class GmBellSummons : MonoBehaviour
{
    public float firstTollDelay = 45f;      // ~4m45s of grounds before toll 9 lands (9 * 30 + 45)
    public float tollInterval = 30f;        // real cadence -- see BellCadenceIsShippable guard test
    public int Toll { get; private set; }   // 0..9, read by everything downstream
    public bool Taken => Toll >= 9;
    public GmBellReviewProfile reviewProfile = GmBellReviewProfile.Control285;
    public string ReviewProfileId => reviewProfile == GmBellReviewProfile.Tight195 ? "tight-195" :
        reviewProfile == GmBellReviewProfile.Middle240 ? "middle-240" : "control-285";

    // The bell is diegetic: it comes from the chapel and is louder near it. Distance attenuates the
    // volume; it never attenuates the count.
    //
    // Serialized rather than const because this is a WORLD position, and the estate's chapel and the
    // village's church are 200m apart. Left at the estate's coordinate by default so that scene is
    // unchanged; the village builder overwrites it with the church's measured position. Getting this
    // wrong is silent and nasty -- the count still runs, it just tolls from an empty field.
    [SerializeField] Vector3 chapelPosition = new Vector3(28.9f, 6f, 30f);

    public Vector3 ChapelPosition
    {
        get => chapelPosition;
        set => chapelPosition = value;
    }

    GmAmbience amb;
    GmDesignRuntime rt;
    GmThreshold threshold;
    GmGroundsExploration exploration;
    AudioSource bell;
    float next = -1f;
    bool armed;
    public bool Armed => armed;
    System.Random jitterRng;

    void Start()
    {
        ApplyLaunchArguments(Environment.GetCommandLineArgs());
        ApplyReviewProfile(reviewProfile);
        amb = FindFirstObjectByType<GmAmbience>();
        rt = FindFirstObjectByType<GmDesignRuntime>();
        threshold = FindFirstObjectByType<GmThreshold>();
        exploration = FindFirstObjectByType<GmGroundsExploration>();

        var go = new GameObject("BellSource");
        go.transform.position = chapelPosition;
        bell = go.AddComponent<AudioSource>();
        bell.spatialBlend = 1f;             // 3D: locatable, walkable-to
        bell.rolloffMode = AudioRolloffMode.Logarithmic;
        bell.minDistance = 12f;
        bell.maxDistance = 260f;            // audible from the whole estate, never comfortable
        bell.clip = GmAmbience.Clip("chapel_bell");
        bell.playOnAwake = false;
        // The count is the one sound the player MUST be able to resolve: the ending is arithmetic
        // over it, and there is no second channel. GmSymptoms puts an AudioLowPassFilter on the
        // camera's AudioListener and walks its cutoff down as the tolls advance (4000Hz by six,
        // 1200 by eight, 500 by nine), so the sanity curve was progressively eating the exact thing
        // it was counting -- worst precisely at toll nine. Bypassing listener effects keeps the bell
        // out of that filter without a mixer asset, and reads better than it measures: as the
        // player's hearing collapses, the bell is the one thing that stays clear.
        bell.bypassListenerEffects = true;
        GmAdaptiveIntentAuthoring.Audio(go, "chapel-bell", GmAudioIntentKind.Diegetic,
            GmAudioLoopPolicy.Never,
            "The count is locatable at the chapel and plays once per authored toll.", "chapel-building");
    }

    /// Armed by GmThreshold when the gate locks: the bell can only count someone the house already
    /// has. Retreating to the car before the gate (the seventh state) beats it outright.
    public void Arm()
    {
        if (armed) return;
        armed = true;
        jitterRng = GmRunSeed.ForStream("toll-jitter");
        next = Time.time + ScheduleInterval(Toll + 1);
        Debug.Log($"[GmBell] armed profile={ReviewProfileId} — first toll in {firstTollDelay}s, then every {tollInterval}s");
        GmExperienceTelemetry.Record("bell-armed", ReviewProfileId);
    }

    /// The interval before the given (1-indexed) toll, per GmReckoningSchedule. At the shipped
    /// default (authority 0, jitter 0) this always returns firstTollDelay/tollInterval exactly --
    /// the same literals the old code used directly -- so arming and every toll boundary reproduce
    /// today's cadence bit for bit until GmFeelConfig.reckoningPressureAuthority moves off zero.
    float ScheduleInterval(int tollIndex)
    {
        GmFeelConfig cfg = GmFeelConfig.Active;
        float pressure = exploration != null ? exploration.Pressure : 0f;
        float jitter = GmReckoningSchedule.JitterFraction(jitterRng, cfg.reckoningTollJitterFraction);
        return GmReckoningSchedule.IntervalForToll(tollIndex, firstTollDelay, tollInterval,
            cfg.reckoningPressureAuthority, pressure, jitter,
            cfg.reckoningFirstTollFloorSeconds, cfg.reckoningTollFloorSeconds);
    }

    public bool SetReviewProfile(GmBellReviewProfile profile)
    {
        if (armed)
        {
            Debug.LogWarning("[GmBell] cadence cannot change after the count is armed");
            return false;
        }
        ApplyReviewProfile(profile);
        GmExperienceTelemetry.Record("pacing-profile", ReviewProfileId);
        return true;
    }

    void ApplyReviewProfile(GmBellReviewProfile profile)
    {
        reviewProfile = profile;
        if (profile == GmBellReviewProfile.Tight195)
        { firstTollDelay = 35f; tollInterval = 20f; }
        else if (profile == GmBellReviewProfile.Middle240)
        { firstTollDelay = 40f; tollInterval = 25f; }
        else
        { firstTollDelay = 45f; tollInterval = 30f; }
    }

    public void ApplyLaunchArguments(string[] args)
    {
        if (args == null) return;
        foreach (string arg in args)
        {
            if (!arg.StartsWith("-gmPacing=", StringComparison.OrdinalIgnoreCase)) continue;
            string value = arg.Substring("-gmPacing=".Length).ToLowerInvariant();
            if (value == "195" || value == "tight") reviewProfile = GmBellReviewProfile.Tight195;
            else if (value == "240" || value == "middle") reviewProfile = GmBellReviewProfile.Middle240;
            else if (value == "285" || value == "control") reviewProfile = GmBellReviewProfile.Control285;
        }
    }

    void CycleReviewProfile()
    {
        SetReviewProfile((GmBellReviewProfile)(((int)reviewProfile + 1) % 3));
        Debug.Log($"[GmBell] review cadence: {ReviewProfileId} ({firstTollDelay:F0}+8x{tollInterval:F0})");
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!armed && Input.GetKeyDown(KeyCode.F9)) CycleReviewProfile();
#endif
        if (!armed || Toll >= 9 || Time.time < next) return;

        Toll++;
        next = Time.time + ScheduleInterval(Toll + 1);
        if (bell != null && bell.clip != null) bell.Play();
        Debug.Log($"[GmBell] toll {Toll}/9");
        GmExperienceTelemetry.Record("bell-toll", Toll.ToString());

        if (Toll == 9)
        {
            Debug.Log("[GmBell] TAKEN — the count is finished");
            // The card and the crossing used to be called on consecutive lines in the same frame.
            // GmCrossing cuts to black immediately and GmPrologueHud hands it the screen, so the one
            // line the whole nine-count exists to pay off -- the reason the number matters -- was
            // never actually rendered to a player. It was dead text that read correctly in source.
            //
            // The toll itself still LANDS on time: the ninth chime plays above, unchanged. Only the
            // cut waits, and only long enough for the card to be readable. The crossing owns
            // everything after that.
            const string main = "The card said nine.";
            const string sub = "I have never been on time for anything in my life. Ask Mara. Ask the bank. Ask the men who call before sunrise. I was early for this.";
            rt?.ShowBeat(main, sub);
            takenCardHold = GmDesignRuntime.BeatSecondsFor(main, sub);
            Invoke(nameof(BeginCrossingAfterCard), takenCardHold);
        }
    }

    /// Seconds the taken-card is held before the cut. Exposed so a proof run can assert the card had
    /// a real window rather than being overwritten in its own frame.
    public float TakenCardHold => takenCardHold;
    float takenCardHold;

    void BeginCrossingAfterCard() => threshold?.BeginCrossing();
}
