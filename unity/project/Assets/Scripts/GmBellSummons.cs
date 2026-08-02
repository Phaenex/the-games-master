// The house's clock. The invitation says nine; the chapel bell tolls nine; on the ninth you are
// taken wherever you stand. See docs/superpowers/specs/2026-07-17-the-ninth-bell.md.
//
// Single authority ON PURPOSE: symptoms, the crossing and the wake all read Toll from here rather
// than running their own timers, which would drift out of sync with the audio and kill the effect.
//
// Cadence is fixed real time and ignores route and action. That is the whole point -- no matter what
// the player does, they are counted in at nine. A real bell does not take five minutes to ring nine;
// nobody says so, and the spacing is deniable right up until it isn't.
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
    AudioSource bell;
    float next = -1f;
    bool armed;
    public bool Armed => armed;

    void Start()
    {
        ApplyLaunchArguments(Environment.GetCommandLineArgs());
        ApplyReviewProfile(reviewProfile);
        amb = FindFirstObjectByType<GmAmbience>();
        rt = FindFirstObjectByType<GmDesignRuntime>();
        threshold = FindFirstObjectByType<GmThreshold>();

        var go = new GameObject("BellSource");
        go.transform.position = chapelPosition;
        bell = go.AddComponent<AudioSource>();
        bell.spatialBlend = 1f;             // 3D: locatable, walkable-to
        bell.rolloffMode = AudioRolloffMode.Logarithmic;
        bell.minDistance = 12f;
        bell.maxDistance = 260f;            // audible from the whole estate, never comfortable
        bell.clip = GmAmbience.Clip("chapel_bell");
        bell.playOnAwake = false;
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
        next = Time.time + firstTollDelay;
        Debug.Log($"[GmBell] armed profile={ReviewProfileId} — first toll in {firstTollDelay}s, then every {tollInterval}s");
        GmExperienceTelemetry.Record("bell-armed", ReviewProfileId);
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
        next = Time.time + tollInterval;
        if (bell != null && bell.clip != null) bell.Play();
        Debug.Log($"[GmBell] toll {Toll}/9");
        GmExperienceTelemetry.Record("bell-toll", Toll.ToString());

        if (Toll == 9)
        {
            Debug.Log("[GmBell] TAKEN — the count is finished");
            rt?.ShowBeat("The card said nine.",
                "I have never been on time for anything in my life. Ask Mara. Ask the bank. Ask the men who call before sunrise. I was early for this.");
            threshold?.BeginCrossing();
        }
    }
}
