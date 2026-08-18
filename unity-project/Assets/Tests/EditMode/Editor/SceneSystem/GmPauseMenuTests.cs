using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class GmPauseMenuTests
{
    string directory;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-pause-" + Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"));
        GmRunStore.BeginNewRun();
        ResetAccessibility();
        GmPauseMenu.SetTextScale(1.0f);
        GmPauseMenu.SetReduceMotion(false);
    }

    [TearDown]
    public void TearDown()
    {
        ResetAccessibility();
        GmSaveSystem.ResetTestConfiguration();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void PauseTogglesStateButNeverTouchesTimeScale()
    {
        // GmPlayer is the single owner of Time.timeScale (see GmPlayer.SetPaused) -- this menu only
        // mirrors IsPaused/visibility. If SetPauseState touched timeScale itself it would stomp
        // GmTheReadController's time-dilation mechanic back to a hardcoded 0/1 on every pause
        // toggle instead of preserving whatever scale was active before the pause.
        float sentinel = 0.35f;
        Time.timeScale = sentinel;

        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();

        menu.SetPauseState(true);
        Assert.IsTrue(GmPauseMenu.IsPaused);
        Assert.AreEqual(sentinel, Time.timeScale, "SetPauseState must not own Time.timeScale");

        menu.SetPauseState(false);
        Assert.IsFalse(GmPauseMenu.IsPaused);
        Assert.AreEqual(sentinel, Time.timeScale, "SetPauseState must not own Time.timeScale");

        Time.timeScale = 1f;
        Object.DestroyImmediate(pauseObj);
    }

    [Test]
    public void SyncWithPlayerMirrorsGmPlayerIsPausedAndSetsThePrompt()
    {
        var playerObj = new GameObject("TestPlayer");
        var player = playerObj.AddComponent<GmPlayer>();

        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            BuildUi(menu);
            var prompt = menu.GetComponent<UIDocument>().rootVisualElement.Q<Label>("PausePrompt");
            Assert.IsNotNull(prompt, "the pause menu lost its resume/quit prompt");

            player.SetPaused(true);
            Sync(menu, player);
            Assert.IsTrue(GmPauseMenu.IsPaused, "the menu did not react to GmPlayer.IsPaused becoming true");
            StringAssert.Contains("Esc  Resume", prompt.text);
            StringAssert.Contains("Q  Quit", prompt.text);
            StringAssert.Contains("Enter  Select", prompt.text);

            player.SetPaused(false);
            Sync(menu, player);
            Assert.IsFalse(GmPauseMenu.IsPaused, "the menu did not react to GmPlayer.IsPaused becoming false");
        }
        finally
        {
            Teardown(menu, pauseObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void APrologueHudCedesPauseOwnershipToTheCommonMenu()
    {
        // wend-hill-prologue already has its own bespoke, tested pause overlay (GmPrologueHud).
        // This menu must never show a second pause screen on top of it.
        var hudObj = new GameObject("TestPrologueHud");
        var hud = hudObj.AddComponent<GmPrologueHud>();

        var playerObj = new GameObject("TestPlayer");
        var player = playerObj.AddComponent<GmPlayer>();

        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            player.SetPaused(true);
            InvokeUpdate(menu);
            Assert.IsTrue(GmPauseMenu.IsPaused,
                "the common accessible pause menu was suppressed in the Prologue");
        }
        finally
        {
            Teardown(menu, pauseObj);
            Object.DestroyImmediate(playerObj);
            Object.DestroyImmediate(hudObj);
        }
    }

    [Test]
    public void MenuOwnsACompleteClonedInputContractAndExposesControllerFocus()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            BuildUi(menu);
            Assert.That(ReadInstance<bool>(menu, "HasRequiredActions"), Is.True,
                "pause never acquired Menu/Navigate, Submit and Cancel from an owned clone");
            menu.SetPauseState(true);
            Assert.That(ReadInstance<int>(menu, "FocusedTabIndex"), Is.EqualTo(0));
            InvokeInstance(menu, "MoveMenuFocus", 3);
            Assert.That(menu.ActiveTab, Is.EqualTo(GmPauseTab.Settings));
            InvokeInstance(menu, "ActivateFocusedItem");
            Assert.That(ReadInstance<bool>(menu, "SettingsFocusActive"), Is.True);
            Assert.That(ReadInstance<int>(menu, "SettingsFocusIndex"), Is.Zero);
            Assert.That(menu.GetComponent<UIDocument>().rootVisualElement
                .Q<VisualElement>("CaptionsToggle").ClassListContains("gm-controller-focus"), Is.True,
                "settings focus has no visible non-colour indicator");
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void CustomControllerOwnerPreventsUiToolkitFromSubmittingTheSameWidgets()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            foreach (string tab in new[]
                { "Tab_Journal", "Tab_MirrorShards", "Tab_CaughtTells", "Tab_Settings" })
                Assert.That(root.Q<Button>(tab).focusable, Is.False,
                    $"{tab} still accepts UI Toolkit controller Submit beside GmPauseMenu");

            menu.SwitchTab(GmPauseTab.Settings);
            menu.ActivateFocusedItem();
            foreach (string toggle in new[]
                { "CaptionsToggle", "ReduceMotionToggle", "VibrationToggle",
                    "MonoAudioToggle", "HighContrastToggle" })
                Assert.That(root.Q<Toggle>(toggle).focusable, Is.False,
                    $"{toggle} has a second controller Submit owner");
            Assert.That(root.Q<Slider>("TextScaleSlider").focusable, Is.False,
                "text scale still has a second controller navigation owner");
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void ClosingSettingsFlushesChangedPreferencesWithoutATestCallingFlush()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            menu.SetPauseState(true);
            menu.SwitchTab(GmPauseTab.Settings);
            root.Q<Toggle>("CaptionsToggle").value = true;
            root.Q<Slider>("TextScaleSlider").value = 1.6f;

            menu.SetPauseState(false);

            string json = File.ReadAllText(GmSaveSystem.PreferencesPath);
            StringAssert.Contains("\"captions\": true", json);
            StringAssert.Contains("\"textScale\": 1.6", json);
            Assert.That(GmSaveSystem.HasSave(), Is.False,
                "closing settings created a fake Continue run");
            Assert.That(ReadInstance<bool>(menu, "HasPendingSettingsSave"), Is.False);
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void FailedLifecycleFlushKeepsPreferencesDirtyAndRetriesAtTheNextBoundary()
    {
        var backend = new FailingBackend { Fail = true };
        GmSaveSystem.ConfigureForTests(Path.Combine(directory, "save.json"), backend);
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            menu.SetPauseState(true);
            menu.SwitchTab(GmPauseTab.Settings);
            root.Q<Toggle>("CaptionsToggle").value = true;
            LogAssert.Expect(LogType.Error, new Regex("injected accessibility writer failure"));
            LogAssert.Expect(LogType.Error, new Regex("Accessibility settings remain pending"));

            menu.SetPauseState(false);

            Assert.That(menu.HasPendingSettingsSave, Is.True,
                "failed flush was falsely marked durable");
            backend.Fail = false;
            InvokeInstance(menu, "OnApplicationPause", true);
            Assert.That(menu.HasPendingSettingsSave, Is.False);
            StringAssert.Contains("\"captions\": true", backend.LastJson);
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void SettingReduceMotionAndTextScaleClampsCorrectly()
    {
        GmPauseMenu.SetReduceMotion(true);
        Assert.IsTrue(GmPauseMenu.ReduceMotion);

        GmPauseMenu.SetReduceMotion(false);
        Assert.IsFalse(GmPauseMenu.ReduceMotion);

        GmPauseMenu.SetTextScale(1.25f);
        Assert.AreEqual(1.25f, GmPauseMenu.TextScale, 0.001f);

        // Clamping check
        GmPauseMenu.SetTextScale(3.0f);
        Assert.AreEqual(2.0f, GmPauseMenu.TextScale, 0.001f);

        GmPauseMenu.SetTextScale(0.1f);
        Assert.AreEqual(0.8f, GmPauseMenu.TextScale, 0.001f);
    }

    [Test]
    public void HighContrastTwoHundredPercentRepaintsEverySettingsLabelAndHint()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            menu.SetPauseState(true);
            menu.SwitchTab(GmPauseTab.Settings);
            GmPauseMenu.SetTextScale(2f);
            GmPauseMenu.SetHighContrast(true);

            foreach (Label label in root.Query<Label>().ToList())
            {
                Color color = label.style.color.value;
                Assert.That(Mathf.Max(color.r, color.g, color.b), Is.GreaterThanOrEqualTo(0.98f),
                    $"{label.name}/{label.text} stayed muted in high contrast");
            }
            foreach (Toggle toggle in root.Query<Toggle>().ToList())
                Assert.That(toggle.labelElement.style.color.value, Is.EqualTo(Color.white),
                    $"{toggle.name} label stayed muted in high contrast");
            Slider slider = root.Q<Slider>("TextScaleSlider");
            Assert.That(slider.labelElement.style.color.value, Is.EqualTo(Color.white));
            Assert.That(root.Q<Label>("PausePrompt").style.fontSize.value.value,
                Is.EqualTo(BodyFontSizeForTest * 2f).Within(0.001f));
        }
        finally
        {
            GmPauseMenu.SetHighContrast(false);
            GmPauseMenu.SetTextScale(1f);
            Teardown(menu, pauseObj);
        }
    }

    const int BodyFontSizeForTest = 16;

    [Test]
    public void TabSwitchingUpdatesActiveTab()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();

        menu.SwitchTab(GmPauseTab.MirrorShards);
        Assert.AreEqual(GmPauseTab.MirrorShards, menu.ActiveTab);

        menu.SwitchTab(GmPauseTab.CaughtTells);
        Assert.AreEqual(GmPauseTab.CaughtTells, menu.ActiveTab);

        menu.SwitchTab(GmPauseTab.Settings);
        Assert.AreEqual(GmPauseTab.Settings, menu.ActiveTab);

        Object.DestroyImmediate(pauseObj);
    }

    // The three tests above only read flags and an enum, which move whether or not the menu ever
    // draws. These build the real tree Start() builds and read what a paused player would see.

    [Test]
    public void BuildUiRaisesTheDossierChromeAndKeepsItHiddenUntilThePause()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);

            var document = pauseObj.GetComponent<UIDocument>();
            Assert.IsNotNull(document, "the pause menu built no UIDocument, so it can never be drawn");
            Assert.IsNotNull(document.panelSettings, "the pause panel has no PanelSettings to render through");
            Assert.IsNotNull(document.panelSettings.themeStyleSheet,
                "Resources/GmHudTheme.tss did not load; UI Toolkit falls back to its default look");
            Assert.AreEqual(900f, document.sortingOrder,
                "the pause menu no longer sorts above the HUD it must cover");

            Assert.AreEqual("GmPauseMenuRoot", root.name);
            Assert.AreEqual(DisplayStyle.None, root.style.display.value,
                "the pause menu is visible before anything paused");

            VisualElement container = root.Q<VisualElement>("PauseMenuContainer");
            Assert.IsNotNull(container, "the dossier panel is missing");
            var title = root.Q<Label>("PauseTitle");
            Assert.IsNotNull(title, "the dossier has no title");
            Assert.IsFalse(string.IsNullOrWhiteSpace(title.text));

            VisualElement header = root.Q<VisualElement>("TabHeaderRow");
            Assert.IsNotNull(header, "the tab header row is missing");
            string[] tabs = header.Children().OfType<Button>().Select(button => button.name).ToArray();
            Assert.AreEqual(new[] { "Tab_Journal", "Tab_MirrorShards", "Tab_CaughtTells", "Tab_Settings" },
                tabs, "the four dossier tabs are no longer all reachable from the header");

            menu.SetPauseState(true);
            Assert.AreEqual(DisplayStyle.Flex, root.style.display.value,
                "the menu stayed hidden through a pause");
            menu.SetPauseState(false);
            Assert.AreEqual(DisplayStyle.None, root.style.display.value);
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void EveryTabRendersContentAndTheShardTabsReadTheLiveRun()
    {
        GmRunStore.CollectShard(0);
        GmRunStore.CollectShard(2);
        GmRunStore.RecordCatch("parlor-palm-trick-4");

        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            VisualElement content = root.Q<VisualElement>("TabContentContainer");
            Assert.IsNotNull(content, "the dossier has no content area");

            foreach (GmPauseTab tab in System.Enum.GetValues(typeof(GmPauseTab)))
            {
                menu.SwitchTab(tab);
                Assert.Greater(content.childCount, 0, $"the {tab} tab renders nothing at all");
            }

            menu.SwitchTab(GmPauseTab.MirrorShards);
            string[] shardLines = content.Children().OfType<Label>().Select(line => line.text).ToArray();
            Assert.AreEqual(4, shardLines.Length, "the shard tab lost its summary or one of the three shards");
            Assert.AreEqual("Recovered 2 of 3.", shardLines[0]);
            Assert.IsTrue(shardLines[1].Contains("recovered"));
            Assert.IsTrue(shardLines[2].Contains("not found"),
                "a shard the run never recovered is shown as held");
            Assert.IsTrue(shardLines[3].Contains("recovered"));

            menu.SwitchTab(GmPauseTab.CaughtTells);
            string[] tellLines = content.Children().OfType<Label>().Select(line => line.text).ToArray();
            Assert.AreEqual(2, tellLines.Length);
            Assert.AreEqual("1 caught this run.", tellLines[0]);
            Assert.IsTrue(tellLines[1].Contains("parlor-palm-trick-4"),
                "the caught tell the run banked is not listed");

            // A run with nothing banked states the empty case rather than showing a bare panel.
            GmRunStore.BeginNewRun();
            menu.SwitchTab(GmPauseTab.CaughtTells);
            Assert.AreEqual(1, content.childCount);
            Assert.AreEqual("No tells caught yet.", content.Children().OfType<Label>().First().text);

            menu.SwitchTab(GmPauseTab.Settings);
            Assert.IsNotNull(content.Q<Toggle>("CaptionsToggle"), "settings lost its captions control");
            Assert.IsNotNull(content.Q<Toggle>("ReduceMotionToggle"), "settings lost its reduce-motion control");
            Assert.IsNotNull(content.Q<Toggle>("VibrationToggle"), "settings lost its vibration control");
            Assert.IsNotNull(content.Q<Toggle>("MonoAudioToggle"), "settings lost its mono-audio control");
            Assert.IsNotNull(content.Q<Toggle>("HighContrastToggle"), "settings lost its high-contrast control");
            Assert.IsNotNull(content.Q<Slider>("TextScaleSlider"), "settings lost its text-size control");
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void EveryDynamicPauseTextElementUsesTheRuntimeSafeStandardGenerator()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            foreach (GmPauseTab tab in Enum.GetValues(typeof(GmPauseTab)))
            {
                menu.SwitchTab(tab);
                AssertStandardText(root, $"pause tab {tab}");
            }
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void PauseSourceStandardizesTheTreeAfterEveryDynamicTabRebuild()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts/GmPauseMenu.cs"));
        int render = source.IndexOf("void RenderActiveTab()", StringComparison.Ordinal);
        int nextMethod = source.IndexOf("void BuildJournalTab()", render,
            StringComparison.Ordinal);
        Assert.That(render, Is.GreaterThanOrEqualTo(0));
        Assert.That(nextMethod, Is.GreaterThan(render));
        StringAssert.Contains("GmUiText.UseStandardGenerator(root)",
            source.Substring(render, nextMethod - render),
            "each rebuilt tab needs a post-construction standard-generator sweep");
    }

    [Test]
    public void EverySettingsControlAppliesImmediatelyAndQueuesDurableState()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            menu.SetPauseState(true);
            menu.SwitchTab(GmPauseTab.Settings);
            root.Q<Toggle>("CaptionsToggle").value = true;
            root.Q<Toggle>("ReduceMotionToggle").value = true;
            root.Q<Toggle>("VibrationToggle").value = false;
            root.Q<Toggle>("MonoAudioToggle").value = true;
            root.Q<Toggle>("HighContrastToggle").value = true;
            root.Q<Slider>("TextScaleSlider").value = 2f;

            Assert.That(ReadAccessibility<bool>("Captions"), Is.True);
            Assert.That(ReadAccessibility<bool>("ReducedMotion"), Is.True);
            Assert.That(ReadAccessibility<bool>("Vibration"), Is.False);
            Assert.That(ReadAccessibility<bool>("MonoAudio"), Is.True);
            Assert.That(ReadAccessibility<bool>("HighContrast"), Is.True);
            Assert.That(ReadAccessibility<float>("TextScale"), Is.EqualTo(2f).Within(0.001f));
            menu.SetPauseState(false);
            Assert.That(File.Exists(GmSaveSystem.PreferencesPath), Is.True,
                "closing settings did not durably flush changed preferences");
            Assert.That(GmSaveSystem.HasSave(), Is.False,
                "settings durability must not create a run save");
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    [Test]
    public void TextScaleResizesTheTypeTheMenuAlreadyDrew()
    {
        var pauseObj = new GameObject("TestPauseMenu");
        var menu = pauseObj.AddComponent<GmPauseMenu>();
        try
        {
            VisualElement root = BuildUi(menu);
            var title = root.Q<Label>("PauseTitle");
            float authored = title.style.fontSize.value.value;
            Assert.Greater(authored, 0f, "the title never took its authored size");

            GmPauseMenu.SetTextScale(GmPauseMenu.MaxTextScale);
            menu.SwitchTab(GmPauseTab.Journal);
            Assert.AreEqual(Mathf.RoundToInt(authored * GmPauseMenu.MaxTextScale),
                Mathf.RoundToInt(title.style.fontSize.value.value),
                "text size is a setting the menu stores but never applies to its own type");

            GmPauseMenu.SetTextScale(1.0f);
            menu.SwitchTab(GmPauseTab.Journal);
            Assert.AreEqual(Mathf.RoundToInt(authored),
                Mathf.RoundToInt(title.style.fontSize.value.value));
        }
        finally
        {
            Teardown(menu, pauseObj);
        }
    }

    // Start() is what builds the menu in play, and EditMode never runs it for a plain MonoBehaviour.
    // Driving the same private body is the only way to read the tree the player is shown.
    static VisualElement BuildUi(GmPauseMenu menu)
    {
        MethodInfo build = typeof(GmPauseMenu).GetMethod("BuildUi",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(build, "GmPauseMenu no longer builds its own UI");
        build.Invoke(menu, null);
        var document = menu.GetComponent<UIDocument>();
        Assert.IsNotNull(document);
        Assert.IsNotNull(document.rootVisualElement, "the pause menu built no visual tree");
        return document.rootVisualElement;
    }

    // SyncWithPlayer is the reactive-poll body Update() calls every frame; EditMode never pumps
    // Update() for a plain MonoBehaviour, so tests drive it directly instead.
    static void Sync(GmPauseMenu menu, GmPlayer player)
    {
        MethodInfo sync = typeof(GmPauseMenu).GetMethod("SyncWithPlayer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(sync, "GmPauseMenu no longer exposes SyncWithPlayer");
        sync.Invoke(menu, new object[] { player });
    }

    static void InvokeUpdate(GmPauseMenu menu)
    {
        MethodInfo update = typeof(GmPauseMenu).GetMethod("Update",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(update, "GmPauseMenu no longer has an Update loop");
        update.Invoke(menu, null);
    }

    static void Teardown(GmPauseMenu menu, GameObject owner)
    {
        if (GmPauseMenu.IsPaused) menu.SetPauseState(false);
        // OnDestroy releases the panel with Destroy(), which is illegal outside play mode. Take the
        // PanelSettings off the component first and release it here instead.
        FieldInfo panelField = typeof(GmPauseMenu).GetField("panelSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(panelField, "GmPauseMenu no longer owns the panel it creates");
        var settings = (PanelSettings)panelField.GetValue(menu);
        panelField.SetValue(menu, null);
        Object.DestroyImmediate(owner);
        if (settings != null) Object.DestroyImmediate(settings);
    }

    static Type AccessibilityType => typeof(GmRunStore).Assembly.GetType("GmAccessibilitySettings");

    static T ReadAccessibility<T>(string property)
    {
        Assert.That(AccessibilityType, Is.Not.Null);
        return (T)AccessibilityType.GetProperty(property,
            BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    static void ResetAccessibility()
    {
        AccessibilityType?.GetMethod("ResetToDefaultsForTests",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }

    static T ReadInstance<T>(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(info, Is.Not.Null, $"{target.GetType().Name} has no {property} property");
        return (T)info.GetValue(target);
    }

    static object InvokeInstance(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(info, Is.Not.Null, $"{target.GetType().Name} has no {method} method");
        return info.Invoke(target, args);
    }

    sealed class FailingBackend : IGmAtomicSaveBackend
    {
        public bool Fail;
        public string LastJson { get; private set; }

        public void WriteAtomic(string target, string json)
        {
            if (Fail) throw new IOException("injected accessibility writer failure");
            LastJson = json;
        }
    }

    static void AssertStandardText(VisualElement root, string context)
    {
        var text = root.Query<TextElement>().ToList();
        Assert.That(text, Is.Not.Empty, $"{context} built no text");
        foreach (TextElement element in text)
            Assert.That(element.style.unityTextGenerator.value,
                Is.EqualTo(TextGeneratorType.Standard),
                $"{context}: {element.name} can hit Unity 6's missing ICU path");
    }
}
