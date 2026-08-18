using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// This asmdef cannot reference predefined Assembly-CSharp, so runtime types are exercised by name.
public sealed class GmParlorSaveRestorePlayModeTests
{
    string savePath;
    string directory;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "gm-parlor-play-" + Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(directory, "save.json");
        StaticCall("GmSaveSystem", "ConfigureForTests", savePath, null);
        StaticCall("GmRunStore", "BeginNewRun");
    }

    [TearDown]
    public void TearDown()
    {
        StaticCall("GmSaveSystem", "Flush");
        StaticCall("GmSaveSystem", "ResetTestConfiguration");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        StaticCall("GmRunStore", "BeginNewRun");
    }

    [UnityTest]
    public IEnumerator AwakeRestoresSavedMatchBeforeAControllerCanStartAFreshDeal()
    {
        object baseline = NewMatch(313);
        object snapshot = Call(baseline, "ExportSnapshot");
        object[] setArgs = { snapshot, null };
        Assert.That((bool)StaticCall("GmRunStore", "TrySetParlorMatch", setArgs), Is.True,
            setArgs[1] as string);

        GameObject go = new GameObject("runtime-parlor-rules");
        Component rules = go.AddComponent(TypeNamed("GmParlorRules"));
        yield return null;

        Assert.That(Property(rules, "LastInitializeResult").ToString(), Is.EqualTo("Restored"));
        object restored = Property(rules, "Match");
        Assert.That(Property(restored, "PublicStateBytes"),
            Is.EqualTo(Property(baseline, "PublicStateBytes")));
        string restoredState = (string)Property(restored, "PublicStateBytes");
        Call(rules, "StartGame", 999, 1, 0, false, false);
        Assert.That(Property(Property(rules, "Match"), "PublicStateBytes"), Is.EqualTo(restoredState),
            "a legacy controller Start call overwrote the restored match");
        UnityEngine.Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator AwakeRefusesCorruptSavedMatchInsteadOfSilentlyDealing()
    {
        object baseline = NewMatch(314);
        object corrupt = Call(baseline, "ExportSnapshot");
        corrupt.GetType().GetField("randomState").SetValue(corrupt, (uint)0);
        object saveData = Activator.CreateInstance(TypeNamed("GmSaveData"));
        saveData.GetType().GetField("parlorMatch").SetValue(saveData, corrupt);
        StaticCall("GmRunStore", "LoadFromSaveData", saveData);

        LogAssert.Expect(LogType.Error,
            "[GmParlorRules] Refusing corrupt saved match: random state cannot be zero");
        GameObject go = new GameObject("runtime-corrupt-parlor-rules");
        Component rules = go.AddComponent(TypeNamed("GmParlorRules"));
        yield return null;

        Assert.That(Property(rules, "LastInitializeResult").ToString(),
            Is.EqualTo("CorruptSavedState"));
        Assert.That(Property(rules, "Match"), Is.Null);
        Assert.That((bool)StaticProperty("GmRunStore", "HasParlorMatch"), Is.True);
        LogAssert.Expect(LogType.Error,
            "[GmParlorRules] StartGame refused while corrupt saved state is unresolved.");
        Call(rules, "StartGame", 999, 1, 0, false, false);
        Assert.That(Property(rules, "Match"), Is.Null);
        UnityEngine.Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator StartDoesNotDeliverRestoredPendingOutcomeBeforeExplicitActivation()
    {
        object pending = Activator.CreateInstance(TypeNamed("GmParlorMatch"), 315, 3, 5, true, (ulong)0);
        Call(pending, "Start");
        Call(pending, "PlayPlayerCard", 0);
        Call(pending, "ContinueJudgement");
        ulong sequence = (ulong)Property(pending, "OutcomeSequence");
        Assert.That((bool)Call(pending, "MarkOutcomeDurable", sequence), Is.True);
        object snapshot = Call(pending, "ExportSnapshot");
        object[] setArgs = { snapshot, null };
        Assert.That((bool)StaticCall("GmRunStore", "TrySetParlorMatch", setArgs), Is.True,
            setArgs[1] as string);

        GameObject go = new GameObject("restored-outcome-order");
        go.SetActive(false);
        Component rules = go.AddComponent(TypeNamed("GmParlorRules"));
        var probe = go.AddComponent<GmParlorOutcomeAwakeProbe>();
        probe.Rules = rules;
        go.SetActive(true);
        yield return null;

        Assert.That(probe.Received, Is.Zero,
            "Unity Start must not cross the explicit restore activation boundary");
        Call(rules, "ActivateRestoredOutcomes");
        Assert.That(probe.Received, Is.EqualTo(1));
        Call(rules, "ActivateRestoredOutcomes");
        Assert.That(probe.Received, Is.EqualTo(1));
        UnityEngine.Object.Destroy(go);
    }

    static object NewMatch(int seed)
    {
        object match = Activator.CreateInstance(TypeNamed("GmParlorMatch"), seed, 3, 5, true, (ulong)0);
        Call(match, "Start");
        Call(match, "PlayPlayerCard", 0);
        return match;
    }

    static Type TypeNamed(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new AssertionException("Runtime type not found: " + name);
    }

    static object Property(object target, string name) =>
        target.GetType().GetProperty(name).GetValue(target);

    static object StaticProperty(string type, string name) =>
        TypeNamed(type).GetProperty(name).GetValue(null);

    static object Call(object target, string name, params object[] args) =>
        Method(target.GetType(), name, args.Length).Invoke(target, args);

    static object StaticCall(string type, string name, params object[] args) =>
        Method(TypeNamed(type), name, args.Length).Invoke(null, args);

    static MethodInfo Method(Type type, string name, int parameterCount)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                     BindingFlags.Static))
            if (method.Name == name && method.GetParameters().Length == parameterCount) return method;
        throw new AssertionException($"Method not found: {type.Name}.{name}/{parameterCount}");
    }
}

public sealed class GmParlorOutcomeAwakeProbe : MonoBehaviour
{
    public Component Rules;
    public int Received { get; private set; }

    void Awake()
    {
        EventInfo outcomeEvent = Rules.GetType().GetEvent("OnOutcomeReady");
        Type outcomeType = outcomeEvent.EventHandlerType.GetGenericArguments()[1];
        MethodInfo subscribe = GetType().GetMethod(nameof(Subscribe),
            BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(outcomeType);
        subscribe.Invoke(this, new object[] { outcomeEvent });
    }

    void Subscribe<TOutcome>(EventInfo outcomeEvent)
    {
        Action<ulong, TOutcome> handler = (_, __) => Received++;
        outcomeEvent.AddEventHandler(Rules, handler);
    }
}
