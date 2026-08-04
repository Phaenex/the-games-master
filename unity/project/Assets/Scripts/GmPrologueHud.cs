// Runtime UI for the Prologue. Replaces resolution-dependent IMGUI labels with a scaled UI Toolkit
// hierarchy that is captured in real standalone screenshots and never intercepts player input.
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(100)]
public sealed class GmPrologueHud : MonoBehaviour
{
    GmDesignRuntime runtime;
    GmColdOpen coldOpen;
    GmCrossing crossing;
    GmAmbience ambience;
    GmPlayer player;
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement scrim, card, beatPanel, examinePanel, reticle;
    VisualElement brightnessRow, brightnessTrack, brightnessFill;
    Label brightnessLabel, brightnessValue, runState;
    PauseRow resumeRow, quitRow;

    /// Three shards gate the true ending and 8+ catches gate it with them, so a player who has
    /// started collecting needs to be able to check without leaving the game. Reads the static run
    /// totals, so it works in any scene whether or not that scene owns a GmHouseProgress.
    static string RunStateLine()
    {
        int caught = GmHouseProgress.CheatsCaughtTotal;
        int shards = GmHouseProgress.MirrorShardsTotal;
        if (caught == 0 && shards == 0) return "";
        string catchText = caught == 1 ? "1 cheat caught" : $"{caught} cheats caught";
        string shardText = shards == 1 ? "1 shard" : $"{shards} shards";
        return $"{catchText}   ·   {shardText}";
    }


    /// Proof reads this instead of matching prompt prose. Assertions pinned to UI copy break every
    /// time the copy is improved, which punishes exactly the work that should be encouraged.
    public bool PromptUsesControllerLabels { get; private set; }
    public bool BrightnessUiVisible => brightnessRow != null &&
        brightnessRow.style.display.value == DisplayStyle.Flex;
    /// Same reason as PromptUsesControllerLabels: the controls legend was frozen by two assertions
    /// matching its literal wording, so improving it would have failed two gates.
    public bool ControlsUseControllerLabels { get; private set; }
    Label eyebrow, cardBody, prompt, beatMain, beatSub, examine, focusPrompt, controls, statusToast;
    bool coldWasRunning;
    float controlsUntil;
    string observedBeatKey = "";
    string observedWindProfile = "";
    float beatUntil;
    float statusUntil;
    bool houseMode;

    void Start()
    {
        runtime = FindAnyObjectByType<GmDesignRuntime>();
        coldOpen = FindAnyObjectByType<GmColdOpen>();
        crossing = FindAnyObjectByType<GmCrossing>();
        ambience = FindAnyObjectByType<GmAmbience>();
        player = FindAnyObjectByType<GmPlayer>();
        BuildUi();
        coldWasRunning = coldOpen != null && coldOpen.IsRunning;
        controlsUntil = Time.unscaledTime + 10f;
        observedWindProfile = ambience != null ? ambience.ReviewProfileId : "";
    }

    void BuildUi()
    {
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmRuntimePanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 500;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        if (panelSettings.themeStyleSheet == null)
            Debug.LogError("[GmPrologueHud] FAILED: Resources/GmHudTheme.tss is missing");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 500;
        var root = document.rootVisualElement;
        root.name = "GmPrologueHud";
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;

        scrim = new VisualElement { name = "StoryScrim", pickingMode = PickingMode.Ignore };
        scrim.style.position = Position.Absolute;
        scrim.style.left = 0; scrim.style.right = 0; scrim.style.top = 0; scrim.style.bottom = 0;
        scrim.style.alignItems = Align.Center;
        scrim.style.justifyContent = Justify.Center;
        // The scrim does the work the panel used to fake: it sinks the scene so unboxed text still
        // has contrast to sit on, and the bars give the words a frame to be centred in.
        scrim.style.backgroundColor = new Color(0.008f, 0.008f, 0.010f, 0.86f);
        root.Add(scrim);
        scrim.Add(MakeLetterbox(true));
        scrim.Add(MakeLetterbox(false));

        // No panel, no border. A bordered rectangle floating mid-screen is a debug overlay; story
        // cards in shipping games sit directly on the darkened frame, held by letterboxing and
        // measure alone. The scrim itself carries the dim, so the words look printed on the night.
        card = new VisualElement { name = "StoryCard", pickingMode = PickingMode.Ignore };
        card.style.width = Length.Percent(64);
        card.style.maxWidth = 900;
        card.style.paddingLeft = 0; card.style.paddingRight = 0;
        card.style.alignItems = Align.Center;
        card.style.justifyContent = Justify.Center;
        scrim.Add(card);

        eyebrow = MakeLabel("Eyebrow", 14, new Color(0.60f, 0.46f, 0.27f), TextAnchor.MiddleCenter);
        ApplyFont(eyebrow, true);
        eyebrow.style.letterSpacing = 5;
        eyebrow.style.marginBottom = 34;
        card.Add(eyebrow);

        // 38px over a 900px measure keeps the two-line cards near 45 characters a line, so they
        // break where the sentence breathes instead of orphaning a word.
        cardBody = MakeLabel("CardBody", 38, new Color(0.90f, 0.86f, 0.78f), TextAnchor.MiddleCenter);
        cardBody.style.maxWidth = 820;
        card.Add(cardBody);

        // A real track with a fill and a marked centre detent. The previous build drew the level as
        // the literal string "[--|--] 0.00" - an ASCII slider and a raw float, which is the single
        // clearest tell that a screen was never designed.
        brightnessRow = new VisualElement { name = "BrightnessRow", pickingMode = PickingMode.Ignore };
        brightnessRow.style.flexDirection = FlexDirection.Row;
        brightnessRow.style.alignItems = Align.Center;
        brightnessRow.style.marginTop = 44;
        card.Add(brightnessRow);

        brightnessLabel = MakeLabel("BrightnessLabel", 14, new Color(0.60f, 0.46f, 0.27f),
            TextAnchor.MiddleLeft);
        ApplyFont(brightnessLabel, true);
        brightnessLabel.style.letterSpacing = 4;
        brightnessLabel.text = "BRIGHTNESS";
        brightnessRow.Add(brightnessLabel);

        brightnessTrack = new VisualElement { name = "BrightnessTrack", pickingMode = PickingMode.Ignore };
        brightnessTrack.style.width = 260;
        brightnessTrack.style.height = 2;
        brightnessTrack.style.marginLeft = 26; brightnessTrack.style.marginRight = 22;
        brightnessTrack.style.backgroundColor = new Color(0.30f, 0.27f, 0.23f, 0.85f);
        brightnessRow.Add(brightnessTrack);

        brightnessFill = new VisualElement { name = "BrightnessFill", pickingMode = PickingMode.Ignore };
        brightnessFill.style.position = Position.Absolute;
        brightnessFill.style.left = 0; brightnessFill.style.top = -3;
        brightnessFill.style.width = 10; brightnessFill.style.height = 8;
        brightnessFill.style.backgroundColor = new Color(0.85f, 0.62f, 0.30f, 1f);
        brightnessTrack.Add(brightnessFill);

        brightnessValue = MakeLabel("BrightnessValue", 15, new Color(0.72f, 0.68f, 0.60f),
            TextAnchor.MiddleLeft);
        brightnessValue.style.minWidth = 132;
        brightnessRow.Add(brightnessValue);

        // A pause screen in a shipping game is a menu you operate, not a wall of key hints. Two
        // real rows with a focus rule the player can see; the rule stays a single amber marker
        // rather than a highlight bar, so it reads as the same furniture as the brightness track.
        // Run state belongs on pause, not on the HUD. A permanent "cheats caught: 0" readout during
        // a horror walk is atmosphere-killing clutter, and in the Prologue there is no game yet to
        // have caught anything in — so this stays hidden until the run has something to report.
        runState = MakeLabel("RunState", 14, new Color(0.58f, 0.54f, 0.47f), TextAnchor.MiddleCenter);
        runState.style.letterSpacing = 2;
        runState.style.marginTop = 30;
        card.Add(runState);

        resumeRow = MakePauseRow("PauseResume", "Resume");
        quitRow = MakePauseRow("PauseQuit", "Quit to desktop");
        card.Add(resumeRow.root);
        card.Add(quitRow.root);

        prompt = MakeLabel("Prompt", 15, new Color(0.50f, 0.47f, 0.42f), TextAnchor.MiddleCenter);
        prompt.style.letterSpacing = 2;
        prompt.style.marginTop = 52;
        card.Add(prompt);

        beatPanel = new VisualElement { name = "BeatPanel", pickingMode = PickingMode.Ignore };
        beatPanel.style.position = Position.Absolute;
        beatPanel.style.left = Length.Percent(20); beatPanel.style.right = Length.Percent(20);
        beatPanel.style.bottom = 105;
        beatPanel.style.paddingLeft = 32; beatPanel.style.paddingRight = 32;
        beatPanel.style.paddingTop = 18; beatPanel.style.paddingBottom = 19;
        beatPanel.style.backgroundColor = new Color(0.015f, 0.014f, 0.013f, 0.84f);
        beatPanel.style.borderLeftWidth = 3;
        beatPanel.style.borderLeftColor = new Color(0.58f, 0.39f, 0.19f, 0.9f);
        root.Add(beatPanel);
        beatMain = MakeLabel("BeatMain", 25, new Color(0.94f, 0.90f, 0.82f), TextAnchor.MiddleLeft);
        beatMain.style.unityFontStyleAndWeight = FontStyle.Bold;
        beatSub = MakeLabel("BeatSub", 17, new Color(0.71f, 0.68f, 0.62f), TextAnchor.MiddleLeft);
        beatSub.style.marginTop = 6;
        beatPanel.Add(beatMain); beatPanel.Add(beatSub);

        examinePanel = new VisualElement { name = "ExaminePanel", pickingMode = PickingMode.Ignore };
        examinePanel.style.position = Position.Absolute;
        examinePanel.style.left = Length.Percent(22); examinePanel.style.right = Length.Percent(22);
        examinePanel.style.bottom = 270;
        examinePanel.style.paddingLeft = 28; examinePanel.style.paddingRight = 28;
        examinePanel.style.paddingTop = 16; examinePanel.style.paddingBottom = 16;
        examinePanel.style.backgroundColor = new Color(0.02f, 0.018f, 0.015f, 0.82f);
        root.Add(examinePanel);
        examine = MakeLabel("Examine", 19, new Color(0.82f, 0.70f, 0.49f), TextAnchor.MiddleCenter);
        examinePanel.Add(examine);

        controls = MakeLabel("Controls", 16, new Color(0.74f, 0.71f, 0.65f), TextAnchor.MiddleCenter);
        controls.style.position = Position.Absolute;
        controls.style.left = Length.Percent(24); controls.style.right = Length.Percent(24);
        controls.style.bottom = 30;
        controls.style.paddingTop = 9; controls.style.paddingBottom = 9;
        controls.style.backgroundColor = new Color(0.01f, 0.01f, 0.01f, 0.72f);
        root.Add(controls);

        statusToast = MakeLabel("StatusToast", 16, new Color(0.82f, 0.70f, 0.49f), TextAnchor.MiddleCenter);
        statusToast.style.position = Position.Absolute;
        statusToast.style.right = 30;
        statusToast.style.top = 30;
        statusToast.style.paddingLeft = 18; statusToast.style.paddingRight = 18;
        statusToast.style.paddingTop = 10; statusToast.style.paddingBottom = 10;
        statusToast.style.backgroundColor = new Color(0.01f, 0.01f, 0.01f, 0.82f);
        root.Add(statusToast);

        reticle = new VisualElement { name = "Reticle", pickingMode = PickingMode.Ignore };
        reticle.style.position = Position.Absolute;
        reticle.style.left = Length.Percent(50); reticle.style.top = Length.Percent(50);
        reticle.style.marginLeft = -2; reticle.style.marginTop = -2;
        reticle.style.width = 4; reticle.style.height = 4;
        reticle.style.backgroundColor = new Color(0.82f, 0.77f, 0.67f, 0.72f);
        root.Add(reticle);

        focusPrompt = MakeLabel("FocusPrompt", 16, new Color(0.76f, 0.70f, 0.60f), TextAnchor.MiddleCenter);
        focusPrompt.style.position = Position.Absolute;
        focusPrompt.style.left = Length.Percent(38); focusPrompt.style.right = Length.Percent(38);
        focusPrompt.style.top = Length.Percent(54);
        focusPrompt.style.paddingTop = 5; focusPrompt.style.paddingBottom = 5;
        focusPrompt.style.backgroundColor = new Color(0.01f, 0.01f, 0.01f, 0.46f);
        root.Add(focusPrompt);
    }

    // Ibarra Real Nova is a revival of Joaquin Ibarra's 1780 types. The Prologue is a story told in
    // written things - an invitation, a ledger, nine names in a book - so the UI is set in a book
    // face rather than the engine default sans, which reads as a prototype in any game.
    static Font regularFont, semiBoldFont;
    static Font RegularFont => regularFont != null
        ? regularFont : regularFont = Resources.Load<Font>("Fonts/IbarraRealNova-Regular");
    static Font SemiBoldFont => semiBoldFont != null
        ? semiBoldFont : semiBoldFont = Resources.Load<Font>("Fonts/IbarraRealNova-SemiBold");

    static void ApplyFont(Label label, bool emphasis)
    {
        Font font = emphasis ? SemiBoldFont : RegularFont;
        // Fall back silently to the engine default rather than drawing nothing if the resource is
        // missing from a stripped build.
        if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font);
    }

    struct PauseRow { public VisualElement root; public VisualElement marker; public Label label; }

    static PauseRow MakePauseRow(string name, string text)
    {
        var row = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 18;
        var marker = new VisualElement { name = name + "Marker", pickingMode = PickingMode.Ignore };
        marker.style.width = 7; marker.style.height = 7;
        marker.style.marginRight = 18;
        marker.style.backgroundColor = new Color(0.85f, 0.62f, 0.30f, 1f);
        row.Add(marker);
        var label = MakeLabel(name + "Label", 20, new Color(0.86f, 0.82f, 0.74f), TextAnchor.MiddleLeft);
        label.text = text;
        row.Add(label);
        return new PauseRow { root = row, marker = marker, label = label };
    }

    /// The focused row carries the marker and full-strength ink; the other dims. Encoding focus in
    /// two channels rather than colour alone keeps it readable for players who cannot separate the
    /// amber from the parchment.
    void SetPauseFocus(bool resumeFocused)
    {
        SetVisible(resumeRow.marker, resumeFocused);
        SetVisible(quitRow.marker, !resumeFocused);
        resumeRow.label.style.color = resumeFocused
            ? new Color(0.92f, 0.88f, 0.80f) : new Color(0.48f, 0.45f, 0.40f);
        quitRow.label.style.color = resumeFocused
            ? new Color(0.48f, 0.45f, 0.40f) : new Color(0.92f, 0.88f, 0.80f);
    }

    static VisualElement MakeLetterbox(bool top)
    {
        var bar = new VisualElement { name = top ? "LetterboxTop" : "LetterboxBottom",
            pickingMode = PickingMode.Ignore };
        bar.style.position = Position.Absolute;
        bar.style.left = 0; bar.style.right = 0;
        bar.style.height = Length.Percent(11);
        if (top) bar.style.top = 0; else bar.style.bottom = 0;
        bar.style.backgroundColor = new Color(0f, 0f, 0f, 1f);
        return bar;
    }

    static Label MakeLabel(string name, int size, Color color, TextAnchor alignment)
    {
        var label = new Label { name = name, pickingMode = PickingMode.Ignore };
        label.style.fontSize = size;
        label.style.color = color;
        ApplyFont(label, false);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.unityTextAlign = alignment;
        // Runtime-created PanelSettings do not carry Unity 6's optional ICU payload. The advanced
        // generator then throws every frame and draws no text. The standard generator is fully
        // adequate for this Latin-script Prologue and is deterministic in player builds.
        label.style.unityTextGenerator = TextGeneratorType.Standard;
        return label;
    }

    void Update()
    {
        if (houseMode)
        {
            SetVisible(scrim, false); SetVisible(beatPanel, false); SetVisible(examinePanel, false);
            SetVisible(controls, false); SetVisible(statusToast, false); SetVisible(reticle, false);
            SetVisible(focusPrompt, false);
            return;
        }
        if (runtime == null) runtime = FindAnyObjectByType<GmDesignRuntime>();
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (ambience == null) ambience = FindAnyObjectByType<GmAmbience>();

        bool coldRunning = coldOpen != null && coldOpen.IsRunning;
        if (coldWasRunning && !coldRunning) controlsUntil = Time.unscaledTime + 10f;
        coldWasRunning = coldRunning;

        string crossingCard = crossing != null ? crossing.Card : "";
        string story = !string.IsNullOrEmpty(crossingCard) ? crossingCard : runtime?.AftermathText ?? "";
        float crossingFade = crossing != null ? crossing.Fade : 0f;
        bool hasStory = !string.IsNullOrEmpty(story);
        bool paused = player != null && player.IsPaused;
        bool ownsScreen = paused || hasStory || crossingFade > 0.01f;

        SetVisible(scrim, ownsScreen);
        if (ownsScreen)
        {
            scrim.style.backgroundColor = new Color(0.004f, 0.004f, 0.004f,
                paused ? 0.88f : hasStory ? 0.985f : crossingFade);
            SetVisible(card, paused || hasStory);
            if (paused)
            {
                GmDisplayCalibration display = player.DisplayCalibration;
                eyebrow.text = "PAUSED";
                string line = RunStateLine();
                runState.text = line;
                SetVisible(runState, line.Length > 0);
                SetVisible(resumeRow.root, true);
                SetVisible(quitRow.root, true);
                // Resume is the safe default and stays focused; Quit is never the resting choice on
                // a screen a player reaches by accident mid-walk.
                SetPauseFocus(true);
                cardBody.text = "Wend Hill waits.";
                SetVisible(brightnessRow, display != null);
                if (display != null)
                {
                    // -0.5..+0.5 stops around the authored grade. Name the level instead of showing
                    // a raw float: a player choosing a brightness wants to know it is the authored
                    // one, not that it is 0.00.
                    float normalised = Mathf.Clamp01((display.PostExposureOffset + 0.5f) / 1f);
                    brightnessFill.style.left = Mathf.Round(normalised * 250f);
                    brightnessValue.text = Mathf.Abs(display.PostExposureOffset) < 0.01f
                        ? "As authored"
                        : $"{display.PostExposureOffset:+0.00;-0.00} stops";
                }
                PromptUsesControllerLabels = player.UsingGamepad;
                prompt.text = player.UsingGamepad
                    ? "D-pad  Brightness      A  Resume      Y  Quit"
                    : "← →  Brightness      Esc  Resume      Q  Quit";
            }
            else if (hasStory)
            {
                SetVisible(brightnessRow, false);
                SetVisible(runState, false);
                SetVisible(resumeRow.root, false);
                SetVisible(quitRow.root, false);
                if (coldRunning)
                {
                    // No "01/05". A counter turns a cold open into a slideshow and tells the player
                    // exactly how long until the game starts, which is the opposite of the intent.
                    eyebrow.text = "THE ROAD TO WEND HILL";
                    PromptUsesControllerLabels = player != null && player.UsingGamepad;
                    prompt.text = PromptUsesControllerLabels
                        ? "A  Continue      B  Skip"
                        : "Space  Continue      Esc  Skip";
                }
                else
                {
                    eyebrow.text = string.IsNullOrEmpty(crossingCard) ? "THE HOUSE REMEMBERS" : "NINE O'CLOCK";
                    prompt.text = "";
                }
                cardBody.text = story;
            }
        }

        string beatKey = runtime == null ? "" : runtime.activeBeatMain + "\n" + runtime.activeBeatSub;
        if (!ownsScreen && !string.IsNullOrEmpty(runtime?.activeBeatMain) && beatKey != observedBeatKey)
        {
            observedBeatKey = beatKey;
            beatUntil = Time.unscaledTime + 7.5f;
        }
        else if (runtime == null || string.IsNullOrEmpty(runtime.activeBeatMain)) observedBeatKey = "";

        // Story beats announce a place, then get out of the player's way. The old panel remained for
        // the entire walk and made every composition look like a captioned review screenshot.
        bool showBeat = !ownsScreen && runtime != null && !string.IsNullOrEmpty(runtime.activeBeatMain) &&
                        Time.unscaledTime < beatUntil;
        SetVisible(beatPanel, showBeat);
        if (showBeat) { beatMain.text = runtime.activeBeatMain; beatSub.text = runtime.activeBeatSub; }

        bool showExamine = !ownsScreen && runtime != null && !string.IsNullOrEmpty(runtime.lastExamine);
        SetVisible(examinePanel, showExamine);
        if (showExamine) examine.text = runtime.lastExamine;

        bool canPlay = !ownsScreen && player != null && !player.ControlBlocked;
        SetVisible(reticle, canPlay && player.HasPointerCapture);
        var scanner = player?.InteractionScanner;
        bool showFocus = canPlay && scanner != null && scanner.HasFocus;
        SetVisible(focusPrompt, showFocus);
        if (showFocus)
            focusPrompt.text = $"{(player.UsingGamepad ? "A / CROSS" : "E")}   {scanner.PromptText}";

        bool needsCapture = canPlay && !player.HasPointerCapture && !player.UsingGamepad;
        bool showControls = canPlay && (needsCapture || Time.unscaledTime < controlsUntil);
        SetVisible(controls, showControls);
        if (showControls)
            ControlsUseControllerLabels = !needsCapture && player.UsingGamepad;
        if (showControls)
            controls.text = needsCapture
                ? "CLICK TO CAPTURE MOUSE     •     ESC OPENS PAUSE"
                : player.UsingGamepad
                    ? "LEFT STICK / D-PAD  MOVE   •   RIGHT STICK  LOOK   •   A / CROSS  INTERACT   •   RB / R1  WIND   •   MENU / OPTIONS  PAUSE"
                    : "WASD  MOVE   •   MOUSE  LOOK   •   E  INTERACT   •   F8  WIND   •   ESC  PAUSE";

        string windProfile = ambience != null ? ambience.ReviewProfileId : "";
        if (!string.IsNullOrEmpty(windProfile) && !string.IsNullOrEmpty(observedWindProfile) &&
            windProfile != observedWindProfile)
        {
            statusToast.text = $"WIND REVIEW: {windProfile.ToUpperInvariant()}";
            statusUntil = Time.unscaledTime + 3.5f;
        }
        if (!string.IsNullOrEmpty(windProfile)) observedWindProfile = windProfile;
        SetVisible(statusToast, !ownsScreen && Time.unscaledTime < statusUntil);
    }

    static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // The standalone probe first exercises real interaction/story UI, then captures clean scene
    // compositions. Clear only transient presentation state between those proof phases; normal
    // launches never call this and authored story progression remains untouched.
    public void HideTransientForReview()
    {
        if (runtime != null)
        {
            runtime.lastExamine = "";
            runtime.activeBeatMain = "";
            runtime.activeBeatSub = "";
            runtime.ClearAftermathForReview();
        }
        observedBeatKey = "";
        observedWindProfile = ambience != null ? ambience.ReviewProfileId : "";
        beatUntil = 0f;
        controlsUntil = 0f;
        statusUntil = 0f;
        SetVisible(beatPanel, false);
        SetVisible(examinePanel, false);
        SetVisible(controls, false);
        SetVisible(statusToast, false);
        SetVisible(scrim, false);
    }

    public void SetHouseMode()
    {
        houseMode = true;
        HideTransientForReview();
    }

    void OnDestroy()
    {
        if (panelSettings != null) Destroy(panelSettings);
    }
}
