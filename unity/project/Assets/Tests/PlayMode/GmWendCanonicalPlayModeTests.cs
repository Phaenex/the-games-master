using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GmWendCanonicalPlayModeTests
{
    const float GateMetres = 18f;

    [UnitySetUp]
    public IEnumerator LoadCanonicalOpening()
    {
        SceneManager.LoadScene("WendHill_Prologue", LoadSceneMode.Single);
        yield return null;
        yield return null;
        Time.timeScale = 1f;
    }

    [UnityTearDown]
    public IEnumerator RestoreTime()
    {
        Time.timeScale = 1f;
        Time.captureDeltaTime = 0f;
        AudioListener.volume = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator CanonicalSceneLoadsTheExactAnchoredStoryContract()
    {
        MonoBehaviour design = FindBehaviour("GmDesignRuntime");
        Assert.IsNotNull(design);
        Assert.AreEqual("wend-opening-design.json", Field<string>(design, "designFile"));
        Assert.AreEqual(5, CollectionField(design, "coldOpen").Count);
        Assert.AreEqual(13, CollectionField(design, "pois").Count);
        Assert.AreEqual(5, CollectionField(design, "beats").Count);
        Assert.AreEqual(7, CollectionField(design, "branchBeats").Count);
        Assert.AreEqual(0, Property<int>(design, "AnchorIssueCount"));

        MonoBehaviour[] anchors = FindBehaviours("GmWorldAnchor");
        string[] ids = anchors.Select(anchor => Property<string>(anchor, "AnchorId")).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count(), "world anchor ids are not unique");
        foreach (string required in new[] { "arrival-car", "gate", "chapel", "manor-porch", "wake-pose" })
            CollectionAssert.Contains(ids, required);
        Assert.AreEqual(13, anchors.Count(anchor => anchor.name.StartsWith("POI_")));
        Assert.IsNotNull(GameObject.Find("WakeRoom/WakePose"));
        Assert.IsNotNull(FindBehaviour("GmMansionIdentity"));
        Assert.AreEqual(10, Property<int>(FindBehaviour("GmWendStoryTour"), "ShotCount"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReturningToCarBeforeGateFiresOnlyTheSecretEnding()
    {
        MonoBehaviour player = FindBehaviour("GmPlayer");
        MonoBehaviour route = FindBehaviour("GmRouteSpline");
        MonoBehaviour secret = FindBehaviour("GmSecretEnding");
        MonoBehaviour threshold = FindBehaviour("GmThreshold");
        MonoBehaviour bell = FindBehaviour("GmBellSummons");
        SkipColdOpen();
        Move(player, PointAt(route, 9f));
        yield return null;
        Move(player, PointAt(route, 0f));
        yield return null;
        Assert.IsTrue(Property<bool>(secret, "Fired"));
        Assert.IsFalse(Property<bool>(secret, "PassedGate"));
        Assert.IsFalse(Property<bool>(threshold, "GateLocked"));
        Assert.IsFalse(Property<bool>(bell, "Armed"));
        Assert.IsTrue(Property<bool>(player, "ControlBlocked"));
    }

    [UnityTest]
    public IEnumerator GateRetreatArmsTheReal285SecondCadenceAndExcludesSecretEnding()
    {
        MonoBehaviour player = FindBehaviour("GmPlayer");
        MonoBehaviour route = FindBehaviour("GmRouteSpline");
        MonoBehaviour secret = FindBehaviour("GmSecretEnding");
        MonoBehaviour threshold = FindBehaviour("GmThreshold");
        MonoBehaviour bell = FindBehaviour("GmBellSummons");
        SkipColdOpen();
        Move(player, PointAt(route, GateMetres + 4f));
        yield return null;
        Move(player, PointAt(route, GateMetres + 2f));
        yield return null;
        Assert.IsTrue(Property<bool>(secret, "PassedGate"));
        Assert.IsFalse(Property<bool>(secret, "Fired"));
        Assert.IsTrue(Property<bool>(threshold, "GateLocked"));
        Assert.IsTrue(Property<bool>(bell, "Armed"));
        float first = Field<float>(bell, "firstTollDelay");
        float interval = Field<float>(bell, "tollInterval");
        Assert.AreEqual(285f, first + 8f * interval);
        yield return new WaitForSeconds(0.7f);
        Assert.IsTrue(Property<bool>(FindBehaviour("GmGateLeaves"), "IsClosed"));
    }

    [UnityTest]
    public IEnumerator NinthTollCrossesToWakeRoomWithoutFiringSecretEnding()
    {
        MonoBehaviour player = FindBehaviour("GmPlayer");
        MonoBehaviour route = FindBehaviour("GmRouteSpline");
        MonoBehaviour secret = FindBehaviour("GmSecretEnding");
        MonoBehaviour bell = FindBehaviour("GmBellSummons");
        MonoBehaviour crossing = FindBehaviour("GmCrossing");
        SkipColdOpen();
        SetField(crossing, "deadAir", 0.08f);
        SetField(crossing, "whisperTime", 0.12f);
        SetField(crossing, "irisTime", 0.12f);
        SetField(crossing, "cardTime", 0.12f);
        SetField(bell, "firstTollDelay", 0.02f);
        SetField(bell, "tollInterval", 0.02f);
        // The ninth toll holds its card before handing over to the crossing, so that the one line
        // the whole count pays off ("I was early for this") is actually on screen rather than being
        // overwritten in its own frame. Compress it here the same way the crossing's own timings
        // above are compressed -- the hold is real behaviour under test, not something to skip.
        SetField(bell, "takenCardHoldOverride", 0.05f);
        Move(player, PointAt(route, GateMetres + 4f));
        yield return null;
        Move(player, PointAt(route, GateMetres + 2f));
        yield return null;
        Time.captureDeltaTime = 1f / 60f;
        Time.timeScale = 20f;
        float deadline = Time.realtimeSinceStartup + 5f;
        while (Property<int>(bell, "Toll") < 9 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual(9, Property<int>(bell, "Toll"));

        // The crossing no longer begins in the same frame as the ninth toll, so wait for it rather
        // than assuming the player teleports instantly. Asserting immediately here is what made this
        // test encode "toll nine IS the crossing" -- true only because the payoff card was being
        // destroyed before anyone could read it.
        Transform wake = GameObject.Find("WakeRoom/WakePose").transform;
        deadline = Time.realtimeSinceStartup + 5f;
        while (Vector3.Distance(player.transform.position, wake.position) >= 0.05f &&
               Time.realtimeSinceStartup < deadline) yield return null;
        Assert.Less(Vector3.Distance(player.transform.position, wake.position), 0.05f,
            "the ninth toll never delivered the player to the wake room");
        Assert.IsFalse(Property<bool>(secret, "Fired"));
        deadline = Time.realtimeSinceStartup + 5f;
        while (Property<bool>(player, "ControlBlocked") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(Property<bool>(player, "ControlBlocked"));
    }

    [UnityTest]
    public IEnumerator HouseBeginningCarriesTheFullEntryAndFirstGameContract()
    {
        MonoBehaviour house = FindBehaviour("GmHouseBeginning");
        Assert.IsNotNull(house);
        Assert.IsNotNull(GameObject.Find("HouseBeginning/EntryHall/LedgerDesk/Ledger"));
        Assert.IsNotNull(GameObject.Find("HouseBeginning/EntryHall/Portraits/Portrait_Percival/MirrorShard"));
        Assert.IsNotNull(GameObject.Find("HouseBeginning/Parlor/AldricVoss"));
        Assert.IsNotNull(GameObject.Find("HouseBeginning/Parlor/FirstGameTable"));
        Assert.AreEqual(9, GameObject.Find("HouseBeginning/EntryHall/Portraits").transform.Cast<Transform>()
            .Count(child => child.name.StartsWith("Portrait_")));
        MonoBehaviour[] interactions = GameObject.Find("HouseBeginning")
            .GetComponentsInChildren<MonoBehaviour>(true)
            .Where(component => component != null && component.GetType().Name == "GmInteractable").ToArray();
        Assert.GreaterOrEqual(interactions.Length, 20);
        Assert.AreEqual(interactions.Length, interactions.Select(item => Property<string>(item, "InteractionId")).Distinct().Count());
        yield return null;
    }

    [UnityTest]
    public IEnumerator HouseFlowEarnsReadAndCompletesTheRealBestOfThreeGame()
    {
        MonoBehaviour house = FindBehaviour("GmHouseBeginning");
        Call(house, "ReviewEnterHouse");
        Assert.AreEqual("EntryHall", Property<object>(house, "Phase").ToString());
        Call(house, "ReviewUnlockParlor");
        Assert.IsTrue(Property<bool>(house, "CanEnterParlor"));
        object progress = Property<object>(house, "Progress");
        Assert.IsTrue((bool)Call(progress, "HasClue", "ledger-open-line"));
        Assert.IsTrue((bool)Call(progress, "HasClue", "percival-ledger-pair"));
        Assert.AreEqual(3, Property<int>(house, "PortraitsRead"));

        Call(house, "BeginHostIntroduction");
        Assert.AreEqual(9, Property<int>(house, "IntroCount"));
        while (Property<object>(house, "Phase").ToString() == "HostIntroduction") Call(house, "AdvanceDialogue");
        Assert.AreEqual("ParlorGame", Property<object>(house, "Phase").ToString());
        Assert.AreEqual(7, ((ICollection)Property<object>(house, "PlayerHand")).Count);

        bool earnedRead = false;
        int decisions = 0;
        while (Property<object>(house, "Phase").ToString() == "ParlorGame" && decisions++ < 100)
        {
            string turn = Property<object>(house, "TurnPhase").ToString();
            if (turn == "ChooseCard")
                Assert.IsTrue((bool)Call(house, "ReviewPlayFirstLegalCard"));
            else if (turn == "JudgePlay")
            {
                if (!earnedRead && Property<bool>(house, "CurrentPlayCanBeRead") &&
                    Property<bool>(house, "ReviewCurrentPlayWasCheat"))
                {
                    Call(house, "ReadHand");
                    earnedRead = true;
                }
                else Call(house, "AllowTrick");
            }
            else Call(house, "ContinueAfterResult");
            yield return null;
        }
        Assert.Less(decisions, 100, "first game did not reach a terminal state");
        Assert.AreEqual("Complete", Property<object>(house, "Phase").ToString());
        Assert.IsTrue(earnedRead, "suspicion never earned a valid Read tutorial");
        Assert.GreaterOrEqual(Property<int>(progress, "CheatsCaught"), 1);
        Assert.IsTrue((bool)Call(progress, "HasClue", "parlor-complete"));
    }

    [UnityTest]
    public IEnumerator EntireCanonicalRouteHasCompleteNavMeshPaths()
    {
        MonoBehaviour route = FindBehaviour("GmRouteSpline");
        var points = ((IEnumerable)Property<object>(route, "Points")).Cast<Vector3>().ToArray();
        Assert.Greater(points.Length, 2);
        var failures = new List<string>();
        for (int i = 1; i < points.Length; i++)
        {
            if (!NavMesh.SamplePosition(points[i - 1], out NavMeshHit from, 6f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(points[i], out NavMeshHit to, 6f, NavMesh.AllAreas))
            {
                failures.Add($"route leg {i - 1}->{i} has no nearby NavMesh: {points[i - 1]} -> {points[i]}");
                continue;
            }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                failures.Add($"route leg {i - 1}->{i} is {path.status}: {from.position} -> {to.position}");
        }
        Assert.IsEmpty(failures, string.Join("\n", failures));
        yield return null;
    }

    static void SkipColdOpen() => FindBehaviour("GmColdOpen").GetType()
        .GetMethod("SkipIntroForReview").Invoke(FindBehaviour("GmColdOpen"), null);
    static Vector3 PointAt(MonoBehaviour route, float metres) =>
        (Vector3)route.GetType().GetMethod("PointAt").Invoke(route, new object[] { metres });
    static void Move(MonoBehaviour player, Vector3 position)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position + Vector3.up * 0.1f;
        controller.enabled = true;
        Physics.SyncTransforms();
    }
    static MonoBehaviour FindBehaviour(string name) => FindBehaviours(name).FirstOrDefault();
    static MonoBehaviour[] FindBehaviours(string name) => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .Where(behaviour => behaviour.GetType().Name == name).ToArray();
    static ICollection CollectionField(object target, string name) => (ICollection)target.GetType().GetField(name).GetValue(target);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
    static void SetField(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name).Invoke(target, args);
}
