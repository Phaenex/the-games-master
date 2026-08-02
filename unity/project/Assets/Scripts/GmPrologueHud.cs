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
        root.Add(scrim);

        card = new VisualElement { name = "StoryCard", pickingMode = PickingMode.Ignore };
        card.style.width = Length.Percent(72);
        card.style.maxWidth = 1040;
        card.style.minHeight = 320;
        card.style.paddingLeft = 72; card.style.paddingRight = 72;
        card.style.paddingTop = 54; card.style.paddingBottom = 48;
        card.style.backgroundColor = new Color(0.025f, 0.022f, 0.019f, 0.98f);
        card.style.borderLeftWidth = 2; card.style.borderRightWidth = 2;
        card.style.borderTopWidth = 1; card.style.borderBottomWidth = 1;
        var border = new Color(0.38f, 0.29f, 0.18f, 0.72f);
        card.style.borderLeftColor = border; card.style.borderRightColor = border;
        card.style.borderTopColor = border; card.style.borderBottomColor = border;
        card.style.alignItems = Align.Center;
        card.style.justifyContent = Justify.Center;
        scrim.Add(card);

        eyebrow = MakeLabel("Eyebrow", 15, new Color(0.68f, 0.52f, 0.30f), TextAnchor.MiddleCenter);
        eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
        eyebrow.style.marginBottom = 28;
        card.Add(eyebrow);

        cardBody = MakeLabel("CardBody", 34, new Color(0.91f, 0.87f, 0.79f), TextAnchor.MiddleCenter);
        cardBody.style.maxWidth = 860;
        cardBody.style.minHeight = 150;
        card.Add(cardBody);

        prompt = MakeLabel("Prompt", 18, new Color(0.62f, 0.58f, 0.50f), TextAnchor.MiddleCenter);
        prompt.style.marginTop = 34;
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

    static Label MakeLabel(string name, int size, Color color, TextAnchor alignment)
    {
        var label = new Label { name = name, pickingMode = PickingMode.Ignore };
        label.style.fontSize = size;
        label.style.color = color;
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
                cardBody.text = display == null
                    ? "Wend Hill waits."
                    : $"Wend Hill waits.\n\nBRIGHTNESS  {display.Meter}  " +
                      $"{display.PostExposureOffset:+0.00;-0.00;0.00}";
                prompt.text = player.UsingGamepad
                    ? "D-PAD LEFT / RIGHT  BRIGHTNESS\nA / CROSS  RESUME     •     Y / TRIANGLE  QUIT     •     MENU / OPTIONS  RESUME"
                    : "LEFT / RIGHT ARROW  BRIGHTNESS\nESC  RESUME     •     Q  QUIT";
            }
            else if (hasStory)
            {
                if (coldRunning)
                {
                    eyebrow.text = $"THE ROAD TO WEND HILL   {coldOpen.CardNumber:00}/{coldOpen.CardCount:00}";
                    prompt.text = player != null && player.UsingGamepad
                        ? "A / CROSS  CONTINUE     •     B / CIRCLE  SKIP INTRO"
                        : "SPACE / CLICK  CONTINUE     •     ESC  SKIP INTRO";
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
