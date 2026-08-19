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
