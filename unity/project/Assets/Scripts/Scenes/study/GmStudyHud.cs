using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmStudyHud : MonoBehaviour
{
    static readonly Color Ink = new Color(0.92f, 0.88f, 0.78f);
    static readonly Color Gold = new Color(0.95f, 0.70f, 0.26f);
    GmStudyController controller;
    GmStudyInput tableInput;
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    Label status;
    Label board;
    VisualElement boardPanel;
    VisualElement arbiterMarker;
    Label alteredFen;
    Label observedOriginalFen;
    VisualElement actions;
    VisualElement intervention;
    Label evidence;
    Label actionLog;
    Label result;
    Label caption;
    Label feedback;
    Button[] cardButtons;
    Button challenge;
    Button proceed;
    bool subscribed;
    bool interactionsSuspended;

    public bool IsConfigured => controller != null;
    public bool InteractionsEnabled => !interactionsSuspended && enabled;
    public int RefreshRevision { get; private set; }

    public bool TryConfigure(GmStudyController tableController, out string error)
    {
        return TryConfigure(tableController, GetComponent<GmStudyInput>(), out error);
    }

    public bool TryConfigure(GmStudyController tableController, GmStudyInput input,
        out string error)
    {
        if (tableController == null || !tableController.IsInitialized || input == null ||
            !input.IsConfiguredFor(tableController))
        {
            error = "Study HUD needs an initialized controller and its configured input owner";
            return false;
        }
        Unsubscribe();
        controller = tableController;
        tableInput = input;
        if (isActiveAndEnabled && !interactionsSuspended) Subscribe();
        error = string.Empty;
        if (root != null) Refresh();
        return true;
    }

    public VisualElement BuildForTests()
    {
        BuildUi();
        return root;
    }

    void Start() { if (IsConfigured) BuildUi(); }
    void OnEnable()
    {
        if (controller == null || interactionsSuspended) return;
        Subscribe();
        Refresh();
    }

    void BuildUi()
    {
        if (root != null) return;
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmStudyPanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        document = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        root = document.rootVisualElement;
        root.name = "GmStudyHudRoot";
        root.style.position = Position.Absolute;
        root.style.left = 28; root.style.right = StyleKeyword.Auto;
        root.style.top = 28; root.style.bottom = StyleKeyword.Auto;
        root.style.width = 660;
        root.style.paddingLeft = 24; root.style.paddingRight = 24;
        root.style.paddingTop = 20; root.style.paddingBottom = 20;
        root.style.backgroundColor = new Color(0.015f, 0.012f, 0.01f, 0.84f);

        status = AddLabel(root, "StudyStatus", 18);
        boardPanel = new VisualElement { name = "StudyBoardPanel", pickingMode = PickingMode.Ignore };
        boardPanel.style.flexDirection = FlexDirection.Row;
        root.Add(boardPanel);
        board = AddLabel(boardPanel, "StudyBoard", 20);
        alteredFen = AddLabel(root, "StudyAlteredFen", 16);
        observedOriginalFen = AddLabel(root, "StudyObservedOriginalFen", 16);
        actions = new VisualElement { name = "StudyActions" };
        actions.style.flexDirection = FlexDirection.Column;
        root.Add(actions);
        cardButtons = new Button[3];
        string[] names = { "StudyCard1", "StudyCard2", "StudyCard3" };
        for (int index = 0; index < cardButtons.Length; index++)
        {
            int target = index;
            cardButtons[index] = AddButton(actions, names[index], () =>
                InvokeButtonAction(names[target]));
        }

        intervention = new VisualElement { name = "StudyIntervention" };
        intervention.style.flexDirection = FlexDirection.Row;
        root.Add(intervention);
        challenge = AddButton(intervention, "StudyChallenge", () => InvokeButtonAction("StudyChallenge"));
        proceed = AddButton(intervention, "StudyProceed", () => InvokeButtonAction("StudyProceed"));
        evidence = AddLabel(root, "StudyEvidence", 18);
        actionLog = AddLabel(root, "StudyActionLog", 16);
        result = AddLabel(root, "StudyResult", 22);
        caption = AddLabel(root, "StudyCaption", 18);
        feedback = AddLabel(root, "StudyFeedback", 18);
        Refresh();
    }

    public void Refresh()
    {
        if (interactionsSuspended || root == null || !IsConfigured) return;
        RefreshRevision++;
        GmStudyPresentationState model = GmStudyPresentationModel.Project(controller);
        bool highContrast = GmAccessibilitySettings.HighContrast;
        float scale = GmAccessibilitySettings.TextScale;
        root.EnableInClassList("gm-high-contrast", highContrast);
        root.EnableInClassList("gm-reduced-motion", GmAccessibilitySettings.ReducedMotion);
        root.style.backgroundColor = highContrast ? Color.black : new Color(0.015f, 0.012f, 0.01f, 0.84f);
        status.text = model.Phase == GmStudyPresentationPhase.Move
            ? $"Position {model.PositionIndex + 1}: {model.PositionTitle} • {model.CorrectCount} correct"
            : model.Phase == GmStudyPresentationPhase.Intervention
                ? "Aldric applied an arbiter override. Challenge or proceed."
                : "Study match complete";
        board.text = $"Board: {model.ShownFen}";
        arbiterMarker?.RemoveFromHierarchy();
        arbiterMarker = null;
        if (model.HasArbiterMarker)
        {
            arbiterMarker = new VisualElement
            {
                name = "StudyArbiterMarker",
                tooltip = model.EvidenceIconId,
                pickingMode = PickingMode.Ignore
            };
            arbiterMarker.style.width = 18;
            arbiterMarker.style.height = 18;
            arbiterMarker.style.marginLeft = 10;
            arbiterMarker.style.borderTopWidth = 4; arbiterMarker.style.borderBottomWidth = 4;
            arbiterMarker.style.borderLeftWidth = 4; arbiterMarker.style.borderRightWidth = 4;
            arbiterMarker.style.borderTopLeftRadius = 9; arbiterMarker.style.borderTopRightRadius = 9;
            arbiterMarker.style.borderBottomLeftRadius = 9; arbiterMarker.style.borderBottomRightRadius = 9;
            Color markerColor = highContrast ? Color.white : Gold;
            arbiterMarker.style.borderTopColor = markerColor;
            arbiterMarker.style.borderBottomColor = markerColor;
            arbiterMarker.style.borderLeftColor = markerColor;
            arbiterMarker.style.borderRightColor = markerColor;
            boardPanel.Add(arbiterMarker);
        }
        alteredFen.text = string.IsNullOrEmpty(model.AlteredFen)
            ? "Altered position: none" : $"Altered position shown: {model.AlteredFen}";
        observedOriginalFen.text = string.IsNullOrEmpty(model.ObservedOriginalFen)
            ? "Original position: not observed" : $"Observed original position: {model.ObservedOriginalFen}";
        GmStudyActionPresentation[] cards = model.Actions;
        for (int index = 0; index < cardButtons.Length; index++)
        {
            Button button = cardButtons[index];
            if (index >= cards.Length) { button.style.display = DisplayStyle.None; continue; }
            button.style.display = DisplayStyle.Flex;
            button.text = cards[index].Selected ? $"SELECTED • {cards[index].Label}" : cards[index].Label;
            button.SetEnabled(cards[index].Legal);
            float border = cards[index].Selected ? 3 : 1;
            button.style.borderTopWidth = border; button.style.borderBottomWidth = border;
            button.style.borderLeftWidth = border; button.style.borderRightWidth = border;
            button.style.borderTopColor = highContrast ? Color.white : Gold;
            button.style.borderBottomColor = highContrast ? Color.white : Gold;
            button.style.borderLeftColor = highContrast ? Color.white : Gold;
            button.style.borderRightColor = highContrast ? Color.white : Gold;
        }
        actions.style.display = model.Phase == GmStudyPresentationPhase.Move ? DisplayStyle.Flex : DisplayStyle.None;
        intervention.style.display = model.Phase == GmStudyPresentationPhase.Intervention ? DisplayStyle.Flex : DisplayStyle.None;
        challenge.text = "Challenge the arbiter override";
        proceed.text = "Proceed without challenge";
        challenge.SetEnabled(model.CanChallenge);
        proceed.SetEnabled(model.CanProceed);
        evidence.text = model.HasArbiterMarker
            ? "EVIDENCE • " + model.EvidenceText + (model.EvidenceFacts.Length == 0
                ? string.Empty : " (" + string.Join("; ", model.EvidenceFacts) + ")")
            : "No intervention evidence recorded.";
        actionLog.text = model.ActionLog.Length == 0 ? "No choices recorded." : string.Join("\n", model.ActionLog);
        result.text = model.HasResult
            ? $"Result: {PresentResult(model.Result)}" + (model.HasArbiterMarker
                ? model.CorrectedByChallenge
                    ? " • Challenge restored the honest position"
                    : " • Proceeded with the altered board display"
                : string.Empty)
            : string.Empty;
        result.style.display = model.HasResult ? DisplayStyle.Flex : DisplayStyle.None;
        caption.text = "CAPTION • The board position and intervention facts are always shown as text.";
        caption.style.display = GmAccessibilitySettings.Captions ? DisplayStyle.Flex : DisplayStyle.None;
        feedback.text = tableInput.FeedbackMessage;
        feedback.style.display = string.IsNullOrEmpty(tableInput.FeedbackMessage)
            ? DisplayStyle.None : DisplayStyle.Flex;
        foreach (TextElement text in root.Query<TextElement>().ToList())
        {
            int baseSize = text == board ? 20 : text == result ? 22 : text == actionLog ? 16 : 18;
            text.style.fontSize = Mathf.RoundToInt(baseSize * scale);
            text.style.color = highContrast ? Color.white : Ink;
        }
        GmUiText.UseStandardGenerator(root);
    }

    static string PresentResult(GmStudyMatchResult value) => value == GmStudyMatchResult.PlayerWin
        ? "You win" : "Aldric wins";

    GmStudyActionError InvokeButtonAction(string buttonName)
    {
        if (tableInput == null) return GmStudyActionError.NotInitialized;
        switch (buttonName)
        {
            case "StudyCard1": return tableInput.FocusThenConfirm(0);
            case "StudyCard2": return tableInput.FocusThenConfirm(1);
            case "StudyCard3": return tableInput.FocusThenConfirm(2);
            case "StudyChallenge": return tableInput.ChallengeAction();
            case "StudyProceed": return tableInput.ConfirmAction();
            default: return GmStudyActionError.WrongPhase;
        }
    }

    public GmStudyActionError InvokeButtonActionForTests(string buttonName) =>
        InvokeButtonAction(buttonName);

    public void SuspendInteractions()
    {
        interactionsSuspended = true;
        Unsubscribe();
        root?.SetEnabled(false);
    }

    public void ResumeInteractions()
    {
        interactionsSuspended = false;
        root?.SetEnabled(true);
        if (isActiveAndEnabled && controller != null)
        {
            Subscribe();
            Refresh();
        }
    }

    static Label AddLabel(VisualElement parent, string name, int size)
    {
        var label = new Label { name = name, pickingMode = PickingMode.Ignore };
        label.style.fontSize = size;
        label.style.color = Ink;
        label.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(label);
        return label;
    }

    static Button AddButton(VisualElement parent, string name, System.Action callback)
    {
        var button = new Button(callback) { name = name };
        button.style.minHeight = 44;
        button.style.minWidth = 150;
        button.style.marginRight = 8;
        button.style.marginBottom = 6;
        button.style.unityTextGenerator = TextGeneratorType.Standard;
        parent.Add(button);
        return button;
    }

    void Unsubscribe()
    {
        if (controller != null)
        {
            controller.OnStateChanged -= Refresh;
            controller.OnFocusChanged -= Refresh;
        }
        if (tableInput != null) tableInput.OnFeedbackChanged -= OnInputFeedbackChanged;
        GmAccessibilitySettings.OnChanged -= Refresh;
        subscribed = false;
    }

    void Subscribe()
    {
        if (subscribed || controller == null) return;
        controller.OnStateChanged += Refresh;
        controller.OnFocusChanged += Refresh;
        if (tableInput != null) tableInput.OnFeedbackChanged += OnInputFeedbackChanged;
        GmAccessibilitySettings.OnChanged += Refresh;
        subscribed = true;
    }

    void OnInputFeedbackChanged()
    {
        Refresh();
    }

    void OnDisable() => Unsubscribe();
    void OnDestroy()
    {
        Unsubscribe();
        if (panelSettings != null) Destroy(panelSettings);
    }
}
