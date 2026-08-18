using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmParlorHud : MonoBehaviour
{
    static readonly Color Ink = new Color(0.86f, 0.82f, 0.73f);
    static readonly Color Gild = new Color(0.86f, 0.66f, 0.31f);
    static readonly Color Dim = new Color(0.56f, 0.53f, 0.48f);
    static readonly Color HighContrastInk = Color.white;
    static readonly Color HighContrastGild = new Color(1f, 0.84f, 0.28f);

    GmParlorFocusView focus;
    GmParlorPresentationCoordinator coordinator;
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    VisualElement focusPanel;
    VisualElement handRow;
    VisualElement evidenceList;
    Label score;
    Label action;
    Label feedback;
    Label caption;

    public bool IsConfigured => focus != null;
    public bool HasUi => root != null;

    void Awake()
    {
        GmParlorFocusView found = FindAnyObjectByType<GmParlorFocusView>();
        if (found != null) TryConfigure(found,
            FindAnyObjectByType<GmParlorPresentationCoordinator>(), out _);
    }

    void Start()
    {
        if (!IsConfigured)
        {
            GmParlorFocusView found = FindAnyObjectByType<GmParlorFocusView>();
            if (found != null) TryConfigure(found,
                FindAnyObjectByType<GmParlorPresentationCoordinator>(), out _);
        }
        if (IsConfigured) BuildUi();
    }

    public bool TryConfigure(GmParlorFocusView focusView, out string error)
    {
        return TryConfigure(focusView, FindAnyObjectByType<GmParlorPresentationCoordinator>(), out error);
    }

    public bool TryConfigure(GmParlorFocusView focusView,
        GmParlorPresentationCoordinator presentationCoordinator, out string error)
    {
        if (focusView == null || !focusView.IsConfigured)
        {
            error = "Parlor HUD needs a configured focus view";
            return false;
        }
        if (focus != null) focus.OnChanged -= Refresh;
        focus = focusView;
        focus.OnChanged += Refresh;
        if (coordinator != null)
        {
            coordinator.OnActiveCaptionChanged -= Refresh;
            coordinator.OnCaptionStateReconstructed -= Refresh;
        }
        coordinator = presentationCoordinator;
        if (coordinator != null)
        {
            coordinator.OnActiveCaptionChanged += Refresh;
            coordinator.OnCaptionStateReconstructed += Refresh;
        }
        GmAccessibilitySettings.OnChanged -= Refresh;
        GmAccessibilitySettings.OnChanged += Refresh;
        error = string.Empty;
        return true;
    }

    public VisualElement BuildForTests()
    {
        BuildUi();
        return root;
    }

    void BuildUi()
    {
        if (root != null) return;
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmParlorPanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 720;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 720;
        root = document.rootVisualElement;
        root.name = "GmParlorHudRoot";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        score = Label("ParlorScore", 18, Ink);
        score.style.position = Position.Absolute;
        score.style.top = 30; score.style.left = 36;
        // Reserve a stable, single-line text mesh. Letting the absolutely positioned label infer
        // its size dropped glyph quads after the longer MatchResult score replaced the first hand.
        score.style.width = 900; score.style.height = 60;
        score.style.whiteSpace = WhiteSpace.NoWrap;
        score.style.overflow = Overflow.Visible;
        score.style.letterSpacing = 1;
        root.Add(score);
        action = Label("ParlorAction", 19, Gild);
        action.style.position = Position.Absolute;
        action.style.bottom = 30; action.style.left = 36;
        action.style.letterSpacing = 1;
        root.Add(action);
        feedback = Label("ParlorFeedback", 18, Ink);
        feedback.style.position = Position.Absolute;
        feedback.style.left = 340; feedback.style.right = 340; feedback.style.bottom = 92;
        feedback.style.paddingLeft = 24; feedback.style.paddingRight = 24;
        feedback.style.paddingTop = 12; feedback.style.paddingBottom = 12;
        feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        feedback.style.backgroundColor = new Color(0.015f, 0.010f, 0.008f, 0.96f);
        feedback.style.borderLeftWidth = 1; feedback.style.borderRightWidth = 1;
        feedback.style.borderTopWidth = 1; feedback.style.borderBottomWidth = 1;
        feedback.style.borderLeftColor = Gild; feedback.style.borderRightColor = Gild;
        feedback.style.borderTopColor = Gild; feedback.style.borderBottomColor = Gild;
        feedback.style.display = DisplayStyle.None;
        root.Add(feedback);

        caption = Label("ParlorCaptionSurface", 18, Ink);
        caption.style.position = Position.Absolute;
        caption.style.left = 320; caption.style.right = 320; caption.style.bottom = 36;
        caption.style.paddingLeft = 28; caption.style.paddingRight = 28;
        caption.style.paddingTop = 14; caption.style.paddingBottom = 14;
        caption.style.unityTextAlign = TextAnchor.MiddleCenter;
        caption.style.backgroundColor = new Color(0.018f, 0.012f, 0.010f, 0.96f);
        caption.style.borderLeftWidth = 1; caption.style.borderRightWidth = 1;
        caption.style.borderTopWidth = 1; caption.style.borderBottomWidth = 1;
        caption.style.borderLeftColor = Gild; caption.style.borderRightColor = Gild;
        caption.style.borderTopColor = Gild; caption.style.borderBottomColor = Gild;
        caption.style.display = DisplayStyle.None;
        root.Add(caption);

        focusPanel = new VisualElement { name = "ParlorFocusPanel", pickingMode = PickingMode.Position };
        focusPanel.style.position = Position.Absolute;
        focusPanel.style.left = 220; focusPanel.style.right = 220; focusPanel.style.bottom = 76;
        focusPanel.style.minHeight = 310;
        focusPanel.style.paddingLeft = 32; focusPanel.style.paddingRight = 32;
        focusPanel.style.paddingTop = 24; focusPanel.style.paddingBottom = 24;
        focusPanel.style.backgroundColor = new Color(0.025f, 0.018f, 0.014f, 0.97f);
        focusPanel.style.borderTopWidth = 2; focusPanel.style.borderBottomWidth = 2;
        focusPanel.style.borderLeftWidth = 2; focusPanel.style.borderRightWidth = 2;
        focusPanel.style.borderTopColor = Gild; focusPanel.style.borderBottomColor = Gild;
        focusPanel.style.borderLeftColor = Gild; focusPanel.style.borderRightColor = Gild;
        handRow = new VisualElement { name = "ParlorHand", pickingMode = PickingMode.Position };
        handRow.style.flexDirection = FlexDirection.Row;
        handRow.style.justifyContent = Justify.Center;
        focusPanel.Add(handRow);
        evidenceList = new VisualElement { name = "ParlorEvidence", pickingMode = PickingMode.Ignore };
        evidenceList.style.marginTop = 20;
        evidenceList.style.paddingTop = 14;
        evidenceList.style.borderTopWidth = 1;
        evidenceList.style.borderTopColor = new Color(0.45f, 0.35f, 0.20f, 0.65f);
        focusPanel.Add(evidenceList);
        root.Add(focusPanel);
        GmUiText.UseStandardGenerator(root);
        Refresh();
    }

    public void Refresh()
    {
        if (root == null || !IsConfigured) return;
        GmParlorFocusModel model = focus.Model;
        score.text = model.Score;
        action.text = model.Action;
        feedback.text = model.Feedback;
        bool highContrast = GmAccessibilitySettings.HighContrast;
        float textScale = GmAccessibilitySettings.TextScale;
        score.style.fontSize = Mathf.RoundToInt(18 * textScale);
        action.style.fontSize = Mathf.RoundToInt(19 * textScale);
        score.style.color = highContrast ? HighContrastInk : Ink;
        action.style.color = highContrast ? HighContrastGild : Gild;
        feedback.style.fontSize = Mathf.RoundToInt(18 * textScale);
        feedback.style.color = highContrast ? HighContrastInk : Ink;
        feedback.style.display = string.IsNullOrEmpty(model.Feedback)
            ? DisplayStyle.None : DisplayStyle.Flex;
        feedback.style.bottom = focus.IsOpen ? 410 : 92;
        feedback.style.backgroundColor = highContrast
            ? new Color(0f, 0f, 0f, 1f) : new Color(0.008f, 0.006f, 0.004f, 0.985f);
        feedback.style.borderTopWidth = 1; feedback.style.borderBottomWidth = 1;
        feedback.style.borderLeftWidth = 1; feedback.style.borderRightWidth = 1;
        Color feedbackBorder = highContrast ? Color.white : Gild;
        feedback.style.borderTopColor = feedbackBorder;
        feedback.style.borderBottomColor = feedbackBorder;
        feedback.style.borderLeftColor = feedbackBorder;
        feedback.style.borderRightColor = feedbackBorder;
        if (!string.IsNullOrEmpty(model.Feedback)) feedback.BringToFront();
        focusPanel.style.display = focus.IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        focusPanel.EnableInClassList("gm-high-contrast", highContrast);
        Color border = highContrast ? Color.white : Gild;
        focusPanel.style.borderTopColor = border;
        focusPanel.style.borderBottomColor = border;
        focusPanel.style.borderLeftColor = border;
        focusPanel.style.borderRightColor = border;
        focusPanel.style.backgroundColor = highContrast
            ? new Color(0f, 0f, 0f, 0.985f)
            : new Color(0.018f, 0.014f, 0.012f, 0.96f);
        handRow.Clear();
        foreach (GmParlorFocusCard card in model.Cards)
        {
            int index = card.Index;
            var button = new Button(() => focus.Select(index))
            {
                name = $"ParlorCard_{index}",
                text = card.Label,
            };
            button.SetEnabled(card.Legal);
            button.style.fontSize = Mathf.RoundToInt(19 * textScale);
            button.style.color = card.Focused
                ? (highContrast ? HighContrastGild : Gild)
                : (highContrast ? HighContrastInk : Ink);
            button.style.backgroundColor = card.Focused
                ? new Color(0.32f, 0.18f, 0.09f, 0.96f)
                : new Color(0.07f, 0.055f, 0.045f, 0.90f);
            button.style.borderTopWidth = card.Focused ? 2 : 1;
            button.style.borderBottomWidth = card.Focused ? 2 : 1;
            button.style.borderLeftWidth = card.Focused ? 2 : 1;
            button.style.borderRightWidth = card.Focused ? 2 : 1;
            Color cardBorder = card.Focused
                ? (highContrast ? Color.white : Gild)
                : (highContrast ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.35f, 0.28f, 0.18f, 0.8f));
            button.style.borderTopColor = cardBorder;
            button.style.borderBottomColor = cardBorder;
            button.style.borderLeftColor = cardBorder;
            button.style.borderRightColor = cardBorder;
            button.style.marginLeft = 6; button.style.marginRight = 6;
            button.style.minWidth = 124; button.style.minHeight = 78;
            handRow.Add(button);
        }
        evidenceList.Clear();
        int first = Mathf.Max(0, model.Evidence.Length - 3);
        for (int index = first; index < model.Evidence.Length; index++)
        {
            string rowText = model.Evidence[index];
            if (!rowText.StartsWith("✦ ", System.StringComparison.Ordinal)) rowText = "✦ " + rowText;
            Label evidence = Label($"ParlorEvidence_{index}", 16,
                highContrast ? HighContrastInk : Dim, rowText);
            evidence.style.fontSize = Mathf.RoundToInt(16 * textScale);
            evidenceList.Add(evidence);
        }
        bool showCaption = GmAccessibilitySettings.Captions &&
            coordinator != null && coordinator.HasActiveCaption;
        caption.text = showCaption ? coordinator.ActiveCaptionText : string.Empty;
        caption.style.fontSize = Mathf.RoundToInt(18 * textScale);
        caption.style.color = highContrast ? HighContrastInk : Ink;
        caption.style.display = showCaption ? DisplayStyle.Flex : DisplayStyle.None;
        GmUiText.UseStandardGenerator(root);
    }

    void OnDestroy()
    {
        if (focus != null) focus.OnChanged -= Refresh;
        if (coordinator != null)
        {
            coordinator.OnActiveCaptionChanged -= Refresh;
            coordinator.OnCaptionStateReconstructed -= Refresh;
        }
        GmAccessibilitySettings.OnChanged -= Refresh;
        if (panelSettings != null) Destroy(panelSettings);
    }

    static Label Label(string name, int size, Color color, string text = "")
    {
        var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
        label.style.fontSize = Mathf.RoundToInt(size * GmAccessibilitySettings.TextScale);
        label.style.color = color;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }
}
