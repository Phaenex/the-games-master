using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class GmStudyInputPlayModeTests
{
    string directory;
    GameObject graph;
    Gamepad pad;
    object controller;
    MonoBehaviour input;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-study-play-" + Guid.NewGuid().ToString("N"));
        StaticCall("GmSaveSystem", "ConfigureForTests", Path.Combine(directory, "save.json"), null);
        StaticCall("GmRunSeed", "ForceForReview", 4409);
        StaticCall("GmRunStore", "BeginNewRun");
        Type controllerType = TypeNamed("GmStudyController");
        controller = Activator.CreateInstance(controllerType);
        controllerType.GetMethod("InitializeOrRestore").Invoke(controller, null);
        graph = new GameObject("StudyEmptyGraph");
        input = (MonoBehaviour)graph.AddComponent(TypeNamed("GmStudyInput"));
        input.GetType().GetMethod("ConfigureForTests").Invoke(input,
            new object[] { controller, (Func<bool>)(() => false) });
        pad = InputSystem.AddDevice<Gamepad>();
        yield return null;
        Assert.That(Property<bool>(input, "HasRequiredActions"), Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        UnityEngine.Object.Destroy(graph);
        yield return null;
        StaticCall("GmSaveSystem", "ResetTestConfiguration");
        StaticCall("GmRunSeed", "ResetForTests");
        StaticCall("GmRunStore", "BeginNewRun");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [UnityTest]
    public IEnumerator VirtualGamepadDrivesSharedMoveAndInteractBindings()
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadRight));
        InputSystem.Update();
        yield return null;
        Assert.That(Property<int>(controller, "FocusIndex"), Is.EqualTo(1));
        InputSystem.QueueStateEvent(pad, new GamepadState());
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        InputSystem.Update();
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.EqualTo(1));
        object snapshot = Property<object>(controller, "Snapshot");
        string expectedActionId = WrongActionIdAt(0);
        Assert.That(((string[])snapshot.GetType().GetField("actionJournal").GetValue(snapshot))[0],
            Is.EqualTo(expectedActionId));
    }

    [UnityTest]
    public IEnumerator CancelChordNeverConfirms()
    {
        InputSystem.QueueStateEvent(pad, new GamepadState()
            .WithButton(GamepadButton.East).WithButton(GamepadButton.South)
            .WithButton(GamepadButton.DpadRight));
        InputSystem.Update();
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.Zero);
        Assert.That(Property<int>(controller, "FocusIndex"), Is.Zero,
            "Cancel did not consume the simultaneous navigation intent");
    }

    [UnityTest]
    public IEnumerator StartAndSouthConsumePauseBeforeStudyUpdateCanConfirm()
    {
        string fingerprint = Fingerprint(controller);
        InputSystem.QueueStateEvent(pad, new GamepadState()
            .WithButton(GamepadButton.Start).WithButton(GamepadButton.South)
            .WithButton(GamepadButton.DpadRight));
        InputSystem.Update();
        Invoke(input, "Update"); // Explicitly prove the dangerous Study-first ordering.
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.Zero);
        Assert.That(Property<int>(controller, "FocusIndex"), Is.Zero);
        Assert.That(Fingerprint(controller), Is.EqualTo(fingerprint));
    }

    [UnityTest]
    public IEnumerator StartAndWestConsumePauseBeforeStudyUpdateCanChallenge()
    {
        ConfigurePendingInterventionAtFinalPosition();
        string fingerprint = Fingerprint(controller);
        Assert.That(Property<object>(controller, "Phase").ToString(), Is.EqualTo("AwaitingIntervention"));
        InputSystem.QueueStateEvent(pad, new GamepadState()
            .WithButton(GamepadButton.Start).WithButton(GamepadButton.West));
        InputSystem.Update();
        Invoke(input, "Update");
        yield return null;
        Assert.That(Property<object>(controller, "Phase").ToString(), Is.EqualTo("AwaitingIntervention"));
        Assert.That(Fingerprint(controller), Is.EqualTo(fingerprint));
    }

    [UnityTest]
    public IEnumerator OwnedCloneDisablesReenablesAndDestroysWithoutMutatingSharedAsset()
    {
        InputActionAsset shared = Resources.Load<InputActionAsset>("Input/GmControls");
        string sharedJson = shared.ToJson();
        bool sharedEnabled = shared.FindActionMap("Gameplay", true).enabled;
        InputActionAsset owned = Field<InputActionAsset>(input, "controls");
        Assert.That(owned, Is.Not.SameAs(shared));
        Assert.That(Field<InputActionMap>(input, "gameplay").enabled, Is.True);

        input.enabled = false;
        Assert.That(Field<InputActionMap>(input, "gameplay").enabled, Is.False);
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        InputSystem.Update();
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.Zero);

        InputSystem.QueueStateEvent(pad, new GamepadState());
        InputSystem.Update();
        yield return null;
        input.enabled = true;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        InputSystem.Update();
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.EqualTo(1));
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.EqualTo(1),
            "held input repeated after re-enable");

        UnityEngine.Object.Destroy(input);
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState());
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        InputSystem.Update();
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.EqualTo(1));
        Assert.That(owned == null, Is.True, "owned action asset survived input destruction");
        Assert.That(shared.ToJson(), Is.EqualTo(sharedJson));
        Assert.That(shared.FindActionMap("Gameplay", true).enabled, Is.EqualTo(sharedEnabled));
    }

    void ConfigurePendingInterventionAtFinalPosition()
    {
        Type matchType = TypeNamed("GmStudyMatch");
        object match = Activator.CreateInstance(matchType, new object[] { 11UL });
        MethodInfo choose = matchType.GetMethod("TryChoose");
        choose.Invoke(match, new object[] { CorrectActionIdAt(0), null });
        choose.Invoke(match, new object[] { WrongActionIdAt(1), null });
        choose.Invoke(match, new object[] { CorrectActionIdAt(2), null });
        object save = Activator.CreateInstance(TypeNamed("GmSaveData"));
        save.GetType().GetField("studyMatch").SetValue(save,
            matchType.GetMethod("ExportSnapshot").Invoke(match, null));
        StaticCall("GmRunStore", "LoadFromSaveData", save);
        controller = Activator.CreateInstance(TypeNamed("GmStudyController"));
        controller.GetType().GetMethod("InitializeOrRestore").Invoke(controller, null);
        input.GetType().GetMethod("ConfigureForTests").Invoke(input,
            new object[] { controller, (Func<bool>)(() => false) });
    }

    static string CorrectActionIdAt(int positionIndex)
    {
        object position = TypeNamed("GmStudyRules").GetMethod("GetPosition")
            .Invoke(null, new object[] { positionIndex });
        Array cards = (Array)position.GetType().GetField("cards").GetValue(position);
        foreach (object card in cards)
            if ((bool)card.GetType().GetField("isCorrect").GetValue(card))
                return (string)card.GetType().GetField("actionId").GetValue(card);
        throw new InvalidOperationException("position " + positionIndex + " has no correct card");
    }

    static string WrongActionIdAt(int positionIndex)
    {
        object position = TypeNamed("GmStudyRules").GetMethod("GetPosition")
            .Invoke(null, new object[] { positionIndex });
        Array cards = (Array)position.GetType().GetField("cards").GetValue(position);
        foreach (object card in cards)
            if (!(bool)card.GetType().GetField("isCorrect").GetValue(card))
                return (string)card.GetType().GetField("actionId").GetValue(card);
        throw new InvalidOperationException("position " + positionIndex + " has no wrong card");
    }

    static string Fingerprint(object tableController)
    {
        object snapshot = Property<object>(tableController, "Snapshot");
        return (string)snapshot.GetType().GetField("stateFingerprint").GetValue(snapshot);
    }

    static void Invoke(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(assembly => { try { return assembly.GetTypes(); } catch { return Array.Empty<Type>(); } })
        .Single(type => type.Name == name);

    static object StaticCall(string type, string method, params object[] args)
    {
        MethodInfo selected = TypeNamed(type).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .First(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length);
        return selected.Invoke(null, args);
    }

    static T Property<T>(object target, string name) =>
        (T)target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(target);
}
