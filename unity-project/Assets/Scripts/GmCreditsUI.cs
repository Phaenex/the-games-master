using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmCreditsUI : MonoBehaviour
{
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;

    public bool IsVisible { get; private set; } = false;

    void Start()
    {
        BuildUi();
        SetVisible(false);
    }

    void BuildUi()
    {
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmCreditsPanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 950;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        if (panelSettings.themeStyleSheet == null)
            Debug.LogError("[GmCreditsUI] FAILED: Resources/GmHudTheme.tss is missing");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 950;

        root = document.rootVisualElement;
        root.name = "GmCreditsRoot";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.style.backgroundColor = new Color(0.015f, 0.012f, 0.010f, 0.96f);
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.display = DisplayStyle.None;

        var container = new VisualElement { name = "CreditsContainer" };
        container.style.width = 900;
        container.style.maxHeight = 650;
        container.style.paddingLeft = 40; container.style.paddingRight = 40;
        container.style.paddingTop = 30; container.style.paddingBottom = 30;

        var title = new Label("THE GAMES MASTER — CREDITS & ATTRIBUTIONS") { name = "CreditsTitle" };
        title.style.fontSize = 22;
        title.style.color = new Color(0.88f, 0.76f, 0.45f);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        container.Add(title);

        // GmCreditsCatalog is the attribution record the licence gate checks against, so the screen
        // renders it verbatim instead of keeping a second, drifting copy of the credit lines.
        var pages = GmCreditsCatalog.Pages;
        if (pages == null || pages.Count == 0)
        {
            Debug.LogError("[GmCreditsUI] FAILED: GmCreditsCatalog is empty; CC-BY attribution would ship missing");
        }

        var body = new ScrollView(ScrollViewMode.Vertical) { name = "CreditsBody" };
        body.style.flexGrow = 1;
        body.style.marginTop = 25;
        container.Add(body);

        for (int i = 0; pages != null && i < pages.Count; i++)
        {
            var page = new Label(pages[i]) { name = $"CreditsPage{i + 1}" };
            page.style.fontSize = 16;
            page.style.color = new Color(0.80f, 0.78f, 0.72f);
            page.style.marginBottom = 22;
            page.style.whiteSpace = WhiteSpace.Normal;
            page.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(page);
        }

        root.Add(container);
        // Same Unity 6 trap as the boot menu and both HUDs: a runtime PanelSettings has no ICU data,
        // so the advanced text generator throws every frame and the credits render blank.
        GmUiText.UseStandardGenerator(root);
    }

    void OnDestroy()
    {
        if (panelSettings != null) Destroy(panelSettings);
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        if (root != null)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
