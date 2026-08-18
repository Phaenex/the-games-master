using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GmParlorReviewProbePlayModeTests
{
    string directory;
    Gamepad gamepad;
    int ruleStateEvents;

    [UnitySetUp]
    public IEnumerator LoadSavedParlorWithIsolatedSave()
    {
        directory = Path.Combine(Path.GetTempPath(),
            "gm-parlor-review-play-" + Guid.NewGuid().ToString("N"));
        StaticCall("GmSaveSystem", "ConfigureForTests", Path.Combine(directory, "save.json"), null);
        StaticCall("GmAccessibilitySettings", "ResetToDefaultsForTests");
        StaticCall("GmRunStore", "BeginNewRun");
        StaticCall("GmRunStore", "RecordCatch", "parlor-review-input-unlock");
        gamepad = InputSystem.AddDevice<Gamepad>();
        SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator SavedSceneSettingsSouthEdgeTogglesAndPersistsExactlyOnce()
    {
        Component player = Find("GmPlayer");
        Component[] pauseMenus = UnityEngine.Object
            .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(item => item != null && item.GetType().Name == "GmPauseMenu")
            .Cast<Component>()
            .ToArray();
        Assert.That(pauseMenus, Has.Length.EqualTo(1),
            "saved Parlor instantiated duplicate shipping pause-menu input owners");
        Component pauseMenu = pauseMenus[0];

        Press(GamepadButton.Start);
        yield return null;
        Release();
        yield return null;
        Assert.That((bool)Property(player, "IsPaused"), Is.True,
            "Start did not enter the authoritative shipping pause state");

        for (int tab = 0; tab < 3; tab++)
        {
            Press(GamepadButton.DpadRight);
            yield return null;
            Release();
            yield return null;
        }
        Assert.That(Property(pauseMenu, "ActiveTab").ToString(), Is.EqualTo("Settings"));

        Press(GamepadButton.South);
        yield return null;
        Release();
        yield return null;
        Assert.That((bool)Property(pauseMenu, "SettingsFocusActive"), Is.True,
            "South did not enter the shipping Settings controls");
        Assert.That((int)Property(pauseMenu, "SettingsFocusIndex"), Is.Zero,
            "Settings did not begin on Captions");
        Assert.That(StaticBoolProperty("GmAccessibilitySettings", "Captions"), Is.False);

        int changeEvents = 0;
        Action changed = () => changeEvents++;
        EventInfo onChanged = TypeNamed("GmAccessibilitySettings").GetEvent("OnChanged",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(onChanged, Is.Not.Null);
        onChanged.AddEventHandler(null, changed);
        long writesBefore = PreferencesDurableGeneration();
        try
        {
            Press(GamepadButton.South);
            yield return null;
            Release();
            yield return null;

            Assert.That(StaticBoolProperty("GmAccessibilitySettings", "Captions"), Is.True,
                "one South edge toggled Captions twice or did not reach the shipping control");
            Assert.That(changeEvents, Is.EqualTo(1),
                "one South edge produced more than one accessibility state change");
            Assert.That((bool)Property(pauseMenu, "HasPendingSettingsSave"), Is.True);

            Press(GamepadButton.East);
            yield return null;
            Release();
            yield return null;

            Assert.That((bool)Property(pauseMenu, "SettingsFocusActive"), Is.False);
            Assert.That((bool)Property(pauseMenu, "HasPendingSettingsSave"), Is.False,
                "leaving Settings did not flush the controller-authored preference");
            Assert.That(PreferencesDurableGeneration(), Is.EqualTo(writesBefore + 1),
                "one Settings change did not produce exactly one durable preferences write");
            Assert.That(File.Exists(Path.Combine(directory, "preferences.json")), Is.True);
            StringAssert.Contains("\"captions\": true",
                File.ReadAllText(Path.Combine(directory, "preferences.json")));
        }
        finally
        {
            onChanged.RemoveEventHandler(null, changed);
        }
    }

    [UnityTest]
    public IEnumerator SameFrameInteractAndCallTellAlwaysConsumesReadBeforeInteract()
    {
        Component rules = Find("GmParlorRules");
        Component controller = Find("GmParlorController");
        Component focus = Find("GmParlorFocusView");
        AttachRuleStateCounter(rules);

        Press(GamepadButton.South);
        yield return null;
        Release();
        yield return null;
        Assert.That((bool)Property(focus, "IsOpen"), Is.True);
        string selectionState = PublicState(rules);
        StaticCall("GmSaveSystem", "Flush");
        long selectionWrite = StaticLongProperty("GmSaveSystem", "DurableGeneration");
        ruleStateEvents = 0;

        Chord(GamepadButton.South, GamepadButton.West);
        yield return null;

        Assert.That(PublicState(rules), Is.EqualTo(selectionState));
        Assert.That(Property(Property(rules, "Match"), "Phase").ToString(),
            Is.EqualTo("PlayerLeads"));
        Assert.That((bool)Property(focus, "IsOpen"), Is.True,
            "selection chord consumed Interact after CallTell");
        StringAssert.Contains("closed", ((string)Property(controller,
            "LastPlayerFeedback")).ToLowerInvariant());
        Assert.That(ruleStateEvents, Is.Zero);
        Assert.That(StaticLongProperty("GmSaveSystem", "DurableGeneration"),
            Is.EqualTo(selectionWrite));

        Release();
        yield return null;
        Press(GamepadButton.South);
        yield return null;
        Release();
        yield return null;
        Assert.That(Property(Property(rules, "Match"), "Phase").ToString(),
            Is.EqualTo("AwaitingAldricJudgement"));
        yield return CancelPresentationThroughGamepad();
        string judgementState = PublicState(rules);
        ruleStateEvents = 0;

        Chord(GamepadButton.South, GamepadButton.West);
        yield return null;

        Assert.That(PublicState(rules), Is.Not.EqualTo(judgementState));
        Assert.That(Property(Property(rules, "Match"), "Phase").ToString(),
            Is.EqualTo("TrickResult"));
        StringAssert.Contains("honest", ((string)Property(controller,
            "LastPlayerFeedback")).ToLowerInvariant(),
            "judgement chord did not resolve through the Read result");
        Assert.That(ruleStateEvents, Is.EqualTo(1),
            "one chord produced more than one canonical transition");
    }

    [UnityTest]
    public IEnumerator CancelCoversEveryPhaseOfBothPlayerAndAldricCardMotion()
    {
        string[] phases = { "Approach", "Contact", "Manipulate", "Release", "Settle" };
        for (int owner = 0; owner < 2; owner++)
        {
            foreach (string phase in phases)
            {
                yield return ReloadFreshParlor();
                Component rules = Find("GmParlorRules");
                Component presentation = Find("GmParlorPresentationCoordinator");
                AttachRuleStateCounter(rules);
                yield return StartCardPlayThroughGamepad();
                yield return WaitForMotion(presentation, owner, phase);
                Assert.That(Property(presentation, "CurrentMotionPhase").ToString(),
                    Is.EqualTo(phase), $"{(owner == 0 ? "player" : "Aldric")} {phase} was not live");
                string canonical = PublicState(rules);
                string beforeTransform = CardTransformHash();
                StaticCall("GmSaveSystem", "Flush");
                long writes = StaticLongProperty("GmSaveSystem", "DurableGeneration");
                ruleStateEvents = 0;

                Press(GamepadButton.East);
                yield return null;
                Release();
                yield return null;

                Assert.That((bool)Property(presentation, "IsBlocking"), Is.False,
                    $"Cancel did not finish {(owner == 0 ? "player" : "Aldric")} {phase}");
                Assert.That(Property(presentation, "CurrentMotionPhase").ToString(),
                    Is.EqualTo("Complete"));
                Assert.That(PublicState(rules), Is.EqualTo(canonical));
                Assert.That(ruleStateEvents, Is.Zero);
                Assert.That(StaticLongProperty("GmSaveSystem", "DurableGeneration"),
                    Is.EqualTo(writes));
                Assert.That(CardTransformHash(), Is.Not.Empty);
                if (phase != "Settle")
                    Assert.That(CardTransformHash(), Is.Not.EqualTo(beforeTransform),
                        "Cancel did not visibly settle the moving card");
            }
        }
    }

    [UnityTest]
    public IEnumerator CancelWithFocusOpenOnlyClosesFocus()
    {
        Component rules = Find("GmParlorRules");
        Component focus = Find("GmParlorFocusView");
        Component presentation = Find("GmParlorPresentationCoordinator");
        AttachRuleStateCounter(rules);
        Press(GamepadButton.South);
        yield return null;
        Release();
        yield return null;
        Assert.That((bool)Property(focus, "IsOpen"), Is.True);
        string canonical = PublicState(rules);
        string transforms = CardTransformHash();
        StaticCall("GmSaveSystem", "Flush");
        long writes = StaticLongProperty("GmSaveSystem", "DurableGeneration");
        ruleStateEvents = 0;

        Press(GamepadButton.East);
        yield return null;
        Release();
        yield return null;

        Assert.That((bool)Property(focus, "IsOpen"), Is.False);
        Assert.That((bool)Property(presentation, "IsBlocking"), Is.False);
        Assert.That(PublicState(rules), Is.EqualTo(canonical));
        Assert.That(CardTransformHash(), Is.EqualTo(transforms));
        Assert.That(ruleStateEvents, Is.Zero);
        Assert.That(StaticLongProperty("GmSaveSystem", "DurableGeneration"), Is.EqualTo(writes));
    }

    [UnityTest]
    public IEnumerator StartPauseFreezesActiveJudgementMotionAndCaptionThenResumes()
    {
        StaticCall("GmAccessibilitySettings", "SetCaptions", true);
        StaticCall("GmAccessibilitySettings", "FlushPendingSave");
        Component rules = Find("GmParlorRules");
        Component presentation = Find("GmParlorPresentationCoordinator");
        Component player = Find("GmPlayer");
        AttachRuleStateCounter(rules);
        yield return StartCardPlayThroughGamepad();
        yield return WaitForMotion(presentation, 1, "Manipulate");
        string canonical = PublicState(rules);
        StaticCall("GmSaveSystem", "Flush");
        long writes = StaticLongProperty("GmSaveSystem", "DurableGeneration");
        ruleStateEvents = 0;

        Press(GamepadButton.Start);
        yield return null;
        Release();
        Assert.That((bool)Property(player, "IsPaused"), Is.True);
        Assert.That((bool)Property(presentation, "IsBlocking"), Is.True,
            "Start did not pause an actually active card presentation");
        string transforms = CardTransformHash();
        string motion = Property(presentation, "CurrentMotionPhase").ToString();
        yield return WaitRealtime(0.25d);

        Assert.That(PublicState(rules), Is.EqualTo(canonical));
        Assert.That(CardTransformHash(), Is.EqualTo(transforms));
        Assert.That(Property(presentation, "CurrentMotionPhase").ToString(), Is.EqualTo(motion));
        Assert.That(ruleStateEvents, Is.Zero);
        Assert.That(StaticLongProperty("GmSaveSystem", "DurableGeneration"), Is.EqualTo(writes));

        Press(GamepadButton.Start);
        yield return null;
        Release();
        yield return WaitUntilRealtime(() => (bool)Property(presentation, "HasActiveCaption"), 4d,
            "judgement caption never became active after resume");
        string caption = (string)Property(presentation, "ActiveCaptionText");
        Assert.That(caption, Is.Not.Empty);
        long captionWrites = StaticLongProperty("GmSaveSystem", "DurableGeneration");
        Assert.That(captionWrites, Is.EqualTo(writes + 1),
            "opening the observed tell should persist exactly one evidence acknowledgement");
        Press(GamepadButton.Start);
        yield return null;
        Release();
        Assert.That((bool)Property(player, "IsPaused"), Is.True);
        string captionTransforms = CardTransformHash();
        yield return WaitRealtime(1d);
        Assert.That((string)Property(presentation, "ActiveCaptionText"), Is.EqualTo(caption));
        Assert.That((bool)Property(presentation, "HasActiveCaption"), Is.True);
        Assert.That(PublicState(rules), Is.EqualTo(canonical));
        Assert.That(CardTransformHash(), Is.EqualTo(captionTransforms));
        Assert.That(ruleStateEvents, Is.Zero);
        Assert.That(StaticLongProperty("GmSaveSystem", "DurableGeneration"),
            Is.EqualTo(captionWrites));

        Press(GamepadButton.Start);
        yield return null;
        Release();
        yield return WaitUntilRealtime(() => !(bool)Property(presentation, "HasActiveCaption"), 4d,
            "caption timer did not resume after unpause");
        Assert.That(PublicState(rules), Is.EqualTo(canonical));
        Assert.That(StaticLongProperty("GmSaveSystem", "DurableGeneration"),
            Is.EqualTo(captionWrites));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        StaticCall("GmAccessibilitySettings", "SetCaptions", false);
        StaticCall("GmSaveSystem", "Flush");
        StaticCall("GmSaveSystem", "ResetTestConfiguration");
        StaticCall("GmRunStore", "BeginNewRun");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SavedSceneVirtualGamepadSurvivesHeldInputDisconnectPauseAndQuitFromFocus()
    {
        Component probe = Find("GmParlorReviewProbe");
        Component controller = Find("GmParlorController");
        Component focus = Find("GmParlorFocusView");
        Component rules = Find("GmParlorRules");
        Component player = Find("GmPlayer");
        Assert.That((bool)Property(probe, "IsConfigured"), Is.True);
        Assert.That((bool)Property(probe, "IsArmed"), Is.False);

        Release();
        Press(GamepadButton.DpadRight);
        yield return null;
        int once = (int)Property(controller, "FocusedCardIndex");
        Press(GamepadButton.DpadRight);
        yield return null;
        Press(GamepadButton.DpadRight);
        yield return null;
        Assert.That((int)Property(controller, "FocusedCardIndex"), Is.EqualTo(once),
            "held D-pad repeated in the saved scene");

        InputSystem.RemoveDevice(gamepad);
        yield return null;
        gamepad = InputSystem.AddDevice<Gamepad>();
        Release();
        yield return null;
        Press(GamepadButton.DpadRight);
        yield return null;
        Assert.That((int)Property(controller, "FocusedCardIndex"), Is.Not.EqualTo(once),
            "replacement controller did not regain deterministic focus ownership");

        object[] stage = { true, null };
        Assert.That((bool)Call(probe, "StageFirstJudgement", stage), Is.True, stage[1] as string);
        string beforePause = (string)Property(Property(rules, "Match"), "PublicStateBytes");
        Call(focus, "Open");
        Assert.That((bool)Property(focus, "IsOpen"), Is.True);
        Release();
        yield return null;
        Press(GamepadButton.Start);
        yield return null;
        Assert.That((bool)Property(player, "IsPaused"), Is.True);

        InputSystem.QueueStateEvent(gamepad, new GamepadState()
            .WithButton(GamepadButton.South)
            .WithButton(GamepadButton.West));
        yield return null;
        Assert.That((string)Property(Property(rules, "Match"), "PublicStateBytes"),
            Is.EqualTo(beforePause), "paused Interact+CallTell mutated canonical state");
        Assert.That((bool)Property(focus, "IsOpen"), Is.True,
            "paused gameplay chord closed the focus surface");

        Release();
        yield return null;
        Press(GamepadButton.North);
        yield return null;
        Assert.That((bool)Property(player, "QuitRequested"), Is.True,
            "Quit from an open focus surface did not use the common paused quit path");
    }

    void Press(GamepadButton button) =>
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));

    void Chord(GamepadButton first, GamepadButton second) =>
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(first).WithButton(second));

    void Release() => InputSystem.QueueStateEvent(gamepad, new GamepadState());

    IEnumerator ReloadFreshParlor()
    {
        StaticCall("GmRunStore", "BeginNewRun");
        StaticCall("GmRunStore", "RecordCatch", "parlor-review-input-unlock");
        SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;
        Release();
        yield return null;
    }

    IEnumerator StartCardPlayThroughGamepad()
    {
        Release();
        yield return null;
        Press(GamepadButton.South);
        yield return null;
        Release();
        yield return null;
        Press(GamepadButton.South);
        yield return null;
        Release();
    }

    IEnumerator CancelPresentationThroughGamepad()
    {
        Component presentation = Find("GmParlorPresentationCoordinator");
        if (!(bool)Property(presentation, "IsBlocking")) yield break;
        Press(GamepadButton.East);
        yield return null;
        Release();
        yield return null;
        Assert.That((bool)Property(presentation, "IsBlocking"), Is.False);
    }

    IEnumerator WaitForMotion(Component presentation, int motionIndex, string expected)
    {
        string expectedAction = motionIndex == 0 ? "PlayerCardToLead" : "AldricCardToFollow";
        var observed = new StringBuilder();
        string previous = string.Empty;
        double deadline = Time.realtimeSinceStartupAsDouble + 4d;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            string phase = Property(presentation, "CurrentMotionPhase").ToString();
            object actionValue = NullableProperty(presentation, "CurrentPresentationAction");
            string action = actionValue?.ToString() ?? string.Empty;
            string current = action + "/" + phase;
            if (current != previous)
            {
                if (observed.Length > 0) observed.Append(", ");
                observed.Append(current);
                previous = current;
            }
            if (action == expectedAction && phase == expected) yield break;
            yield return new WaitForSecondsRealtime(0.005f);
        }
        Component player = Find("GmPlayer");
        Assert.Fail($"motion {expectedAction}/{expected} was never observed; saw {observed}; " +
            $"paused={Property(player, "IsPaused")}, timeScale={Time.timeScale:R}, " +
            $"audioPause={AudioListener.pause}, unscaledDelta={Time.unscaledDeltaTime:R}");
    }

    static IEnumerator WaitRealtime(double seconds)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < deadline)
            yield return new WaitForSecondsRealtime(0.01f);
    }

    static IEnumerator WaitUntilRealtime(Func<bool> condition, double seconds, string failure)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            yield return new WaitForSecondsRealtime(0.005f);
        Assert.That(condition(), Is.True, failure);
    }

    void AttachRuleStateCounter(Component rules)
    {
        ruleStateEvents = 0;
        EventInfo found = rules.GetType().GetEvent("OnStateChanged");
        var callback = new Action(() => ruleStateEvents++);
        found.AddEventHandler(rules, callback);
    }

    static string PublicState(Component rules) =>
        (string)Property(Property(rules, "Match"), "PublicStateBytes");

    static string CardTransformHash()
    {
        Type type = TypeNamed("GmParlorCardView");
        string text = string.Join("\n", UnityEngine.Object.FindObjectsByType(type,
                FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>()
            .OrderBy(item => item.name, StringComparer.Ordinal)
            .Select(item => $"{item.name}|{item.transform.localPosition:R}|" +
                $"{item.transform.localRotation.eulerAngles:R}"));
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text))
            .Select(value => value.ToString("x2")));
    }

    static long StaticLongProperty(string type, string name) => Convert.ToInt64(TypeNamed(type)
        .GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?.GetValue(null) ?? throw new MissingMemberException(name));

    static bool StaticBoolProperty(string type, string name) => Convert.ToBoolean(TypeNamed(type)
        .GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?.GetValue(null) ?? throw new MissingMemberException(name));

    static long PreferencesDurableGeneration()
    {
        Type saveSystem = TypeNamed("GmSaveSystem");
        object writer = saveSystem.GetField("preferencesWriter",
            BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ??
            throw new MissingFieldException(saveSystem.Name, "preferencesWriter");
        return Convert.ToInt64(writer.GetType().GetProperty("DurableGeneration",
            BindingFlags.Public | BindingFlags.Instance)?.GetValue(writer) ??
            throw new MissingMemberException(writer.GetType().Name, "DurableGeneration"));
    }

    static Component Find(string typeName)
    {
        Type type = TypeNamed(typeName);
        Component found = UnityEngine.Object.FindObjectsByType(type,
            FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().SingleOrDefault();
        Assert.That(found, Is.Not.Null, $"saved Parlor has no {typeName}");
        return found;
    }

    static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name, false))
        .FirstOrDefault(type => type != null) ?? throw new InvalidOperationException(name);

    static object Property(object target, string name) => target.GetType()
        .GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
            BindingFlags.Static)?.GetValue(target) ?? throw new MissingMemberException(name);

    static object NullableProperty(object target, string name)
    {
        PropertyInfo found = target.GetType().GetProperty(name, BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) ??
            throw new MissingMemberException(name);
        return found.GetValue(target);
    }

    static object Call(object target, string method, params object[] arguments)
    {
        MethodInfo found = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance)
            .Where(candidate => candidate.Name == method)
            .First(candidate => candidate.GetParameters().Length == arguments.Length);
        return found.Invoke(target, arguments);
    }

    static object StaticCall(string type, string method, params object[] arguments)
    {
        MethodInfo found = TypeNamed(type).GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static)
            .Where(candidate => candidate.Name == method)
            .First(candidate => candidate.GetParameters().Length == arguments.Length);
        return found.Invoke(null, arguments);
    }
}
