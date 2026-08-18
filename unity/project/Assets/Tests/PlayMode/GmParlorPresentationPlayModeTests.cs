using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public sealed class GmParlorPresentationPlayModeTests
{
    [UnityTest]
    public IEnumerator LoadedParlorSelfConfiguresPhysicalTableControllerAndSharedInput()
    {
        SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
        yield return null;

        Component binder = Behaviour("GmParlorPropBinder");
        Component coordinator = Behaviour("GmParlorPresentationCoordinator");
        Component controller = Behaviour("GmParlorController");
        Component input = Behaviour("GmParlorInput");
        Component player = Behaviour("GmPlayer");
        Component presenter = Behaviour("GmParlorAldricPresenter");
        Component focus = Behaviour("GmParlorFocusView");
        Component hud = Behaviour("GmParlorHud");
        Component pause = Behaviour("GmPauseMenu");
        Behaviour("GmParlorEvidenceLog");
        Assert.That(Property<bool>(binder, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(coordinator, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(coordinator, "HasAldricPresenter"), Is.True);
        Assert.That(Property<bool>(presenter, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(presenter, "UsesSubstituteProxy"), Is.True);
        Assert.That(Property<bool>(presenter, "HasCharacterMotionChannel"), Is.False);
        Assert.That(Property<bool>(controller, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(input, "HasRequiredActions"), Is.True);
        Assert.That(Property<bool>(input, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(focus, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(hud, "IsConfigured"), Is.True);
        Assert.That(Property<bool>(hud, "HasUi"), Is.True);
        UIDocument pauseDocument = pause.GetComponent<UIDocument>();
        Assert.That(pauseDocument, Is.Not.Null, "shipping pause menu never built its UI document");
        Assert.That(pauseDocument.rootVisualElement.Q<VisualElement>("TabContentContainer"), Is.Not.Null);
        Assert.That(controller.GetType().GetMethod("Activate")?.Invoke(controller, null).ToString(),
            Is.EqualTo("None"), "the loaded scene needs an explicit activation boundary");
        focus.GetType().GetMethod("Open")?.Invoke(focus, null);
        hud.GetType().GetMethod("Refresh")?.Invoke(hud, null);
        UIDocument document = hud.GetComponent<UIDocument>();
        Assert.That(document, Is.Not.Null);
        Assert.That(document.rootVisualElement.Q<VisualElement>("ParlorFocusPanel")
            .resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        Assert.That(document.rootVisualElement.Q<VisualElement>("ParlorHand").childCount,
            Is.EqualTo(7));
        Assert.That(Property<bool>(player, "ControlBlocked"), Is.True);
        Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Count(component => component != null && component.GetType().Name == "GmParlorCardView"),
            Is.EqualTo(28));
        Assert.That(BehaviourOrNull("GmTheReadController"), Is.Null);
        Assert.That(BehaviourOrNull("GmHostAI"), Is.Null);
    }

    [UnityTest]
    public IEnumerator VirtualGamepadPausesOpensSettingsChangesAllControlsAndResumes()
    {
        Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
        try
        {
            SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Component pause = Behaviour("GmPauseMenu");
            UIDocument document = pause.GetComponent<UIDocument>();

            Press(gamepad, GamepadButton.Start);
            yield return null;
            yield return null;
            Assert.That(document.rootVisualElement.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                "Start did not open the shipping pause menu");

            for (int tab = 0; tab < 3; tab++)
            {
                Release(gamepad);
                yield return null;
                Press(gamepad, GamepadButton.DpadRight);
                yield return null;
            }
            Release(gamepad);
            yield return null;
            Press(gamepad, GamepadButton.South);
            yield return null;
            VisualElement root = document.rootVisualElement;
            Assert.That(root.Q<Toggle>("CaptionsToggle"), Is.Not.Null,
                "controller navigation never reached Settings");
            bool captionsBefore = root.Q<Toggle>("CaptionsToggle").value;
            bool reducedBefore = root.Q<Toggle>("ReduceMotionToggle").value;
            bool vibrationBefore = root.Q<Toggle>("VibrationToggle").value;
            bool monoBefore = root.Q<Toggle>("MonoAudioToggle").value;
            bool contrastBefore = root.Q<Toggle>("HighContrastToggle").value;
            float scaleBefore = root.Q<Slider>("TextScaleSlider").value;

            for (int control = 0; control < 5; control++)
            {
                Release(gamepad);
                yield return null;
                Press(gamepad, GamepadButton.South);
                yield return null;
                if (control == 4) break;
                Release(gamepad);
                yield return null;
                Press(gamepad, GamepadButton.DpadDown);
                yield return null;
            }
            Release(gamepad);
            yield return null;
            Press(gamepad, GamepadButton.DpadDown);
            yield return null;
            Release(gamepad);
            yield return null;
            Press(gamepad, scaleBefore >= 1.95f ? GamepadButton.DpadLeft : GamepadButton.DpadRight);
            yield return null;

            Assert.That(root.Q<Toggle>("CaptionsToggle").value, Is.Not.EqualTo(captionsBefore));
            Assert.That(root.Q<Toggle>("ReduceMotionToggle").value, Is.Not.EqualTo(reducedBefore));
            Assert.That(root.Q<Toggle>("VibrationToggle").value, Is.Not.EqualTo(vibrationBefore));
            Assert.That(root.Q<Toggle>("MonoAudioToggle").value, Is.Not.EqualTo(monoBefore));
            Assert.That(root.Q<Toggle>("HighContrastToggle").value, Is.Not.EqualTo(contrastBefore));
            Assert.That(root.Q<Slider>("TextScaleSlider").value,
                Is.Not.EqualTo(scaleBefore).Within(0.001f));

            Release(gamepad);
            yield return null;
            Press(gamepad, GamepadButton.East);
            yield return null;
            Release(gamepad);
            yield return null;
            Press(gamepad, GamepadButton.East);
            yield return null;
            Assert.That(document.rootVisualElement.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "Cancel was consumed by GmPlayer before the menu could exit and resume");
        }
        finally
        {
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        }
    }

    [UnityTest]
    public IEnumerator AdjacentFrameCancelResumesWithoutExecutionOrderDependence()
    {
        Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
        try
        {
            SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Component player = Behaviour("GmPlayer");

            Press(gamepad, GamepadButton.Start);
            yield return null;
            Press(gamepad, GamepadButton.East);
            yield return null;

            Assert.That(Property<bool>(player, "IsPaused"), Is.False,
                "adjacent-frame Cancel was dropped or double-consumed");
        }
        finally
        {
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        }
    }

    [UnityTest]
    public IEnumerator AdjacentFrameSubmitEntersRememberedSettingsWithoutAccidentalResume()
    {
        Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
        try
        {
            SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Component player = Behaviour("GmPlayer");
            Component pause = Behaviour("GmPauseMenu");
            UIDocument document = pause.GetComponent<UIDocument>();

            Press(gamepad, GamepadButton.Start);
            yield return null;
            for (int tab = 0; tab < 3; tab++)
            {
                Release(gamepad); yield return null;
                Press(gamepad, GamepadButton.DpadRight); yield return null;
            }
            Release(gamepad); yield return null;
            Press(gamepad, GamepadButton.East); yield return null;
            Assert.That(Property<bool>(player, "IsPaused"), Is.False);

            Press(gamepad, GamepadButton.Start);
            yield return null;
            Press(gamepad, GamepadButton.South);
            yield return null;

            Assert.That(Property<bool>(player, "IsPaused"), Is.True,
                "GmPlayer consumed adjacent Submit as Resume");
            Assert.That(document.rootVisualElement.Q<VisualElement>("CaptionsToggle")
                .ClassListContains("gm-controller-focus"), Is.True,
                "pause menu rejected the first adjacent-frame Submit");
        }
        finally
        {
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        }
    }

    static void Press(Gamepad gamepad, GamepadButton button) =>
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));

    static void Release(Gamepad gamepad) =>
        InputSystem.QueueStateEvent(gamepad, new GamepadState());

    static Component Behaviour(string typeName)
    {
        Component found = BehaviourOrNull(typeName);
        Assert.IsNotNull(found, $"{typeName} is missing from the loaded Parlor scene");
        return found;
    }

    static Component BehaviourOrNull(string typeName) =>
        Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .FirstOrDefault(component => component != null && component.GetType().Name == typeName);

    static T Property<T>(object target, string name) =>
        (T)(target.GetType().GetProperty(name)?.GetValue(target) ??
            throw new MissingMemberException(target.GetType().Name, name));
}
