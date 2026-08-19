using System;
using System.Reflection;
using NUnit.Framework;

public sealed class GmBonesRulesTests
{
    static readonly Assembly GameAssembly = typeof(GmRunStore).Assembly;
    static Type RulesType => GameAssembly.GetType("GmBonesRules");

    [Test]
    public void BonesRulesTypeExists()
    {
        Assert.That(RulesType, Is.Not.Null);
    }

    [Test]
    public void BankAndPressUseTheApprovedScoring()
    {
        Assert.That(Call("ScoreBank", new[] { 2, 4, 6 }), Is.EqualTo(12));
        Assert.That(Call("ScorePress", 6, new[] { 2, 5 }), Is.EqualTo(16));
        Assert.That(Call("ScorePress", 6, new[] { 1, 5 }), Is.EqualTo(0));
        Assert.That(Call("ScorePress", 6, new[] { 1, 1 }), Is.EqualTo(0));
    }

    [Test]
    public void AldricPolicyAndLockChoiceAreDeterministic()
    {
        Assert.That(Call("ChooseAldricAction", 1, false, 0, 0, 9).ToString(), Is.EqualTo("Press"));
        Assert.That(Call("ChooseAldricAction", 1, false, 0, 0, 10).ToString(), Is.EqualTo("Bank"));
        Assert.That(Call("ChooseAldricAction", 3, true, 31, 18, 12).ToString(), Is.EqualTo("Press"));
        Assert.That(Call("ChooseLockIndex", new[] { 5, 6, 6 }), Is.EqualTo(1));
        Assert.That(Call("IsPlayerFirst", 1), Is.EqualTo(true));
        Assert.That(Call("IsPlayerFirst", 2), Is.EqualTo(false));
        Assert.That(Call("IsPlayerFirst", 3), Is.EqualTo(true));
    }

    [Test]
    public void LoadedSixOnlyPreventsAnHonestFinalLoss()
    {
        object[] eligible = { true, 30, 20, 6, 1, 2, 0 };
        Assert.That(Invoke("TryLoadedSixIntervention", eligible), Is.EqualTo(true));
        Assert.That(eligible[6], Is.EqualTo(17));

        object[] tooSmall = { true, 40, 20, 6, 1, 2, 0 };
        Assert.That(Invoke("TryLoadedSixIntervention", tooSmall), Is.EqualTo(false));
        object[] notFinal = { false, 30, 20, 6, 1, 2, 0 };
        Assert.That(Invoke("TryLoadedSixIntervention", notFinal), Is.EqualTo(false));
        object[] doubleBust = { true, 30, 20, 6, 1, 1, 0 };
        Assert.That(Invoke("TryLoadedSixIntervention", doubleBust), Is.EqualTo(false));
        object[] honestWin = { true, 20, 21, 6, 1, 2, 0 };
        Assert.That(Invoke("TryLoadedSixIntervention", honestWin), Is.EqualTo(false));
    }

    [Test]
    public void MatchResultIncludesTheApprovedTie()
    {
        Assert.That(Call("ResolveMatch", 20, 19).ToString(), Is.EqualTo("PlayerWin"));
        Assert.That(Call("ResolveMatch", 19, 20).ToString(), Is.EqualTo("AldricWin"));
        Assert.That(Call("ResolveMatch", 20, 20).ToString(), Is.EqualTo("Tie"));
    }

    static object Call(string name, params object[] arguments) => Invoke(name, arguments);

    static object Invoke(string name, object[] arguments)
    {
        if (RulesType == null) Assert.Ignore("Bones rules have not been implemented yet");
        MethodInfo method = RulesType.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"missing {name}");
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            throw error.InnerException;
        }
    }
}
