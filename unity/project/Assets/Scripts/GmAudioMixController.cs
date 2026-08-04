using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmAudioBus
{
    Ambience,
    Weather,
    Wildlife,
    Foley,
    Interaction,
    Story,
    Bell,
    UI
}

public enum GmAudioMixState
{
    Exploration,
    Threshold,
    Taken
}

/// <summary>
/// Small, deterministic mix authority for generated scenes. It keeps authored source volumes and
/// state attenuation separate, so story systems never permanently overwrite the sound designer's
/// baseline. This deliberately avoids a hidden dependency on an imported demo AudioMixer.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmAudioMixController : MonoBehaviour
{
    readonly Dictionary<GmAudioBus, float> gains = new Dictionary<GmAudioBus, float>();
    public GmAudioMixState State { get; private set; } = GmAudioMixState.Exploration;

    void Awake() => Apply(GmAudioMixState.Exploration);

    public float Gain(GmAudioBus bus) => gains.TryGetValue(bus, out float value) ? value : 1f;

    public void Apply(GmAudioMixState state)
    {
        State = state;
        foreach (GmAudioBus bus in Enum.GetValues(typeof(GmAudioBus))) gains[bus] = 1f;

        if (state == GmAudioMixState.Threshold)
        {
            gains[GmAudioBus.Ambience] = 0.62f;
            gains[GmAudioBus.Weather] = 0.68f;
            gains[GmAudioBus.Wildlife] = 0.35f;
            gains[GmAudioBus.Foley] = 0.82f;
            gains[GmAudioBus.Story] = 1.08f;
            gains[GmAudioBus.Bell] = 1.05f;
        }
        else if (state == GmAudioMixState.Taken)
        {
            gains[GmAudioBus.Ambience] = 0.08f;
            gains[GmAudioBus.Weather] = 0.10f;
            gains[GmAudioBus.Wildlife] = 0f;
            gains[GmAudioBus.Foley] = 0.25f;
            gains[GmAudioBus.Story] = 1f;
            gains[GmAudioBus.Bell] = 1f;
        }

        GmExperienceTelemetry.Record("audio-mix", state.ToString().ToLowerInvariant());
    }
}

