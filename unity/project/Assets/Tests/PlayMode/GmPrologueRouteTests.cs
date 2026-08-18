using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public class GmPrologueRouteTests
{
    [UnitySetUp]
    public IEnumerator LoadWendHill()
    {
        SceneManager.LoadScene("WendHill", LoadSceneMode.Single);
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
    public IEnumerator NinthBellCrossingUsesTheLiveCuratedGraphAndRestoresControl()
    {
        var crossing = FindBehaviour("GmCrossing");
        var symptoms = FindBehaviour("GmSymptoms");
        var player = FindBehaviour("GmPlayer");
        var wake = GameObject.Find("WakeRoom/WakePose");
        Assert.IsNotNull(crossing);
        Assert.IsNotNull(symptoms);
        Assert.IsNotNull(player);
        Assert.IsNotNull(wake);

        // Preserve production ordering while shortening only the waits for a live runtime proof.
        crossing.GetType().GetField("deadAir").SetValue(crossing, 0.20f);
        crossing.GetType().GetField("whisperTime").SetValue(crossing, 0.40f);
        crossing.GetType().GetField("irisTime").SetValue(crossing, 0.30f);
        crossing.GetType().GetField("cardTime").SetValue(crossing, 0.40f);
        // Batchmode can advance WaitForSeconds while reporting a near-zero frame delta. A fixed
        // capture delta keeps the production iris coroutine deterministic without changing it.
        Time.captureDeltaTime = 1f / 60f;
        Time.timeScale = 10f;
        var run = crossing.GetType().GetMethod("Run");
        Assert.IsNotNull(run);
        crossing.StartCoroutine((IEnumerator)run.Invoke(crossing, null));

        // Run() executes through the first yield synchronously: cutoff, symptom stop and unseen
        // teleport must all have happened before the next frame.
        Assert.AreEqual(1f, Property<float>(crossing, "Fade"), 0.0001f);
        Assert.AreEqual(0f, AudioListener.volume, 0.0001f);
        Assert.IsTrue(Property<bool>(player, "ControlBlocked"));
        Assert.IsTrue(Property<bool>(symptoms, "InternalLayersMuted"),
            "heartbeat or tinnitus survived the crossing cutoff");
        Assert.Less(Vector3.Distance(player.transform.position, wake.transform.position), 0.05f,
            "closed-door crossing did not land at the authored wake pose");

        bool sawWhisper = false;
        bool sawChime = false;
        bool sawFilteredReturn = false;
        bool completed = false;
        for (int frame = 0; frame < 300; frame++)
        {
            var whisper = GameObject.Find("cross_whisper_bed");
            if (whisper != null)
            {
                var source = whisper.GetComponent<AudioSource>();
                Assert.IsNotNull(source);
                Assert.AreEqual("whisper_bed", source.clip?.name);
                Assert.IsFalse(source.loop, "crossing repeated the human whisper recording");
                Assert.AreSame(crossing.transform, whisper.transform.parent);
                sawWhisper = true;
            }

            var chime = GameObject.Find("cross_clock_chime");
            if (chime != null)
            {
                var source = chime.GetComponent<AudioSource>();
                Assert.IsNotNull(source);
                Assert.AreEqual("clock_chime", source.clip?.name);
                Assert.IsFalse(source.loop);
                sawChime = true;
            }

            var filter = Camera.main?.GetComponent<AudioLowPassFilter>();
            if (filter != null && filter.cutoffFrequency >= 690f && filter.cutoffFrequency < 22000f &&
                AudioListener.volume > 0f)
                sawFilteredReturn = true;

            if (frame > 10 && Property<float>(crossing, "Fade") <= 0.0001f &&
                string.IsNullOrEmpty(Property<string>(crossing, "Card")) &&
                !Property<bool>(player, "ControlBlocked"))
            {
                completed = true;
                break;
            }
            yield return null;
        }

        Assert.IsTrue(sawWhisper, "live crossing never created the curated wordless whisper source");
        Assert.IsTrue(sawChime, "live crossing never created the curated single clock strike");
        Assert.IsTrue(sawFilteredReturn, "hearing never returned through the listener low-pass");
        Assert.IsTrue(completed, "crossing did not restore player control within its bounded sequence");
        Assert.AreEqual(1f, AudioListener.volume, 0.0001f);
        Assert.AreEqual(22000f, Camera.main.GetComponent<AudioLowPassFilter>().cutoffFrequency, 1f);
        Assert.IsNull(crossing.GetComponent<AudioLowPassFilter>(),
            "crossing created a no-op filter on its source-less Systems object");
    }

    [UnityTest]
    public IEnumerator VirtualGamepadDrivesTheCompletePhaseZeroControlSurface()
    {
        var player = FindBehaviour("GmPlayer");
        var coldOpen = FindBehaviour("GmColdOpen");
        var ambience = FindBehaviour("GmAmbience");
        var runtime = FindBehaviour("GmDesignRuntime");
        Assert.IsNotNull(player);
        Assert.IsNotNull(coldOpen);
        Assert.IsNotNull(ambience);
        Assert.IsNotNull(runtime);

        Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
        GameObject interactionTarget = null;
        try
        {
            yield return null;
            Assert.IsTrue(Property<bool>(coldOpen, "IsRunning"));
            Assert.IsTrue(Property<bool>(player, "ControlBlocked"));
            int firstCard = Property<int>(coldOpen, "CardNumber");

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.IsTrue(Property<bool>(player, "UsingGamepad"),
                "south-button input did not switch the prompt mode");
            Assert.Greater(Property<int>(coldOpen, "CardNumber"), firstCard,
                "A/Cross did not advance the cold open");

            var hud = FindBehaviour("GmPrologueHud");
            var document = hud?.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            var prompt = document.rootVisualElement.Q<Label>("Prompt");
            Assert.IsNotNull(prompt);
            // Assert the state, not the wording — pinning to prompt prose turned every copy
            // improvement into a gate failure.
            Assert.IsFalse(string.IsNullOrWhiteSpace(prompt.text), "cold-open prompt is empty");
            Assert.IsTrue(Property<bool>(hud, "PromptUsesControllerLabels"),
                "cold-open prompt did not switch to controller labels");

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.IsFalse(Property<bool>(coldOpen, "IsRunning"),
                "B/Circle did not skip the remaining cold-open cards");
            Assert.IsFalse(Property<bool>(player, "ControlBlocked"));

            // Batchmode runs uncapped and can report a near-zero frame delta. Pin only this input
            // exercise to 60 Hz so CharacterController.SimpleMove and controller look receive the
            // same non-zero delta they have in the standalone player.
            Time.captureDeltaTime = 1f / 60f;
            Vector3 startPosition = player.transform.position;
            float startYaw = player.transform.eulerAngles.y;
            var driveState = new GamepadState
            {
                leftStick = new Vector2(0.42f, 0.78f),
                rightStick = new Vector2(0.72f, 0.18f)
            };
            yield return SendGamepad(gamepad, driveState, 20);
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.Greater(Vector3.Distance(startPosition, player.transform.position), 0.08f,
                "left stick did not move the standalone player");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(startYaw, player.transform.eulerAngles.y)), 0.5f,
                "right stick did not rotate the standalone player");

            Vector3 dpadStart = player.transform.position;
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.DpadUp), 4);
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.Greater(Vector3.Distance(dpadStart, player.transform.position), 0.03f,
                "D-pad did not provide the advertised movement fallback");

            var camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camera);
            interactionTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            interactionTarget.name = "ControllerInteractionProof";
            interactionTarget.transform.position = camera.transform.position + camera.transform.forward * 1.45f;
            interactionTarget.transform.localScale = Vector3.one * 0.28f;
            var interactableType = System.Type.GetType("GmInteractable, Assembly-CSharp");
            Assert.IsNotNull(interactableType);
            var interactable = interactionTarget.AddComponent(interactableType);
            interactableType.GetMethod("Configure").Invoke(interactable,
                new object[] { "controller-proof", "Examine", 3f, 7f,
                    System.Enum.Parse(System.Type.GetType("GmInteractionRepeatPolicy, Assembly-CSharp"),
                        "FirstThenSecond"), "" });
            interactableType.GetMethod("BindContent").Invoke(interactable,
                new object[] { "The controller reached this authored target.", "It still reaches it." });
            Physics.SyncTransforms();
            object scanner = Property<object>(player, "InteractionScanner");
            scanner.GetType().GetMethod("Scan").Invoke(scanner, null);
            Assert.AreSame(interactable, Property<object>(scanner, "Focused"),
                "controller proof target was not focusable from the live camera");
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.AreEqual(1, Property<int>(interactable, "Uses"),
                "A/Cross did not execute the focused interaction");
            string lastExamine = (string)runtime.GetType().GetField("lastExamine").GetValue(runtime);
            StringAssert.Contains("controller reached", lastExamine.ToLowerInvariant());

            string originalWind = Property<string>(ambience, "ReviewProfileId");
            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.AreNotEqual(originalWind, Property<string>(ambience, "ReviewProfileId"),
                "RB/R1 did not cycle the in-game wind comparison");

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.Start));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.IsTrue(Property<bool>(player, "IsPaused"), "Menu/Options did not pause gameplay");
            Assert.AreEqual(0f, Time.timeScale, 0.0001f);
            Assert.IsTrue(AudioListener.pause, "pause did not suspend game audio");
            MonoBehaviour commonPause = FindBehaviour("GmPauseMenu");
            Assert.IsNotNull(commonPause, "Prologue has no common accessibility pause menu");
            UIDocument commonPauseDocument = commonPause.GetComponent<UIDocument>();
            Assert.IsNotNull(commonPauseDocument);
            Assert.AreEqual(DisplayStyle.Flex,
                commonPauseDocument.rootVisualElement.resolvedStyle.display);
            Assert.IsNull(document.rootVisualElement.Q<VisualElement>("PauseResume"),
                "Prologue still rendered its competing bespoke pause card");
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                commonPauseDocument.rootVisualElement.Q<Label>("PausePrompt").text));

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.North));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.IsTrue(Property<bool>(player, "QuitRequested"),
                "Y/Triangle did not reach the pause-menu quit path");

            yield return SendGamepad(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return SendGamepad(gamepad, new GamepadState());
            Assert.IsFalse(Property<bool>(player, "IsPaused"), "B/Circle did not resume gameplay");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
            Assert.IsFalse(AudioListener.pause);

            yield return null;
            var controls = document.rootVisualElement.Q<Label>("Controls");
            Assert.IsNotNull(controls);
            // State, not wording — the last two assertions that had frozen the controls legend.
            Assert.IsFalse(string.IsNullOrWhiteSpace(controls.text), "controls legend is empty");
            Assert.IsTrue(Property<bool>(hud, "ControlsUseControllerLabels"),
                "controls legend did not switch to controller labels");
        }
        finally
        {
            if (player != null && Property<bool>(player, "IsPaused"))
                player.GetType().GetMethod("SetPaused").Invoke(player, new object[] { false });
            if (interactionTarget != null) Object.Destroy(interactionTarget);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        }
    }

    static IEnumerator SendGamepad(Gamepad gamepad, GamepadState state, int frames = 2)
    {
        InputSystem.QueueStateEvent(gamepad, state);
        for (int i = 0; i < frames; i++) yield return null;
    }

    static MonoBehaviour FindBehaviour(string typeName)
    {
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour.GetType().Name == typeName) return behaviour;
        return null;
    }

    static T Property<T>(object target, string name)
    {
        var property = target.GetType().GetProperty(name);
        Assert.IsNotNull(property, $"{target.GetType().Name}.{name} property is missing");
        return (T)property.GetValue(target);
    }

    [UnityTest]
    public IEnumerator ExplorationRouteReachesEveryAuthoredExteriorZone()
    {
        var player = GameObject.Find("Player");
        Assert.IsNotNull(player);
        DisablePlayerInput(player);
        var controller = player.GetComponent<CharacterController>();
        Assert.IsNotNull(controller);

        Vector3[] route =
        {
            P(0, 72), P(0, 60), P(0, 29),
            P(7, 28.5f), P(12, 28.5f), P(16.2f, 27), P(16.2f, 22),
            P(16.2f, 27), P(21.5f, 30), P(26.2f, 30),
            P(21.5f, 30), P(12, 28.5f), P(7, 28.5f), P(0, 28.5f),
            // The scarecrow's player stance is in front of the coat, not inside its focus collider.
            P(-7, 22), P(-12, 22), P(-16, 24), P(-18.4f, 27.5f), P(-22, 34),
            P(-23.5f, 38), P(-24.5f, 42), P(-26.5f, 48),
            P(-24.5f, 42), P(-23.5f, 38), P(-22, 34), P(-16, 24), P(-7, 22),
            P(0, 22), P(0, -36)
        };

        foreach (Vector3 target in route)
            yield return WalkTo(controller, target, 0.55f);

        Assert.Less(Vector3.Distance(Flat(player.transform.position), P(0, -36)), 0.7f,
            "full exploration route did not reach the sealed porch threshold");
    }

    [UnityTest]
    public IEnumerator AdversarialBoundaryPushesCannotEscapeOrEnterTheMansion()
    {
        var player = GameObject.Find("Player");
        DisablePlayerInput(player);
        var controller = player.GetComponent<CharacterController>();

        controller.enabled = false; player.transform.position = P(0, 10) + Vector3.up * 0.1f; controller.enabled = true;
        yield return PushFor(controller, Vector3.right, 45);
        Assert.LessOrEqual(player.transform.position.x, 3.75f, "player escaped east of the drive walk bounds");

        controller.enabled = false; player.transform.position = P(0, 10) + Vector3.up * 0.1f; controller.enabled = true;
        yield return PushFor(controller, Vector3.left, 45);
        Assert.GreaterOrEqual(player.transform.position.x, -3.75f, "player escaped west of the drive walk bounds");

        controller.enabled = false; player.transform.position = P(0, -34.5f) + Vector3.up * 0.1f; controller.enabled = true;
        yield return PushFor(controller, Vector3.back, 45);
        Assert.GreaterOrEqual(player.transform.position.z, -37.4f,
            "player crossed the sealed Threshold Refusal boundary into the mansion");
    }

    /// Pressure the seven side-zone walls themselves, not merely an arbitrary collider somewhere
    /// before them. The previous version checked only a final in-bounds coordinate. One case did not
    /// even command enough travel to cross its failure tolerance, and garden fences intercepted two
    /// more, so those cases passed with the intended WalkBounds wall absent.
    ///
    /// This version temporarily ignores non-WalkBounds collision for this controller, finds the
    /// exact generated wall by fixed coordinate and span, commands travel well beyond it, requires a
    /// side collision, and requires the controller to finish within contact distance of that wall.
    [UnityTest]
    public IEnumerator SideZonePerimetersPressureTheirIntendedWalkBounds()
    {
        var player = GameObject.Find("Player");
        Assert.IsNotNull(player);
        DisablePlayerInput(player);
        var controller = player.GetComponent<CharacterController>();
        Assert.IsNotNull(controller);

        var ignored = IgnoreNonWalkBoundCollisions(controller, true);
        try
        {
            yield return PressureBound(controller, "cemetery south", P(16.2f, 17f), Vector3.back,
                45, true, 12.6f, 16.2f);

            // The old 70-frame command ended at x=38.4 while the assertion allowed x<=38.9, so a
            // missing x=38.5 wall passed. Ninety frames deliberately travel well through it.
            yield return PressureBound(controller, "chapel forecourt east", P(23f, 28.5f), Vector3.right,
                90, false, 38.5f, 28.5f);

            yield return PressureBound(controller, "cemetery north", P(16.2f, 36f), Vector3.forward,
                45, true, 40.6f, 16.2f);
            yield return PressureBound(controller, "kitchen garden west", P(-22f, 22.1f), Vector3.left,
                45, false, -23.6f, 22.1f);
            yield return PressureBound(controller, "kitchen garden south", P(-16f, 18f), Vector3.back,
                45, true, 15.6f, -16f);
            yield return PressureBound(controller, "coach yard west", P(-28f, 48f), Vector3.left,
                45, false, -33.5f, 48f);
            yield return PressureBound(controller, "coach yard north", P(-31.5f, 52f), Vector3.forward,
                45, true, 56f, -31.5f);
        }
        finally
        {
            foreach (var collider in ignored)
                if (collider != null) Physics.IgnoreCollision(controller, collider, false);
        }
    }

    sealed class PushProbe
    {
        public CollisionFlags flags;
        public Vector3 end;
    }

    static IEnumerator PressureBound(CharacterController controller, string label, Vector3 start,
        Vector3 direction, int frames, bool fixedIsZ, float fixedCoord, float spanCoord)
    {
        var wall = FindWalkBound(fixedIsZ, fixedCoord, spanCoord);
        Assert.IsNotNull(wall, $"{label}: intended WalkBounds wall does not exist");

        float startAxis = fixedIsZ ? start.z : start.x;
        float directionAxis = fixedIsZ ? direction.z : direction.x;
        float requestedEnd = startAxis + directionAxis * frames * 0.22f;
        bool commandsPastWall = directionAxis > 0f
            ? requestedEnd >= fixedCoord + 1f
            : requestedEnd <= fixedCoord - 1f;
        Assert.IsTrue(commandsPastWall,
            $"{label}: push only commands axis {requestedEnd:F2} toward wall {fixedCoord:F2}; " +
            "a missing wall could still pass");

        var probe = new PushProbe();
        yield return PushFrom(controller, start, direction, frames, probe);

        Assert.AreNotEqual(0, (int)(probe.flags & CollisionFlags.Sides),
            $"{label}: push never reported a side collision");
        float gap = Mathf.Sqrt(wall.bounds.SqrDistance(controller.bounds.center));
        float contactDistance = controller.radius + controller.skinWidth + 0.18f;
        Assert.LessOrEqual(gap, contactDistance,
            $"{label}: stopped {gap:F2}m from intended wall, so another collider absorbed the push");

        float endAxis = fixedIsZ ? probe.end.z : probe.end.x;
        if (directionAxis > 0f)
            Assert.LessOrEqual(endAxis, fixedCoord + 0.05f, $"{label}: crossed through intended wall");
        else
            Assert.GreaterOrEqual(endAxis, fixedCoord - 0.05f, $"{label}: crossed through intended wall");

        Debug.Log($"[GmRouteTest] {label}: start={startAxis:F2} requested={requestedEnd:F2} " +
                  $"end={endAxis:F2} wall={fixedCoord:F2} gap={gap:F2} flags={probe.flags}");
    }

    static BoxCollider FindWalkBound(bool fixedIsZ, float fixedCoord, float spanCoord)
    {
        var root = GameObject.Find("WalkBounds");
        Assert.IsNotNull(root, "WalkBounds root missing");
        var matches = new List<BoxCollider>();
        foreach (var wall in root.GetComponentsInChildren<BoxCollider>(true))
        {
            var b = wall.bounds;
            bool horizontal = b.size.x > b.size.z;
            if (horizontal != fixedIsZ) continue;
            float actualFixed = fixedIsZ ? b.center.z : b.center.x;
            float spanMin = fixedIsZ ? b.min.x : b.min.z;
            float spanMax = fixedIsZ ? b.max.x : b.max.z;
            if (Mathf.Abs(actualFixed - fixedCoord) <= 0.12f &&
                spanCoord >= spanMin - 0.05f && spanCoord <= spanMax + 0.05f)
                matches.Add(wall);
        }
        Assert.AreEqual(1, matches.Count,
            $"expected one WalkBounds wall at {(fixedIsZ ? "z" : "x")}={fixedCoord:F2}, " +
            $"span={spanCoord:F2}; found {matches.Count}");
        return matches[0];
    }

    static List<Collider> IgnoreNonWalkBoundCollisions(CharacterController controller, bool ignore)
    {
        var root = GameObject.Find("WalkBounds");
        Assert.IsNotNull(root, "WalkBounds root missing");
        var changed = new List<Collider>();
        foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (collider == controller || !collider.enabled || collider.isTrigger) continue;
            if (collider.transform == root.transform || collider.transform.IsChildOf(root.transform)) continue;
            Physics.IgnoreCollision(controller, collider, ignore);
            changed.Add(collider);
        }
        Physics.SyncTransforms();
        return changed;
    }

    /// Teleport-then-push. The CharacterController has to be disabled across the reposition or it
    /// fights the move and the player arrives somewhere other than the requested start.
    static IEnumerator PushFrom(CharacterController controller, Vector3 start, Vector3 direction, int frames,
        PushProbe probe = null)
    {
        controller.enabled = false;
        controller.transform.position = start + Vector3.up * 0.1f;
        controller.enabled = true;
        yield return PushFor(controller, direction, frames, probe);
        if (probe != null) probe.end = controller.transform.position;
    }

    static IEnumerator WalkTo(CharacterController controller, Vector3 target, float tolerance)
    {
        int stalled = 0;
        int budget = Mathf.CeilToInt(Vector3.Distance(Flat(controller.transform.position), target) / 0.22f) + 80;
        for (int i = 0; i < budget; i++)
        {
            Vector3 at = Flat(controller.transform.position);
            Vector3 remaining = target - at;
            if (remaining.magnitude <= tolerance) yield break;
            Vector3 before = controller.transform.position;
            controller.Move(remaining.normalized * Mathf.Min(0.22f, remaining.magnitude));
            if (Vector3.Distance(before, controller.transform.position) < 0.002f) stalled++; else stalled = 0;
            if (stalled > 35) Assert.Fail($"route stalled at {at} while walking to {target}; nearby=" +
                string.Join(", ", NearbyColliderPaths(controller)));
            yield return null;
        }
        Assert.Fail($"route budget exhausted at {Flat(controller.transform.position)} while walking to {target}");
    }

    static IEnumerator PushFor(CharacterController controller, Vector3 direction, int frames,
        PushProbe probe = null)
    {
        for (int i = 0; i < frames; i++)
        {
            var flags = controller.Move(direction * 0.22f);
            if (probe != null) probe.flags |= flags;
            yield return null;
        }
    }

    static void DisablePlayerInput(GameObject player)
    {
        foreach (var behaviour in player.GetComponents<Behaviour>())
            if (behaviour.GetType().Name == "GmPlayer") behaviour.enabled = false;
    }

    static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);
    static Vector3 P(float x, float z) => new Vector3(x, 0f, z);

    static IEnumerable<string> NearbyColliderPaths(CharacterController controller)
    {
        foreach (var collider in Physics.OverlapSphere(controller.bounds.center, 1.25f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (collider == controller) continue;
            string path = collider.name;
            for (Transform t = collider.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            Bounds b = collider.bounds;
            yield return $"{path}[c={b.center.x:F1},{b.center.z:F1} s={b.size.x:F1},{b.size.z:F1}]";
        }
    }
}
