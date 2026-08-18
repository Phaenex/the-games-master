using System;
using UnityEngine;

[Serializable]
public sealed class GmAccessibilityPreferencesData
{
    public int version = GmAccessibilitySettings.CurrentSaveVersion;
    public bool captions;
    public bool reducedMotion;
    public bool vibration = true;
    public bool monoAudio;
    public bool highContrast;
    public float textScale = 1f;
}

/// <summary>
/// Process-wide accessibility authority. These preferences belong to the player, not a run, and
/// are persisted independently so creating or loading a run cannot replace them.
/// </summary>
public static class GmAccessibilitySettings
{
    internal const int CurrentSaveVersion = 1;

    static bool captions;
    static bool reducedMotion;
    static bool vibration = true;
    static bool monoAudio;
    static bool highContrast;
    static float textScale = 1f;
    static bool persistenceDirty;

    public static bool Captions => captions;
    public static bool ReducedMotion => reducedMotion;
    public static bool Vibration => vibration;
    public static bool MonoAudio => monoAudio;
    public static bool HighContrast => highContrast;
    public static float TextScale => textScale;
    public static float MinTextScale => GmFeelConfig.Active.minTextScale;
    public static float MaxTextScale => GmFeelConfig.Active.maxTextScale;
    public static bool HasPendingSave => persistenceDirty;

    public static event Action OnChanged;

    public static void SetCaptions(bool enabled) => Set(ref captions, enabled);
    public static void SetReducedMotion(bool enabled) => Set(ref reducedMotion, enabled);
    public static void SetVibration(bool enabled) => Set(ref vibration, enabled);
    public static void SetMonoAudio(bool enabled) => Set(ref monoAudio, enabled);
    public static void SetHighContrast(bool enabled) => Set(ref highContrast, enabled);

    public static void SetTextScale(float scale)
    {
        float safe = float.IsNaN(scale) || float.IsInfinity(scale)
            ? 1f : Mathf.Clamp(scale, MinTextScale, MaxTextScale);
        if (Mathf.Approximately(textScale, safe)) return;
        textScale = safe;
        persistenceDirty = true;
        OnChanged?.Invoke();
    }

    internal static void WriteTo(GmSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.accessibilitySettingsVersion = CurrentSaveVersion;
        data.accessibilityCaptions = captions;
        data.accessibilityReducedMotion = reducedMotion;
        data.accessibilityVibration = vibration;
        data.accessibilityMonoAudio = monoAudio;
        data.accessibilityHighContrast = highContrast;
        data.accessibilityTextScale = textScale;
    }

    internal static GmAccessibilityPreferencesData ToPreferencesData() =>
        new GmAccessibilityPreferencesData
        {
            version = CurrentSaveVersion,
            captions = captions,
            reducedMotion = reducedMotion,
            vibration = vibration,
            monoAudio = monoAudio,
            highContrast = highContrast,
            textScale = textScale,
        };

    internal static bool TryLoadPreferences(GmAccessibilityPreferencesData data)
    {
        if (data == null) return false;
        switch (data.version)
        {
            case 0:
                return ApplyLoaded(false, false, true, false, false, 1f);
            case CurrentSaveVersion:
                return ApplyLoaded(data.captions, data.reducedMotion, data.vibration,
                    data.monoAudio, data.highContrast, data.textScale);
            default:
                return false;
        }
    }

    internal static void LoadFrom(GmSaveData data)
    {
        TryLoadFrom(data);
    }

    internal static bool TryLoadFrom(GmSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        bool nextCaptions;
        bool nextReducedMotion;
        bool nextVibration;
        bool nextMonoAudio;
        bool nextHighContrast;
        float rawScale;
        switch (data.accessibilitySettingsVersion)
        {
            case 0:
                nextCaptions = false;
                nextReducedMotion = false;
                nextVibration = true;
                nextMonoAudio = false;
                nextHighContrast = false;
                rawScale = 1f;
                break;
            case CurrentSaveVersion:
                nextCaptions = data.accessibilityCaptions;
                nextReducedMotion = data.accessibilityReducedMotion;
                nextVibration = data.accessibilityVibration;
                nextMonoAudio = data.accessibilityMonoAudio;
                nextHighContrast = data.accessibilityHighContrast;
                rawScale = data.accessibilityTextScale;
                break;
            default:
                return false;
        }
        return ApplyLoaded(nextCaptions, nextReducedMotion, nextVibration, nextMonoAudio,
            nextHighContrast, rawScale);
    }

    static bool ApplyLoaded(bool nextCaptions, bool nextReducedMotion, bool nextVibration,
        bool nextMonoAudio, bool nextHighContrast, float rawScale)
    {
        float nextTextScale = float.IsNaN(rawScale) || float.IsInfinity(rawScale)
            ? 1f : Mathf.Clamp(rawScale, MinTextScale, MaxTextScale);
        bool changed = captions != nextCaptions || reducedMotion != nextReducedMotion ||
            vibration != nextVibration || monoAudio != nextMonoAudio ||
            highContrast != nextHighContrast || !Mathf.Approximately(textScale, nextTextScale);
        captions = nextCaptions;
        reducedMotion = nextReducedMotion;
        vibration = nextVibration;
        monoAudio = nextMonoAudio;
        highContrast = nextHighContrast;
        textScale = nextTextScale;
        if (changed) OnChanged?.Invoke();
        return true;
    }

    public static void MarkPersistenceDirty()
    {
        persistenceDirty = true;
    }

    public static bool FlushPendingSave()
    {
        if (!persistenceDirty) return true;
        if (!GmSaveSystem.QueueAccessibilityPreferences(out _)) return false;
        if (!GmSaveSystem.FlushAccessibilityPreferences()) return false;
        persistenceDirty = false;
        return true;
    }

    internal static void ResetToDefaultsForTests()
    {
        bool changed = captions || reducedMotion || !vibration || monoAudio || highContrast ||
            !Mathf.Approximately(textScale, 1f);
        captions = false;
        reducedMotion = false;
        vibration = true;
        monoAudio = false;
        highContrast = false;
        textScale = 1f;
        persistenceDirty = false;
        if (changed) OnChanged?.Invoke();
    }

    static void Set(ref bool field, bool value)
    {
        if (field == value) return;
        field = value;
        persistenceDirty = true;
        OnChanged?.Invoke();
    }
}
