using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(120)]
public sealed class GmCourtHud : MonoBehaviour
{
    GmCourtController court;
    PanelSettings panelSettings;
    UIDocument document;
    Label clock;
    Label seals;
    Label argument;
    Label feedback;
    VisualElement cards;
    Button shard;
    Button present;
    int revision = -1;

    public bool IsBuilt => document != null && cards != null;

    void Start()
    {
        court = GetComponent<GmCourtController>() ?? FindAnyObjectByType<GmCourtController>();
        BuildUi();
        Refresh();
    }

    void BuildUi()
    {
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmCourtPanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 680;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 680;

        VisualElement root = document.rootVisualElement;
        root.name = "GmCourtHud";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        VisualElement top = Panel("CourtStatus");
        top.style.position = Position.Absolute;
        top.style.left = 32; top.style.right = 32; top.style.top = 26;
        top.style.flexDirection = FlexDirection.Row;
        top.style.justifyContent = Justify.SpaceBetween;
        top.Add(Text("Title", "THE ASSIZE OF ONE", 18, Gold()));
        seals = Text("Seals", "ARGUMENTS  0 / 3", 17, Pale());
        clock = Text("Clock", "01:30", 20, Gold());
        top.Add(seals); top.Add(clock); root.Add(top);

        VisualElement hearing = Panel("Hearing");
        hearing.style.position = Position.Absolute;
        hearing.style.left = Length.Percent(8); hearing.style.right = Length.Percent(8);
        hearing.style.bottom = 34;
        argument = Text("Argument", "", 24, Pale());
        argument.style.unityFontStyleAndWeight = FontStyle.Bold;
        feedback = Text("Feedback", "", 17, new Color(0.76f, 0.69f, 0.58f));
        feedback.style.marginTop = 9;
        cards = new VisualElement { name = "EvidenceCards", pickingMode = PickingMode.Position };
        cards.style.flexDirection = FlexDirection.Row;
        cards.style.justifyContent = Justify.Center;
        cards.style.marginTop = 18;
        shard = new Button(() => court?.CollectEvidenceShard())
            { name = "CollectShard", text = "COLLECT MISFILED MIRROR GLASS" };
        StyleButton(shard, 17);
        shard.style.alignSelf = Align.Center;
        shard.style.marginTop = 16;
        present = new Button(() => court?.PresentSelectedEvidence())
            { name = "PresentEvidence", text = "PRESENT SELECTED EVIDENCE  [E / A]" };
        StyleButton(present, 17);
        present.style.alignSelf = Align.Center;
        present.style.marginTop = 16;
        present.style.minWidth = 360;
        hearing.Add(argument); hearing.Add(feedback); hearing.Add(cards); hearing.Add(present);
        hearing.Add(shard);
        root.Add(hearing);
    }

    void Update()
    {
        if (court == null) return;
        int seconds = Mathf.CeilToInt(court.PressureTimeRemaining);
        clock.text = $"{seconds / 60:00}:{seconds % 60:00}";
        if (revision != court.Revision) Refresh();
    }

    void Refresh()
    {
        if (court == null || cards == null) return;
        revision = court.Revision;
        seals.text = $"ARGUMENTS  {3 - court.WaxSealsRemaining} / 3";
        argument.text = court.HearingResolved
            ? (court.WaxSealsRemaining == 0 ? "VERDICT: THE ARGUMENT STANDS" : "VERDICT: TIME")
            : $"ARGUMENT {court.CurrentArgumentIndex + 1}   {court.CurrentArgument}";
        feedback.text = court.Feedback;
        cards.Clear();
        for (int index = 0; index < court.EvidenceDeck.Count; index++)
        {
            int selected = index;
            GmCourtController.EvidenceCard card = court.EvidenceDeck[index];
            bool used = court.IsEvidenceUnavailableForCurrentArgument(card.Id);
            var button = new Button(() => court.SelectEvidence(selected))
            {
                name = $"Evidence_{index + 1}_{card.Id}",
                text = $"{index + 1}  {card.Title.ToUpperInvariant()}\n{card.Body}",
            };
            StyleButton(button, 15);
            button.style.flexBasis = Length.Percent(19);
            button.style.flexGrow = 1;
            button.style.flexShrink = 1;
            button.style.maxWidth = 285;
            button.style.minWidth = 0;
            button.style.minHeight = 128;
            button.style.marginLeft = 5; button.style.marginRight = 5;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.color = used ? new Color(0.46f, 0.43f, 0.38f) : Pale();
            Color border = index == court.SelectedEvidenceIndex ? Gold() : new Color(0.31f, 0.25f, 0.18f);
            SetBorder(button, border, index == court.SelectedEvidenceIndex ? 2 : 1);
            button.SetEnabled(!used && !court.HearingResolved);
            cards.Add(button);
        }
        shard.style.display = court.CurrentArgumentIndex > 0 && !court.ShardTwoCollected
            ? DisplayStyle.Flex : DisplayStyle.None;
        present.SetEnabled(!court.HearingResolved &&
            !court.IsEvidenceUnavailableForCurrentArgument(
                court.EvidenceDeck[court.SelectedEvidenceIndex].Id));
    }

    static VisualElement Panel(string name)
    {
        var panel = new VisualElement { name = name, pickingMode = PickingMode.Position };
        panel.style.paddingLeft = 24; panel.style.paddingRight = 24;
        panel.style.paddingTop = 18; panel.style.paddingBottom = 18;
        panel.style.backgroundColor = new Color(0.018f, 0.012f, 0.009f, 0.94f);
        SetBorder(panel, new Color(0.63f, 0.45f, 0.22f, 0.9f), 1);
        return panel;
    }

    static Label Text(string name, string value, int size, Color color)
    {
        var label = new Label(value) { name = name, pickingMode = PickingMode.Ignore };
        label.style.fontSize = size; label.style.color = color;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.unityTextGenerator = TextGeneratorType.Standard;
        return label;
    }

    static void StyleButton(Button button, int size)
    {
        button.style.fontSize = size;
        button.style.color = Pale();
        button.style.unityTextGenerator = TextGeneratorType.Standard;
        button.style.backgroundColor = new Color(0.065f, 0.043f, 0.028f, 0.98f);
        button.style.paddingLeft = 12; button.style.paddingRight = 12;
        button.style.paddingTop = 10; button.style.paddingBottom = 10;
    }

    static void SetBorder(VisualElement element, Color color, float width)
    {
        element.style.borderLeftWidth = width; element.style.borderRightWidth = width;
        element.style.borderTopWidth = width; element.style.borderBottomWidth = width;
        element.style.borderLeftColor = color; element.style.borderRightColor = color;
        element.style.borderTopColor = color; element.style.borderBottomColor = color;
    }

    static Color Pale() => new Color(0.91f, 0.87f, 0.79f);
    static Color Gold() => new Color(0.83f, 0.62f, 0.31f);

    void OnDestroy()
    {
        if (panelSettings != null) Destroy(panelSettings);
    }
}
