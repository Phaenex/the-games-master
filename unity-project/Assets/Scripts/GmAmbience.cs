// Silence-led exterior soundscape. Licensed Book of the Dead recordings supply the broad air,
// localized gusts and material footsteps; short legacy wildlife clips remain sparse one-shots.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmWindReviewProfile
{
    ControlContinuous,
    SparseLocalized,
    HybridBreathing
}

[DisallowMultipleComponent]
public sealed class GmAmbience : MonoBehaviour
{
    public bool useContinuousWind = false;
    public GmWindReviewProfile reviewProfile = GmWindReviewProfile.SparseLocalized;
    public string ReviewProfileId => reviewProfile == GmWindReviewProfile.SparseLocalized ? "sparse" :
        reviewProfile == GmWindReviewProfile.HybridBreathing ? "hybrid" : "control";
    public GmSurfaceKind CurrentSurface { get; private set; } = GmSurfaceKind.DeadGrass;
    public int LocalizedEmitterCount => localGusts.Count;
    public string ContinuousBedName => windBed != null && windBed.clip != null ? windBed.clip.name : "";

    sealed class GustEmitter
    {
        public AudioSource source;
        public AudioClip clip;
        public float remaining;
        public float duration;
        public float seed;
    }

    AudioSource windBed, oneShot, stepSource, cricketSource, owlSource;
    AudioClip cricketsClip, owlClip;
    readonly List<GustEmitter> localGusts = new List<GustEmitter>();
    readonly Dictionary<GmSurfaceKind, List<AudioClip>> footstepPools =
        new Dictionary<GmSurfaceKind, List<AudioClip>>();
    readonly Dictionary<GmSurfaceKind, int> lastFootstep = new Dictionary<GmSurfaceKind, int>();
    Transform player;
    GmPlayer playerController;
    Vector3 lastPosition;
    float stepDistance, owlTimer, insectsTimer, bedScale = 1f, windSeed;
    float localizedSilenceRemaining;
    int activeLocalizedGust = -1, lastLocalizedGust = -1;
    GmAudioMixController mix;

    public int ActiveLocalizedGustCount
    {
        get
        {
            int count = 0;
            foreach (GustEmitter gust in localGusts)
                if (gust.remaining > 0f || (gust.source != null && gust.source.isPlaying)) count++;
            return count;
        }
    }
    public float LocalizedSilenceRemaining => localizedSilenceRemaining;
    public int LastLocalizedEmitterIndex => lastLocalizedGust;
    public int ActiveLocalizedEmitterIndex => activeLocalizedGust;
    public int LocalizedWildlifeEmitterCount =>
        (cricketSource != null ? 1 : 0) + (owlSource != null ? 1 : 0);

    const float ControlWindVolume = 0.052f;
    // The hybrid source measures roughly 10.8 dB louder than the long natural recording. This lower
    // fader equalises their delivered bed energy instead of making the preferred candidate win by
    // simple loudness during review.
    const float HybridWindVolume = 0.015f;
    const float LocalGustVolume = 0.11f;
    const float CricketVolume = 0.032f;
    const float FootstepVolume = 0.30f;

    static readonly Vector3[] GustPositions =
    {
        new Vector3(-15f, 4.5f, 54f),
        new Vector3(16f, 3.8f, 30f),
        new Vector3(-18f, 4.2f, -2f),
        new Vector3(19f, 5.0f, -27f)
    };

    static readonly Vector3[] CricketPositions =
    {
        new Vector3(-20f, 0.8f, 25f),
        new Vector3(18f, 0.8f, 34f),
        new Vector3(-14f, 0.8f, -8f)
    };

    static readonly Vector3[] OwlPositions =
    {
        new Vector3(18f, 8f, 8f),
        new Vector3(-17f, 7f, -5f),
        new Vector3(13f, 9f, -29f)
    };

    void Start()
    {
        ApplyLaunchArguments(Environment.GetCommandLineArgs());
        Initialize(true);
    }

    // Kept separate from Start so EditMode verification can inspect the exact graph without asking
    // Unity to play audio outside Play mode.
    void Initialize(bool beginPlayback)
    {
        mix = GetComponent<GmAudioMixController>() ?? gameObject.AddComponent<GmAudioMixController>();
        windBed = MakeBed("amb_wind_natural");
        cricketsClip = Find("amb_crickets");
        owlClip = Find("amb_owl");
        oneShot = MakeOneShotSource("story_one_shots", GmAudioIntentKind.Stinger,
            "Authored story sounds interrupt silence without becoming a second ambience bed.");
        stepSource = MakeOneShotSource("surface_footsteps", GmAudioIntentKind.Foley,
            "Distance-driven, semantic-surface footsteps use eight-clip no-repeat pools.");
        cricketSource = MakeLocalizedWildlifeSource("localized_crickets", cricketsClip,
            CricketPositions[0], 5f, 32f,
            "A sparse insect call belongs to one garden or verge pocket, never the whole stereo field.");
        owlSource = MakeLocalizedWildlifeSource("localized_owl", owlClip,
            OwlPositions[0], 9f, 58f,
            "A rare owl call occupies a distant tree rather than the player's non-diegetic audio centre.");
        BuildLocalizedGusts();
        LoadFootstepPools();

        windSeed = UnityEngine.Random.value * 100f;
        var foundPlayer = FindFirstObjectByType<GmPlayer>();
        if (foundPlayer != null)
        {
            playerController = foundPlayer;
            player = foundPlayer.transform;
            lastPosition = player.position;
            CurrentSurface = DetectSurface(player.position);
        }
        owlTimer = 48f + UnityEngine.Random.value * 52f;
        insectsTimer = 11f + UnityEngine.Random.value * 20f;
        ApplyReviewProfile(reviewProfile, beginPlayback);
        GmPerceptualAuthoring.Soundscape(gameObject, "wend-hill-night",
            "Organic rural silence interrupted by one moving localized wind event at a time and semantic footsteps; never a continuous machine-like wash.",
            0, 0.35f, 8f, Array.Empty<string>());
    }

    AudioSource MakeBed(string clipName)
    {
        var owner = new GameObject("exterior_wind_bed");
        owner.transform.SetParent(transform, false);
        var source = owner.AddComponent<AudioSource>();
        source.clip = Find(clipName);
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        var filter = owner.AddComponent<AudioHighPassFilter>();
        filter.cutoffFrequency = 105f;
        filter.highpassResonanceQ = 1f;
        GmAdaptiveIntentAuthoring.Audio(owner, "exterior-wind-bed", GmAudioIntentKind.Bed,
            GmAudioLoopPolicy.Allowed,
            "Optional review-only natural air control; shipping sparse mode keeps this source stopped and non-looping.");
        return source;
    }

    void BuildLocalizedGusts()
    {
        for (int i = 0; i < GustPositions.Length; i++)
        {
            var owner = new GameObject($"localized_tree_wind_{i + 1:00}");
            owner.transform.SetParent(transform, true);
            owner.transform.position = GustPositions[i];
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 5f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
            var clip = Find($"wind_local_{i + 1:00}");
            source.clip = clip;
            localGusts.Add(new GustEmitter
            {
                source = source,
                clip = clip,
                seed = 13.7f + i * 19.3f
            });
            GmAdaptiveIntentAuthoring.Audio(owner, $"localized-tree-wind-{i + 1:00}",
                GmAudioIntentKind.Diegetic, GmAudioLoopPolicy.Never,
                "A short mono breeze is anchored to vegetation and separated by authored silence.");
        }
    }

    void LoadFootstepPools()
    {
        AddFootstepPool(GmSurfaceKind.PackedMud, "dirt");
        AddFootstepPool(GmSurfaceKind.WetMud, "dirt");
        AddFootstepPool(GmSurfaceKind.Gravel, "gravel");
        AddFootstepPool(GmSurfaceKind.DeadGrass, "grass");
        AddFootstepPool(GmSurfaceKind.LeafLitter, "leaves");
        AddFootstepPool(GmSurfaceKind.Stone, "stone");
        AddFootstepPool(GmSurfaceKind.Wood, "wood");
    }

    void AddFootstepPool(GmSurfaceKind surface, string prefix)
    {
        var clips = new List<AudioClip>(8);
        for (int i = 1; i <= 8; i++)
        {
            var clip = Find($"foot_{prefix}_{i:00}");
            if (clip != null) clips.Add(clip);
        }
        footstepPools[surface] = clips;
        lastFootstep[surface] = -1;
    }

    AudioSource MakeOneShotSource(string name, GmAudioIntentKind kind, string rationale)
    {
        var owner = new GameObject(name);
        owner.transform.SetParent(transform, false);
        var source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        GmAdaptiveIntentAuthoring.Audio(owner, name, kind, GmAudioLoopPolicy.Never, rationale);
        return source;
    }

    AudioSource MakeLocalizedWildlifeSource(string name, AudioClip clip, Vector3 position,
        float minDistance, float maxDistance, string rationale)
    {
        var owner = new GameObject(name);
        owner.transform.SetParent(transform, true);
        owner.transform.position = position;
        var source = owner.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        GmAdaptiveIntentAuthoring.Audio(owner, name, GmAudioIntentKind.Diegetic,
            GmAudioLoopPolicy.Never, rationale);
        return source;
    }

    static AudioClip Find(string name) => Resources.Load<AudioClip>($"Sfx/{name}");
    public static AudioClip Clip(string name) => Find(name);
    public int FootstepClipCount(GmSurfaceKind surface) =>
        footstepPools.TryGetValue(surface, out var clips) ? clips.Count : 0;

    public void PlayOneShot(string clipName, float volume)
    {
        var clip = Find(clipName);
        if (clip == null || oneShot == null) return;
        oneShot.pitch = 1f;
        oneShot.PlayOneShot(clip, volume * Gain(GmAudioBus.Story));
    }

    public void SetBedVolume(float scale) => bedScale = Mathf.Clamp01(scale);

    public void SetContinuousWindForReview(bool enabled) =>
        ApplyReviewProfile(enabled ? GmWindReviewProfile.ControlContinuous :
            GmWindReviewProfile.SparseLocalized, true);

    public void SetReviewProfile(GmWindReviewProfile profile) => ApplyReviewProfile(profile, true);

    void ApplyReviewProfile(GmWindReviewProfile profile, bool beginPlayback)
    {
        reviewProfile = profile;
        useContinuousWind = profile != GmWindReviewProfile.SparseLocalized;
        if (windBed != null)
        {
            windBed.Stop();
            windBed.clip = Find(profile == GmWindReviewProfile.HybridBreathing
                ? "amb_wind_hybrid" : "amb_wind_natural");
            windBed.loop = profile != GmWindReviewProfile.SparseLocalized;
            windBed.pitch = 1f;
            windBed.volume = 0f;
            if (beginPlayback && windBed.loop && windBed.clip != null)
            {
                windBed.timeSamples = UnityEngine.Random.Range(0, windBed.clip.samples);
                windBed.Play();
            }
        }
        foreach (var gust in localGusts)
        {
            gust.source.Stop();
            gust.remaining = 0f;
            gust.source.volume = 0f;
        }
        activeLocalizedGust = -1;
        lastLocalizedGust = -1;
        localizedSilenceRemaining = 3f + UnityEngine.Random.value * 5f;
        Debug.Log($"[GmAmbience] review profile: {ReviewProfileId}");
        GmExperienceTelemetry.Record("wind-profile", ReviewProfileId);
    }

    // The built-player proof must restore the accepted profile after exercising the controller A/B
    // path. Keep normal profile application private so gameplay still has one bounded input route.
    public void RestoreReviewProfileForProof(GmWindReviewProfile profile, bool beginPlayback) =>
        ApplyReviewProfile(profile, beginPlayback);

    public void ApplyLaunchArguments(string[] args)
    {
        if (args == null) return;
        foreach (string arg in args)
        {
            if (!arg.StartsWith("-gmWind=", StringComparison.OrdinalIgnoreCase)) continue;
            string value = arg.Substring("-gmWind=".Length).ToLowerInvariant();
            if (value == "control") reviewProfile = GmWindReviewProfile.ControlContinuous;
            else if (value == "sparse") reviewProfile = GmWindReviewProfile.SparseLocalized;
            else if (value == "hybrid") reviewProfile = GmWindReviewProfile.HybridBreathing;
        }
    }

    void Update()
    {
        if (playerController != null && playerController.ReviewWindPressedThisFrame)
            // Player-facing A/B is deliberately sparse versus the natural control. The rejected
            // hybrid remains launch-argument-only evidence and cannot sneak back into normal play.
            ApplyReviewProfile(reviewProfile == GmWindReviewProfile.SparseLocalized
                ? GmWindReviewProfile.ControlContinuous : GmWindReviewProfile.SparseLocalized, true);
        UpdateWind();
        UpdateFootsteps();
        UpdateWildlife();
    }

    void UpdateWind()
    {
        float weatherGain = bedScale * Gain(GmAudioBus.Weather);
        if (windBed != null && windBed.loop)
        {
            if (!windBed.isPlaying && windBed.clip != null) windBed.Play();
            float drift = Mathf.PerlinNoise(windSeed, Time.unscaledTime * 0.021f);
            if (reviewProfile == GmWindReviewProfile.ControlContinuous)
                windBed.volume = ControlWindVolume * weatherGain * Mathf.Lerp(0.72f, 1f, drift);
            else
            {
                float silence = Mathf.PerlinNoise(windSeed + 71.3f, Time.unscaledTime * 0.012f);
                float gate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.68f, silence));
                windBed.volume = HybridWindVolume * weatherGain * Mathf.Lerp(0.38f, 1f, drift) * gate;
            }
            windBed.pitch = 1f;
        }
        else if (windBed != null) windBed.volume = 0f;

        if (reviewProfile == GmWindReviewProfile.ControlContinuous)
        {
            foreach (var gust in localGusts) if (gust.source.isPlaying) gust.source.Stop();
            return;
        }

        UpdateLocalizedWind(weatherGain, Time.deltaTime);
    }

    void UpdateLocalizedWind(float weatherGain, float deltaTime)
    {
        if (activeLocalizedGust >= 0 && activeLocalizedGust < localGusts.Count)
        {
            GustEmitter gust = localGusts[activeLocalizedGust];
            UpdateActiveLocalizedGust(gust, weatherGain, deltaTime);
            return;
        }

        localizedSilenceRemaining -= deltaTime;
        if (localizedSilenceRemaining > 0f || localGusts.Count == 0) return;

        int selected = UnityEngine.Random.Range(0, localGusts.Count);
        if (localGusts.Count > 1 && selected == lastLocalizedGust)
            selected = (selected + 1 + UnityEngine.Random.Range(0, localGusts.Count - 1)) % localGusts.Count;
        GustEmitter next = localGusts[selected];
        if (next.source == null || next.clip == null)
        {
            localizedSilenceRemaining = 8f;
            return;
        }
        next.duration = Mathf.Min(next.clip.length, 6f + UnityEngine.Random.value * 6f);
        next.remaining = next.duration;
        next.source.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, next.clip.length - next.duration));
        next.source.pitch = 1f;
        next.source.volume = 0f;
        next.source.Play();
        activeLocalizedGust = selected;
        GmExperienceTelemetry.Record("localized-wind", next.source.gameObject.name);
    }

    void UpdateActiveLocalizedGust(GustEmitter gust, float weatherGain, float deltaTime)
    {
        if (gust.remaining > 0f)
        {
            gust.remaining -= deltaTime;
            float progress = 1f - Mathf.Clamp01(gust.remaining / Mathf.Max(0.01f, gust.duration));
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float texture = Mathf.Lerp(0.72f, 1f,
                Mathf.PerlinNoise(gust.seed, Time.unscaledTime * 0.19f));
            gust.source.volume = LocalGustVolume * weatherGain * envelope * texture;
            if (gust.remaining <= 0f)
            {
                gust.source.Stop();
                gust.source.volume = 0f;
                lastLocalizedGust = activeLocalizedGust;
                activeLocalizedGust = -1;
                localizedSilenceRemaining = 8f + UnityEngine.Random.value * 14f;
            }
        }
    }

    void UpdateFootsteps()
    {
        if (player == null) return;
        float distance = Vector3.Distance(player.position, lastPosition);
        lastPosition = player.position;
        if (distance <= 0.001f) return;
        stepDistance += distance;
        if (stepDistance < 1.68f) return;
        stepDistance = 0f;
        CurrentSurface = DetectSurface(player.position);
        if (!footstepPools.TryGetValue(CurrentSurface, out var clips) || clips.Count == 0)
            clips = footstepPools[GmSurfaceKind.DeadGrass];
        int previous = lastFootstep.TryGetValue(CurrentSurface, out int old) ? old : -1;
        int index = UnityEngine.Random.Range(0, clips.Count);
        if (clips.Count > 1 && index == previous) index = (index + 1 + UnityEngine.Random.Range(0, clips.Count - 1)) % clips.Count;
        lastFootstep[CurrentSurface] = index;
        stepSource.pitch = UnityEngine.Random.Range(0.96f, 1.045f);
        stepSource.PlayOneShot(clips[index], FootstepVolume * Gain(GmAudioBus.Foley));
        GmExperienceTelemetry.Record("footstep-surface", CurrentSurface.ToString());
    }

    GmSurfaceKind DetectSurface(Vector3 position)
    {
        var hits = Physics.RaycastAll(position + Vector3.up * 1.1f, Vector3.down, 3.4f,
            ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var tag = hit.collider.GetComponentInParent<GmSurfaceTag>();
            if (tag != null && tag.Surface != GmSurfaceKind.Unknown) return tag.Surface;
        }
        return GmSurfaceKind.DeadGrass;
    }

    void UpdateWildlife()
    {
        float wildlifeGain = bedScale * Gain(GmAudioBus.Wildlife);
        insectsTimer -= Time.deltaTime;
        if (insectsTimer <= 0f)
        {
            insectsTimer = 14f + UnityEngine.Random.value * 24f;
            if (cricketsClip != null && cricketSource != null && wildlifeGain > 0f)
            {
                cricketSource.transform.position = CricketPositions[
                    UnityEngine.Random.Range(0, CricketPositions.Length)];
                cricketSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                cricketSource.PlayOneShot(cricketsClip, CricketVolume * wildlifeGain);
            }
        }
        owlTimer -= Time.deltaTime;
        if (owlTimer <= 0f)
        {
            owlTimer = 58f + UnityEngine.Random.value * 64f;
            if (owlClip != null && owlSource != null && wildlifeGain > 0f)
            {
                owlSource.transform.position = OwlPositions[
                    UnityEngine.Random.Range(0, OwlPositions.Length)];
                owlSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                owlSource.PlayOneShot(owlClip, 0.15f * wildlifeGain);
            }
        }
    }

    float Gain(GmAudioBus bus) => mix == null ? 1f : mix.Gain(bus);
}

