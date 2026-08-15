// The ninth-bell crossing became a scene change, so the black that hides it has to survive one.
//
// The failure this guards against is specific and unrecoverable in canon terms: if the curtain is
// absent or drops early, the player watches the house dissolve into the Entry Hall. Threshold
// Refusal is the whole opening -- he is never admitted, never sees a door open -- and a visible
// load contradicts it more completely than any line of dialogue could repair.
//
// Reflection rather than direct references, for the same reason GmPrologueRouteTests uses it: this
// is an asmdef test assembly and the runtime types live in the predefined Assembly-CSharp, which an
// asmdef cannot reference. PlayMode is required regardless -- GmSceneArrival does its work in a
// Start coroutine and waits on real seconds, and EditMode runs neither.
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GmSceneCurtainTests
{
    GameObject host;
    MonoBehaviour curtain;

    [SetUp]
    public void MakeCurtain()
    {
        host = new GameObject("CurtainFixture");
        curtain = (MonoBehaviour)host.AddComponent(TypeNamed("GmSceneCurtain"));
    }

    [TearDown]
    public void ClearFixture()
    {
        if (host != null) Object.DestroyImmediate(host);
    }

    [Test]
    public void ACurtainStartsDownSoAnOrdinarySceneIsNotBornBlack()
    {
        Assert.AreEqual(0f, Property<float>(curtain, "Alpha"), 0.0001f);
        Assert.IsFalse(Property<bool>(curtain, "IsRaised"),
            "every scene that is not a crossing would open on black");
    }

    [Test]
    public void RaisingIsInstant()
    {
        // The cut lands on the same frame as the ninth toll. A fade here would be a fade to black
        // ON the ninth bell, which is a softer and different moment than the one written.
        Invoke(curtain, "Raise");
        Assert.AreEqual(1f, Property<float>(curtain, "Alpha"), 0.0001f);
        Assert.IsTrue(Property<bool>(curtain, "IsRaised"));
    }

    [UnityTest]
    public IEnumerator OpeningReturnsSightCompletely()
    {
        Invoke(curtain, "Raise");
        yield return (IEnumerator)curtain.GetType()
            .GetMethod("Open").Invoke(curtain, new object[] { 0.1f });
        Assert.AreEqual(0f, Property<float>(curtain, "Alpha"), 0.0001f,
            "a curtain that never fully opens leaves a permanent veil over the game");
        Assert.IsFalse(Property<bool>(curtain, "IsRaised"));
    }

    [UnityTest]
    public IEnumerator TheArrivalLeavesARoomEnteredWithoutACrossingAlone()
    {
        // Loading the Entry Hall from a save, or from the boot menu during development, must not put
        // the player through a wake they never had -- and above all must not leave control blocked,
        // which is the shape of bug that ends a playthrough with no error at all.
        var arrivalObject = new GameObject("ArrivalFixture");
        try
        {
            var arrival = (MonoBehaviour)arrivalObject.AddComponent(TypeNamed("GmSceneArrival"));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!Property<bool>(arrival, "HasArrived") && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(Property<bool>(arrival, "HasArrived"),
                "the arrival never resolved in a room with no curtain over it");
            Assert.AreEqual(0f, Property<float>(curtain, "Alpha"), 0.0001f,
                "an ordinary room entry raised the crossing curtain");
        }
        finally { Object.DestroyImmediate(arrivalObject); }
    }

    [UnityTest]
    public IEnumerator TheArrivalOpensTheCurtainOnTheRoomHeWasNeverLetInto()
    {
        var arrivalObject = new GameObject("ArrivalFixture");
        try
        {
            Invoke(curtain, "Raise");
            Assert.IsTrue(Property<bool>(curtain, "IsRaised"), "fixture did not start behind black");

            var arrival = (MonoBehaviour)arrivalObject.AddComponent(TypeNamed("GmSceneArrival"));
            SetField(arrival, "irisSeconds", 0.1f);
            SetField(arrival, "cardSeconds", 0.1f);

            float deadline = Time.realtimeSinceStartup + 20f;
            while (!Property<bool>(arrival, "HasArrived") && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(Property<bool>(arrival, "HasArrived"),
                "the arrival never finished — the player is stranded in black inside the house");
            Assert.AreEqual(0f, Property<float>(curtain, "Alpha"), 0.0001f, "sight never came back");
            Assert.IsEmpty(Property<string>(curtain, "Card"), "the closing card was left on screen");
        }
        finally { Object.DestroyImmediate(arrivalObject); }
    }

    static System.Type TypeNamed(string name)
    {
        foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type found = assembly.GetType(name);
            if (found != null) return found;
        }
        Assert.Fail($"runtime type '{name}' does not exist in any loaded assembly");
        return null;
    }

    static void Invoke(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method}() is missing");
        info.Invoke(target, null);
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} field is missing");
        field.SetValue(target, value);
    }

    static T Property<T>(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name);
        Assert.IsNotNull(property, $"{target.GetType().Name}.{name} property is missing");
        return (T)property.GetValue(target);
    }
}
