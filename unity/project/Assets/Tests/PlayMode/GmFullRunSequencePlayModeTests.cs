// The room controllers had thorough isolated tests while the game itself still had no proven path
// between them. This test spends real scene loads on the contract a player actually needs: finish a
// room, cross its authored threshold, arrive in the next room, and preserve the run state all the
// way to a visible ending. Reflection is required because this asmdef cannot reference types in the
// predefined Assembly-CSharp assembly.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GmFullRunSequencePlayModeTests
{
    string saveDirectory;
    string savePath;

    [UnitySetUp]
    public IEnumerator StartAtTheFirstBuiltGame()
    {
        saveDirectory = Path.Combine(Path.GetTempPath(), "gm-fullrun-" + Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(saveDirectory, "save.json");
        StaticCall(TypeNamed("GmSaveSystem"), "ConfigureForTests", savePath, null);

        StaticCall(TypeNamed("GmRunStore"), "BeginNewRun");
        SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
        yield return null;
        yield return null;

        // Boot normally owns this persistent pair. Constructing it here keeps the proof focused on
        // the playable spine rather than paying for the boot menu and prologue a second time; those
        // crossings already have their own PlayMode coverage.
        var host = new GameObject("FullRunDirectorFixture");
        host.AddComponent(TypeNamed("GmSceneDirector"));
        host.AddComponent(TypeNamed("GmSceneCurtain"));
        Time.captureDeltaTime = 1f / 60f;
        Time.timeScale = 20f;
    }

    [UnityTearDown]
    public IEnumerator RestorePlayerSaveAndRuntime()
    {
        Time.timeScale = 1f;
        Time.captureDeltaTime = 0f;

        foreach (MonoBehaviour director in Behaviours("GmSceneDirector"))
            if (director != null) UnityEngine.Object.Destroy(director.gameObject);
        yield return null;

        StaticCall(TypeNamed("GmSaveSystem"), "Flush");
        StaticCall(TypeNamed("GmSaveSystem"), "ResetTestConfiguration");
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        StaticCall(TypeNamed("GmRunStore"), "BeginNewRun");
    }

    [UnityTest]
    public IEnumerator BuiltSpineCrossesEveryAuthoredThresholdAndResolvesAnEnding()
    {
        Assert.AreEqual("Parlor", SceneManager.GetActiveScene().name);
        Assert.IsNotNull(Behaviour("GmParlorRules"), "Parlor loaded without its real game rules");

        // The deterministic Parlor match itself is covered separately. This test banks the same
        // public run fact it emits so the expensive part can concentrate on real scene handoffs.
        Assert.IsTrue((bool)StaticCall(TypeNamed("GmRunStore"), "CompleteRoom", "parlor", true));
        Assert.IsTrue(Property<bool>(Behaviour("GmSequenceExit"), "IsUnlocked"),
            "finishing the Parlor did not release its physical doors");
        TriggerTo("court");
        yield return WaitForScene("Court");
        yield return WaitForArrival();

        MonoBehaviour court = Behaviour("GmCourtController");
        Assert.IsNotNull(court, "Court loaded without its verdict controller");
        Call(court, "StartHearing");
        for (int evidence = 0; evidence < 3; evidence++)
            Assert.IsTrue((bool)Call(court, "PresentEvidence", $"sequence-proof-{evidence}", false));
        Assert.AreEqual("Verdict", Property<object>(court, "Phase").ToString());
        Assert.IsTrue(StaticBool("GmRunStore", "IsRoomComplete", "court"));
        TriggerTo("shut-the-box");
        yield return WaitForScene("ShutTheBox");
        yield return WaitForArrival();

        MonoBehaviour shutBox = Behaviour("GmShutTheBoxController");
        Assert.IsNotNull(shutBox, "Shut the Box loaded without its match controller");
        Call(shutBox, "ResetMatch");
        OpenTileNine(shutBox);
        Assert.IsTrue(Property<bool>(shutBox, "SecretDoorUnlocked"));
        Assert.IsTrue(StaticBool("GmRunStore", "HasCatch", "stb-tile-9-door-latch"));

        MonoBehaviour hiddenTransition = TransitionTo("hidden-room");
        Call(hiddenTransition, "TriggerTransition");
        yield return null;
        Assert.AreEqual("ShutTheBox", SceneManager.GetActiveScene().name,
            "Tile 9 discarded the unfinished table game by loading the Hidden Room early");
        Assert.IsFalse(Property<bool>(hiddenTransition, "IsTriggered"),
            "an early Hidden Room attempt burned the transition's one shot");

        Assert.IsTrue((bool)Call(shutBox, "BankPlayerBox"));
        Assert.IsTrue((bool)Call(shutBox, "BankHostBox"));
        Assert.AreEqual("GameOver", Property<object>(shutBox, "Phase").ToString());
        Assert.IsTrue(StaticBool("GmRunStore", "IsRoomComplete", "shut-the-box"));
        Call(hiddenTransition, "TriggerTransition");
        yield return WaitForScene("HiddenRoom");
        yield return WaitForArrival();

        Assert.IsNotNull(Behaviour("GmHiddenRoomController"),
            "the optional route loaded a shell without its room mechanic");
        TriggerTo("labyrinth");
        yield return WaitForScene("Labyrinth");
        yield return WaitForArrival();
        Assert.IsTrue(StaticBool("GmRunStore", "IsRoomComplete", "hidden-room"),
            "leaving the Hidden Room did not bank its visit");

        MonoBehaviour ending = Behaviour("GmEndingTrigger");
        MonoBehaviour player = Behaviour("GmPlayer");
        Assert.IsNotNull(ending, "Labyrinth has no reachable ending threshold");
        Assert.IsNotNull(player, "Labyrinth loaded without a player body");
        object resolved = Call(ending, "TriggerEnding", player);
        Assert.AreNotEqual("None", resolved.ToString());
        Assert.IsTrue(Property<bool>(ending, "IsTriggered"));
        Assert.IsTrue(StaticBool("GmRunStore", "IsRoomComplete", "labyrinth"));
        Assert.AreEqual("ending", StaticProperty(TypeNamed("GmRunStore"), "LastCheckpoint"));
        Assert.AreEqual(2, StaticProperty(TypeNamed("GmRunStore"), "TableGameIndex"),
            "Court, Hidden Room, or Labyrinth was incorrectly counted as one of seven table games");

        MonoBehaviour curtain = Behaviour("GmSceneCurtain");
        Assert.IsNotNull(curtain);
        Assert.IsTrue(Property<bool>(curtain, "IsRaised"), "ending resolved without a visible final frame");
        Assert.IsNotEmpty(Property<string>(curtain, "Card"), "ending resolved without its title");
        Assert.IsTrue(Property<bool>(player, "ControlBlocked"), "player could walk through the resolved ending");
    }

    static void OpenTileNine(MonoBehaviour controller)
    {
        Type claimType = TypeNamed("GamesMaster.ShutTheBox.HoldClaim");
        Type truthType = TypeNamed("GamesMaster.ShutTheBox.HoldTruth");
        Type kindType = TypeNamed("GamesMaster.ShutTheBox.HoldKind");
        object claim = Activator.CreateInstance(claimType, new object[] { null, (int?)9 });
        object truth = Activator.CreateInstance(truthType,
            new object[] { false, null, (int?)9, true, true });
        object kind = Enum.Parse(kindType, "Tamper");
        object result = Call(controller, "ExecuteHold", 9, kind, claim, truth);
        Assert.IsTrue(Property<bool>(result, "OpensHiddenDoor"));
    }

    static void TriggerTo(string targetSceneId)
    {
        Call(TransitionTo(targetSceneId), "TriggerTransition");
    }

    static MonoBehaviour TransitionTo(string targetSceneId)
    {
        MonoBehaviour found = Behaviours("GmSceneTransitionTrigger")
            .FirstOrDefault(item => Field<string>(item, "TargetSceneId") == targetSceneId);
        Assert.IsNotNull(found, $"active scene has no authored transition to {targetSceneId}");
        return found;
    }

    static IEnumerator WaitForScene(string sceneName)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name,
            $"scene transition did not reach {sceneName}");

        deadline = Time.realtimeSinceStartup + 5f;
        MonoBehaviour director = Behaviour("GmSceneDirector");
        while (director != null && Property<bool>(director, "IsTransitioning") &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.IsNotNull(director, "persistent scene director was destroyed during a room load");
        Assert.IsFalse(Property<bool>(director, "IsTransitioning"),
            $"director never completed its handoff to {sceneName}");
    }

    static IEnumerator WaitForArrival()
    {
        MonoBehaviour arrival = Behaviour("GmSceneArrival");
        Assert.IsNotNull(arrival, "loaded room has no arrival recovery");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!Property<bool>(arrival, "HasArrived") && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.IsTrue(Property<bool>(arrival, "HasArrived"),
            "loaded room left the player behind the transition curtain");
    }

    static MonoBehaviour Behaviour(string typeName) => Behaviours(typeName).FirstOrDefault();

    static MonoBehaviour[] Behaviours(string typeName) =>
        UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item != null && item.GetType().Name == typeName).ToArray();

    static bool StaticBool(string typeName, string method, params object[] args) =>
        (bool)StaticCall(TypeNamed(typeName), method, args);

    static object StaticCall(Type type, string method, params object[] args)
    {
        MethodInfo info = CompatibleMethod(type, method, args, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, $"{type.Name}.{method} static method is missing");
        return info.Invoke(null, args);
    }

    static object Call(object target, string method, params object[] args)
    {
        MethodInfo info = CompatibleMethod(target.GetType(), method, args,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method} method is missing");
        return info.Invoke(target, args);
    }

    static MethodInfo CompatibleMethod(Type type, string name, object[] args, BindingFlags flags) =>
        type.GetMethods(flags).FirstOrDefault(method => method.Name == name &&
            method.GetParameters().Length == args.Length);

    static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} field is missing");
        return (T)field.GetValue(target);
    }

    static T Property<T>(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name);
        Assert.IsNotNull(property, $"{target.GetType().Name}.{name} property is missing");
        return (T)property.GetValue(target);
    }

    static object StaticProperty(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(property, $"{type.Name}.{name} static property is missing");
        return property.GetValue(null);
    }

    static Type TypeNamed(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(name);
            if (found != null) return found;
        }
        Assert.Fail($"runtime type '{name}' does not exist in any loaded assembly");
        return null;
    }
}
