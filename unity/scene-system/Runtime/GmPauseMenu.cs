using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

public enum GmPauseTab
{
    Journal,
    MirrorShards,
    CaughtTells,
    Settings
}

public sealed class GmPauseMenu : MonoBehaviour
{
    // Accessibility bounds are a feel call as much as a layout one, so they come from GmFeelConfig.
    public static float MinTextScale => GmAccessibilitySettings.MinTextScale;
    public static float MaxTextScale => GmAccessibilitySettings.MaxTextScale;

    const int TitleFontSize = 24;
    const int TabFontSize = 18;
    const int HeadingFontSize = 18;
    const int BodyFontSize = 16;

    static readonly (GmPauseTab Tab, string Label)[] TabLabels =
    {
        (GmPauseTab.Journal, "JOURNAL"),
        (GmPauseTab.MirrorShards, "MIRROR SHARDS"),
        (GmPauseTab.CaughtTells, "CAUGHT TELLS"),
        (GmPauseTab.Settings, "SETTINGS"),
    };

    static readonly Color InkColor = new Color(0.80f, 0.78f, 0.72f);
    static readonly Color GildColor = new Color(0.88f, 0.76f, 0.45f);
    static readonly Color DimColor = new Color(0.48f, 0.45f, 0.42f);

    public static bool IsPaused { get; private set; } = false;
    public static bool Captions => GmAccessibilitySettings.Captions;
    public static bool ReduceMotion => GmAccessibilitySettings.ReducedMotion;
    public static bool Vibration => GmAccessibilitySettings.Vibration;
    public static bool MonoAudio => GmAccessibilitySettings.MonoAudio;
    public static bool HighContrast => GmAccessibilitySettings.HighContrast;
    public static float TextScale => GmAccessibilitySettings.TextScale;

    public GmPauseTab ActiveTab { get; private set; } = GmPauseTab.Journal;

    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    VisualElement menuContainer;
    VisualElement tabHeaderRow;
    VisualElement tabContentContainer;
    Label titleLabel;
    Label promptLabel;

    GmPlayer player;
    InputActionAsset ownedControls;
    InputActionMap menuInput;
    InputAction navigateAction;
    InputAction submitAction;
    InputAction cancelAction;
    bool inputActive;
    bool navigationHeld;
    GmAccessibilitySettingsView settingsView;

    readonly List<Button> tabButtons = new List<Button>();

    // Each tracked element keeps its authored size and is rescaled in place, never rebuilt, so
    // dragging the global text-size slider does not destroy the slider being dragged.
    readonly List<(VisualElement Element, int BaseFontSize)> scaledText =
        new List<(VisualElement, int)>();

    public event Action<bool> OnPauseStateChanged;
    public event Action<GmPauseTab> OnTabChanged;
    public bool HasRequiredActions => menuInput != null && navigateAction != null &&
        submitAction != null && cancelAction != null;
    public int FocusedTabIndex { get; private set; }
    public bool SettingsFocusActive { get; private set; }
    public int SettingsFocusIndex => settingsView?.FocusIndex ?? 0;
    public bool HasPendingSettingsSave => GmAccessibilitySettings.HasPendingSave;

    void Awake()
    {
        EnsureInput();
    }

    void EnsureInput()
    {
        if (ownedControls != null) return;
        InputActionAsset shared = Resources.Load<InputActionAsset>("Input/GmControls");
        if (shared == null) return;
        ownedControls = Instantiate(shared);
        ownedControls.name = "GmPauseMenuControls";
        menuInput = ownedControls.FindActionMap("Menu");
        navigateAction = menuInput?.FindAction("Navigate");
        submitAction = menuInput?.FindAction("Submit");
        cancelAction = menuInput?.FindAction("Cancel");
    }

    void OnEnable() => EnableInput();

    void OnDisable()
    {
        FlushSettingsBoundary();
        DisableInput();
    }

    void Start()
    {
        BuildUi();
        SetPauseState(false);
    }

    // Reacts to GmPlayer.IsPaused rather than being called by it, the same pattern
    // wend-hill-prologue's GmPrologueHud already uses -- GmPlayer stays scene-agnostic and owns
    // Time.timeScale/AudioListener.pause/pointer lock as the single source of truth; this menu only
    // ever decides whether to be visible.
    void Update()
    {
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (player == null) return;

        SyncWithPlayer(player);
        if (!IsPaused || navigateAction == null) return;
        Vector2 navigation = ReadNavigation();
        if (Mathf.Abs(navigation.x) < 0.5f && Mathf.Abs(navigation.y) < 0.5f)
        {
            navigationHeld = false;
            return;
        }
        if (navigationHeld) return;
        navigationHeld = true;
        if (SettingsFocusActive)
        {
            if (Mathf.Abs(navigation.y) >= Mathf.Abs(navigation.x))
                settingsView?.MoveFocus(navigation.y > 0f ? -1 : 1);
            else
                settingsView?.AdjustFocused(navigation.x > 0f ? 1 : -1);
        }
        else if (Mathf.Abs(navigation.x) >= 0.5f)
        {
            MoveMenuFocus(navigation.x > 0f ? 1 : -1);
        }
    }

    void EnableInput()
    {
        if (inputActive || !HasRequiredActions) return;
        submitAction.performed += OnSubmit;
        cancelAction.performed += OnCancel;
        menuInput.Enable();
        inputActive = true;
        navigationHeld = false;
    }

    void DisableInput()
    {
        if (inputActive)
        {
            submitAction.performed -= OnSubmit;
            cancelAction.performed -= OnCancel;
        }
        if (menuInput != null && menuInput.enabled) menuInput.Disable();
        inputActive = false;
        navigationHeld = false;
    }

    void OnSubmit(InputAction.CallbackContext _)
    {
        if (OwnsAuthoritativePause()) ActivateFocusedItem();
    }

    void OnCancel(InputAction.CallbackContext _)
    {
        if (!OwnsAuthoritativePause()) return;
        if (SettingsFocusActive)
        {
            SettingsFocusActive = false;
            settingsView?.SetFocusVisible(false);
            RenderActiveTab();
            FlushSettingsBoundary();
            return;
        }
        player ??= FindAnyObjectByType<GmPlayer>();
        if (player != null) player.SetPaused(false);
        else SetPauseState(false);
    }

    bool OwnsAuthoritativePause()
    {
        player ??= FindAnyObjectByType<GmPlayer>();
        if (player == null) return IsPaused;
        if (!player.IsPaused) return false;
        // Input callbacks run before MonoBehaviour.Update. Mirror the authoritative player here so
        // the first button on the frame after Pause is neither rejected nor consumed by gameplay.
        if (!IsPaused) SetPauseState(true);
        return true;
    }

    Vector2 ReadNavigation()
    {
        return navigateAction.ReadValue<Vector2>();
    }

    // Split out of Update() so EditMode tests -- which never pump Unity's per-frame Update loop for
    // a plain MonoBehaviour -- can drive the reactive sync deterministically instead of reflecting
    // into Update() itself.
    void SyncWithPlayer(GmPlayer activePlayer)
    {
        if (activePlayer.IsPaused != IsPaused) SetPauseState(activePlayer.IsPaused);

        if (IsPaused && promptLabel != null)
        {
            // One project convention for the same GmPlayer bindings. Navigation and selection are
            // owned by the cloned Menu map, while GmPlayer retains pause and quit ownership.
            promptLabel.text = activePlayer.UsingGamepad
                ? "D-pad  Navigate      A  Select      B  Resume      Y  Quit"
                : "Arrows  Navigate      Enter  Select      Esc  Resume      Q  Quit";
        }
    }

    void BuildUi()
    {
        EnsureInput();
        EnableInput();
        GmAccessibilitySettings.OnChanged -= HandleAccessibilityChanged;
        GmAccessibilitySettings.OnChanged += HandleAccessibilityChanged;
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmPausePanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 900;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        if (panelSettings.themeStyleSheet == null)
            Debug.LogError("[GmPauseMenu] FAILED: Resources/GmHudTheme.tss is missing");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 900;

        root = document.rootVisualElement;
        root.name = "GmPauseMenuRoot";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.style.backgroundColor = new Color(0.015f, 0.010f, 0.008f, 0.94f);
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.display = DisplayStyle.None;

        menuContainer = new VisualElement { name = "PauseMenuContainer" };
        menuContainer.style.width = 1200;
        menuContainer.style.height = 760;
        menuContainer.style.backgroundColor = new Color(0.045f, 0.025f, 0.018f, 0.97f);
        menuContainer.style.borderLeftWidth = 2; menuContainer.style.borderRightWidth = 2;
        menuContainer.style.borderTopWidth = 2; menuContainer.style.borderBottomWidth = 2;
        menuContainer.style.borderLeftColor = new Color(0.82f, 0.68f, 0.38f, 0.85f);
        menuContainer.style.borderRightColor = new Color(0.82f, 0.68f, 0.38f, 0.85f);
        menuContainer.style.borderTopColor = new Color(0.82f, 0.68f, 0.38f, 0.85f);
        menuContainer.style.borderBottomColor = new Color(0.82f, 0.68f, 0.38f, 0.85f);
        menuContainer.style.paddingLeft = 45; menuContainer.style.paddingRight = 45;
        menuContainer.style.paddingTop = 32; menuContainer.style.paddingBottom = 32;

        // Title
        titleLabel = new Label("THE GAMES MASTER — DOSSIER") { name = "PauseTitle" };
        titleLabel.style.fontSize = TitleFontSize;
        titleLabel.style.color = GildColor;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.letterSpacing = 2;
        menuContainer.Add(titleLabel);

        // Tab Headers Row
        tabHeaderRow = new VisualElement { name = "TabHeaderRow" };
        tabHeaderRow.style.flexDirection = FlexDirection.Row;
        tabHeaderRow.style.justifyContent = Justify.SpaceAround;
        tabHeaderRow.style.marginTop = 22;
        tabHeaderRow.style.paddingBottom = 16;
        tabHeaderRow.style.borderBottomWidth = 1;
        tabHeaderRow.style.borderBottomColor = new Color(0.65f, 0.52f, 0.30f, 0.45f);
        menuContainer.Add(tabHeaderRow);

        tabButtons.Clear();
        foreach ((GmPauseTab tab, string label) in TabLabels)
        {
            var button = new Button(() => SwitchTab(tab))
            {
                name = $"Tab_{tab}",
                text = label,
                // GmPauseMenu's cloned Menu map is the sole controller owner. Pointer clicks stay
                // enabled, while UI Toolkit cannot also submit a focused tab for the same A edge.
                focusable = false,
            };
            button.style.fontSize = TabFontSize;
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.borderLeftWidth = 0; button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0; button.style.borderBottomWidth = 0;
            button.style.paddingLeft = 16; button.style.paddingRight = 16;
            button.style.paddingTop = 6; button.style.paddingBottom = 6;
            button.style.marginLeft = 4; button.style.marginRight = 4;
            tabButtons.Add(button);
            tabHeaderRow.Add(button);
        }

        tabContentContainer = new VisualElement { name = "TabContentContainer" };
        tabContentContainer.style.flexGrow = 1;
        tabContentContainer.style.marginTop = 20;
        tabContentContainer.style.paddingLeft = 12;
        tabContentContainer.style.paddingRight = 12;
        menuContainer.Add(tabContentContainer);

        // Tabs alone give no way to tell a player how to leave. GmPlayer's Update loop already
        // resumes on Interact/Cancel and quits on Quit regardless of which pause overlay is
        // showing -- this just states that, the same guidance GmPrologueHud prints in its own
        // pause card.
        promptLabel = new Label { name = "PausePrompt" };
        promptLabel.style.fontSize = BodyFontSize;
        promptLabel.style.color = DimColor;
        promptLabel.style.marginTop = 16;
        promptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        menuContainer.Add(promptLabel);

        root.Add(menuContainer);
        RenderActiveTab();
        HandleAccessibilityChanged();
    }

    public void TogglePause()
    {
        SetPauseState(!IsPaused);
    }

    public void SetPauseState(bool pause)
    {
        if (!pause && IsPaused) FlushSettingsBoundary();
        IsPaused = pause;
        // Time.timeScale/AudioListener.pause/pointer lock belong to GmPlayer.SetPaused, the single
        // owner every scene shares -- this menu only mirrors the state it's told, matching the
        // pattern GmPrologueHud already proved out for wend-hill-prologue.

        if (root != null)
        {
            root.style.display = pause ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (pause)
        {
            FocusedTabIndex = Array.FindIndex(TabLabels, item => item.Tab == ActiveTab);
            if (FocusedTabIndex < 0) FocusedTabIndex = 0;
            SettingsFocusActive = false;
            SwitchTab(ActiveTab);
        }
        else SettingsFocusActive = false;

        OnPauseStateChanged?.Invoke(pause);
    }

    public void SwitchTab(GmPauseTab tab)
    {
        ActiveTab = tab;
        FocusedTabIndex = Array.FindIndex(TabLabels, item => item.Tab == tab);
        RenderActiveTab();
        OnTabChanged?.Invoke(tab);
    }

    public void MoveMenuFocus(int delta)
    {
        FocusedTabIndex = (FocusedTabIndex + delta % TabLabels.Length + TabLabels.Length) %
            TabLabels.Length;
        SwitchTab(TabLabels[FocusedTabIndex].Tab);
    }

    public void ActivateFocusedItem()
    {
        if (SettingsFocusActive)
        {
            settingsView?.ActivateFocused();
            return;
        }
        if (ActiveTab == GmPauseTab.Settings)
        {
            SettingsFocusActive = true;
            settingsView?.SetFocusVisible(true);
            RenderActiveTab();
        }
    }

    void RenderActiveTab()
    {
        if (tabContentContainer == null) return;

        tabContentContainer.Clear();
        scaledText.Clear();
        if (titleLabel != null) scaledText.Add((titleLabel, TitleFontSize));
        if (promptLabel != null) scaledText.Add((promptLabel, BodyFontSize));

        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool active = i < TabLabels.Length && TabLabels[i].Tab == ActiveTab;
            tabButtons[i].style.color = active ? GildColor : DimColor;
            bool controllerFocused = IsPaused && !SettingsFocusActive && active;
            tabButtons[i].EnableInClassList("gm-controller-focus", controllerFocused);
            tabButtons[i].style.borderBottomWidth = controllerFocused ? 3 : 0;
            tabButtons[i].style.borderBottomColor = GildColor;
            scaledText.Add((tabButtons[i], TabFontSize));
        }

        switch (ActiveTab)
        {
            case GmPauseTab.MirrorShards:
                BuildMirrorShardsTab();
                break;
            case GmPauseTab.CaughtTells:
                BuildCaughtTellsTab();
                break;
            case GmPauseTab.Settings:
                BuildSettingsTab();
                break;
            case GmPauseTab.Journal:
            default:
                BuildJournalTab();
                break;
        }

        ApplyTextScale();
        ApplyHighContrastPalette();
        GmUiText.UseStandardGenerator(root);
    }

    // Nothing in the runtime records ledger entries yet, so this tab states the empty case rather
    // than showing guests the run never met.
    void BuildJournalTab()
    {
        tabContentContainer.Add(TabLine("No entries recorded.", HeadingFontSize, InkColor));
    }

    void BuildMirrorShardsTab()
    {
        int total = GmRunStore.MirrorShards.Count;
        tabContentContainer.Add(TabLine($"Recovered {GmRunStore.ShardsCount} of {total}.", HeadingFontSize, InkColor));

        for (int i = 0; i < total; i++)
        {
            bool held = GmRunStore.HasShard(i);
            tabContentContainer.Add(TabLine(
                $"Shard {i + 1}   {(held ? "recovered" : "not found")}",
                BodyFontSize,
                held ? GildColor : DimColor));
        }
    }

    void BuildCaughtTellsTab()
    {
        if (GmRunStore.CheatsCaughtCount == 0)
        {
            tabContentContainer.Add(TabLine("No tells caught yet.", HeadingFontSize, InkColor));
            return;
        }

        tabContentContainer.Add(TabLine($"{GmRunStore.CheatsCaughtCount} caught this run.", HeadingFontSize, InkColor));
        foreach (string clueId in GmRunStore.CheatsCaught)
        {
            tabContentContainer.Add(TabLine($"• {clueId}", BodyFontSize, GildColor));
        }
    }

    void BuildSettingsTab()
    {
        settingsView = new GmAccessibilitySettingsView(HandleAccessibilityChanged);
        VisualElement settingsRoot = settingsView.Build("PauseSettingsPanel");
        tabContentContainer.Add(settingsRoot);
        foreach (VisualElement element in settingsRoot.Children())
            scaledText.Add((element, HeadingFontSize));
        settingsView.SetFocusVisible(SettingsFocusActive);
    }

    Label TabLine(string text, int baseFontSize, Color color)
    {
        var line = new Label(text);
        line.style.fontSize = baseFontSize;
        line.style.color = color;
        line.style.marginBottom = 6;
        line.style.whiteSpace = WhiteSpace.Normal;
        scaledText.Add((line, baseFontSize));
        return line;
    }

    void ApplyTextScale()
    {
        foreach ((VisualElement element, int baseFontSize) in scaledText)
        {
            if (element != null) element.style.fontSize = Mathf.RoundToInt(baseFontSize * TextScale);
        }
    }

    void OnDestroy()
    {
        FlushSettingsBoundary();
        DisableInput();
        GmAccessibilitySettings.OnChanged -= HandleAccessibilityChanged;
        // Only resets this menu's own visibility flag. Must NOT touch Time.timeScale here:
        // GmPlayer owns that and has its own OnDestroy that restores it correctly. If this menu is
        // torn down (scene swap) while GmPlayer is still alive and paused, forcing timeScale back
        // to 1 here would desync from GmPlayer.IsPaused, which would still read true -- gameplay
        // would unfreeze while GmPlayer still believes it's showing a pause screen.
        IsPaused = false;
        if (panelSettings != null) Destroy(panelSettings);
        if (ownedControls != null) Destroy(ownedControls);
    }

    void OnApplicationPause(bool pausedByApplication)
    {
        if (pausedByApplication) FlushSettingsBoundary();
    }

    void OnApplicationQuit() => FlushSettingsBoundary();

    void FlushSettingsBoundary()
    {
        if (!GmAccessibilitySettings.HasPendingSave) return;
        if (!GmAccessibilitySettings.FlushPendingSave())
            Debug.LogError($"[GmPauseMenu] Accessibility settings remain pending: {GmSaveSystem.LastError}");
    }

    public static void SetReduceMotion(bool enabled)
    {
        GmAccessibilitySettings.SetReducedMotion(enabled);
        Debug.Log($"[GmPauseMenu] Reduce motion set to: {enabled}");
    }

    public static void SetTextScale(float scale)
    {
        GmAccessibilitySettings.SetTextScale(scale);
        Debug.Log($"[GmPauseMenu] Text scale set to: {TextScale:F2}");
    }

    public static void SetCaptions(bool enabled) => GmAccessibilitySettings.SetCaptions(enabled);
    public static void SetVibration(bool enabled) => GmAccessibilitySettings.SetVibration(enabled);
    public static void SetMonoAudio(bool enabled) => GmAccessibilitySettings.SetMonoAudio(enabled);
    public static void SetHighContrast(bool enabled) => GmAccessibilitySettings.SetHighContrast(enabled);

    void HandleAccessibilityChanged()
    {
        ApplyTextScale();
        if (root == null || menuContainer == null) return;
        root.Q<Toggle>("CaptionsToggle")?.SetValueWithoutNotify(Captions);
        root.Q<Toggle>("ReduceMotionToggle")?.SetValueWithoutNotify(ReduceMotion);
        root.Q<Toggle>("VibrationToggle")?.SetValueWithoutNotify(Vibration);
        root.Q<Toggle>("MonoAudioToggle")?.SetValueWithoutNotify(MonoAudio);
        root.Q<Toggle>("HighContrastToggle")?.SetValueWithoutNotify(HighContrast);
        root.Q<Slider>("TextScaleSlider")?.SetValueWithoutNotify(TextScale);
        settingsView?.Refresh();
        root.style.backgroundColor = HighContrast
            ? new Color(0f, 0f, 0f, 0.985f)
            : new Color(0.02f, 0.015f, 0.012f, 0.94f);
        Color border = HighContrast ? Color.white : new Color(0.72f, 0.58f, 0.32f, 0.6f);
        menuContainer.style.borderLeftColor = border;
        menuContainer.style.borderRightColor = border;
        menuContainer.style.borderTopColor = border;
        menuContainer.style.borderBottomColor = border;
        ApplyHighContrastPalette();
    }

    void ApplyHighContrastPalette()
    {
        if (!HighContrast || root == null) return;
        foreach (Label label in root.Query<Label>().ToList()) label.style.color = Color.white;
        if (titleLabel != null) titleLabel.style.color = Color.white;
        if (promptLabel != null) promptLabel.style.color = Color.white;
        for (int index = 0; index < tabButtons.Count; index++)
        {
            bool active = index < TabLabels.Length && TabLabels[index].Tab == ActiveTab;
            tabButtons[index].style.color = active ? new Color(1f, 0.86f, 0.3f) : Color.white;
            tabButtons[index].style.borderBottomColor = Color.white;
        }
        settingsView?.Refresh();
    }
}
