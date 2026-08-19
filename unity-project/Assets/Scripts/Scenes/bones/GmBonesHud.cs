using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmBonesHud : MonoBehaviour
{
    static readonly Color Ink = new Color(0.92f, 0.88f, 0.78f);
    static readonly Color Gold = new Color(0.95f, 0.70f, 0.26f);
    GmBonesController controller;
    GmBonesInput tableInput;
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    Label status;
    Label dice;
    VisualElement diceSlots;
    VisualElement[] dieSlots;
    Label[] dieValues;
    Label displayedReroll;
    Label observedHonestReroll;
    VisualElement changedDieMarker;
    VisualElement actions;
    VisualElement intervention;
    Label evidence;
    Label actionLog;
    Label result;
    Label caption;
    Label feedback;
    Button[] choiceButtons;
    Button challenge;
    Button proceed;
    bool subscribed;
    string feedbackText = string.Empty;

    public bool IsConfigured => controller != null;
    public int RefreshRevision { get; private set; }

    public bool TryConfigure(GmBonesController tableController, out string error)
    {
        return TryConfigure(tableController, GetComponent<GmBonesInput>(), out error);
    }

    public bool TryConfigure(GmBonesController tableController, GmBonesInput input,
        out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Bones HUD needs an initialized controller";
            return false;
        }
        Unsubscribe();
        controller = tableController;
        tableInput = input;
        feedbackText = tableInput?.FeedbackMessage ?? string.Empty;
        if (isActiveAndEnabled) Subscribe();
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
        if (controller == null) return;
        Subscribe();
        Refresh();
    }

    void BuildUi()
    {
        if (root != null) return;
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmBonesPanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        document = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        root = document.rootVisualElement;
        root.name = "GmBonesHudRoot";
        root.style.position = Position.Absolute;
        root.style.left = 28; root.style.right = 28; root.style.top = 28; root.style.bottom = 28;
        root.style.paddingLeft = 24; root.style.paddingRight = 24;
        root.style.paddingTop = 20; root.style.paddingBottom = 20;
        root.style.backgroundColor = new Color(0.015f, 0.012f, 0.01f, 0.94f);

        status = AddLabel(root, "BonesStatus", 18);
        dice = AddLabel(root, "BonesDice", 28);
        diceSlots = new VisualElement { name = "BonesDiceSlots", pickingMode = PickingMode.Ignore };
        diceSlots.style.flexDirection = FlexDirection.Row;
        root.Add(diceSlots);
        dieSlots = new VisualElement[3];
        dieValues = new Label[3];
        for (int index = 0; index < 3; index++)
        {
            VisualElement slot = new VisualElement
            {
                name = $"BonesDie{index + 1}",
                pickingMode = PickingMode.Ignore
            };
            slot.style.width = 72;
            slot.style.minHeight = 72;
            slot.style.marginRight = 12;
            slot.style.borderTopWidth = 2; slot.style.borderBottomWidth = 2;
            slot.style.borderLeftWidth = 2; slot.style.borderRightWidth = 2;
            Label value = AddLabel(slot, $"BonesDie{index + 1}Value", 28);
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            dieSlots[index] = slot;
            dieValues[index] = value;
            diceSlots.Add(slot);
        }
        displayedReroll = AddLabel(root, "BonesDisplayedReroll", 18);
        observedHonestReroll = AddLabel(root, "BonesObservedHonestReroll", 18);
        actions = new VisualElement { name = "BonesActions" };
        actions.style.flexDirection = FlexDirection.Row;
        root.Add(actions);
        choiceButtons = new Button[4];
        string[] names = { "BonesBank", "BonesPress1", "BonesPress2", "BonesPress3" };
        for (int index = 0; index < choiceButtons.Length; index++)
        {
            int target = index;
            choiceButtons[index] = AddButton(actions, names[index], () =>
            {
                controller.MoveFocus(target - controller.FocusIndex);
                RunAction(controller.ConfirmFocusedAction);
            });
        }

        intervention = new VisualElement { name = "BonesIntervention" };
        intervention.style.flexDirection = FlexDirection.Row;
        root.Add(intervention);
        challenge = AddButton(intervention, "BonesChallenge", () => RunAction(controller.CallTell));
        proceed = AddButton(intervention, "BonesProceed", () => RunAction(controller.ConfirmFocusedAction));
        evidence = AddLabel(root, "BonesEvidence", 18);
        actionLog = AddLabel(root, "BonesActionLog", 16);
        result = AddLabel(root, "BonesResult", 22);
        caption = AddLabel(root, "BonesCaption", 18);
        feedback = AddLabel(root, "BonesFeedback", 18);
        Refresh();
    }

    public void Refresh()
    {
        if (root == null || !IsConfigured) return;
        RefreshRevision++;
        GmBonesPresentationState model = GmBonesPresentationModel.Project(controller);
        bool highContrast = GmAccessibilitySettings.HighContrast;
        float scale = GmAccessibilitySettings.TextScale;
        root.EnableInClassList("gm-high-contrast", highContrast);
        root.EnableInClassList("gm-reduced-motion", GmAccessibilitySettings.ReducedMotion);
        root.style.backgroundColor = highContrast ? Color.black : new Color(0.015f, 0.012f, 0.01f, 0.94f);
        status.text = model.Phase == GmBonesPresentationPhase.PlayerChoice
            ? $"Round {model.Round} • You {model.PlayerTotal} • Aldric {model.AldricTotal}"
            : model.Phase == GmBonesPresentationPhase.Intervention
                ? "Aldric's final throw changed. Challenge or proceed."
                : "Bones match complete";
        int[] shown = model.Dice;
        dice.text = $"Dice: {shown[0]}  {shown[1]}  {shown[2]}";
        for (int index = 0; index < dieValues.Length; index++)
        {
            dieValues[index].text = shown[index].ToString();
            dieSlots[index].EnableInClassList("gm-bones-changed-die", index == model.ChangedDieSlot);
        }
        changedDieMarker?.RemoveFromHierarchy();
        changedDieMarker = null;
        if (model.ChangedDieSlot >= 0 && model.ChangedDieSlot < dieSlots.Length)
        {
            changedDieMarker = new VisualElement
            {
                name = "BonesChangedDieMarker",
                pickingMode = PickingMode.Ignore
            };
            changedDieMarker.style.width = 18;
            changedDieMarker.style.height = 18;
            changedDieMarker.style.borderTopWidth = 4; changedDieMarker.style.borderBottomWidth = 4;
            changedDieMarker.style.borderLeftWidth = 4; changedDieMarker.style.borderRightWidth = 4;
            changedDieMarker.style.borderTopLeftRadius = 9; changedDieMarker.style.borderTopRightRadius = 9;
            changedDieMarker.style.borderBottomLeftRadius = 9; changedDieMarker.style.borderBottomRightRadius = 9;
            Color markerColor = highContrast ? Color.white : Gold;
            changedDieMarker.style.borderTopColor = markerColor;
            changedDieMarker.style.borderBottomColor = markerColor;
            changedDieMarker.style.borderLeftColor = markerColor;
            changedDieMarker.style.borderRightColor = markerColor;
            dieSlots[model.ChangedDieSlot].Add(changedDieMarker);
        }
        int[] tableReroll = model.DisplayedReroll;
        displayedReroll.text = tableReroll.Length == 2
            ? $"Table showed reroll: {tableReroll[0]}, {tableReroll[1]}"
            : "Table reroll: none";
        int[] honestReroll = model.ObservedHonestReroll;
        observedHonestReroll.text = honestReroll.Length == 2
            ? $"Observed honest reroll: {honestReroll[0]}, {honestReroll[1]}"
            : "Honest reroll: not observed";
        GmBonesActionPresentation[] cards = model.Actions;
        for (int index = 0; index < choiceButtons.Length; index++)
        {
            Button button = choiceButtons[index];
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
        actions.style.display = model.Phase == GmBonesPresentationPhase.PlayerChoice ? DisplayStyle.Flex : DisplayStyle.None;
        intervention.style.display = model.Phase == GmBonesPresentationPhase.Intervention ? DisplayStyle.Flex : DisplayStyle.None;
        challenge.text = "Challenge the loaded six";
        proceed.text = "Proceed without challenge";
        challenge.SetEnabled(model.CanChallenge);
        proceed.SetEnabled(model.CanProceed);
        evidence.text = model.HasLoadedSixMarker ? "EVIDENCE • " + model.EvidenceText : "No intervention evidence recorded.";
        actionLog.text = model.ActionLog.Length == 0 ? "No choices recorded." : string.Join("\n", model.ActionLog);
        result.text = model.HasResult
            ? $"Result: {PresentResult(model.Result)}" + (model.HasLoadedSixMarker
                ? model.CorrectedByChallenge
                    ? " • Challenge restored the honest throw"
                    : " • Proceeded with the table-shown throw"
                : string.Empty)
            : string.Empty;
        result.style.display = model.HasResult ? DisplayStyle.Flex : DisplayStyle.None;
        caption.text = "CAPTION • Dice and intervention facts are always shown as text.";
        caption.style.display = GmAccessibilitySettings.Captions ? DisplayStyle.Flex : DisplayStyle.None;
        feedback.text = feedbackText;
        feedback.style.display = string.IsNullOrEmpty(feedbackText) ? DisplayStyle.None : DisplayStyle.Flex;
        foreach (TextElement text in root.Query<TextElement>().ToList())
        {
            int baseSize = text == dice ? 28 : text == result ? 22 : text == actionLog ? 16 : 18;
            text.style.fontSize = Mathf.RoundToInt(baseSize * scale);
            text.style.color = highContrast ? Color.white : Ink;
        }
        GmUiText.UseStandardGenerator(root);
    }

    static string PresentResult(GmBonesMatchResult value) => value == GmBonesMatchResult.PlayerWin
        ? "You win" : value == GmBonesMatchResult.AldricWin ? "Aldric wins" : "Tie";

    void RunAction(System.Func<GmBonesActionError> action)
    {
        GmBonesActionError error = action();
        feedbackText = GmBonesInput.FeedbackFor(error, controller);
        Refresh();
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
        feedbackText = tableInput?.FeedbackMessage ?? string.Empty;
        Refresh();
    }

    void OnDisable() => Unsubscribe();
    void OnDestroy()
    {
        Unsubscribe();
        if (panelSettings != null) Destroy(panelSettings);
    }
}
