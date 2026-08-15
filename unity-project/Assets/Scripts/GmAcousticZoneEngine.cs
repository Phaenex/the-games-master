using UnityEngine;

public enum GmAcousticEnvironment
{
    OutdoorEstateGrounds,
    StoneChapelCrypt,
    VictorianManorHall,
    ClaustrophobicCoachHouse
}

/// <summary>
/// Spatial acoustics and environmental reverb manager.
/// Configures audio reverb filters and low-pass characteristics to match physical room geometry.
/// </summary>
public static class GmAcousticZoneEngine
{
    public struct AcousticSettings
    {
        public AudioReverbPreset preset;
        public float dryLevel;
        public float roomLevel;
        public float decayTime;
        public float lowPassCutoffHz;
    }

    public static AcousticSettings GetSettings(GmAcousticEnvironment env)
    {
        switch (env)
        {
            case GmAcousticEnvironment.StoneChapelCrypt:
                return new AcousticSettings
                {
                    preset = AudioReverbPreset.StoneCorridor,
                    dryLevel = 0f,
                    roomLevel = -400f,
                    decayTime = 3.2f,
                    lowPassCutoffHz = 18000f
                };

            case GmAcousticEnvironment.VictorianManorHall:
                return new AcousticSettings
                {
                    preset = AudioReverbPreset.Livingroom,
                    dryLevel = 0f,
                    roomLevel = -1200f,
                    decayTime = 1.1f,
                    lowPassCutoffHz = 20000f
                };

            case GmAcousticEnvironment.ClaustrophobicCoachHouse:
                return new AcousticSettings
                {
                    preset = AudioReverbPreset.PaddedCell,
                    dryLevel = 0f,
                    roomLevel = -2000f,
                    decayTime = 0.5f,
                    lowPassCutoffHz = 12000f
                };

            case GmAcousticEnvironment.OutdoorEstateGrounds:
            default:
                return new AcousticSettings
                {
                    preset = AudioReverbPreset.Off,
                    dryLevel = 0f,
                    roomLevel = -10000f,
                    decayTime = 0.1f,
                    lowPassCutoffHz = 22000f
                };
        }
    }

    /// <summary>
    /// Applies acoustic environment parameters to an AudioReverbFilter and AudioLowPassFilter.
    /// </summary>
    public static void ApplyEnvironment(GmAcousticEnvironment env, AudioReverbFilter reverb, AudioLowPassFilter lowPass)
    {
        AcousticSettings settings = GetSettings(env);

        if (reverb != null)
        {
            reverb.reverbPreset = settings.preset;
            reverb.dryLevel = settings.dryLevel;
            reverb.room = settings.roomLevel;
            reverb.decayTime = settings.decayTime;
        }

        if (lowPass != null)
        {
            lowPass.cutoffFrequency = settings.lowPassCutoffHz;
        }
    }
}
