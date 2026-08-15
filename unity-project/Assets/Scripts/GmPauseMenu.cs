using System;
using System.Collections.Generic;
using UnityEngine;
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
    public static float MinTextScale => GmFeelConfig.Active.minTextScale;
    public static float MaxTextScale => GmFeelConfig.Active.maxTextScale;

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
    public static bool ReduceMotion { get; private set; } = false;
    public static float TextScale { get; private set; } = 1.0f;

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
    // Set once a scene with its own bespoke pause overlay is detected (wend-hill-prologue's
    // GmPrologueHud), never re-checked -- a scene's HUD roster doesn't change at runtime, and
    // polling FindAnyObjectByType every frame for a answer that can't change is wasted work.
    bool ownerConflictChecked;
    bool suppressedByOtherPauseUi;

    readonly List<Button> tabButtons = new List<Button>();

    // Text scale has no global consumer, so the menu at least applies it to its own type. Each
    // tracked element keeps its authored size and is rescaled in place, never rebuilt, so dragging
    // the slider does not destroy the slider being dragged.
    readonly List<(VisualElement Element, int BaseFontSize)> scaledText =
        new List<(VisualElement, int)>();

    public event Action<bool> OnPauseStateChanged;
    public event Action<GmPauseTab> OnTabChanged;

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
        if (suppressedByOtherPauseUi) return;
        if (!ownerConflictChecked)
        {
            ownerConflictChecked = true;
            if (FindAnyObjectByType<GmPrologueHud>() != null)
            {
                suppressedByOtherPauseUi = true;
                return;
            }
        }

        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (player == null) return;

        SyncWithPlayer(player);
    }

    // Split out of Update() so EditMode tests -- which never pump Unity's per-frame Update loop for
    // a plain MonoBehaviour -- can drive the reactive sync deterministically instead of reflecting
    // into Update() itself.
    void SyncWithPlayer(GmPlayer activePlayer)
    {
        if (activePlayer.IsPaused != IsPaused) SetPauseState(activePlayer.IsPaused);

        if (IsPaused && promptLabel != null)
        {
            // Matches GmPrologueHud's exact wording for the same GmPlayer bindings (Cancel/Quit) --
            // one project convention, not a second one invented here. Tab switching has no gamepad
            // binding of its own; the tab buttons are mouse/keyboard-clickable only today.
            promptLabel.text = activePlayer.UsingGamepad ? "A  Resume      Y  Quit" : "Esc  Resume      Q  Quit";
        }
    }

    void BuildUi()
    {
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
        root.style.backgroundColor = new Color(0.02f, 0.015f, 0.012f, 0.94f);
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.display = DisplayStyle.None;

        menuContainer = new VisualElement { name = "PauseMenuContainer" };
        menuContainer.style.width = 1200;
        menuContainer.style.height = 750;
        menuContainer.style.borderLeftWidth = 2; menuContainer.style.borderRightWidth = 2;
        menuContainer.style.borderTopWidth = 2; menuContainer.style.borderBottomWidth = 2;
        menuContainer.style.borderLeftColor = new Color(0.72f, 0.58f, 0.32f, 0.6f);
        menuContainer.style.borderRightColor = new Color(0.72f, 0.58f, 0.32f, 0.6f);
        menuContainer.style.borderTopColor = new Color(0.72f, 0.58f, 0.32f, 0.6f);
        menuContainer.style.borderBottomColor = new Color(0.72f, 0.58f, 0.32f, 0.6f);
        menuContainer.style.paddingLeft = 40; menuContainer.style.paddingRight = 40;
        menuContainer.style.paddingTop = 30; menuContainer.style.paddingBottom = 30;

        // Title
        titleLabel = new Label("THE GAMES MASTER — DOSSIER") { name = "PauseTitle" };
        titleLabel.style.fontSize = TitleFontSize;
        titleLabel.style.color = GildColor;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        menuContainer.Add(titleLabel);

        // Tab Headers Row
        tabHeaderRow = new VisualElement { name = "TabHeaderRow" };
        tabHeaderRow.style.flexDirection = FlexDirection.Row;
        tabHeaderRow.style.justifyContent = Justify.SpaceAround;
        tabHeaderRow.style.marginTop = 20;
        tabHeaderRow.style.paddingBottom = 15;
        tabHeaderRow.style.borderBottomWidth = 1;
        tabHeaderRow.style.borderBottomColor = new Color(0.5f, 0.4f, 0.25f, 0.4f);
        menuContainer.Add(tabHeaderRow);

        tabButtons.Clear();
        foreach ((GmPauseTab tab, string label) in TabLabels)
        {
            var button = new Button(() => SwitchTab(tab)) { name = $"Tab_{tab}", text = label };
            button.style.fontSize = TabFontSize;
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.borderLeftWidth = 0; button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0; button.style.borderBottomWidth = 0;
            button.style.paddingLeft = 12; button.style.paddingRight = 12;
            button.style.marginLeft = 0; button.style.marginRight = 0;
            tabButtons.Add(button);
            tabHeaderRow.Add(button);
        }

        tabContentContainer = new VisualElement { name = "TabContentContainer" };
        tabContentContainer.style.flexGrow = 1;
        tabContentContainer.style.marginTop = 20;
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
    }

    public void TogglePause()
    {
        SetPauseState(!IsPaused);
    }

    public void SetPauseState(bool pause)
    {
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
            SwitchTab(ActiveTab);
        }

        OnPauseStateChanged?.Invoke(pause);
    }

    public void SwitchTab(GmPauseTab tab)
    {
        ActiveTab = tab;
        RenderActiveTab();
        OnTabChanged?.Invoke(tab);
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
        var reduceMotion = new Toggle("Reduce motion") { name = "ReduceMotionToggle", value = ReduceMotion };
        reduceMotion.labelElement.style.color = InkColor;
        reduceMotion.RegisterValueChangedCallback(evt => SetReduceMotion(evt.newValue));
        tabContentContainer.Add(reduceMotion);
        scaledText.Add((reduceMotion, HeadingFontSize));

        var textScale = new Slider("Text size", MinTextScale, MaxTextScale)
        {
            name = "TextScaleSlider",
            value = TextScale,
            showInputField = true,
        };
        textScale.labelElement.style.color = InkColor;
        textScale.style.marginTop = 12;
        textScale.RegisterValueChangedCallback(evt =>
        {
            SetTextScale(evt.newValue);
            ApplyTextScale();
        });
        tabContentContainer.Add(textScale);
        scaledText.Add((textScale, HeadingFontSize));
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
        // Only resets this menu's own visibility flag. Must NOT touch Time.timeScale here:
        // GmPlayer owns that and has its own OnDestroy that restores it correctly. If this menu is
        // torn down (scene swap) while GmPlayer is still alive and paused, forcing timeScale back
        // to 1 here would desync from GmPlayer.IsPaused, which would still read true -- gameplay
        // would unfreeze while GmPlayer still believes it's showing a pause screen.
        IsPaused = false;
        if (panelSettings != null) Destroy(panelSettings);
    }

    public static void SetReduceMotion(bool enabled)
    {
        ReduceMotion = enabled;
        Debug.Log($"[GmPauseMenu] Reduce motion set to: {enabled}");
    }

    public static void SetTextScale(float scale)
    {
        TextScale = Mathf.Clamp(scale, MinTextScale, MaxTextScale);
        Debug.Log($"[GmPauseMenu] Text scale set to: {TextScale:F2}");
    }
}
