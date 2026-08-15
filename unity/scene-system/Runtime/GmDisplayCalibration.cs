// Project-wide display calibration. The authored fixed exposure remains level 0; players can move
// within a deliberately narrow stop range from the pause menu. Automated review always runs at the
// authored default and never overwrites the player's persisted preference.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
public sealed class GmDisplayCalibration : MonoBehaviour
{
    // The stop range and its step live on GmFeelConfig: display brightness is one of the human-owned
    // feel gates, and it is the one dial a player is most likely to want moved on their own screen.
    public static int MinimumLevel => GmFeelConfig.Active.displayMinimumLevel;
    public static int MaximumLevel => GmFeelConfig.Active.displayMaximumLevel;
    public static float StopsPerLevel => GmFeelConfig.Active.displayStopsPerLevel;
    public const string PreferenceKey = "gm.display.brightness.v1";

    int level;
    bool automatedReview;
    Volume volume;
    ColorAdjustments colour;

    public int Level => level;
    public float PostExposureOffset => OffsetForLevel(level);
    public string Meter => $"[{new string('-', level - MinimumLevel)}|{new string('-', MaximumLevel - level)}]";
    public event Action Changed;

    void Awake()
    {
        automatedReview = Array.IndexOf(Environment.GetCommandLineArgs(), "-gmReviewAutoExit") >= 0;
        level = automatedReview ? 0 : ClampLevel(PlayerPrefs.GetInt(PreferenceKey, 0));
        Apply();
    }

    public void Step(int direction)
    {
        if (direction == 0) return;
        SetLevel(level + Math.Sign(direction), !automatedReview);
    }

    public void SetLevel(int value, bool persist)
    {
        int next = ClampLevel(value);
        if (next == level && colour != null) return;
        level = next;
        Apply();
        if (persist)
        {
            PlayerPrefs.SetInt(PreferenceKey, level);
            PlayerPrefs.Save();
        }
        Changed?.Invoke();
        Debug.Log($"[GmDisplayCalibration] level={level:+#;-#;0} postExposure={PostExposureOffset:+0.00;-0.00;0.00} stops" +
            (automatedReview ? " (review only, not persisted)" : ""));
    }

    public static int ClampLevel(int value) => Mathf.Clamp(value, MinimumLevel, MaximumLevel);
    public static float OffsetForLevel(int value) => ClampLevel(value) * StopsPerLevel;

    void Apply()
    {
        if (volume != null && !OwnsGrade(volume)) volume = null;
        if (volume == null) volume = ResolveGradeVolume();
        if (volume == null)
        {
            colour = null;
            Debug.LogError("[GmDisplayCalibration] FAILED: no Volume owns a ColorAdjustments override");
            return;
        }
        if (!volume.profile.TryGet(out colour))
        {
            Debug.LogError("[GmDisplayCalibration] FAILED: graded Volume lost its ColorAdjustments override");
            return;
        }
        colour.postExposure.Override(PostExposureOffset);
    }

    // Scenes raise extra global Volumes at runtime (the symptom grade, for one), so the first Volume
    // found is not necessarily the one carrying the display grade. Caching one that cannot hold the
    // grade short-circuits every later Apply() and loses calibration for the rest of the session.
    Volume ResolveGradeVolume()
    {
        Volume owner = GetComponent<Volume>();
        if (OwnsGrade(owner)) return owner;
        foreach (Volume candidate in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (OwnsGrade(candidate)) return candidate;
        return null;
    }

    // sharedProfile reads the asset-based profile without instantiating a runtime copy (profile
    // would), so probing a Volume this component does not own leaves it exactly as the scene authored it.
    static bool OwnsGrade(Volume candidate) =>
        candidate != null && candidate.sharedProfile != null && candidate.sharedProfile.Has<ColorAdjustments>();
}
