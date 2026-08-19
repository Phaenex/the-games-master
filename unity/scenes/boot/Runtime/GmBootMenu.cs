// The title screen, and the reason it exists is not the title.
//
// GmSceneDirector is a DontDestroyOnLoad singleton that owns every scene transition and the only
// call to GmEndingManager.ResolveEnding(). Nothing in this project ever instantiated it, because
// there was no scene that ran before the Prologue to do so -- and endings were scheduled last, so
// the omission was never going to surface until the end. The result: six authored endings, twelve
// passing ending tests, and not one ending that has ever resolved in a real playthrough.
//
// This scene instantiates the director exactly once, before anything else, and then never matters
// again. Everything else here -- the title, menu rows, recovery controls and save probe -- is
// ordinary menu work.
//
// Deliberately NOT auto-resuming. Continue is an explicit choice the player makes, because a horror
// game that drops you back into a run without asking has taken away the one decision the opening is
// built on (turn back at the gate, and the house never has you). Nick owns that call; explicit is
// the safer default to build first and the cheaper of the two to reverse.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

public interface IGmSupportDiagnosticsDestinationPicker
{
    bool TryChooseDestination(out string path,out string error);
}

public sealed class GmMacSupportDiagnosticsDestinationPicker :
    IGmSupportDiagnosticsDestinationPicker
{
    public bool TryChooseDestination(out string path,out string error)
    {
        path=null;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        try
        {
            const string script = "POSIX path of (choose file name with prompt \"Export support diagnostics\" default name \"the-games-master-support.json\")";
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = "-e \"" + script.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("macOS save panel did not start");
                string output = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return TryInterpretAppleScriptResult(process.ExitCode, output,
                    standardError, out path, out error);
            }
        }
        catch(Exception ex) { error=ex.Message; return false; }
#else
        error="native diagnostics destination selection is unavailable on this platform";
        return false;
#endif
    }

    public static bool TryInterpretAppleScriptResult(int exitCode,string output,
        string standardError,out string path,out string error)
    {
        path=null;
        string details=(standardError??string.Empty).Trim();
        if(exitCode!=0)
        {
            if(details.IndexOf("(-128)",StringComparison.Ordinal)>=0||
                details.IndexOf("User canceled",StringComparison.OrdinalIgnoreCase)>=0)
                error="diagnostics export cancelled";
            else error=details.Length>0?details:
                $"macOS save panel failed with exit code {exitCode}";
            return false;
        }
        path=(output??string.Empty).TrimEnd('\r','\n');
        if(string.IsNullOrWhiteSpace(path))
        { path=null;error="macOS save panel returned no destination";return false; }
        error=string.Empty;return true;
    }
}

public sealed class GmBootMenu : MonoBehaviour
{
    public const string PrologueSceneId = "wend-hill-prologue";

    static readonly Color Ink = new Color(0.80f, 0.78f, 0.72f);
    static readonly Color Gild = new Color(0.88f, 0.76f, 0.45f);
    static readonly Color Dim = new Color(0.42f, 0.40f, 0.37f);

    const int TitleFontSize = 46;
    const int RowFontSize = 20;
    const int NoteFontSize = 18;

    public enum Row { Continue, NewRun, RestoreLastValid, ResetHouseMemory, ExportDiagnostics, Mirror, LedgerReview, Recollection, Settings, Quit }

    PanelSettings panelSettings;
    UIDocument document;
    VisualElement root;
    Label continueLabel, newRunLabel, restoreLastValidLabel, resetHouseLabel,
        exportDiagnosticsLabel, mirrorLabel, ledgerReviewLabel,
        recollectionLabel, settingsLabel, quitLabel, note;
    Label titleLabel;
    VisualElement continueMarker, newRunMarker, restoreLastValidMarker, resetHouseMarker,
        exportDiagnosticsMarker, mirrorMarker, ledgerReviewMarker,
        recollectionMarker, settingsMarker, quitMarker;
    VisualElement settingsPanel;
    ScrollView ledgerReviewPanel, diagnosticsPanel;
    Label diagnosticsStatusLabel;
    GmAccessibilitySettingsView settingsView;
    bool settingsOpen, ledgerReviewOpen;
    IGmSupportDiagnosticsDestinationPicker diagnosticsDestinationPicker =
        new GmMacSupportDiagnosticsDestinationPicker();
    InputActionAsset menuControls;
    InputActionMap menuInput;
    InputAction navigateAction, submitAction, cancelAction, quitAction;
    bool inputActive;
    bool navigationHeld;
    bool resetArmed, restoreArmed;

    public Row Focused { get; private set; }
    public bool ContinueAvailable { get; private set; }
    public bool QuitRequested { get; private set; }
    public bool MirrorAvailable { get; private set; }
    public bool LedgerReviewAvailable { get; private set; }
    public int LedgerReviewCompletedRuns { get; private set; }
    public IReadOnlyList<string> LedgerReviewLines { get; private set; } = Array.Empty<string>();
    public bool HouseRecoveryRequired { get; private set; }
    public bool RestoreLastValidAvailable { get; private set; }
    public long RestoreLastValidGeneration { get; private set; }
    public bool ExportDiagnosticsAvailable { get; private set; }
    public bool DiagnosticsPreviewOpen { get; private set; }
    public string DiagnosticsStatus { get; private set; } = string.Empty;
    public IReadOnlyList<GmHouseKnownPackage> RecollectionPackages { get; private set; } =
        Array.Empty<GmHouseKnownPackage>();
    public int RecollectionSelection { get; private set; }

    /// Raised instead of loading a scene directly, so EditMode tests can drive the menu without a
    /// scene load and a future console port can route it somewhere else.
    public event Action<string> OnStartRun;

    void Awake()
    {
        InputActionAsset sharedControls = Resources.Load<InputActionAsset>("Input/GmControls");
        if (sharedControls == null)
        {
            Debug.LogError("[GmBoot] Resources/Input/GmControls.inputactions is missing");
            return;
        }

        // Boot owns a clone. Enabling or destroying this menu must never touch the shared asset
        // GmPlayer enables after the scene transition.
        menuControls = Instantiate(sharedControls);
        menuControls.name = "GmBootMenuControls";
        menuInput = menuControls.FindActionMap("Menu");
        navigateAction = menuInput?.FindAction("Navigate");
        submitAction = menuInput?.FindAction("Submit");
        cancelAction = menuInput?.FindAction("Cancel");
        quitAction = menuInput?.FindAction("Quit");
        if (menuInput == null || navigateAction == null || submitAction == null ||
            cancelAction == null || quitAction == null)
        {
            Debug.LogError("[GmBoot] GmControls/Menu is missing Navigate, Submit, Cancel, or Quit");
            ReleaseInput();
            return;
        }

    }

    void OnEnable()
    {
        EnableInput();
    }

    void OnDisable()
    {
        FlushSettingsBoundary();
        DisableInput();
    }

    void EnableInput()
    {
        if (inputActive || menuInput == null) return;

        submitAction.performed += OnSubmit;
        cancelAction.performed += OnCancel;
        quitAction.performed += OnQuit;
        menuInput.Enable();
        inputActive = true;
        navigationHeld = false;
    }

    void DisableInput()
    {
        if (inputActive)
        {
            submitAction.performed -= OnSubmit;
            cancelAction.performed -= OnCancel;
            quitAction.performed -= OnQuit;
            inputActive = false;
        }
        if (menuInput != null && menuInput.enabled) menuInput.Disable();
        navigationHeld = false;
    }

    void Start()
    {
        EnsureSceneDirector();
        GmSaveSystem.TryLoadAccessibilityPreferences();
        BuildUi();
        Refresh();
    }

    void Update()
    {
        if (!inputActive || navigateAction == null) return;

        Vector2 navigation = ReadNavigation();
        if (Mathf.Abs(navigation.x) < 0.5f && Mathf.Abs(navigation.y) < 0.5f)
        {
            navigationHeld = false;
            return;
        }
        if (navigationHeld) return;

        navigationHeld = true;
        if (ledgerReviewOpen || DiagnosticsPreviewOpen) return;
        if (settingsOpen)
        {
            if (Mathf.Abs(navigation.y) >= Mathf.Abs(navigation.x))
                settingsView?.MoveFocus(navigation.y > 0f ? -1 : 1);
            else settingsView?.AdjustFocused(navigation.x > 0f ? 1 : -1);
        }
        else if (Mathf.Abs(navigation.y) >= Mathf.Abs(navigation.x))
            MoveFocus(navigation.y > 0f ? -1 : 1);
        else if (Focused == Row.Recollection && RecollectionPackages.Count > 1)
        {
            RecollectionSelection = (RecollectionSelection +
                (navigation.x > 0f ? 1 : -1) + RecollectionPackages.Count) %
                RecollectionPackages.Count;
            Repaint();
        }
    }

    Vector2 ReadNavigation()
    {
        return navigateAction.ReadValue<Vector2>();
    }

    float ReadVerticalNavigation()
    {
        float strongestUp = 0f;
        float strongestDown = 0f;
        foreach (InputControl control in navigateAction.controls)
        {
            float vertical = 0f;
            if (control is Vector2Control vector)
            {
                vertical = vector.ReadValue().y;
            }
            else if (control is KeyControl key && key.isPressed)
            {
                if (key.name == "w" || key.name == "upArrow") vertical = 1f;
                else if (key.name == "s" || key.name == "downArrow") vertical = -1f;
            }

            if (vertical > strongestUp) strongestUp = vertical;
            else if (-vertical > strongestDown) strongestDown = -vertical;
        }

        if (Mathf.Approximately(strongestUp, strongestDown)) return 0f;
        return strongestUp > strongestDown ? strongestUp : -strongestDown;
    }

    void OnSubmit(InputAction.CallbackContext _)
    {
        if (DiagnosticsPreviewOpen) ConfirmDiagnosticsExport();
        else if (ledgerReviewOpen) CloseLedgerReview();
        else if (settingsOpen) settingsView?.ActivateFocused();
        else Activate();
    }

    void OnCancel(InputAction.CallbackContext _)
    {
        if (settingsOpen)
        {
            CloseSettings();
            return;
        }
        if (ledgerReviewOpen)
        {
            CloseLedgerReview();
            return;
        }
        if (DiagnosticsPreviewOpen)
        {
            CloseDiagnosticsPreview();
            return;
        }
        // New Run is always actionable. Cancel on it is deliberately a no-op; on either other row
        // it gives the player a safe place to land without starting or quitting anything.
        if (Focused == Row.NewRun) return;
        Focused = Row.NewRun;
        Repaint();
    }

    void OnQuit(InputAction.CallbackContext _)
    {
        Quit();
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

        titleLabel = new Label("THE GAMES MASTER") { name = "BootTitle" };
        titleLabel.style.fontSize = TitleFontSize;
        titleLabel.style.color = Gild;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.letterSpacing = 8;
        root.Add(titleLabel);

        var rule = new VisualElement { name = "BootRule" };
        rule.style.height = 1;
        rule.style.width = 380;
        rule.style.marginTop = 14;
        rule.style.marginBottom = 34;
        rule.style.backgroundColor = new Color(0.45f, 0.36f, 0.22f, 0.55f);
        root.Add(rule);

        (continueMarker, continueLabel) = AddRow("Continue", "BootContinue");
        (newRunMarker, newRunLabel) = AddRow("New Run", "BootNewRun");
        (restoreLastValidMarker, restoreLastValidLabel) = AddRow(
            "Recover House Memory", "BootRestoreLastValid");
        (resetHouseMarker, resetHouseLabel) = AddRow("Reset House Memory", "BootResetHouse");
        (exportDiagnosticsMarker, exportDiagnosticsLabel) = AddRow(
            "Export Support Diagnostics", "BootExportDiagnostics");
        (mirrorMarker, mirrorLabel) = AddRow("The Mirror", "BootMirror");
        (ledgerReviewMarker, ledgerReviewLabel) = AddRow("Ledger Review", "BootLedgerReview");
        (recollectionMarker, recollectionLabel) = AddRow("Recollection", "BootRecollection");
        (settingsMarker, settingsLabel) = AddRow("Settings", "BootSettings");
        (quitMarker, quitLabel) = AddRow("Quit", "BootQuit");

        settingsView = new GmAccessibilitySettingsView(ApplyAccessibility);
        settingsPanel = settingsView.Build("BootSettingsPanel");
        settingsPanel.style.minWidth = 520;
        settingsPanel.style.marginTop = 18;
        settingsPanel.style.display = DisplayStyle.None;
        root.Add(settingsPanel);

        ledgerReviewPanel = new ScrollView(ScrollViewMode.Vertical)
            { name = "BootLedgerReviewPanel" };
        ledgerReviewPanel.style.width = Length.Percent(80);
        ledgerReviewPanel.style.maxWidth = 760;
        ledgerReviewPanel.style.maxHeight = Length.Percent(55);
        ledgerReviewPanel.style.marginTop = 18;
        ledgerReviewPanel.style.paddingLeft = 24; ledgerReviewPanel.style.paddingRight = 24;
        ledgerReviewPanel.style.paddingTop = 18; ledgerReviewPanel.style.paddingBottom = 18;
        ledgerReviewPanel.style.backgroundColor = new Color(0.025f, 0.018f, 0.014f, 0.97f);
        ledgerReviewPanel.style.display = DisplayStyle.None;
        root.Add(ledgerReviewPanel);

        diagnosticsPanel = new ScrollView(ScrollViewMode.Vertical)
            { name = "BootDiagnosticsPreview" };
        diagnosticsPanel.style.width = Length.Percent(80);
        diagnosticsPanel.style.maxWidth = 760;
        diagnosticsPanel.style.maxHeight = Length.Percent(55);
        diagnosticsPanel.style.marginTop = 18;
        diagnosticsPanel.style.paddingLeft = 24; diagnosticsPanel.style.paddingRight = 24;
        diagnosticsPanel.style.paddingTop = 18; diagnosticsPanel.style.paddingBottom = 18;
        diagnosticsPanel.style.backgroundColor = new Color(0.025f, 0.018f, 0.014f, 0.97f);
        diagnosticsPanel.style.display = DisplayStyle.None;
        root.Add(diagnosticsPanel);

        note = new Label { name = "BootNote" };
        note.style.fontSize = NoteFontSize;
        note.style.color = Dim;
        note.style.marginTop = 26;
        note.style.unityTextAlign = TextAnchor.MiddleCenter;
        root.Add(note);

        // Sweep the tree built so far. AddRow sweeps its own row because rows are created after this
        // returns; this covers the title, the rule and the note.
        GmUiText.UseStandardGenerator(root);
        GmAccessibilitySettings.OnChanged -= ApplyAccessibility;
        GmAccessibilitySettings.OnChanged += ApplyAccessibility;
        ApplyAccessibility();
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
        CloseLedgerReview();
        CloseDiagnosticsPreview();
        resetArmed = false;
        restoreArmed = false;
        HouseRecoveryRequired = false;
        RestoreLastValidAvailable = false;
        RestoreLastValidGeneration = 0;
        ExportDiagnosticsAvailable = false;
        DiagnosticsPreviewOpen = false;
        LedgerReviewAvailable = false;
        LedgerReviewCompletedRuns = 0;
        LedgerReviewLines = Array.Empty<string>();
        ContinueAvailable = GmSaveSystem.HasSave();
        if (!GmHousePersistenceCoordinator.TryGetTitleUnlocks(out bool mirror,
            out IReadOnlyList<GmHouseKnownPackage> recollections, out string houseError))
        {
            MirrorAvailable = false;
            RecollectionPackages = Array.Empty<GmHouseKnownPackage>();
            HouseRecoveryRequired = GmHousePersistenceCoordinator.HouseRecoveryRequired;
            if (HouseRecoveryRequired)
            {
                ExportDiagnosticsAvailable = true;
                Debug.Log($"[GmBoot] House memory unreadable; recovery options on the title: {houseError}");
                if (GmHousePersistenceCoordinator.TryGetLastValidProfileCandidate(
                    out long generation,out _))
                {
                    RestoreLastValidAvailable = true;
                    RestoreLastValidGeneration = generation;
                }
            }
            else
                Debug.LogError($"[GmBoot] House title state unavailable: {houseError}");
        }
        else
        {
            MirrorAvailable = mirror;
            RecollectionPackages = recollections ?? Array.Empty<GmHouseKnownPackage>();
            RecollectionSelection = Mathf.Clamp(RecollectionSelection, 0,
                Mathf.Max(0, RecollectionPackages.Count - 1));
            if (GmHousePersistenceCoordinator.TryGetLedgerReview(
                out GmHouseLedgerReview ledger,out _))
            {
                LedgerReviewAvailable = true;
                LedgerReviewCompletedRuns = ledger.CompletedRunsAnalyzed;
                LedgerReviewLines = ledger.LearnedTendencies;
            }
        }
        if (HouseRecoveryRequired) ContinueAvailable = false;
        // Landing on a row that does nothing is the worst first impression a menu can make, so focus
        // starts on New Run whenever there is nothing to continue.
        Focused = RestoreLastValidAvailable ? Row.RestoreLastValid :
            ContinueAvailable ? Row.Continue : Row.NewRun;
        Repaint();
    }

    public void MoveFocus(int delta)
    {
        resetArmed = false;
        restoreArmed = false;
        var order = new List<Row>();
        if (ContinueAvailable) order.Add(Row.Continue);
        order.Add(Row.NewRun);
        if (RestoreLastValidAvailable) order.Add(Row.RestoreLastValid);
        if (HouseRecoveryRequired) order.Add(Row.ResetHouseMemory);
        if (ExportDiagnosticsAvailable) order.Add(Row.ExportDiagnostics);
        if (MirrorAvailable) order.Add(Row.Mirror);
        if (LedgerReviewAvailable) order.Add(Row.LedgerReview);
        if (RecollectionPackages.Count > 0) order.Add(Row.Recollection);
        order.Add(Row.Settings);order.Add(Row.Quit);
        int index = order.IndexOf(Focused);
        if (index < 0) index = 0;
        index = (index + delta % order.Count + order.Count) % order.Count;
        Focused = order[index];
        Repaint();
    }

    void Repaint()
    {
        if (root == null) return;
        SetRow(continueMarker, continueLabel, Focused == Row.Continue, ContinueAvailable);
        SetRow(newRunMarker, newRunLabel, Focused == Row.NewRun, true);
        SetRow(restoreLastValidMarker, restoreLastValidLabel,
            Focused == Row.RestoreLastValid, RestoreLastValidAvailable, true);
        SetRow(resetHouseMarker, resetHouseLabel, Focused == Row.ResetHouseMemory,
            HouseRecoveryRequired, true);
        SetRow(exportDiagnosticsMarker, exportDiagnosticsLabel,
            Focused == Row.ExportDiagnostics, ExportDiagnosticsAvailable, true);
        SetRow(mirrorMarker, mirrorLabel, Focused == Row.Mirror, MirrorAvailable);
        SetRow(ledgerReviewMarker, ledgerReviewLabel, Focused == Row.LedgerReview,
            LedgerReviewAvailable);
        SetRow(recollectionMarker, recollectionLabel, Focused == Row.Recollection,
            RecollectionPackages.Count > 0);
        SetRow(settingsMarker, settingsLabel, Focused == Row.Settings, true);
        SetRow(quitMarker, quitLabel, Focused == Row.Quit, true);
        note.text = NoteForFocus();
    }

    string NoteForFocus()
    {
        if (HouseRecoveryRequired && Focused == Row.ResetHouseMemory)
            return resetArmed
                ? "Confirm again. This starts a new lineage and forgets the unreadable files."
                : "This forgets the house and starts a new lineage.";
        if (HouseRecoveryRequired && Focused == Row.RestoreLastValid)
            return restoreArmed
                ? "Confirm again. Later House memories and the current Continue will be set aside."
                : "Return the house to its last readable memory. Later memories and Continue will be removed.";
        if (HouseRecoveryRequired && Focused == Row.ExportDiagnostics)
            return "Preview redacted support data, then choose where to save it.";
        if (HouseRecoveryRequired && Focused == Row.NewRun)
            return "This sitting will not be remembered.";
        if (HouseRecoveryRequired && Focused == Row.Quit)
            return "Keeps the unreadable files and leaves.";
        if (Focused == Row.Mirror && MirrorAvailable)
            return "The house remembers.";
        if (Focused == Row.LedgerReview && LedgerReviewAvailable)
            return "Review what the house has learned. It does not reveal future strategies.";
        if (Focused == Row.Recollection && RecollectionPackages.Count > 0)
            return $"Known hand {RecollectionSelection + 1} of {RecollectionPackages.Count}. Left or right changes it.";
        if (ContinueAvailable)
            return "A run is already under way.";
        return "No run recorded. The house has not met you yet.";
    }

    static void SetRow(VisualElement marker, Label label, bool focused, bool enabled,
        bool hideWhenDisabled=false)
    {
        if (marker != null) marker.style.display = focused && enabled ? DisplayStyle.Flex : DisplayStyle.None;
        if (label == null) return;
        if(hideWhenDisabled&&label.parent!=null)
            label.parent.style.display=enabled?DisplayStyle.Flex:DisplayStyle.None;
        bool highContrast = GmAccessibilitySettings.HighContrast;
        label.style.color = !enabled
            ? (highContrast ? new Color(0.62f, 0.62f, 0.62f) : Dim)
            : focused
                ? (highContrast ? new Color(1f, 0.86f, 0.2f) : Gild)
                : (highContrast ? Color.white : Ink);
    }

    /// Activates the focused row. Returns false when the row cannot act -- Continue with no save --
    /// so the caller can play a refusal rather than silently doing nothing.
    public bool Activate()
    {
        switch (Focused)
        {
            case Row.Continue: return Continue();
            case Row.NewRun: NewRun(); return true;
            case Row.RestoreLastValid: return RestoreLastValid();
            case Row.ResetHouseMemory: return ResetHouseMemory();
            case Row.ExportDiagnostics: return OpenDiagnosticsPreview();
            case Row.Mirror: return MirrorAvailable && MirrorRun();
            case Row.LedgerReview: return OpenLedgerReview();
            case Row.Recollection:
                if (RecollectionPackages.Count == 0) return false;
                GmHouseKnownPackage known = RecollectionPackages[RecollectionSelection];
                return Recollection(known.Package, known.Binding);
            case Row.Settings: OpenSettings(); return true;
            case Row.Quit: Quit(); return true;
            default: return false;
        }
    }

    void OpenSettings()
    {
        settingsOpen = true;
        settingsPanel.style.display = DisplayStyle.Flex;
        settingsView.SetFocusVisible(true);
    }

    void CloseSettings()
    {
        settingsOpen = false;
        settingsView?.SetFocusVisible(false);
        if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
        FlushSettingsBoundary();
    }

    bool OpenLedgerReview()
    {
        if (!LedgerReviewAvailable || ledgerReviewPanel == null) return false;
        ledgerReviewPanel.contentContainer.Clear();
        var heading = new Label("THE LEDGER'S READING") { name="BootLedgerReviewHeading" };
        heading.style.fontSize = Mathf.RoundToInt(RowFontSize * GmAccessibilitySettings.TextScale);
        heading.style.color = PanelGild;
        heading.style.marginBottom = 12;
        ledgerReviewPanel.Add(heading);
        string nights=LedgerReviewCompletedRuns==1?"one completed night":
            $"{LedgerReviewCompletedRuns} completed nights";
        var scope=new Label($"An interpretation of {nights}. These are broad patterns, not certainties.")
            { name="BootLedgerReviewScope" };
        scope.style.whiteSpace=WhiteSpace.Normal;
        scope.style.fontSize=Mathf.RoundToInt(NoteFontSize*GmAccessibilitySettings.TextScale);
        scope.style.color=PanelInk;
        scope.style.marginBottom=10;
        ledgerReviewPanel.Add(scope);
        foreach (string line in LedgerReviewLines)
        {
            var entry = new Label("• " + line);
            entry.style.whiteSpace = WhiteSpace.Normal;
            entry.style.fontSize = Mathf.RoundToInt(NoteFontSize * GmAccessibilitySettings.TextScale);
            entry.style.color = PanelInk;
            entry.style.marginBottom = 8;
            ledgerReviewPanel.Add(entry);
        }
        var close = new Label("Confirm or Cancel to close.");
        close.style.fontSize = Mathf.RoundToInt(NoteFontSize * GmAccessibilitySettings.TextScale);
        close.style.color = PanelDim;
        close.style.marginTop = 8;
        ledgerReviewPanel.Add(close);
        GmUiText.UseStandardGenerator(ledgerReviewPanel);
        ledgerReviewOpen = true;
        ledgerReviewPanel.style.display = DisplayStyle.Flex;
        return true;
    }

    void CloseLedgerReview()
    {
        ledgerReviewOpen = false;
        if (ledgerReviewPanel != null) ledgerReviewPanel.style.display = DisplayStyle.None;
    }

    bool OpenDiagnosticsPreview()
    {
        if (!ExportDiagnosticsAvailable || diagnosticsPanel == null) return false;
        if (!GmHousePersistenceCoordinator.TryPreviewSupportDiagnostics(
            out string preview,out string error))
        {
            Debug.LogError($"[GmBoot] diagnostics preview refused: {error}");
            return false;
        }
        diagnosticsPanel.contentContainer.Clear();
        DiagnosticsStatus=string.Empty;
        var heading = new Label("SUPPORT DIAGNOSTICS PREVIEW")
            { name="BootDiagnosticsHeading" };
        heading.style.fontSize = Mathf.RoundToInt(RowFontSize * GmAccessibilitySettings.TextScale);
        heading.style.color = PanelGild;
        heading.style.marginBottom = 12;
        diagnosticsPanel.Add(heading);
        var disclosure = new Label(
            "Includes build and platform class, validation code, schema version and artifact counts. " +
            "Excludes saves, paths, seeds, inputs, evidence and player identity.")
            { name="BootDiagnosticsDisclosure" };
        disclosure.style.whiteSpace = WhiteSpace.Normal;
        disclosure.style.fontSize = Mathf.RoundToInt(NoteFontSize * GmAccessibilitySettings.TextScale);
        disclosure.style.color = PanelInk;
        disclosure.style.marginBottom = 10;
        diagnosticsPanel.Add(disclosure);
        var payload = new Label(preview.Replace(",\"", ",\n\""));
        payload.style.whiteSpace = WhiteSpace.Normal;
        payload.style.fontSize = Mathf.RoundToInt(NoteFontSize * GmAccessibilitySettings.TextScale);
        payload.style.color = PanelDim;
        diagnosticsPanel.Add(payload);
        diagnosticsStatusLabel=new Label { name="BootDiagnosticsStatus" };
        diagnosticsStatusLabel.style.whiteSpace=WhiteSpace.Normal;
        diagnosticsStatusLabel.style.fontSize=Mathf.RoundToInt(
            NoteFontSize*GmAccessibilitySettings.TextScale);
        diagnosticsStatusLabel.style.color=PanelGild;
        diagnosticsStatusLabel.style.marginTop=10;
        diagnosticsPanel.Add(diagnosticsStatusLabel);
        var close = new Label("Confirm to choose a destination. Cancel keeps every file unchanged.");
        close.style.whiteSpace = WhiteSpace.Normal;
        close.style.fontSize = Mathf.RoundToInt(NoteFontSize * GmAccessibilitySettings.TextScale);
        close.style.color = PanelGild;
        close.style.marginTop = 10;
        diagnosticsPanel.Add(close);
        GmUiText.UseStandardGenerator(diagnosticsPanel);
        DiagnosticsPreviewOpen = true;
        diagnosticsPanel.style.display = DisplayStyle.Flex;
        return true;
    }

    public bool ConfirmDiagnosticsExport()
    {
        if (!DiagnosticsPreviewOpen || diagnosticsDestinationPicker == null) return false;
        if (!diagnosticsDestinationPicker.TryChooseDestination(
            out string destination,out string pickerError))
        {
            if (pickerError == "diagnostics export cancelled")
                SetDiagnosticsStatus("Export cancelled. No file was written.");
            else
            {
                SetDiagnosticsStatus("Export unavailable: "+pickerError);
                Debug.LogError($"[GmBoot] diagnostics destination unavailable: {pickerError}");
            }
            return false;
        }
        if (!GmHousePersistenceCoordinator.TryExportSupportDiagnostics(
            destination,out _,out string error))
        {
            SetDiagnosticsStatus("Export failed: "+error+" Choose a different new filename.");
            Debug.LogError($"[GmBoot] diagnostics export refused: {error}");
            return false;
        }
        SetDiagnosticsStatus("Support diagnostics saved as "+Path.GetFileName(destination)+".");
        return true;
    }

    void SetDiagnosticsStatus(string status)
    {
        DiagnosticsStatus=status??string.Empty;
        if(diagnosticsStatusLabel!=null) diagnosticsStatusLabel.text=DiagnosticsStatus;
    }

    void CloseDiagnosticsPreview()
    {
        DiagnosticsPreviewOpen = false;
        DiagnosticsStatus=string.Empty;
        diagnosticsStatusLabel=null;
        if (diagnosticsPanel != null) diagnosticsPanel.style.display = DisplayStyle.None;
    }

    static Color PanelGild => GmAccessibilitySettings.HighContrast
        ? new Color(1f,0.86f,0.2f) : Gild;
    static Color PanelInk => GmAccessibilitySettings.HighContrast ? Color.white : Ink;
    static Color PanelDim => GmAccessibilitySettings.HighContrast
        ? new Color(0.78f,0.78f,0.78f) : Dim;

    public void SetDiagnosticsDestinationPickerForTests(
        IGmSupportDiagnosticsDestinationPicker picker) =>
        diagnosticsDestinationPicker = picker??throw new ArgumentNullException(nameof(picker));

    void ApplyAccessibility()
    {
        if (root == null) return;
        float scale = GmAccessibilitySettings.TextScale;
        if (titleLabel != null) titleLabel.style.fontSize = Mathf.RoundToInt(TitleFontSize * scale);
        foreach (Label label in new[] { continueLabel, newRunLabel, restoreLastValidLabel,
                     resetHouseLabel, exportDiagnosticsLabel, mirrorLabel,
                     ledgerReviewLabel,
                     recollectionLabel, settingsLabel, quitLabel })
            if (label != null) label.style.fontSize = Mathf.RoundToInt(RowFontSize * scale);
        if (note != null) note.style.fontSize = Mathf.RoundToInt(NoteFontSize * scale);
        if (settingsPanel != null)
            foreach (VisualElement control in settingsPanel.Children())
                control.style.fontSize = Mathf.RoundToInt(RowFontSize * scale);
        bool highContrast = GmAccessibilitySettings.HighContrast;
        root.EnableInClassList("gm-high-contrast", highContrast);
        root.style.backgroundColor = highContrast ? Color.black : new Color(0.012f, 0.010f, 0.009f);
        if (titleLabel != null)
            titleLabel.style.color = highContrast ? new Color(1f, 0.86f, 0.2f) : Gild;
        Repaint();
        settingsView?.Refresh();
    }

    /// Starts a fresh run. Clears the store FIRST: a New Run that inherits the previous run's
    /// catches, shards and corruption would silently hand the player someone else's true ending.
    public void NewRun()
    {
        EnsureSceneDirector();
        GmSaveSystem.TryLoadAccessibilityPreferences();
        if (HouseRecoveryRequired || ProbeRecoveryRequired())
        {
            GmRunStore.BeginNewRun();
            if (!GmHousePersistenceCoordinator.TryBeginIsolatedOrdinaryRun(GmRunSeed.Value,
                out string isolatedError))
            {
                Debug.LogError($"[GmBoot] isolated New Run refused: {isolatedError}");
                return;
            }
            GmExperienceTelemetry.Record("boot", "new-run-isolated");
            Debug.Log("[GmBoot] isolated new run — this sitting will not be remembered");
            StartRun(PrologueSceneId, GmSceneDirector.PrologueScenePath);
            return;
        }
        if (!TryRetireCampaignForReplacement(out string retireError))
        {
            Debug.LogError($"[GmBoot] existing campaign could not be retired: {retireError}");
            return;
        }
        GmRunStore.BeginNewRun();
        if (!GmHousePersistenceCoordinator.TryBeginOrdinaryRun(GmRunSeed.Value,
            out string houseError))
        {
            Debug.LogError($"[GmBoot] House memory unavailable; persistent New Run refused: {houseError}");
            return;
        }
        GmExperienceTelemetry.Record("boot", "new-run");
        Debug.Log("[GmBoot] new run — store cleared, entering the Prologue");
        StartRun(PrologueSceneId, GmSceneDirector.PrologueScenePath);
    }

    bool ProbeRecoveryRequired()
    {
        if (GmHousePersistenceCoordinator.TryGetTitleUnlocks(out _, out _, out _))
            return false;
        HouseRecoveryRequired = GmHousePersistenceCoordinator.HouseRecoveryRequired;
        return HouseRecoveryRequired;
    }

    bool ResetHouseMemory()
    {
        if (!HouseRecoveryRequired) return false;
        if (!resetArmed)
        {
            resetArmed = true;
            Repaint();
            return true;
        }
        if (!GmHousePersistenceCoordinator.TryAuthorizeUnreadableDomainRecovery(
            out string error))
        {
            Debug.LogError($"[GmBoot] House recovery refused: {error}");
            return false;
        }
        resetArmed = false;
        Refresh();
        return true;
    }

    bool RestoreLastValid()
    {
        if (!HouseRecoveryRequired || !RestoreLastValidAvailable) return false;
        if (!restoreArmed)
        {
            restoreArmed = true;
            Repaint();
            return true;
        }
        if (!GmHousePersistenceCoordinator.TryRestoreLastValidProfile(out string error))
        {
            Debug.LogError($"[GmBoot] House profile restore refused: {error}");
            return false;
        }
        restoreArmed = false;
        Refresh();
        return true;
    }

    /// Production entry point for the opt-in campaign mode. The title treatment can bind this to
    /// an authored row later; keeping it explicit here prevents Ordinary New Run from consulting
    /// House memory while still giving tests and the eventual menu one real route.
    public bool MirrorRun()
    {
        EnsureSceneDirector();
        if (!TryRetireCampaignForReplacement(out string retireError))
        {
            Debug.LogError($"[GmBoot] existing campaign could not be retired: {retireError}");
            return false;
        }
        GmRunStore.BeginNewRun();
        if (!GmHousePersistenceCoordinator.TryBeginMirrorRun(GmRunSeed.Value,
            out string error))
        {
            Debug.LogError($"[GmBoot] Mirror refused: {error}");
            return false;
        }
        StartRun(PrologueSceneId, GmSceneDirector.PrologueScenePath);
        return true;
    }

    bool TryRetireCampaignForReplacement(out string error)
    {
        return GmHousePersistenceCoordinator.TryRetireActiveRunForReplacement(out error);
    }

    /// Recollection is deliberately a non-campaign Parlor shell: no ordinal, Continue pointer,
    /// ending, or profile write. Only an exact package already retained by the profile can enter.
    public bool Recollection(GmParlorAdaptivePackage known,GmHousePackageBinding binding)
    {
        EnsureSceneDirector();
        if (!GmSaveSystem.Flush())
        {
            Debug.LogError($"[GmBoot] Recollection refused until campaign save settles: {GmSaveSystem.LastError}");
            return false;
        }
        GmRunStore.BeginNewRun();
        if (!GmHousePersistenceCoordinator.TryBeginRecollection(known,binding,
            out string error))
        {
            Debug.LogError($"[GmBoot] Recollection refused: {error}");
            return false;
        }
        string path=ScenePathFor("parlor");
        if(path==null) return false;
        StartRun("parlor",path);
        return true;
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
        if (!string.IsNullOrEmpty(GmRunStore.HouseRunId) &&
            !GmHousePersistenceCoordinator.TryResume(GmRunStore.HouseRunId,
                out string houseError))
        {
            Debug.LogError($"[GmBoot] FAILED: House run could not resume: {houseError}");
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
        FlushSettingsBoundary();
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
        FlushSettingsBoundary();
        GmAccessibilitySettings.OnChanged -= ApplyAccessibility;
        ReleaseInput();
        if (panelSettings != null) Destroy(panelSettings);
    }

    void OnApplicationPause(bool pausedByApplication)
    {
        if (pausedByApplication) FlushSettingsBoundary();
    }

    void OnApplicationQuit() => FlushSettingsBoundary();

    void FlushSettingsBoundary()
    {
        if (!GmAccessibilitySettings.HasPendingSave) return;
        if (!GmAccessibilitySettings.FlushPendingSave())
            Debug.LogError($"[GmBoot] Accessibility settings remain pending: {GmSaveSystem.LastError}");
    }

    void ReleaseInput()
    {
        DisableInput();

        InputActionAsset ownedControls = menuControls;
        menuControls = null;
        menuInput = null;
        navigateAction = null;
        submitAction = null;
        cancelAction = null;
        quitAction = null;
        inputActive = false;
        navigationHeld = false;
        if (ownedControls != null) Destroy(ownedControls);
    }
}
