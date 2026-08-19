using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmBonesHud : MonoBehaviour
{
    static readonly Color Ink = new Color(0.92f, 0.88f, 0.78f);
    static readonly Color Gold = new Color(0.95f, 0.70f, 0.26f);
    GmBonesController controller;
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    Label status;
    Label dice;
    VisualElement actions;
    VisualElement intervention;
    Label evidence;
    Label actionLog;
    Label result;
    Label caption;
    Button[] choiceButtons;
    Button challenge;
    Button proceed;

    public bool IsConfigured => controller != null;

    public bool TryConfigure(GmBonesController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized)
        {
            error = "Bones HUD needs an initialized controller";
            return false;
        }
        Unsubscribe();
        controller = tableController;
        Subscribe();
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
    void OnEnable() { if (controller != null) Subscribe(); }

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
                controller.ConfirmFocusedAction();
            });
        }

        intervention = new VisualElement { name = "BonesIntervention" };
        intervention.style.flexDirection = FlexDirection.Row;
        root.Add(intervention);
        challenge = AddButton(intervention, "BonesChallenge", () => controller.CallTell());
        proceed = AddButton(intervention, "BonesProceed", () => controller.ConfirmFocusedAction());
        evidence = AddLabel(root, "BonesEvidence", 18);
        actionLog = AddLabel(root, "BonesActionLog", 16);
        result = AddLabel(root, "BonesResult", 22);
        caption = AddLabel(root, "BonesCaption", 18);
        Refresh();
    }

    public void Refresh()
    {
        if (root == null || !IsConfigured) return;
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
        result.text = model.HasResult ? $"Result: {PresentResult(model.Result)}" : string.Empty;
        result.style.display = model.HasResult ? DisplayStyle.Flex : DisplayStyle.None;
        caption.text = "CAPTION • Dice and intervention facts are always shown as text.";
        caption.style.display = GmAccessibilitySettings.Captions ? DisplayStyle.Flex : DisplayStyle.None;
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
        GmAccessibilitySettings.OnChanged -= Refresh;
    }

    void Subscribe()
    {
        Unsubscribe();
        controller.OnStateChanged += Refresh;
        controller.OnFocusChanged += Refresh;
        GmAccessibilitySettings.OnChanged += Refresh;
    }

    void OnDisable() => Unsubscribe();
    void OnDestroy()
    {
        Unsubscribe();
        if (panelSettings != null) Destroy(panelSettings);
    }
}
