using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Scaled, controller-readable presentation for the Entry Hall objective and Parlor table.</summary>
[DefaultExecutionOrder(120)]
public sealed class GmHouseHud : MonoBehaviour
{
    GmHouseBeginning house;
    GmPlayer player;
    PanelSettings settings;
    UIDocument document;
    VisualElement root;
    VisualElement objectivePanel;
    VisualElement dialoguePanel;
    VisualElement tablePanel;
    Label objective;
    Label clueStatus;
    Label dialogueSpeaker;
    Label dialogueBody;
    Label dialogueCount;
    Label tableScore;
    Label tableMessage;
    Label tableCards;
    Label reveal;
    VisualElement handRow;
    VisualElement actionRow;
    int revision = -1;

    void Start()
    {
        house = GetComponent<GmHouseBeginning>() ?? FindAnyObjectByType<GmHouseBeginning>();
        player = FindAnyObjectByType<GmPlayer>();
        BuildUi();
    }

    void BuildUi()
    {
        settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "GmHousePanelSettings";
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        settings.sortingOrder = 650;
        settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = settings;
        document.sortingOrder = 650;
        root = document.rootVisualElement;
        root.name = "GmHouseHud";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        objectivePanel = Panel("HouseObjective");
        objectivePanel.style.position = Position.Absolute;
        objectivePanel.style.left = 30; objectivePanel.style.top = 30;
        objectivePanel.style.width = 470;
        objectivePanel.style.borderLeftWidth = 3;
        objectivePanel.style.borderLeftColor = Gold(0.9f);
        objective = Text("Objective", 18, Gold(1f), TextAnchor.MiddleLeft, true);
        clueStatus = Text("ClueStatus", 14, Pale(0.72f), TextAnchor.MiddleLeft);
        clueStatus.style.marginTop = 7;
        objectivePanel.Add(objective); objectivePanel.Add(clueStatus);
        root.Add(objectivePanel);

        dialoguePanel = Panel("HostDialogue");
        dialoguePanel.style.position = Position.Absolute;
        dialoguePanel.style.left = Length.Percent(12); dialoguePanel.style.right = Length.Percent(12);
        dialoguePanel.style.bottom = 70;
        dialoguePanel.style.minHeight = 290;
        dialoguePanel.style.paddingLeft = 62; dialoguePanel.style.paddingRight = 62;
        dialoguePanel.style.paddingTop = 38; dialoguePanel.style.paddingBottom = 34;
        dialoguePanel.style.alignItems = Align.Center;
        dialogueSpeaker = Text("Speaker", 16, Gold(1f), TextAnchor.MiddleCenter, true);
        dialogueBody = Text("Dialogue", 30, Pale(1f), TextAnchor.MiddleCenter);
        dialogueBody.style.marginTop = 22;
        dialogueBody.style.maxWidth = 1250;
        dialogueCount = Text("DialoguePrompt", 15, Pale(0.64f), TextAnchor.MiddleCenter);
        dialogueCount.style.marginTop = 25;
        dialoguePanel.Add(dialogueSpeaker); dialoguePanel.Add(dialogueBody); dialoguePanel.Add(dialogueCount);
        root.Add(dialoguePanel);

        tablePanel = Panel("ParlorTable");
        tablePanel.style.position = Position.Absolute;
        tablePanel.style.left = Length.Percent(8); tablePanel.style.right = Length.Percent(8);
        tablePanel.style.top = 45; tablePanel.style.bottom = 35;
        tablePanel.style.backgroundColor = new Color(0.012f, 0.010f, 0.009f, 0.86f);
        tablePanel.style.paddingLeft = 44; tablePanel.style.paddingRight = 44;
        tablePanel.style.paddingTop = 32; tablePanel.style.paddingBottom = 30;
        tableScore = Text("Score", 17, Gold(1f), TextAnchor.MiddleCenter, true);
        tableMessage = Text("TableMessage", 25, Pale(1f), TextAnchor.MiddleCenter);
        tableMessage.style.marginTop = 22; tableMessage.style.minHeight = 72;
        tableCards = Text("PlayedCards", 30, new Color(0.95f, 0.79f, 0.44f), TextAnchor.MiddleCenter, true);
        tableCards.style.marginTop = 18; tableCards.style.minHeight = 48;
        reveal = Text("SuitAbility", 16, new Color(0.67f, 0.82f, 0.88f), TextAnchor.MiddleCenter);
        reveal.style.marginTop = 12; reveal.style.minHeight = 28;
        handRow = new VisualElement { name = "PlayerHand", pickingMode = PickingMode.Position };
        handRow.style.flexDirection = FlexDirection.Row;
        handRow.style.flexWrap = Wrap.Wrap;
        handRow.style.justifyContent = Justify.Center;
        handRow.style.marginTop = 25;
        handRow.style.minHeight = 115;
        actionRow = new VisualElement { name = "TableActions", pickingMode = PickingMode.Position };
        actionRow.style.flexDirection = FlexDirection.Row;
        actionRow.style.justifyContent = Justify.Center;
        actionRow.style.marginTop = 22;
        tablePanel.Add(tableScore); tablePanel.Add(tableMessage); tablePanel.Add(tableCards);
        tablePanel.Add(reveal); tablePanel.Add(handRow); tablePanel.Add(actionRow);
        root.Add(tablePanel);
    }

    void Update()
    {
        if (house == null) house = FindAnyObjectByType<GmHouseBeginning>();
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (house == null || revision == house.UiRevision) return;
        revision = house.UiRevision;
        Refresh();
    }

    void Refresh()
    {
        bool exploring = house.Phase == GmHousePhase.EntryHall || house.Phase == GmHousePhase.EnteringParlor ||
                         house.Phase == GmHousePhase.Complete;
        Show(objectivePanel, exploring);
        if (exploring)
        {
            objective.text = house.Objective;
            clueStatus.text = $"PORTRAITS {house.PortraitsRead}/9   •   CLUES {house.Progress.ClueCount}   •   " +
                              $"SHARDS {house.Progress.MirrorShards}   •   CAUGHT {house.Progress.CheatsCaught}";
        }

        bool dialogue = house.Phase == GmHousePhase.HostIntroduction;
        Show(dialoguePanel, dialogue);
        if (dialogue)
        {
            dialogueSpeaker.text = house.DialogueSpeaker;
            dialogueBody.text = house.DialogueLine;
            dialogueCount.text = $"{house.IntroIndex + 1:00}/{house.IntroCount:00}   •   " +
                                 $"{(player != null && player.UsingGamepad ? "A / CROSS" : "CLICK / SPACE")} TO CONTINUE";
        }

        bool table = house.Phase == GmHousePhase.ParlorGame;
        Show(tablePanel, table);
        if (!table) return;
        tableScore.text = $"ROUND {house.RoundNumber}   •   TRICKS  YOU {house.PlayerTricks} — {house.AldricTricks} ALDRIC   •   " +
                          $"ROUNDS  YOU {house.PlayerRounds} — {house.AldricRounds} ALDRIC   •   " +
                          (house.ReadUnlocked ? $"READ READY   •   CAUGHT {house.Progress.CheatsCaught}" :
                              $"DOUBT {house.Suspicion}/2");
        tableMessage.text = house.TableMessage;
        tableCards.text = $"YOU  {Name(house.PlayerCard)}     ◇     ALDRIC  {Name(house.AldricCard)}";
        reveal.text = house.RevealedCard;
        BuildHand();
        BuildActions();
    }

    void BuildHand()
    {
        handRow.Clear();
        bool choosing = house.TurnPhase == GmParlorTurnPhase.ChooseCard;
        for (int i = 0; i < house.PlayerHand.Count; i++)
        {
            int cardIndex = i;
            GmParlorCard card = house.PlayerHand[i];
            bool legal = choosing && GmParlorRules.IsLegal(house.PlayerHand, i, house.LeadCard);
            var button = new Button(() => house.SelectCard(cardIndex))
            {
                name = $"Card_{i + 1}_{card.Suit}_{card.Rank}",
                text = $"{i + 1}\n{card.Rank}\n{card.Suit.ToString().ToUpperInvariant()}",
            };
            button.style.width = 126; button.style.height = 108;
            button.style.marginLeft = 6; button.style.marginRight = 6;
            button.style.fontSize = 16;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextGenerator = TextGeneratorType.Standard;
            button.style.color = legal ? Pale(1f) : Pale(0.32f);
            button.style.backgroundColor = SuitColor(card.Suit, legal ? 0.88f : 0.28f);
            button.SetEnabled(legal);
            handRow.Add(button);
        }
    }

    void BuildActions()
    {
        actionRow.Clear();
        if (house.TurnPhase == GmParlorTurnPhase.JudgePlay)
        {
            AddAction("ALLOW TRICK", house.AllowTrick);
            if (house.CurrentPlayCanBeRead) AddAction("READ THE HAND [R]", house.ReadHand, true);
        }
        else if (house.TurnPhase == GmParlorTurnPhase.TrickResult)
            AddAction("NEXT TRICK", house.ContinueAfterResult);
        else if (house.TurnPhase == GmParlorTurnPhase.RoundResult)
            AddAction("DEAL NEXT ROUND", house.ContinueAfterResult);
        else if (house.TurnPhase == GmParlorTurnPhase.MatchResult)
            AddAction("LEAVE THE TABLE", house.ContinueAfterResult);
    }

    void AddAction(string text, System.Action action, bool danger = false)
    {
        var button = new Button(action) { text = text };
        button.style.width = 260; button.style.height = 58;
        button.style.marginLeft = 10; button.style.marginRight = 10;
        button.style.fontSize = 17;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.unityTextGenerator = TextGeneratorType.Standard;
        button.style.color = Pale(1f);
        button.style.backgroundColor = danger ? new Color(0.38f, 0.07f, 0.06f, 0.96f) : new Color(0.14f, 0.11f, 0.08f, 0.96f);
        actionRow.Add(button);
    }

    static string Name(GmParlorCard? card) => card.HasValue ? card.Value.ShortName.ToUpperInvariant() : "—";

    static VisualElement Panel(string name)
    {
        var panel = new VisualElement { name = name, pickingMode = PickingMode.Position };
        panel.style.paddingLeft = 24; panel.style.paddingRight = 24;
        panel.style.paddingTop = 17; panel.style.paddingBottom = 17;
        panel.style.backgroundColor = new Color(0.012f, 0.010f, 0.009f, 0.95f);
        var border = new Color(0.40f, 0.29f, 0.16f, 0.82f);
        panel.style.borderLeftWidth = 1; panel.style.borderRightWidth = 1;
        panel.style.borderTopWidth = 1; panel.style.borderBottomWidth = 1;
        panel.style.borderLeftColor = border; panel.style.borderRightColor = border;
        panel.style.borderTopColor = border; panel.style.borderBottomColor = border;
        return panel;
    }

    static Label Text(string name, int size, Color color, TextAnchor alignment, bool bold = false)
    {
        var label = new Label { name = name, pickingMode = PickingMode.Ignore };
        label.style.fontSize = size;
        label.style.color = color;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.unityTextAlign = alignment;
        label.style.unityTextGenerator = TextGeneratorType.Standard;
        if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    static Color Pale(float alpha) => new Color(0.91f, 0.87f, 0.79f, alpha);
    static Color Gold(float alpha) => new Color(0.76f, 0.56f, 0.29f, alpha);
    static Color SuitColor(GmCardSuit suit, float alpha)
    {
        return suit switch
        {
            GmCardSuit.Flames => new Color(0.48f, 0.09f, 0.045f, alpha),
            GmCardSuit.Eyes => new Color(0.08f, 0.24f, 0.31f, alpha),
            GmCardSuit.Teeth => new Color(0.30f, 0.27f, 0.20f, alpha),
            _ => new Color(0.18f, 0.18f, 0.20f, alpha),
        };
    }

    static void Show(VisualElement element, bool visible) =>
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

    void OnDestroy()
    {
        if (settings != null) Destroy(settings);
    }
}
