using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>One runtime view/controller for the six process-wide accessibility preferences.</summary>
public sealed class GmAccessibilitySettingsView
{
    static readonly Color Ink = new Color(0.88f, 0.84f, 0.76f);
    static readonly Color Gild = new Color(0.88f, 0.76f, 0.45f);
    static readonly Color Focus = new Color(1f, 0.86f, 0.35f);
    static readonly Color ControlBg = new Color(0.04f, 0.03f, 0.025f, 0.55f);
    readonly List<VisualElement> controls = new List<VisualElement>(6);
    VisualElement root;

    public int FocusIndex { get; private set; }
    public int ControlCount => controls.Count;

    // Retain the callback-shaped constructor while callers migrate. The authority's OnChanged
    // event is the one notification path; invoking a second callback here used to repaint twice.
    public GmAccessibilitySettingsView(Action _) { }

    public VisualElement Build(string name)
    {
        root = new VisualElement { name = name };
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 6;
        root.style.paddingBottom = 6;
        controls.Clear();
        AddToggle("Captions (Spoken dialogue subtitles)", "CaptionsToggle", GmAccessibilitySettings.Captions,
            GmAccessibilitySettings.SetCaptions);
        AddToggle("Reduce motion (Steady camera)", "ReduceMotionToggle", GmAccessibilitySettings.ReducedMotion,
            GmAccessibilitySettings.SetReducedMotion);
        AddToggle("Controller vibration (Haptic tells)", "VibrationToggle", GmAccessibilitySettings.Vibration,
            GmAccessibilitySettings.SetVibration);
        AddToggle("Mono audio (Combined channels)", "MonoAudioToggle", GmAccessibilitySettings.MonoAudio,
            GmAccessibilitySettings.SetMonoAudio);
        AddToggle("High contrast (Enhanced luminance)", "HighContrastToggle", GmAccessibilitySettings.HighContrast,
            GmAccessibilitySettings.SetHighContrast);
        var scale = new Slider("Text size (Parchment scale)", GmAccessibilitySettings.MinTextScale,
            GmAccessibilitySettings.MaxTextScale)
        {
            name = "TextScaleSlider",
            value = GmAccessibilitySettings.TextScale,
            showInputField = true,
            focusable = false,
        };
        TextField scaleInput = scale.Q<TextField>();
        if (scaleInput != null)
        {
            scaleInput.focusable = false;
            scaleInput.style.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 0.9f);
            scaleInput.style.borderLeftColor = Gild;
            scaleInput.style.borderRightColor = Gild;
            scaleInput.style.borderTopColor = Gild;
            scaleInput.style.borderBottomColor = Gild;
            scaleInput.style.borderLeftWidth = 1;
            scaleInput.style.borderRightWidth = 1;
            scaleInput.style.borderTopWidth = 1;
            scaleInput.style.borderBottomWidth = 1;
            scaleInput.style.color = Gild;
        }
        scale.labelElement.style.color = Ink;
        scale.style.marginTop = 14;
        scale.style.paddingTop = 6;
        scale.style.paddingBottom = 6;
        scale.style.paddingLeft = 14;
        scale.style.paddingRight = 14;
        scale.style.backgroundColor = ControlBg;
        scale.style.borderLeftWidth = 1;
        scale.style.borderRightWidth = 1;
        scale.style.borderTopWidth = 1;
        scale.style.borderBottomWidth = 1;
        scale.style.borderLeftColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        scale.style.borderRightColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        scale.style.borderTopColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        scale.style.borderBottomColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        scale.RegisterValueChangedCallback(evt => Apply(() =>
            GmAccessibilitySettings.SetTextScale(evt.newValue)));
        root.Add(scale);
        controls.Add(scale);
        FocusIndex = 0;
        PaintFocus(false);
        GmUiText.UseStandardGenerator(root);
        return root;
    }

    public void SetFocusVisible(bool visible)
    {
        PaintFocus(visible);
    }

    public void MoveFocus(int delta)
    {
        if (controls.Count == 0) return;
        FocusIndex = (FocusIndex + delta % controls.Count + controls.Count) % controls.Count;
        PaintFocus(true);
    }

    public void ActivateFocused()
    {
        if (FocusIndex < 0 || FocusIndex >= controls.Count) return;
        if (controls[FocusIndex] is Toggle toggle) toggle.value = !toggle.value;
    }

    public void AdjustFocused(int direction)
    {
        if (direction == 0 || FocusIndex < 0 || FocusIndex >= controls.Count) return;
        if (controls[FocusIndex] is Toggle toggle)
            toggle.value = direction > 0;
        else if (controls[FocusIndex] is Slider slider)
            slider.value = Mathf.Clamp(slider.value + Mathf.Sign(direction) * 0.1f,
                slider.lowValue, slider.highValue);
    }

    public void Refresh()
    {
        if (root == null) return;
        root.Q<Toggle>("CaptionsToggle")?.SetValueWithoutNotify(GmAccessibilitySettings.Captions);
        root.Q<Toggle>("ReduceMotionToggle")?.SetValueWithoutNotify(GmAccessibilitySettings.ReducedMotion);
        root.Q<Toggle>("VibrationToggle")?.SetValueWithoutNotify(GmAccessibilitySettings.Vibration);
        root.Q<Toggle>("MonoAudioToggle")?.SetValueWithoutNotify(GmAccessibilitySettings.MonoAudio);
        root.Q<Toggle>("HighContrastToggle")?.SetValueWithoutNotify(GmAccessibilitySettings.HighContrast);
        root.Q<Slider>("TextScaleSlider")?.SetValueWithoutNotify(GmAccessibilitySettings.TextScale);
        Color textColor = GmAccessibilitySettings.HighContrast ? Color.white : Ink;
        foreach (VisualElement control in controls)
        {
            if (control is Toggle toggle) toggle.labelElement.style.color = textColor;
            else if (control is Slider slider) slider.labelElement.style.color = textColor;
        }
        GmUiText.UseStandardGenerator(root);
    }

    void AddToggle(string label, string name, bool value, Action<bool> setter)
    {
        var toggle = new Toggle(label) { name = name, value = value, focusable = false };
        toggle.labelElement.style.color = Ink;
        toggle.style.marginTop = 10;
        toggle.style.paddingTop = 6;
        toggle.style.paddingBottom = 6;
        toggle.style.paddingLeft = 14;
        toggle.style.paddingRight = 14;
        toggle.style.backgroundColor = ControlBg;
        toggle.style.borderLeftWidth = 1;
        toggle.style.borderRightWidth = 1;
        toggle.style.borderTopWidth = 1;
        toggle.style.borderBottomWidth = 1;
        toggle.style.borderLeftColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        toggle.style.borderRightColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        toggle.style.borderTopColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        toggle.style.borderBottomColor = new Color(0.35f, 0.28f, 0.18f, 0.4f);
        toggle.RegisterValueChangedCallback(evt => Apply(() => setter(evt.newValue)));
        root.Add(toggle);
        controls.Add(toggle);
    }

    void Apply(Action action)
    {
        action();
    }

    void PaintFocus(bool visible)
    {
        for (int index = 0; index < controls.Count; index++)
        {
            bool focused = visible && index == FocusIndex;
            VisualElement control = controls[index];
            control.EnableInClassList("gm-controller-focus", focused);
            control.style.borderLeftWidth = focused ? 4 : 1;
            control.style.borderLeftColor = focused ? Focus : new Color(0.35f, 0.28f, 0.18f, 0.4f);
            control.style.paddingLeft = focused ? 11 : 14;
            control.style.backgroundColor = focused
                ? new Color(0.32f, 0.20f, 0.06f, 0.85f)
                : ControlBg;
        }
    }
}
