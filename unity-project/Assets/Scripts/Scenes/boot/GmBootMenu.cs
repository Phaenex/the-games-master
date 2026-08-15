// The title screen, and the reason it exists is not the title.
//
// GmSceneDirector is a DontDestroyOnLoad singleton that owns every scene transition and the only
// call to GmEndingManager.ResolveEnding(). Nothing in this project ever instantiated it, because
// there was no scene that ran before the Prologue to do so -- and endings were scheduled last, so
// the omission was never going to surface until the end. The result: six authored endings, twelve
// passing ending tests, and not one ending that has ever resolved in a real playthrough.
//
// This scene instantiates the director exactly once, before anything else, and then never matters
// again. Everything else here -- the title, the three rows, the save probe -- is ordinary menu work.
//
// Deliberately NOT auto-resuming. Continue is an explicit choice the player makes, because a horror
// game that drops you back into a run without asking has taken away the one decision the opening is
// built on (turn back at the gate, and the house never has you). Nick owns that call; explicit is
// the safer default to build first and the cheaper of the two to reverse.
using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GmBootMenu : MonoBehaviour
{
    public const string PrologueSceneId = "wend-hill-prologue";

    static readonly Color Ink = new Color(0.80f, 0.78f, 0.72f);
    static readonly Color Gild = new Color(0.88f, 0.76f, 0.45f);
    static readonly Color Dim = new Color(0.42f, 0.40f, 0.37f);

    const int TitleFontSize = 46;
    const int RowFontSize = 20;
    const int NoteFontSize = 14;

    public enum Row { Continue, NewRun, Quit }

    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    Label continueLabel, newRunLabel, quitLabel, note;
    VisualElement continueMarker, newRunMarker, quitMarker;

    public Row Focused { get; private set; }
    public bool ContinueAvailable { get; private set; }
    public bool QuitRequested { get; private set; }

    /// Raised instead of loading a scene directly, so EditMode tests can drive the menu without a
    /// scene load and a future console port can route it somewhere else.
    public event Action<string> OnStartRun;

    void Start()
    {
        EnsureSceneDirector();
        BuildUi();
        Refresh();
    }

    /// The whole point of this scene. Idempotent -- the director's own Awake destroys a duplicate,
    /// but creating one per Start would still churn a DontDestroyOnLoad object on every menu return.
    public static GmSceneDirector EnsureSceneDirector()
    {
        if (GmSceneDirector.Instance != null) return GmSceneDirector.Instance;

        // Look for a real one before trusting the static. GmSceneDirector sets _instance in Awake,
        // and Awake does not run on AddComponent in edit mode or immediately after a domain reload,
        // so the static can read null while a perfectly good director exists in the scene. Creating
        // a second one there would stack DontDestroyOnLoad objects that outlive every scene load.
        var existing = FindAnyObjectByType<GmSceneDirector>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        var host = new GameObject("GmSceneDirector");
        var director = host.AddComponent<GmSceneDirector>();
        // The curtain rides on the same object so it inherits DontDestroyOnLoad. It is the only
        // thing that can hold the black across a load, which is what makes the ninth-bell crossing
        // survive being a scene change rather than a teleport.
        host.AddComponent<GmSceneCurtain>();
        Debug.Log("[GmBoot] scene director instantiated — transitions and endings are now reachable");
        return director;
    }

    void BuildUi()
    {
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GmBootPanelSettings";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        panelSettings.sortingOrder = 950;
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("GmHudTheme");
        if (panelSettings.themeStyleSheet == null)
            Debug.LogError("[GmBoot] FAILED: Resources/GmHudTheme.tss is missing");

        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.sortingOrder = 950;

        root = document.rootVisualElement;
        root.name = "GmBootRoot";
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        root.style.backgroundColor = new Color(0.012f, 0.010f, 0.009f);
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        var title = new Label("THE GAMES MASTER") { name = "BootTitle" };
        title.style.fontSize = TitleFontSize;
        title.style.color = Gild;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.letterSpacing = 8;
        root.Add(title);

        var rule = new VisualElement { name = "BootRule" };
        rule.style.height = 1;
        rule.style.width = 380;
        rule.style.marginTop = 14;
        rule.style.marginBottom = 34;
        rule.style.backgroundColor = new Color(0.45f, 0.36f, 0.22f, 0.55f);
        root.Add(rule);

        (continueMarker, continueLabel) = AddRow("Continue", "BootContinue");
        (newRunMarker, newRunLabel) = AddRow("New Run", "BootNewRun");
        (quitMarker, quitLabel) = AddRow("Quit", "BootQuit");

        note = new Label { name = "BootNote" };
        note.style.fontSize = NoteFontSize;
        note.style.color = Dim;
        note.style.marginTop = 26;
        note.style.unityTextAlign = TextAnchor.MiddleCenter;
        root.Add(note);

        // Sweep the tree built so far. AddRow sweeps its own row because rows are created after this
        // returns; this covers the title, the rule and the note.
        GmUiText.UseStandardGenerator(root);
    }

    (VisualElement, Label) AddRow(string text, string name)
    {
        var row = new VisualElement { name = name };
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 12;

        // Focus is carried on two channels -- a marker AND ink strength -- never colour alone, the
        // same rule GmPrologueHud's pause rows already follow so it stays readable for a player who
        // cannot separate the amber from the parchment.
        var marker = new VisualElement { name = name + "Marker" };
        marker.style.width = 9; marker.style.height = 9;
        marker.style.marginRight = 14;
        marker.style.backgroundColor = Gild;
        marker.style.display = DisplayStyle.None;
        row.Add(marker);

        var label = new Label(text) { name = name + "Label" };
        label.style.fontSize = RowFontSize;
        label.style.color = Ink;
        row.Add(label);

        root.Add(row);
        // Runtime-created PanelSettings carry no ICU payload, so the advanced text generator throws
        // once per element per frame and draws nothing. Measured on the real macOS build before this
        // line existed: 9,906 NullReferenceExceptions in a twenty-second run of this screen.
        GmUiText.UseStandardGenerator(row);
        return (marker, label);
    }

    /// Re-probes the save and repaints. Public so a test can assert the menu reflects disk state
    /// rather than whatever it happened to be built with.
    public void Refresh()
    {
        ContinueAvailable = GmSaveSystem.HasSave();
        // Landing on a row that does nothing is the worst first impression a menu can make, so focus
        // starts on New Run whenever there is nothing to continue.
        Focused = ContinueAvailable ? Row.Continue : Row.NewRun;
        Repaint();
    }

    public void MoveFocus(int delta)
    {
        var order = ContinueAvailable
            ? new[] { Row.Continue, Row.NewRun, Row.Quit }
            : new[] { Row.NewRun, Row.Quit };
        int index = Array.IndexOf(order, Focused);
        if (index < 0) index = 0;
        index = (index + delta % order.Length + order.Length) % order.Length;
        Focused = order[index];
        Repaint();
    }

    void Repaint()
    {
        if (root == null) return;
        SetRow(continueMarker, continueLabel, Focused == Row.Continue, ContinueAvailable);
        SetRow(newRunMarker, newRunLabel, Focused == Row.NewRun, true);
        SetRow(quitMarker, quitLabel, Focused == Row.Quit, true);
        note.text = ContinueAvailable
            ? "A run is already under way."
            : "No run recorded. The house has not met you yet.";
    }

    static void SetRow(VisualElement marker, Label label, bool focused, bool enabled)
    {
        if (marker != null) marker.style.display = focused && enabled ? DisplayStyle.Flex : DisplayStyle.None;
        if (label == null) return;
        label.style.color = !enabled ? Dim : focused ? Gild : Ink;
    }

    /// Activates the focused row. Returns false when the row cannot act -- Continue with no save --
    /// so the caller can play a refusal rather than silently doing nothing.
    public bool Activate()
    {
        switch (Focused)
        {
            case Row.Continue: return Continue();
            case Row.NewRun: NewRun(); return true;
            case Row.Quit: Quit(); return true;
            default: return false;
        }
    }

    /// Starts a fresh run. Clears the store FIRST: a New Run that inherits the previous run's
    /// catches, shards and corruption would silently hand the player someone else's true ending.
    public void NewRun()
    {
        EnsureSceneDirector();
        GmRunStore.BeginNewRun();
        GmExperienceTelemetry.Record("boot", "new-run");
        Debug.Log("[GmBoot] new run — store cleared, entering the Prologue");
        StartRun(PrologueSceneId, GmSceneDirector.PrologueScenePath);
    }

    /// Resumes. Returns false when there is nothing to resume, which is a real outcome rather than
    /// an error -- the row is drawn dim and the note says so.
    public bool Continue()
    {
        if (!GmSaveSystem.HasSave())
        {
            Debug.Log("[GmBoot] continue refused — no save on disk");
            return false;
        }
        EnsureSceneDirector();
        if (!GmSaveSystem.Load())
        {
            Debug.LogError("[GmBoot] FAILED: save exists but did not load — staying on the menu");
            return false;
        }
        string sceneId = GmRunStore.CurrentSceneId;
        string scenePath = ScenePathFor(sceneId);
        if (scenePath == null)
        {
            // Refusing beats guessing. Silently dropping the player at the Prologue would read as
            // the game forgetting their run, and they would have no way to tell that from a bug.
            Debug.LogError($"[GmBoot] FAILED: save names scene '{sceneId}', which has no known path — refusing to resume");
            return false;
        }
        GmExperienceTelemetry.Record("boot", $"continue:{sceneId}");
        Debug.Log($"[GmBoot] continuing '{sceneId}' at checkpoint '{GmRunStore.LastCheckpoint}'");
        StartRun(sceneId, scenePath);
        return true;
    }

    public void Quit()
    {
        QuitRequested = true;
        GmExperienceTelemetry.Record("boot", "quit");
        Debug.Log("[GmBoot] quit requested from the title");
        if (!Application.isEditor) Application.Quit(0);
    }

    void StartRun(string sceneId, string scenePath)
    {
        OnStartRun?.Invoke(sceneId);
        // Explicit Unity-null check rather than `?.`: the null-conditional operator uses C# null,
        // which a destroyed UnityEngine.Object is NOT, so `?.` would call into a destroyed director.
        GmSceneDirector director = GmSceneDirector.Instance;
        if (director != null) director.TransitionTo(sceneId, scenePath);
    }

    /// Maps a saved scene id back to its path. The director owns these constants; resuming into a
    /// scene it cannot name is a corrupt save, and dropping the player at the Prologue silently
    /// would look like the game forgetting their run rather than refusing a bad one.
    public static string ScenePathFor(string sceneId) => sceneId switch
    {
        PrologueSceneId => GmSceneDirector.PrologueScenePath,
        "entry-hall" => GmSceneDirector.EntryHallScenePath,
        "parlor" => GmSceneDirector.ParlorScenePath,
        "shut-the-box" => GmSceneDirector.ShutTheBoxScenePath,
        "court" => GmSceneDirector.CourtScenePath,
        "hidden-room" => GmSceneDirector.HiddenRoomScenePath,
        "labyrinth" => GmSceneDirector.LabyrinthScenePath,
        _ => null,
    };

    void OnDestroy()
    {
        if (panelSettings != null) Destroy(panelSettings);
    }
}
