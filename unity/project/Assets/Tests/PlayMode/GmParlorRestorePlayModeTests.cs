using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The PlayMode asmdef cannot reference Assembly-CSharp, so the complete lifecycle is driven by name.
public sealed class GmParlorRestorePlayModeTests
{
    string directory;
    string savePath;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(),
            "gm-parlor-restore-play-" + Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(directory, "save.json");
        StaticCall("GmSaveSystem", "ConfigureForTests", savePath, null);
        StaticCall("GmRunStore", "BeginNewRun");
    }

    [TearDown]
    public void TearDown()
    {
        StaticCall("GmSaveSystem", "Flush");
        StaticCall("GmSaveSystem", "ResetTestConfiguration");
        StaticCall("GmRunStore", "BeginNewRun");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [UnityTest]
    public IEnumerator DiskReloadKeepsControllerClosedUntilExplicitActivationAndSnapsAllCards()
    {
        GameObject producer = BuildRuntime();
        producer.SetActive(true);
        Component producerRules = producer.GetComponent(TypeNamed("GmParlorRules"));
        Component producerController = producer.GetComponent(TypeNamed("GmParlorController"));
        Component producerCoordinator = producer.GetComponentInChildren(TypeNamed(
            "GmParlorPresentationCoordinator"), true);
        Assert.That(Call(producerRules, "StartGame", 117, 4, 3, true, true).ToString(),
            Is.EqualTo("StartedNew"));
        Assert.That(Call(producerController, "Activate").ToString(), Is.EqualTo("None"));
        Component producerEvidence = producer.GetComponent(TypeNamed("GmParlorEvidenceLog"));
        int driveGuard = 100;
        while (((IList)Property(producerEvidence, "Facts")).Count == 0 && driveGuard-- > 0)
        {
            string phase = Property(Property(producerRules, "Match"), "Phase").ToString();
            if (phase == "PlayerLeads" || phase == "PlayerFollowsAldricLead")
            {
                IList hand = (IList)Property(producerRules, "PlayerHand");
                int legal = -1;
                for (int index = 0; index < hand.Count; index++)
                {
                    if (Call(producerRules, "GetPlayerCardError", index).ToString() != "None")
                        continue;
                    legal = index;
                    break;
                }
                Assert.That(legal, Is.GreaterThanOrEqualTo(0));
                Call(producerController, "SetFocusedCardIndex", legal);
            }
            string action = Call(producerController, "ConfirmFocusedAction").ToString();
            Assert.That(action, Is.EqualTo("None"),
                $"phase={phase}, activated={Property(producerController, "IsActivated")}, " +
                $"configured={Property(producerController, "IsConfigured")}, " +
                $"canInput={Property(producerController, "CanAcceptInput")}, " +
                $"blocking={Property(producerCoordinator, "IsBlocking")}, " +
                $"error={Property(producerController, "LastPresentationError")}");
            int presentationGuard = 100;
            while ((bool)Property(producerCoordinator, "IsBlocking") &&
                   presentationGuard-- > 0)
                Call(producerCoordinator, "Advance", 60f);
            Assert.That((bool)Property(producerCoordinator, "IsBlocking"), Is.False);
        }
        int expectedEvidence = ((IList)Property(producerEvidence, "Facts")).Count;
        Assert.That(expectedEvidence, Is.GreaterThan(0),
            "the live producer graph must observe its own cue before the checkpoint");
        Assert.That((bool)StaticCall("GmSaveSystem", "Flush"), Is.True);
        UnityEngine.Object.Destroy(producer);
        yield return null;

        StaticCall("GmRunStore", "BeginNewRun");
        Assert.That((bool)StaticCall("GmSaveSystem", "Load"), Is.True);
        GameObject root = BuildRuntime();
        root.SetActive(true);
        yield return null;

        Component controller = root.GetComponent(TypeNamed("GmParlorController"));
        Component rules = root.GetComponent(TypeNamed("GmParlorRules"));
        Component evidence = root.GetComponent(TypeNamed("GmParlorEvidenceLog"));
        Component focus = root.GetComponent(TypeNamed("GmParlorFocusView"));
        Component coordinator = root.GetComponentInChildren(TypeNamed(
            "GmParlorPresentationCoordinator"), true);
        GmParlorCueRestoreProbe cueProbe = root.GetComponent<GmParlorCueRestoreProbe>();
        Assert.That((bool)Property(controller, "IsActivated"), Is.False,
            "Awake may reconstruct but must not open input");
        Assert.That((bool)Property(controller, "CanAcceptInput"), Is.False);
        Assert.That((bool)Property(focus, "IsOpen"), Is.False);
        Assert.That(((IList)Property(evidence, "Facts")), Has.Count.EqualTo(expectedEvidence));
        Assert.That((bool)Property(coordinator, "IsBlocking"), Is.False);
        Assert.That(Call(controller, "Activate").ToString(), Is.EqualTo("None"));
        Assert.That((bool)Property(controller, "IsActivated"), Is.True);
        Assert.That((bool)Property(controller, "CanAcceptInput"), Is.True);
        Assert.That((bool)Property(focus, "IsOpen"), Is.False);
        Assert.That((int)Property(controller, "FocusedCardIndex"), Is.Zero);
        Assert.That(cueProbe.Received, Is.Zero, "restore cannot re-emit the cue event");
        Assert.That(((IList)Property(evidence, "Facts")), Has.Count.EqualTo(expectedEvidence));
        Component[] restoredViews = root.GetComponentsInChildren(
            TypeNamed("GmParlorCardView"), true);
        Assert.That(restoredViews.Length, Is.EqualTo(28));
        object restoredSnapshot = Call(Property(rules, "Match"), "ExportSnapshot");
        Array expectedBindings = (Array)StaticCall("GmParlorTableLayout", "Build",
            restoredSnapshot);
        foreach (Component view in restoredViews)
        {
            object physical = Property(view, "PhysicalCard");
            object expected = null;
            foreach (object candidate in expectedBindings)
            {
                if (!Field(candidate, "PhysicalCard").Equals(physical)) continue;
                expected = candidate;
                break;
            }
            Assert.That(expected, Is.Not.Null, "restored view has no canonical physical binding");
            Assert.That(Property(view, "Binding"), Is.EqualTo(expected));
            Vector3 position = (Vector3)StaticCall("GmParlorTableLayout", "LocalPosition",
                expected);
            Quaternion rotation = (Quaternion)StaticCall("GmParlorTableLayout", "LocalRotation",
                expected);
            Assert.That(Vector3.Distance(view.transform.localPosition, position),
                Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(view.transform.localRotation, rotation),
                Is.LessThan(0.01f));
        }

        yield return null;
        UnityEngine.Object.Destroy(root);
    }

    [UnityTest]
    public IEnumerator ShippingParlorSceneActivatesAfterSubscriberFrameWithoutTestActivation()
    {
        GameObject producer = ProduceSavedAwaitingCheckpoint(out int expectedEvidence);
        UnityEngine.Object.Destroy(producer);
        yield return null;

        StaticCall("GmRunStore", "BeginNewRun");
        Assert.That((bool)StaticCall("GmSaveSystem", "Load"), Is.True);
        int catchesBefore = (int)StaticProperty("GmRunStore", "CheatsCaughtCount");
        int tableGamesBefore = (int)StaticProperty("GmRunStore", "TableGameIndex");
        var probeRoot = new GameObject("ShippingParlorLifecycleProbe");
        UnityEngine.Object.DontDestroyOnLoad(probeRoot);
        var lifecycleProbe = probeRoot.AddComponent<GmParlorShippingLifecycleProbe>();
        SceneManager.LoadScene("Parlor", LoadSceneMode.Single);
        for (int guard = 0; !lifecycleProbe.Bound && guard < 3; guard++)
            yield return null;

        Component controller = FindRuntimeComponent("GmParlorController");
        Component input = FindRuntimeComponent("GmParlorInput");
        Component evidence = FindRuntimeComponent("GmParlorEvidenceLog");
        Assert.That(lifecycleProbe.Bound, Is.True);
        Assert.That(lifecycleProbe.ControllerWasInactiveAtSceneLoaded, Is.True,
            "shipping activation crossed the pre-frame subscriber boundary");
        Assert.That((bool)Property(input, "IsConfigured"), Is.True);
        Assert.That(((IList)Property(evidence, "Facts")).Count,
            Is.LessThanOrEqualTo(expectedEvidence));

        for (int guard = 0; !(bool)Property(controller, "IsActivated") && guard < 8; guard++)
            yield return null;

        Assert.That((bool)Property(controller, "IsActivated"), Is.True,
            "shipping Parlor has no post-frame activation owner");
        Assert.That((bool)Property(controller, "CanAcceptInput"), Is.True);
        Assert.That(Property(controller, "FocusedCard"), Is.Not.Null);
        Assert.That(((IList)Property(evidence, "Facts")), Has.Count.EqualTo(expectedEvidence));
        Assert.That(lifecycleProbe.CueEvents, Is.Zero,
            "shipping restore replayed an acknowledged cue");
        Assert.That(lifecycleProbe.EvidenceChanges, Is.LessThanOrEqualTo(1),
            "shipping activation imported retained evidence more than once");
        int evidenceChangesAfterActivation = lifecycleProbe.EvidenceChanges;
        Assert.That((int)StaticProperty("GmRunStore", "CheatsCaughtCount"),
            Is.EqualTo(catchesBefore));
        Assert.That((int)StaticProperty("GmRunStore", "TableGameIndex"),
            Is.EqualTo(tableGamesBefore));
        yield return null;
        Assert.That(lifecycleProbe.CueEvents, Is.Zero);
        Assert.That(lifecycleProbe.EvidenceChanges, Is.EqualTo(evidenceChangesAfterActivation));
        UnityEngine.Object.Destroy(probeRoot);
    }

    static GameObject ProduceSavedAwaitingCheckpoint(out int evidenceCount)
    {
        GameObject producer = BuildRuntime();
        producer.SetActive(true);
        Component rules = producer.GetComponent(TypeNamed("GmParlorRules"));
        Component controller = producer.GetComponent(TypeNamed("GmParlorController"));
        Component coordinator = producer.GetComponentInChildren(TypeNamed(
            "GmParlorPresentationCoordinator"), true);
        Component evidence = producer.GetComponent(TypeNamed("GmParlorEvidenceLog"));
        Assert.That(Call(rules, "StartGame", 117, 4, 3, true, true).ToString(),
            Is.EqualTo("StartedNew"));
        Assert.That(Call(controller, "Activate").ToString(), Is.EqualTo("None"));
        int driveGuard = 100;
        while (((IList)Property(evidence, "Facts")).Count == 0 && driveGuard-- > 0)
        {
            string phase = Property(Property(rules, "Match"), "Phase").ToString();
            if (phase == "PlayerLeads" || phase == "PlayerFollowsAldricLead")
            {
                IList hand = (IList)Property(rules, "PlayerHand");
                int legal = -1;
                for (int index = 0; index < hand.Count; index++)
                {
                    if (Call(rules, "GetPlayerCardError", index).ToString() != "None") continue;
                    legal = index;
                    break;
                }
                Assert.That(legal, Is.GreaterThanOrEqualTo(0));
                Call(controller, "SetFocusedCardIndex", legal);
            }
            Assert.That(Call(controller, "ConfirmFocusedAction").ToString(), Is.EqualTo("None"));
            int presentationGuard = 100;
            while ((bool)Property(coordinator, "IsBlocking") && presentationGuard-- > 0)
                Call(coordinator, "Advance", 60f);
        }
        evidenceCount = ((IList)Property(evidence, "Facts")).Count;
        Assert.That(evidenceCount, Is.GreaterThan(0));
        Assert.That((bool)StaticCall("GmSaveSystem", "Flush"), Is.True);
        return producer;
    }

    static GameObject BuildRuntime()
    {
        var root = new GameObject("ParlorRestoreRuntime");
        root.SetActive(false);
        Component rules = root.AddComponent(TypeNamed("GmParlorRules"));
        var cards = new GameObject("Cards");
        cards.transform.SetParent(root.transform, false);
        Type cardType = TypeNamed("GmCard");
        Type suitType = TypeNamed("GmSuit");
        foreach (string suit in new[] { "Flames", "Eyes", "Teeth", "Bones" })
        {
            object suitValue = Enum.Parse(suitType, suit);
            for (int rank = 1; rank <= 7; rank++)
            {
                var cardGo = new GameObject(suit + "_" + rank);
                cardGo.transform.SetParent(cards.transform, false);
                Component view = cardGo.AddComponent(TypeNamed("GmParlorCardView"));
                object card = Activator.CreateInstance(cardType, suitValue, rank);
                Call(view, "Configure", card, null, null);
            }
        }
        Component binder = cards.AddComponent(TypeNamed("GmParlorPropBinder"));
        Component[] foundViews = cards.GetComponentsInChildren(TypeNamed("GmParlorCardView"), true);
        Array views = Array.CreateInstance(TypeNamed("GmParlorCardView"), foundViews.Length);
        for (int index = 0; index < foundViews.Length; index++)
            views.SetValue(foundViews[index], index);
        object[] binderArgs = { views, null };
        Assert.That((bool)Call(binder, "TryConfigure", binderArgs), Is.True, binderArgs[1] as string);
        Component evidence = root.AddComponent(TypeNamed("GmParlorEvidenceLog"));
        var hand = new GameObject("RightHandCue");
        hand.transform.SetParent(root.transform, false);
        var contact = new GameObject("CardContactCue");
        contact.transform.SetParent(root.transform, false);
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.name = "SleeveCue";
        sleeve.transform.SetParent(root.transform, false);
        Component presenter = root.AddComponent(TypeNamed("GmParlorAldricPresenter"));
        object[] presenterArgs =
        {
            hand.transform,
            sleeve.GetComponent<Renderer>(),
            contact.transform,
            evidence,
            null,
        };
        Assert.That((bool)Call(presenter, "TryConfigure", presenterArgs), Is.True,
            presenterArgs[4] as string);
        Component coordinator = cards.AddComponent(TypeNamed("GmParlorPresentationCoordinator"));
        object profile = StaticCall("GmParlorAccessibilityProfile", "FromGlobal");
        object[] coordinatorArgs = { binder, presenter, profile, null };
        Assert.That((bool)Call(coordinator, "TryConfigure", coordinatorArgs), Is.True,
            coordinatorArgs[3] as string);
        Component controller = root.AddComponent(TypeNamed("GmParlorController"));
        object[] controllerArgs = { rules, binder, coordinator, null };
        Assert.That((bool)Call(controller, "TryConfigure", controllerArgs), Is.True,
            controllerArgs[3] as string);
        Component focus = root.AddComponent(TypeNamed("GmParlorFocusView"));
        object[] focusArgs = { rules, controller, evidence, null };
        Assert.That((bool)Call(focus, "TryConfigure", focusArgs), Is.True,
            focusArgs[3] as string);
        Component hud = root.AddComponent(TypeNamed("GmParlorHud"));
        object[] hudArgs = { focus, coordinator, null };
        Assert.That((bool)Call(hud, "TryConfigure", hudArgs), Is.True,
            hudArgs[2] as string);
        var cueProbe = root.AddComponent<GmParlorCueRestoreProbe>();
        cueProbe.Presenter = presenter;
        return root;
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

    static object Field(object target, string name) =>
        target.GetType().GetField(name).GetValue(target);

    static object Call(object target, string name, params object[] args) =>
        Method(target.GetType(), name, args.Length).Invoke(target, args);

    static object StaticCall(string type, string name, params object[] args) =>
        Method(TypeNamed(type), name, args.Length).Invoke(null, args);

    static object StaticProperty(string type, string name) =>
        TypeNamed(type).GetProperty(name).GetValue(null);

    static Component FindRuntimeComponent(string typeName)
    {
        foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include))
            if (component != null && component.GetType().Name == typeName) return component;
        throw new AssertionException("Runtime component not found: " + typeName);
    }

    static MethodInfo Method(Type type, string name, int parameterCount)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                     BindingFlags.Static))
            if (method.Name == name && method.GetParameters().Length == parameterCount) return method;
        throw new AssertionException($"Method not found: {type.Name}.{name}/{parameterCount}");
    }
}

public sealed class GmParlorShippingLifecycleProbe : MonoBehaviour
{
    public bool Bound { get; private set; }
    public bool ControllerWasInactiveAtSceneLoaded { get; private set; }
    public int CueEvents { get; private set; }
    public int EvidenceChanges { get; private set; }

    void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Parlor") return;
        Component controller = Find("GmParlorController");
        Component presenter = Find("GmParlorAldricPresenter");
        Component evidence = Find("GmParlorEvidenceLog");
        ControllerWasInactiveAtSceneLoaded =
            !(bool)controller.GetType().GetProperty("IsActivated").GetValue(controller);
        evidence.GetType().GetEvent("OnChanged").AddEventHandler(evidence,
            (Action)(() => EvidenceChanges++));
        EventInfo cueEvent = presenter.GetType().GetEvent("OnCuePresented");
        Type cueType = cueEvent.EventHandlerType.GetGenericArguments()[0];
        MethodInfo subscribe = GetType().GetMethod(nameof(SubscribeCue),
            BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(cueType);
        subscribe.Invoke(this, new object[] { presenter, cueEvent });
        Bound = true;
    }

    void SubscribeCue<TCue>(Component presenter, EventInfo cueEvent)
    {
        Action<TCue> handler = _ => CueEvents++;
        cueEvent.AddEventHandler(presenter, handler);
    }

    static Component Find(string typeName)
    {
        foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include))
            if (component != null && component.GetType().Name == typeName) return component;
        throw new AssertionException("Runtime component not found at sceneLoaded: " + typeName);
    }
}

public sealed class GmParlorCueRestoreProbe : MonoBehaviour
{
    public Component Presenter;
    public int Received { get; private set; }
    bool subscribed;

    void Awake() => TrySubscribe();

    public void Bind(Component presenter)
    {
        Presenter = presenter;
        TrySubscribe();
    }

    void TrySubscribe()
    {
        if (subscribed || Presenter == null) return;
        EventInfo cueEvent = Presenter.GetType().GetEvent("OnCuePresented");
        Type cueType = cueEvent.EventHandlerType.GetGenericArguments()[0];
        MethodInfo subscribe = GetType().GetMethod(nameof(Subscribe),
            BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(cueType);
        subscribe.Invoke(this, new object[] { cueEvent });
        subscribed = true;
    }

    void Subscribe<TCue>(EventInfo cueEvent)
    {
        Action<TCue> handler = _ => Received++;
        cueEvent.AddEventHandler(Presenter, handler);
    }
}
