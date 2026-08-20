using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GmAudioManager : MonoBehaviour
{
    private static GmAudioManager _instance;
    public static GmAudioManager Instance => _instance;

    // Seeded from GmFeelConfig in Awake rather than by a field initializer: Unity refuses
    // Resources.Load from a MonoBehaviour constructor, which is where an initializer runs.
    public float MasterVolume { get; private set; }
    public float AmbienceVolume { get; private set; }
    public float SfxVolume { get; private set; }

    public bool IsReadFilterActive { get; private set; } = false;
    public string CurrentAmbience { get; private set; } = "";
    public int TotalSfxPlayed { get; private set; } = 0;

    AudioSource ambienceSource;
    AudioSource sfxSource;
    AudioLowPassFilter lowPassFilter;

    // Resolved clips are cached by cue key, misses included, so a missing asset warns once
    // instead of once per frame.
    readonly Dictionary<string, AudioClip> clipCache =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    public event Action<string> OnSoundPlayed;

    public static GmAudioManager EnsureExists()
    {
        if (_instance != null)
        {
            _instance.gameObject.SetActive(true);
            _instance.enabled = true;
            return _instance;
        }
        GmAudioManager existing = FindAnyObjectByType<GmAudioManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            return existing;
        }
        return new GameObject("GmAudioManager").AddComponent<GmAudioManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        GmFeelConfig feel = GmFeelConfig.Active;
        MasterVolume = feel.masterVolume;
        AmbienceVolume = feel.ambienceVolume;
        SfxVolume = feel.sfxVolume;

        AudioSource[] authoredSources = GetComponents<AudioSource>();
        sfxSource = authoredSources.Length > 0 ? authoredSources[0] : gameObject.AddComponent<AudioSource>();
        ambienceSource = authoredSources.Length > 1 ? authoredSources[1] : gameObject.AddComponent<AudioSource>();
        ambienceSource.loop = true;
        ambienceSource.playOnAwake = false;

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        UpdateSourcesVolume();
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        UpdateSourcesVolume();
    }

    public void SetAmbienceVolume(float volume)
    {
        AmbienceVolume = Mathf.Clamp01(volume);
        UpdateSourcesVolume();
    }

    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        UpdateSourcesVolume();
    }

    void UpdateSourcesVolume()
    {
        if (ambienceSource != null) ambienceSource.volume = MasterVolume * AmbienceVolume;
        if (sfxSource != null) sfxSource.volume = MasterVolume * SfxVolume;
    }

    // Cue keys are slugs ("ninth-bell-toll"); the shipped clips live in Resources/Sfx under
    // underscore names, so try the key verbatim first and then its underscore form.
    AudioClip ResolveClip(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (clipCache.TryGetValue(key, out AudioClip cached)) return cached;

        AudioClip clip = Resources.Load<AudioClip>($"Sfx/{key}");
        if (clip == null) clip = Resources.Load<AudioClip>($"Sfx/{key.Replace('-', '_')}");
        if (clip == null)
        {
            Debug.LogWarning($"[GmAudioManager] No clip in Resources/Sfx for '{key}' — that cue is silent.");
        }
        clipCache[key] = clip;
        return clip;
    }

    public void PlayAmbience(string ambienceKey)
    {
        CurrentAmbience = ambienceKey;
        AudioClip clip = ResolveClip(ambienceKey);
        if (ambienceSource != null)
        {
            ambienceSource.Stop();
            ambienceSource.clip = clip;
            if (clip != null) ambienceSource.Play();
        }
        Debug.Log($"[GmAudioManager] Playing ambience loop: {ambienceKey}");
        OnSoundPlayed?.Invoke(ambienceKey);
    }

    public void PlaySfx(string sfxKey, float relativeVolume = 1.0f)
    {
        TotalSfxPlayed++;
        AudioClip clip = ResolveClip(sfxKey);
        if (clip != null && sfxSource != null)
        {
            // sfxSource.volume already carries master * sfx, so the cue only supplies its own trim.
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(relativeVolume));
        }
        Debug.Log($"[GmAudioManager] Playing SFX: {sfxKey} (vol: {relativeVolume:F2})");
        OnSoundPlayed?.Invoke(sfxKey);
    }

    // The slug is the cue; how loud that cue sits against the others is a mix decision, so it comes
    // from GmFeelConfig instead of being written into the call.
    public void PlayBellToll() => PlaySfx("ninth-bell-toll", GmFeelConfig.Active.bellTollVolume);
    public void PlayHeartbeat() => PlaySfx("sanity-heartbeat-thump", GmFeelConfig.Active.heartbeatVolume);
    public void PlayCardSnap() => PlaySfx("parlor-card-snap", GmFeelConfig.Active.cardSnapVolume);
    public void PlayDiceRoll() => PlaySfx("stb-bone-dice-roll", GmFeelConfig.Active.diceRollVolume);
    public void PlayGavelStrike() => PlaySfx("court-gavel-strike", GmFeelConfig.Active.gavelStrikeVolume);

    // A low-pass filter only touches sources on its own object, or the whole mix when it sits on the
    // AudioListener. The Read dilates the world, not just this manager's two sources, so reuse the
    // listener filter the way GmCrossing does and hand it back open at 22 kHz when the Read ends.
    AudioLowPassFilter EnsureListenerLowPass()
    {
        if (lowPassFilter != null) return lowPassFilter;

        var listener = FindAnyObjectByType<AudioListener>();
        if (listener == null)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[GmAudioManager] No AudioListener or main camera: the Read low-pass has no mix to filter.");
                return null;
            }
            listener = camera.GetComponent<AudioListener>();
            if (listener == null) listener = camera.gameObject.AddComponent<AudioListener>();
        }

        lowPassFilter = listener.GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null) lowPassFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
        return lowPassFilter;
    }

    public void SetReadTimeDilationFilter(bool active)
    {
        IsReadFilterActive = active;
        AudioLowPassFilter filter = EnsureListenerLowPass();
        if (filter != null)
        {
            filter.cutoffFrequency = active ? GmFeelConfig.Active.readLowPassCutoffHz : 22000f;
        }
        Debug.Log($"[GmAudioManager] Time dilation low-pass filter set to: {active}");
    }
}
