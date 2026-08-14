using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public class GmPauseMenuTests
{
    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        GmPauseMenu.SetTextScale(1.0f);
        GmPauseMenu.SetReduceMotion(false);
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
            Assert.AreEqual("Esc  Resume      Q  Quit", prompt.text,
                "the keyboard prompt does not match GmPrologueHud's established wording");

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
    public void APrologueHudInTheSceneSuppressesThisMenuEntirely()
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
            Assert.IsFalse(GmPauseMenu.IsPaused,
                "GmPauseMenu activated even though a GmPrologueHud is present in the scene");
        }
        finally
        {
            Teardown(menu, pauseObj);
            Object.DestroyImmediate(playerObj);
            Object.DestroyImmediate(hudObj);
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
        Assert.AreEqual(1.5f, GmPauseMenu.TextScale, 0.001f);

        GmPauseMenu.SetTextScale(0.1f);
        Assert.AreEqual(0.8f, GmPauseMenu.TextScale, 0.001f);
    }

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
            Assert.IsNotNull(content.Q<Toggle>("ReduceMotionToggle"), "settings lost its reduce-motion control");
            Assert.IsNotNull(content.Q<Slider>("TextScaleSlider"), "settings lost its text-size control");
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
}
