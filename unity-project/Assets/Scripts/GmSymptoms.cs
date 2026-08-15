// Head and hearing coming apart, one toll at a time. Reads GmBellSummons.Toll rather than running
// its own timer -- a second timer would drift against the audio and the effect dies.
//
// Monotonic by construction: values are a pure function of the toll number, so nothing can recover
// between tolls. There is no cure and the player must not be able to find one.
// See docs/superpowers/specs/2026-07-17-the-ninth-bell.md and the Task 3 table in the plan.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class GmSymptoms : MonoBehaviour
{
    static readonly float[] VignetteAt   = { 0, 0, 0, 0, 0.18f, 0.30f, 0.42f, 0.55f, 0.72f, 1.00f };
    static readonly float[] ChromaticAt  = { 0, 0, 0, 0, 0.10f, 0.20f, 0.34f, 0.48f, 0.66f, 0.66f };
    static readonly float[] DistortionAt = { 0, 0, 0, 0, 0.00f, -0.05f, -0.10f, -0.16f, -0.24f, -0.24f };
    static readonly float[] LowpassAt    = { 22000, 22000, 22000, 22000, 9000, 6000, 4000, 2400, 1200, 500 };

    GmBellSummons bell;
    GmPlayer player;
    GmAmbience amb;
    GmAudioMixController mix;
    Vignette vignette;
    ChromaticAberration chromatic;
    LensDistortion distortion;
    AudioLowPassFilter lowpass;
    AudioSource heart, whine;
    float baseWalkSpeed;
    int applied = -1;
    bool crossing;

    public bool InternalLayersMuted => crossing &&
        (heart == null || heart.volume <= 0.0001f) &&
        (whine == null || whine.volume <= 0.0001f);

    void Start()
    {
        bell = FindFirstObjectByType<GmBellSummons>();
        player = FindFirstObjectByType<GmPlayer>();
        amb = FindFirstObjectByType<GmAmbience>();
        mix = FindFirstObjectByType<GmAudioMixController>();
        if (player != null) baseWalkSpeed = player.walkSpeed;

        // Own volume, high priority: must win over the estate's NightVolume without editing it.
        var volGo = new GameObject("SymptomVolume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;   // runtime-only: no asset, so the SaveAssets trap does not apply

        vignette = profile.Add<Vignette>(true);             vignette.intensity.Override(0);
        chromatic = profile.Add<ChromaticAberration>(true); chromatic.intensity.Override(0);
        distortion = profile.Add<LensDistortion>(true);     distortion.intensity.Override(0);

        var cam = Camera.main;
        if (cam != null)
        {
            // AudioLowPassFilter [RequireComponent]s an AudioListener or AudioSource. The builder
            // never puts a listener on the camera (no scene AudioListener at all yet -- a real,
            // separate gap this surfaced), so add one defensively rather than let the filter fail
            // silently and take the symptom curve's hearing effect down with it.
            if (cam.gameObject.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            lowpass = cam.gameObject.AddComponent<AudioLowPassFilter>();
            lowpass.cutoffFrequency = 22000;
        }

        heart = MakeLoop("heartbeat", 0f);
        whine = MakeLoop("ear_whine", 0f);
    }

    AudioSource MakeLoop(string clip, float vol)
    {
        var go = new GameObject($"sym_{clip}");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = GmAmbience.Clip(clip);
        src.loop = true;
        src.spatialBlend = 0f;   // in his head, not in the world
        src.volume = vol;
        GmAdaptiveIntentAuthoring.Audio(go, $"symptom-{clip}", GmAudioIntentKind.Bed,
            GmAudioLoopPolicy.Required,
            "An explicitly paced internal symptom layer appears only at its authored bell threshold.");
        if (src.clip != null) src.Play();
        else Debug.LogWarning($"[GmSymptoms] missing curated clip '{clip}'");
        return src;
    }

    /// The ninth toll cuts the symptom build completely. When hearing returns during the crossing,
    /// the first readable sound must be the human whisper, not a heartbeat and tinnitus mix left
    /// running underneath it from toll eight.
    public void BeginCrossing()
    {
        crossing = true;
        if (heart != null) { heart.volume = 0f; heart.Stop(); }
        if (whine != null) { whine.volume = 0f; whine.Stop(); }
        Debug.Log("[GmSymptoms] internal layers cut for crossing");
    }

    void Update()
    {
        if (crossing) return;
        if (bell == null) return;
        int t = Mathf.Clamp(bell.Toll, 0, 9);
        if (t == applied) return;
        applied = t;

        vignette?.intensity.Override(VignetteAt[t]);
        chromatic?.intensity.Override(ChromaticAt[t]);
        distortion?.intensity.Override(DistortionAt[t]);
        if (lowpass != null) lowpass.cutoffFrequency = LowpassAt[t];

        if (heart != null) heart.volume = t >= 5 ? Mathf.Min(0.85f, 0.25f * (t - 4)) : 0f;
        if (whine != null) whine.volume = t >= 6 ? Mathf.Min(0.55f, 0.16f * (t - 5)) : 0f;

        // Toll 7: the world goes away and leaves him with the inside of his own head.
        if (amb != null) amb.SetBedVolume(t >= 7 ? 0f : 1f);
        if (mix != null) mix.Apply(t >= 7 ? GmAudioMixState.Taken :
            t >= 4 ? GmAudioMixState.Threshold : GmAudioMixState.Exploration);

        // Toll 8: his legs stop being his.
        if (player != null) player.walkSpeed = t >= 8 ? baseWalkSpeed * 0.6f : baseWalkSpeed;

        Debug.Log($"[GmSymptoms] toll {t}: vig={VignetteAt[t]:F2} chrom={ChromaticAt[t]:F2} lowpass={LowpassAt[t]}Hz");
    }
}

