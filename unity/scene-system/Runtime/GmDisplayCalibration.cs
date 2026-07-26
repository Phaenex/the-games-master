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
    public const int MinimumLevel = -2;
    public const int MaximumLevel = 2;
    public const float StopsPerLevel = 0.25f;
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
        if (volume == null) volume = GetComponent<Volume>();
        if (volume == null) volume = FindAnyObjectByType<Volume>();
        if (volume == null)
        {
            Debug.LogError("[GmDisplayCalibration] FAILED: no Volume owns the display grade");
            return;
        }
        VolumeProfile profile = volume.profile;
        if (profile == null || !profile.TryGet(out colour))
        {
            Debug.LogError("[GmDisplayCalibration] FAILED: active Volume has no ColorAdjustments override");
            return;
        }
        colour.postExposure.Override(PostExposureOffset);
    }
}
