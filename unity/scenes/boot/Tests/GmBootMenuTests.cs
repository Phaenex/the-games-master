// The boot menu is the object that makes every ending reachable, so it gets tested as such rather
// than as a screen with three labels on it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class GmBootMenuTests
{
    GameObject host;
    GmBootMenu menu;
    readonly List<GameObject> spawned = new List<GameObject>();
    string saveDirectory;

    [SetUp]
    public void SetUp()
    {
        saveDirectory = Path.Combine(Path.GetTempPath(), "gm-boot-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(saveDirectory, "save.json"));
        GmRunStore.BeginNewRun();
        host = new GameObject("BootMenuFixture");
        menu = host.AddComponent<GmBootMenu>();
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        // The director is DontDestroyOnLoad and static-backed; leaving one behind would let a later
        // test pass because an earlier test created it.
        foreach (var director in Object.FindObjectsByType<GmSceneDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(director.gameObject);   // GmSceneDirector.OnDestroy clears the static
        foreach (var extra in spawned) if (extra != null) Object.DestroyImmediate(extra);
        spawned.Clear();
        GmSaveSystem.Flush();
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
    }

    [Test]
    public void BootInstantiatesTheSceneDirectorNothingElseEverDid()
    {
        // This is the entire reason the scene exists. GmSceneDirector owns every transition and the
        // only call to GmEndingManager.ResolveEnding(); until something created it, six authored
        // endings and twelve passing ending tests described a thing that could not happen.
        // Unity's == operator, not Assert.IsNull: a destroyed UnityEngine.Object is null to Unity
        // and NOT null to C#, so Assert.IsNull would fail on a corpse left by a previous test.
        Assert.AreEqual(0, Object.FindObjectsByType<GmSceneDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            "a director already existed — this test cannot prove anything");

        GmBootMenu.EnsureSceneDirector();

        // Counted in the scene rather than read off GmSceneDirector.Instance: Awake does not run on
        // AddComponent in edit mode, so the static is still null here even though the object exists.
        // Asserting the static would test Unity's edit-mode lifecycle, not whether boot did its job.
        Assert.AreEqual(1, Object.FindObjectsByType<GmSceneDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            "boot did not instantiate GmSceneDirector, so no scene transition and no ending can resolve");
    }

    [Test]
    public void EnsuringTheDirectorTwiceDoesNotStackDontDestroyOnLoadObjects()
    {
        GmBootMenu.EnsureSceneDirector();
        GmBootMenu.EnsureSceneDirector();
        var all = Object.FindObjectsByType<GmSceneDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.AreEqual(1, all.Length, "returning to the title stacked a second undestroyable director");
    }

    [Test]
    public void ContinueIsUnavailableAndRefusesWhenNoRunWasEverSaved()
    {
        menu.Refresh();
        Assert.IsFalse(menu.ContinueAvailable);
        Assert.AreEqual(GmBootMenu.Row.NewRun, menu.Focused,
            "focus started on a row that cannot act; the first press would do nothing");
        Assert.IsFalse(menu.Continue(), "Continue claimed to resume a run that does not exist");
    }

    [Test]
    public void ContinueBecomesAvailableOnceARunIsOnDisk()
    {
        GmRunStore.RecordCatch("parlor-palm-trick-1");
        GmRunStore.CurrentSceneId = "parlor";
        Assert.IsTrue(GmSaveSystem.Save(), "the fixture could not write a save");

        menu.Refresh();
        Assert.IsTrue(menu.ContinueAvailable);
        Assert.AreEqual(GmBootMenu.Row.Continue, menu.Focused);
    }

    [Test]
    public void ContinueRestoresTheRunAndResumesTheSceneItWasSavedIn()
    {
        GmRunStore.RecordCatch("court-rigged-seal");
        GmRunStore.CollectShard(1);
        GmRunStore.CurrentSceneId = "court";
        GmRunStore.LastCheckpoint = "bar";
        Assert.IsTrue(GmSaveSystem.Save());

        // Wipe live state the way quitting to the title would.
        GmRunStore.BeginNewRun();
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount);

        string resumed = null;
        menu.OnStartRun += id => resumed = id;
        menu.Refresh();
        Assert.IsTrue(menu.Continue());

        Assert.AreEqual("court", resumed, "resumed the wrong scene — the saved scene id was ignored");
        Assert.AreEqual(1, GmRunStore.CheatsCaughtCount, "the restored run lost its catches");
        Assert.IsTrue(GmRunStore.HasShard(1), "the restored run lost its shard");
    }

    [Test]
    public void NewRunClearsThePreviousRunBeforeEnteringThePrologue()
    {
        GmRunStore.RecordCatch("stale-catch");
        GmRunStore.CollectShard(0);
        GmRunStore.RaiseCorruption("stale");

        string entered = null;
        menu.OnStartRun += id => entered = id;
        menu.NewRun();

        Assert.AreEqual(GmBootMenu.PrologueSceneId, entered);
        Assert.AreEqual(0, GmRunStore.CheatsCaughtCount,
            "a new run inherited the previous run's catches — it would hand the player someone else's true ending");
        Assert.IsFalse(GmRunStore.HasShard(0), "a new run inherited a shard");
        Assert.AreEqual(GmRunStore.MinCorruptionTier, GmRunStore.CorruptionTier,
            "a new run inherited corruption");
    }

    [Test]
    public void ColdRestartNewRunImportsOnlyDurableAccessibilityPreferences()
    {
        GmAccessibilitySettings.SetCaptions(true);
        GmAccessibilitySettings.SetReducedMotion(true);
        GmAccessibilitySettings.SetVibration(false);
        GmAccessibilitySettings.SetMonoAudio(true);
        GmAccessibilitySettings.SetHighContrast(true);
        GmAccessibilitySettings.SetTextScale(1.8f);
        GmRunStore.RecordCatch("old-run-catch");
        GmRunStore.RaiseCorruption("old-run");
        GmRunStore.CurrentSceneId = "court";
        Assert.That(GmSaveSystem.Save(), Is.True, GmSaveSystem.LastError);

        // Simulate a cold process: run state and accessibility statics are defaults while disk stays.
        GmRunStore.BeginNewRun();
        ResetAccessibilityToColdDefaults();
        int runEvents = 0;
        Action changed = () => runEvents++;
        GmRunStore.OnStateChanged += changed;
        try
        {
            menu.NewRun();
        }
        finally
        {
            GmRunStore.OnStateChanged -= changed;
        }

        Assert.That(GmAccessibilitySettings.Captions, Is.True);
        Assert.That(GmAccessibilitySettings.ReducedMotion, Is.True);
        Assert.That(GmAccessibilitySettings.Vibration, Is.False);
        Assert.That(GmAccessibilitySettings.MonoAudio, Is.True);
        Assert.That(GmAccessibilitySettings.HighContrast, Is.True);
        Assert.That(GmAccessibilitySettings.TextScale, Is.EqualTo(1.8f).Within(0.001f));
        Assert.That(GmRunStore.CheatsCaughtCount, Is.Zero,
            "New Run imported the previous run while trying to recover preferences");
        Assert.That(GmRunStore.CorruptionTier, Is.EqualTo(GmRunStore.MinCorruptionTier));
        Assert.That(GmRunStore.CurrentSceneId, Is.EqualTo(GmBootMenu.PrologueSceneId));
        Assert.That(runEvents, Is.EqualTo(1),
            "accessibility-only import leaked stale run-state events before BeginNewRun");
    }

    [Test]
    public void CorruptSaveCannotBlockNewRun()
    {
        Directory.CreateDirectory(saveDirectory);
        File.WriteAllText(GmSaveSystem.SavePath, "{not-json");
        string entered = null;
        menu.OnStartRun += id => entered = id;

        Assert.DoesNotThrow(() => menu.NewRun());

        Assert.That(entered, Is.EqualTo(GmBootMenu.PrologueSceneId));
        Assert.That(GmRunStore.CheatsCaughtCount, Is.Zero);
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void UnknownPreferenceVersionDoesNotMutateLiveSettingsOrBlockNewRun(int version)
    {
        Directory.CreateDirectory(saveDirectory);
        File.WriteAllText(GmSaveSystem.SavePath, JsonUtility.ToJson(new GmSaveData
        {
            accessibilitySettingsVersion = version,
            accessibilityCaptions = false,
            accessibilityVibration = true,
            accessibilityTextScale = 1f,
        }));
        GmAccessibilitySettings.SetCaptions(true);
        GmAccessibilitySettings.SetVibration(false);
        GmAccessibilitySettings.SetTextScale(1.7f);
        string entered = null;
        menu.OnStartRun += id => entered = id;

        menu.NewRun();

        Assert.That(entered, Is.EqualTo(GmBootMenu.PrologueSceneId));
        Assert.That(GmAccessibilitySettings.Captions, Is.True);
        Assert.That(GmAccessibilitySettings.Vibration, Is.False);
        Assert.That(GmAccessibilitySettings.TextScale, Is.EqualTo(1.7f).Within(0.001f));
    }

    [Test]
    public void AResumeIntoAnUnknownSceneRefusesRatherThanGuessing()
    {
        GmRunStore.CurrentSceneId = "a-room-that-does-not-exist";
        Assert.IsTrue(GmSaveSystem.Save());
        GmRunStore.BeginNewRun();

        // The refusal is loud on purpose -- a player losing a run deserves a reason in the log --
        // and Unity fails a test on any undeclared LogError, correctly. Declare this one.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            @"\[GmBoot\] FAILED: save names scene 'a-room-that-does-not-exist'"));

        menu.Refresh();
        Assert.IsFalse(menu.Continue(),
            "resumed a corrupt save by guessing a scene, which would read as the game forgetting the run");
    }

    [Test]
    public void EveryTransitionableSceneIdHasAPath()
    {
        // A saved run can name any scene the director can transition to. If one of these ever lacks
        // a path, Continue refuses -- correct, but the player just loses their run, so catch it here.
        foreach (string id in new[]
                 { GmBootMenu.PrologueSceneId, "entry-hall", "parlor", "shut-the-box", "court", "hidden-room", "labyrinth" })
        {
            Assert.IsNotNull(GmBootMenu.ScenePathFor(id), $"scene id '{id}' is resumable but has no scene path");
        }
        Assert.IsNull(GmBootMenu.ScenePathFor("not-a-scene"));
    }

    [Test]
    public void TheMenuDrawsSettingsBeforeGameplayAndMarksFocusOnTwoChannels()
    {
        VisualElement root = BuildUi(menu);
        foreach (string name in new[] { "BootContinue", "BootNewRun", "BootSettings", "BootQuit" })
            Assert.IsNotNull(root.Q<VisualElement>(name), $"the {name} row is missing");
        Assert.IsNotNull(root.Q<Label>("BootTitle"));

        menu.Refresh();   // no save -> focus on New Run
        var newRunMarker = root.Q<VisualElement>("BootNewRunMarker");
        var continueMarker = root.Q<VisualElement>("BootContinueMarker");
        var continueLabel = root.Q<Label>("BootContinueLabel");
        var newRunLabel = root.Q<Label>("BootNewRunLabel");

        Assert.AreEqual(DisplayStyle.Flex, newRunMarker.style.display.value);
        Assert.AreEqual(DisplayStyle.None, continueMarker.style.display.value);
        // Second channel: an unavailable row is dimmer than the focused one, not merely a different hue.
        Assert.Less(continueLabel.style.color.value.r, newRunLabel.style.color.value.r,
            "an unavailable Continue is not visibly dimmer than the focused row");
    }

    [Test]
    public void BootAppliesGlobalTextScaleAndHighContrastToItsRealTree()
    {
        GmAccessibilitySettings.SetTextScale(2f);
        GmAccessibilitySettings.SetHighContrast(true);

        VisualElement root = BuildUi(menu);
        Label title = root.Q<Label>("BootTitle");

        Assert.That(title.style.fontSize.value.value, Is.EqualTo(92f).Within(0.01f));
        Assert.That(root.ClassListContains("gm-high-contrast"), Is.True);
        Assert.That(root.Q<VisualElement>("BootSettings"), Is.Not.Null);
    }

    [Test]
    public void BootAndSharedSettingsUseTheRuntimeSafeStandardTextGenerator()
    {
        VisualElement root = BuildUi(menu);
        var text = root.Query<TextElement>().ToList();
        Assert.That(text, Is.Not.Empty);
        Assert.That(root.Q<Toggle>("CaptionsToggle"), Is.Not.Null);
        Assert.That(root.Q<Slider>("TextScaleSlider"), Is.Not.Null);
        foreach (TextElement element in text)
            Assert.That(element.style.unityTextGenerator.value,
                Is.EqualTo(TextGeneratorType.Standard),
                $"Boot dynamic text '{element.name}' can hit Unity 6's missing ICU path");
    }

    [Test]
    public void FocusSkipsContinueEntirelyWhenThereIsNothingToContinue()
    {
        BuildUi(menu);
        menu.Refresh();
        Assert.AreEqual(GmBootMenu.Row.NewRun, menu.Focused);
        menu.MoveFocus(1);
        Assert.AreEqual(GmBootMenu.Row.Settings, menu.Focused);
        menu.MoveFocus(1);
        Assert.AreEqual(GmBootMenu.Row.Quit, menu.Focused);
        menu.MoveFocus(1);
        Assert.AreEqual(GmBootMenu.Row.NewRun, menu.Focused,
            "focus landed on a disabled Continue while wrapping");
    }

    [Test]
    public void MenuInputMapHasKeyboardAndGamepadParity()
    {
        var controls = Resources.Load<InputActionAsset>("Input/GmControls");
        Assert.IsNotNull(controls, "Resources/Input/GmControls.inputactions is missing");
        InputActionMap input = controls.FindActionMap("Menu");
        Assert.IsNotNull(input, "GmControls has no Menu action map");

        AssertBindings(input, "Navigate", "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s",
            "<Keyboard>/d", "<Keyboard>/upArrow", "<Keyboard>/leftArrow", "<Keyboard>/downArrow",
            "<Keyboard>/rightArrow", "<Gamepad>/dpad", "<Gamepad>/leftStick");
        AssertBindings(input, "Submit", "<Keyboard>/enter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        AssertBindings(input, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
        AssertBindings(input, "Quit", "<Keyboard>/q", "<Gamepad>/buttonNorth");
    }

    static void AssertBindings(InputActionMap map, string actionName, params string[] requiredPaths)
    {
        InputAction action = map.FindAction(actionName);
        Assert.IsNotNull(action, $"Menu/{actionName} is missing");
        string[] actual = action.bindings.Select(binding => binding.effectivePath).ToArray();
        foreach (string path in requiredPaths)
            CollectionAssert.Contains(actual, path, $"Menu/{actionName} has no {path} binding");
    }

    static void ResetAccessibilityToColdDefaults()
    {
        typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings")?.GetMethod(
            "ResetToDefaultsForTests", System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static)?.Invoke(null, null);
    }

    static VisualElement BuildUi(GmBootMenu target)
    {
        var build = typeof(GmBootMenu).GetMethod("BuildUi",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(build, "GmBootMenu no longer builds its own UI");
        build.Invoke(target, null);
        var document = target.GetComponent<UIDocument>();
        Assert.IsNotNull(document?.rootVisualElement, "the boot menu built no visual tree");
        return document.rootVisualElement;
    }
}
