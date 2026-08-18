using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Minimal immersive HUD rendering the low-sanity vignette, the center reticle, and the interaction
/// prompt with its context action label. Corruption tier has no element here.
/// </summary>
public sealed class GmGameHud : MonoBehaviour
{
    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    VisualElement reticle;
    VisualElement promptPanel;
    Label promptText;
    Label contextActionText;
    VisualElement sanityVignette;

    public bool IsPromptVisible { get; private set; } = false;
    public string CurrentPrompt { get; private set; } = "";
    public bool LowSanityWarning { get; private set; } = false;

    void Start()
    {
        BuildUi();
    }

    void BuildUi()
    {
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmGameHudSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 700;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        if (panelSettings.themeStyleSheet == null)
            Debug.LogError("[GmGameHud] FAILED: Resources/GmHudTheme.tss is missing");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 700;

        root = document.rootVisualElement;
        root.name = "GmGameHudRoot";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        // Subtle Sanity Vignette border
        sanityVignette = new VisualElement { name = "SanityVignette" };
        sanityVignette.style.position = Position.Absolute;
        sanityVignette.style.left = 0; sanityVignette.style.right = 0; sanityVignette.style.top = 0; sanityVignette.style.bottom = 0;
        sanityVignette.style.borderLeftWidth = 4; sanityVignette.style.borderRightWidth = 4;
        sanityVignette.style.borderTopWidth = 4; sanityVignette.style.borderBottomWidth = 4;
        sanityVignette.style.borderLeftColor = new Color(0.85f, 0.15f, 0.15f, 0f);
        sanityVignette.style.borderRightColor = new Color(0.85f, 0.15f, 0.15f, 0f);
        sanityVignette.style.borderTopColor = new Color(0.85f, 0.15f, 0.15f, 0f);
        sanityVignette.style.borderBottomColor = new Color(0.85f, 0.15f, 0.15f, 0f);
        root.Add(sanityVignette);

        // Center reticle (Victorian antique brass compass pip)
        reticle = new VisualElement { name = "CenterReticle" };
        reticle.style.position = Position.Absolute;
        reticle.style.left = Length.Percent(50);
        reticle.style.top = Length.Percent(50);
        reticle.style.width = 6; reticle.style.height = 6;
        reticle.style.marginLeft = -3; reticle.style.marginTop = -3;
        reticle.style.backgroundColor = new Color(0.95f, 0.85f, 0.58f, 0.85f);
        reticle.style.borderLeftWidth = 1; reticle.style.borderRightWidth = 1;
        reticle.style.borderTopWidth = 1; reticle.style.borderBottomWidth = 1;
        reticle.style.borderLeftColor = new Color(0.45f, 0.32f, 0.15f, 0.9f);
        reticle.style.borderRightColor = new Color(0.45f, 0.32f, 0.15f, 0.9f);
        reticle.style.borderTopColor = new Color(0.45f, 0.32f, 0.15f, 0.9f);
        reticle.style.borderBottomColor = new Color(0.45f, 0.32f, 0.15f, 0.9f);
        root.Add(reticle);

        // Interaction Prompt Panel (bottom-center beveled velvet letterbox)
        promptPanel = new VisualElement { name = "PromptPanel" };
        promptPanel.style.position = Position.Absolute;
        promptPanel.style.left = Length.Percent(28);
        promptPanel.style.right = Length.Percent(28);
        promptPanel.style.bottom = 85;
        promptPanel.style.paddingLeft = 28; promptPanel.style.paddingRight = 28;
        promptPanel.style.paddingTop = 14; promptPanel.style.paddingBottom = 14;
        promptPanel.style.backgroundColor = new Color(0.025f, 0.018f, 0.014f, 0.95f);
        promptPanel.style.borderLeftWidth = 1; promptPanel.style.borderRightWidth = 1;
        promptPanel.style.borderTopWidth = 1; promptPanel.style.borderBottomWidth = 1;
        promptPanel.style.borderLeftColor = new Color(0.82f, 0.68f, 0.38f, 0.80f);
        promptPanel.style.borderRightColor = new Color(0.82f, 0.68f, 0.38f, 0.80f);
        promptPanel.style.borderTopColor = new Color(0.82f, 0.68f, 0.38f, 0.80f);
        promptPanel.style.borderBottomColor = new Color(0.82f, 0.68f, 0.38f, 0.80f);
        promptPanel.style.alignItems = Align.Center;
        promptPanel.style.display = DisplayStyle.None;

        promptText = new Label { name = "PromptLabel" };
        promptText.style.fontSize = 20;
        promptText.style.color = new Color(0.96f, 0.92f, 0.82f);
        promptText.style.unityTextAlign = TextAnchor.MiddleCenter;

        contextActionText = new Label { name = "ContextActionLabel" };
        contextActionText.style.fontSize = 14;
        contextActionText.style.color = new Color(0.85f, 0.72f, 0.44f);
        contextActionText.style.marginTop = 5;
        contextActionText.style.unityTextAlign = TextAnchor.MiddleCenter;

        promptPanel.Add(promptText);
        promptPanel.Add(contextActionText);
        root.Add(promptPanel);
    }

    void Update()
    {
        UpdateSanityFeedback();
    }

    public void ShowPrompt(string text, string actionKey = "[E] Inspect")
    {
        CurrentPrompt = text;
        IsPromptVisible = true;
        if (promptText != null) promptText.text = text;
        if (contextActionText != null) contextActionText.text = actionKey;
        if (promptPanel != null) promptPanel.style.display = DisplayStyle.Flex;
    }

    public void HidePrompt()
    {
        CurrentPrompt = "";
        IsPromptVisible = false;
        if (promptPanel != null) promptPanel.style.display = DisplayStyle.None;
    }

    void UpdateSanityFeedback()
    {
        float sanity = GmRunStore.Sanity;
        LowSanityWarning = sanity < 0.30f;

        if (sanityVignette != null)
        {
            if (LowSanityWarning)
            {
                // Pulsing red vignette
                float alpha = (Mathf.Sin(Time.time * 4f) * 0.5f + 0.5f) * 0.6f * (1f - sanity / 0.3f);
                var pulseColor = new Color(0.85f, 0.15f, 0.15f, alpha);
                sanityVignette.style.borderLeftColor = pulseColor;
                sanityVignette.style.borderRightColor = pulseColor;
                sanityVignette.style.borderTopColor = pulseColor;
                sanityVignette.style.borderBottomColor = pulseColor;
            }
            else
            {
                var clearColor = new Color(0.85f, 0.15f, 0.15f, 0f);
                sanityVignette.style.borderLeftColor = clearColor;
                sanityVignette.style.borderRightColor = clearColor;
                sanityVignette.style.borderTopColor = clearColor;
                sanityVignette.style.borderBottomColor = clearColor;
            }
        }
    }

    void OnDestroy()
    {
        if (panelSettings != null) Destroy(panelSettings);
    }
}
