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

public sealed class GmBonesInputPlayModeTests
{
    string directory;
    GameObject graph;
    Gamepad pad;
    object controller;
    MonoBehaviour input;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-bones-play-" + Guid.NewGuid().ToString("N"));
        StaticCall("GmSaveSystem", "ConfigureForTests", Path.Combine(directory, "save.json"), null);
        StaticCall("GmRunSeed", "ForceForReview", 7711);
        StaticCall("GmRunStore", "BeginNewRun");
        Type controllerType = TypeNamed("GmBonesController");
        controller = Activator.CreateInstance(controllerType);
        controllerType.GetMethod("InitializeOrRestore").Invoke(controller, null);
        graph = new GameObject("BonesEmptyGraph");
        input = (MonoBehaviour)graph.AddComponent(TypeNamed("GmBonesInput"));
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
        Assert.That(((string[])snapshot.GetType().GetField("actionJournal").GetValue(snapshot))[0],
            Is.EqualTo("round-1:press:0"));
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
    public IEnumerator StartAndSouthConsumePauseBeforeBonesUpdateCanConfirm()
    {
        string fingerprint = Fingerprint(controller);
        InputSystem.QueueStateEvent(pad, new GamepadState()
            .WithButton(GamepadButton.Start).WithButton(GamepadButton.South)
            .WithButton(GamepadButton.DpadRight));
        InputSystem.Update();
        Invoke(input, "Update"); // Explicitly prove the dangerous Bones-first ordering.
        yield return null;
        Assert.That(Property<int>(controller, "PlayerDecisionCount"), Is.Zero);
        Assert.That(Property<int>(controller, "FocusIndex"), Is.Zero);
        Assert.That(Fingerprint(controller), Is.EqualTo(fingerprint));
    }

    [UnityTest]
    public IEnumerator StartAndWestConsumePauseBeforeBonesUpdateCanChallenge()
    {
        ConfigurePendingLoadedSix();
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

    void ConfigurePendingLoadedSix()
    {
        Type matchType = TypeNamed("GmBonesMatch");
        int[] dice = { 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2 };
        object match = Activator.CreateInstance(matchType, new object[] { 3UL, dice });
        object bank = Enum.Parse(TypeNamed("GmBonesChoice"), "Bank");
        MethodInfo choose = matchType.GetMethod("TryChoose");
        for (int index = 0; index < 3; index++)
            choose.Invoke(match, new object[] { bank, -1, null });
        object save = Activator.CreateInstance(TypeNamed("GmSaveData"));
        save.GetType().GetField("bonesMatch").SetValue(save,
            matchType.GetMethod("ExportSnapshot").Invoke(match, null));
        StaticCall("GmRunStore", "LoadFromSaveData", save);
        controller = Activator.CreateInstance(TypeNamed("GmBonesController"));
        controller.GetType().GetMethod("InitializeOrRestore").Invoke(controller, null);
        input.GetType().GetMethod("ConfigureForTests").Invoke(input,
            new object[] { controller, (Func<bool>)(() => false) });
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
