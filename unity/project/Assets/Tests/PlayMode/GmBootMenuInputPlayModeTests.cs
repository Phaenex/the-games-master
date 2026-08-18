using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class GmBootMenuInputPlayModeTests
{
    string savePath;
    string saveDirectory;
    Gamepad gamepad;
    Gamepad secondGamepad;
    MonoBehaviour menu;
    string startedSceneId;
    int startedSceneCount;

    [UnitySetUp]
    public IEnumerator LoadBootWithoutAStoredRun()
    {
        startedSceneId = null;
        startedSceneCount = 0;
        secondGamepad = null;
        saveDirectory = Path.Combine(Path.GetTempPath(), "gm-boot-play-" + Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(saveDirectory, "save.json");
        StaticCall(TypeNamed("GmSaveSystem"), "ConfigureForTests", savePath, null);

        SceneManager.LoadScene("Boot", LoadSceneMode.Single);
        yield return null;
        yield return null;

        menu = Behaviours("GmBootMenu").SingleOrDefault();
        Assert.IsNotNull(menu, "Boot loaded without its shipping GmBootMenu");
        EventInfo startRun = menu.GetType().GetEvent("OnStartRun");
        Assert.IsNotNull(startRun, "GmBootMenu no longer exposes its run-start result");
        startRun.AddEventHandler(menu, (Action<string>)RecordStartedScene);
        gamepad = InputSystem.AddDevice<Gamepad>();
    }

    [UnityTearDown]
    public IEnumerator RestorePlayerSaveAndInputDevices()
    {
        if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        if (secondGamepad != null && secondGamepad.added) InputSystem.RemoveDevice(secondGamepad);

        foreach (MonoBehaviour director in Behaviours("GmSceneDirector"))
            if (director != null) UnityEngine.Object.Destroy(director.gameObject);
        yield return null;

        StaticCall(TypeNamed("GmSaveSystem"), "Flush");
        StaticCall(TypeNamed("GmSaveSystem"), "ResetTestConfiguration");
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        StaticCall(TypeNamed("GmRunStore"), "BeginNewRun");
    }

    [UnityTest]
    public IEnumerator VirtualGamepadNavigatesAvailableRowsAndSubmitsNewRun()
    {
        Assert.IsFalse(Property<bool>(menu, "ContinueAvailable"),
            "the fixture left a save on disk, so it cannot prove Continue is skipped");
        Assert.AreEqual("NewRun", Property<object>(menu, "Focused").ToString());

        Press(GamepadButton.DpadDown);
        yield return null;
        Assert.AreEqual("Settings", Property<object>(menu, "Focused").ToString(),
            "D-pad down did not skip unavailable Continue and focus Settings");

        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.DpadUp);
        yield return null;
        Assert.AreEqual("NewRun", Property<object>(menu, "Focused").ToString(),
            "D-pad up did not return focus to New Run");

        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.South);
        yield return null;
        Assert.AreEqual("wend-hill-prologue", startedSceneId,
            "buttonSouth did not activate New Run through the menu input actions");
    }

    [UnityTest]
    public IEnumerator VirtualGamepadOpensSettingsAndChangesEveryAccessibilityControl()
    {
        Press(GamepadButton.DpadDown);
        yield return null;
        Assert.AreEqual("Settings", Property<object>(menu, "Focused").ToString(),
            "Settings is not reachable before starting gameplay");

        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.South);
        yield return null;
        UIDocument document = menu.GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        Assert.That(root.Q<VisualElement>("BootSettingsPanel").resolvedStyle.display,
            Is.EqualTo(DisplayStyle.Flex));
        bool captionsBefore = root.Q<Toggle>("CaptionsToggle").value;
        bool reducedBefore = root.Q<Toggle>("ReduceMotionToggle").value;
        bool vibrationBefore = root.Q<Toggle>("VibrationToggle").value;
        bool monoBefore = root.Q<Toggle>("MonoAudioToggle").value;
        bool contrastBefore = root.Q<Toggle>("HighContrastToggle").value;
        float scaleBefore = root.Q<Slider>("TextScaleSlider").value;

        // Captions, reduced motion, vibration, mono audio and high contrast are all activated by
        // one South press. Each Down is released first to prove held-input debounce.
        for (int control = 0; control < 5; control++)
        {
            ReleaseGamepad();
            yield return null;
            Press(GamepadButton.South);
            yield return null;
            if (control == 4) break;
            ReleaseGamepad();
            yield return null;
            Press(GamepadButton.DpadDown);
            yield return null;
        }
        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.DpadDown);
        yield return null;
        ReleaseGamepad();
        yield return null;
        Press(scaleBefore >= 1.95f ? GamepadButton.DpadLeft : GamepadButton.DpadRight);
        yield return null;

        Assert.That(root.Q<Toggle>("CaptionsToggle").value, Is.Not.EqualTo(captionsBefore));
        Assert.That(root.Q<Toggle>("ReduceMotionToggle").value, Is.Not.EqualTo(reducedBefore));
        Assert.That(root.Q<Toggle>("VibrationToggle").value, Is.Not.EqualTo(vibrationBefore));
        Assert.That(root.Q<Toggle>("MonoAudioToggle").value, Is.Not.EqualTo(monoBefore));
        Assert.That(root.Q<Toggle>("HighContrastToggle").value, Is.Not.EqualTo(contrastBefore));
        Assert.That(root.Q<Slider>("TextScaleSlider").value,
            Is.Not.EqualTo(scaleBefore).Within(0.001f));

        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.East);
        yield return null;
        Assert.That(root.Q<VisualElement>("BootSettingsPanel").resolvedStyle.display,
            Is.EqualTo(DisplayStyle.None), "Cancel did not close Settings");
    }

    [UnityTest]
    public IEnumerator ColdBootImportsPreferencesAndCarriesThemThroughNewRunIntoPrologue()
    {
        Type accessibility = TypeNamed("GmAccessibilitySettings");
        StaticCall(accessibility, "SetTextScale", 2f);
        StaticCall(accessibility, "SetHighContrast", true);
        Assert.That((bool)StaticCall(TypeNamed("GmSaveSystem"), "Save"), Is.True);
        accessibility.GetMethod("ResetToDefaultsForTests",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);

        SceneManager.LoadScene("Boot", LoadSceneMode.Single);
        yield return null;
        yield return null;
        menu = Behaviours("GmBootMenu").Single();
        UIDocument bootDocument = menu.GetComponent<UIDocument>();
        Assert.That(bootDocument.rootVisualElement.Q<Label>("BootTitle").style.fontSize.value.value,
            Is.EqualTo(92f).Within(0.01f), "cold Boot did not import durable text scale");
        Assert.That(bootDocument.rootVisualElement.ClassListContains("gm-high-contrast"), Is.True,
            "cold Boot did not import durable high contrast");

        // A durable file makes Continue the initial row. Move once to New Run, then use the real
        // controller Submit binding and let the shipping curtain/director perform the scene load.
        Press(GamepadButton.DpadDown);
        yield return null;
        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.South);
        for (int frame = 0; frame < 180 && SceneManager.GetActiveScene().name == "Boot"; frame++)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WendHill_Prologue"));
        yield return null;
        yield return null;

        MonoBehaviour prologueHud = Behaviours("GmPrologueHud").Single();
        UIDocument prologueDocument = prologueHud.GetComponent<UIDocument>();
        Assert.That(prologueDocument.rootVisualElement.Q<Label>("CardBody").style.fontSize.value.value,
            Is.EqualTo(76f).Within(0.01f), "Prologue discarded Boot's durable text scale");
        Assert.That(prologueDocument.rootVisualElement.ClassListContains("gm-high-contrast"), Is.True,
            "Prologue discarded Boot's durable high contrast");
        Assert.That(Behaviours("GmPauseMenu"), Has.Length.EqualTo(1),
            "Prologue must expose exactly one common pause/settings menu");
    }

    [UnityTest]
    public IEnumerator VirtualGamepadCancelReturnsToNewRunWithoutActing()
    {
        Press(GamepadButton.DpadDown);
        yield return null;
        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.DpadDown);
        yield return null;
        Assert.AreEqual("Quit", Property<object>(menu, "Focused").ToString());

        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.East);
        yield return null;

        Assert.AreEqual("NewRun", Property<object>(menu, "Focused").ToString());
        Assert.IsNull(startedSceneId, "Cancel started a run instead of returning to New Run");
        Assert.IsFalse(Property<bool>(menu, "QuitRequested"), "Cancel quit the game");

        ReleaseGamepad();
        yield return null;
        Press(GamepadButton.East);
        yield return null;
        Assert.AreEqual("NewRun", Property<object>(menu, "Focused").ToString(),
            "Cancel on New Run did not fail safely in place");
    }

    [UnityTest]
    public IEnumerator VirtualGamepadQuitUsesTheExistingQuitPath()
    {
        Press(GamepadButton.North);
        yield return null;

        Assert.IsTrue(Property<bool>(menu, "QuitRequested"),
            "buttonNorth did not invoke GmBootMenu.Quit through the menu input actions");
    }

    [UnityTest]
    public IEnumerator DisabledMenuIgnoresNavigate()
    {
        menu.enabled = false;
        yield return null;

        Press(GamepadButton.DpadDown);
        yield return null;

        Assert.AreEqual("NewRun", Property<object>(menu, "Focused").ToString(),
            "a disabled Boot menu still processed Navigate");
    }

    [UnityTest]
    public IEnumerator DisabledMenuIgnoresSubmit()
    {
        menu.enabled = false;
        yield return null;

        Press(GamepadButton.South);
        yield return null;

        Assert.IsNull(startedSceneId, "a disabled Boot menu still processed Submit");
    }

    [UnityTest]
    public IEnumerator DisabledMenuIgnoresQuit()
    {
        menu.enabled = false;
        yield return null;

        Press(GamepadButton.North);
        yield return null;

        Assert.IsFalse(Property<bool>(menu, "QuitRequested"),
            "a disabled Boot menu still processed Quit");
    }

    [UnityTest]
    public IEnumerator ReenabledMenuProcessesSubmitExactlyOnce()
    {
        menu.enabled = false;
        yield return null;
        menu.enabled = true;
        yield return null;

        Press(GamepadButton.South);
        yield return null;

        Assert.AreEqual(1, startedSceneCount,
            "disable/re-enable duplicated the input callbacks or failed to restore them");
    }

    [UnityTest]
    public IEnumerator AnalogMagnitudeChangesAboveThresholdDoNotRepeatNavigation()
    {
        MoveStick(gamepad, new Vector2(0f, -0.8f));
        yield return null;
        Assert.AreEqual("Settings", Property<object>(menu, "Focused").ToString());

        MoveStick(gamepad, new Vector2(0f, -0.85f));
        yield return null;
        MoveStick(gamepad, new Vector2(0f, -0.95f));
        yield return null;

        Assert.AreEqual("Settings", Property<object>(menu, "Focused").ToString(),
            "analog magnitude changes repeated navigation while the stick remained held");
    }

    [UnityTest]
    public IEnumerator HorizontalDeviceChangesCannotReleaseAnotherDevicesVerticalLatch()
    {
        secondGamepad = InputSystem.AddDevice<Gamepad>();
        MoveStick(gamepad, new Vector2(0f, -0.8f));
        yield return null;
        Assert.AreEqual("Settings", Property<object>(menu, "Focused").ToString());

        MoveStick(secondGamepad, new Vector2(0.9f, 0f));
        yield return null;
        MoveStick(gamepad, new Vector2(0f, -0.95f));
        yield return null;

        Assert.AreEqual("Settings", Property<object>(menu, "Focused").ToString(),
            "horizontal input released a vertical latch that was still physically held");
    }

    void Press(GamepadButton button) =>
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));

    void ReleaseGamepad() => InputSystem.QueueStateEvent(gamepad, new GamepadState());

    static void MoveStick(Gamepad target, Vector2 value) =>
        InputSystem.QueueStateEvent(target, new GamepadState { leftStick = value });

    void RecordStartedScene(string sceneId)
    {
        startedSceneId = sceneId;
        startedSceneCount++;
    }

    static MonoBehaviour[] Behaviours(string typeName) =>
        UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(item => item != null && item.GetType().Name == typeName)
            .ToArray();

    static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name, false))
        .FirstOrDefault(type => type != null) ?? throw new InvalidOperationException($"type '{name}' was not loaded");

    static object StaticProperty(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ??
        throw new MissingMemberException(type.Name, name);

    static object StaticCall(Type type, string method, params object[] args)
    {
        MethodInfo info = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(candidate => candidate.Name == method &&
                candidate.GetParameters().Length == args.Length);
        if (info == null) throw new MissingMethodException(type.Name, method);
        return info.Invoke(null, args);
    }

    static T Property<T>(object target, string name) =>
        (T)(target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target) ??
            throw new MissingMemberException(target.GetType().Name, name));

}
