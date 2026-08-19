using System;
using System.Reflection;
using NUnit.Framework;

public sealed class GmBonesMatchTests
{
    static readonly Assembly GameAssembly = typeof(GmRunStore).Assembly;
    static Type MatchType => GameAssembly.GetType("GmBonesMatch");

    [Test]
    public void BonesMatchTypeExists()
    {
        Assert.That(MatchType, Is.Not.Null);
    }

    [Test]
    public void SameSeedAndChoicesReplayExactly()
    {
        object first = NewMatch(918273UL, null);
        object second = NewMatch(918273UL, null);
        for (int decision = 0; decision < 3; decision++)
        {
            Assert.That(Read<int[]>(first, "CurrentDice"), Is.EqualTo(Read<int[]>(second, "CurrentDice")));
            Assert.That(Choose(first, GmBonesChoice.Bank, -1, out string firstError), Is.True, firstError);
            Assert.That(Choose(second, GmBonesChoice.Bank, -1, out string secondError), Is.True, secondError);
            Assert.That(Read<string>(first, "StateFingerprint"), Is.EqualTo(Read<string>(second, "StateFingerprint")));
        }
    }

    [Test]
    public void ThreeRoundsUseApprovedOrderAndExactlyThreePlayerChoices()
    {
        object match = NewMatch(1UL, Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6));
        AssertCheckpoint(match, 1, 0, 0);
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out string firstError), Is.True, firstError);
        AssertCheckpoint(match, 2, 18, 36);
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out string secondError), Is.True, secondError);
        AssertCheckpoint(match, 3, 36, 36);
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out string thirdError), Is.True, thirdError);
        Assert.That(Read<string>(match, "Phase"), Is.EqualTo("Complete"));
        Assert.That(Read<string>(match, "Result"), Is.EqualTo("Tie"));
        Assert.That(Read<int>(match, "PlayerDecisionCount"), Is.EqualTo(3));
        string before = Read<string>(match, "StateFingerprint");
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out _), Is.False);
        Assert.That(Read<string>(match, "StateFingerprint"), Is.EqualTo(before));
    }

    [Test]
    public void PressRequiresALegalLockAndIllegalActionsDoNotMutate()
    {
        object match = NewMatch(2UL, Dice(4, 5, 6));
        string before = Read<string>(match, "StateFingerprint");
        Assert.That(Choose(match, GmBonesChoice.Press, -1, out _), Is.False);
        Assert.That(Choose(match, GmBonesChoice.Press, 3, out _), Is.False);
        Assert.That(Choose(match, GmBonesChoice.Bank, 0, out _), Is.False);
        Assert.That(Read<string>(match, "StateFingerprint"), Is.EqualTo(before));
        Assert.That(Read<int>(match, "PlayerDecisionCount"), Is.Zero);
    }

    [Test]
    public void FinalLoadedSixChallengeRestoresHonestLossAndProceedKeepsAlteredResult()
    {
        int[] loaded = Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2);
        object challenge = NewMatch(3UL, loaded);
        BankThreeTimes(challenge);
        Assert.That(Read<string>(challenge, "Phase"), Is.EqualTo("AwaitingIntervention"));
        Assert.That(Read<int>(challenge, "AldricTotal"), Is.EqualTo(53));
        Assert.That(Resolve(challenge, true, out string challengeError), Is.True, challengeError);
        Assert.That(Read<string>(challenge, "Phase"), Is.EqualTo("Complete"));
        Assert.That(Read<int>(challenge, "AldricTotal"), Is.EqualTo(36));
        Assert.That(Read<string>(challenge, "Result"), Is.EqualTo("PlayerWin"));

        object proceed = NewMatch(3UL, loaded);
        BankThreeTimes(proceed);
        Assert.That(Resolve(proceed, false, out string proceedError), Is.True, proceedError);
        Assert.That(Read<int>(proceed, "AldricTotal"), Is.EqualTo(53));
        Assert.That(Read<string>(proceed, "Result"), Is.EqualTo("Tie"));
    }

    [Test]
    public void ControlledReplayCanReachEveryTerminalResult()
    {
        object playerWin = NewMatch(4UL, Dice(6,6,6, 4,4,4, 4,4,4, 6,6,6, 6,6,6, 4,4,4));
        CompleteBankMatch(playerWin);
        Assert.That(Read<string>(playerWin, "Result"), Is.EqualTo("PlayerWin"));

        object aldricWin = NewMatch(5UL, Dice(4,4,4, 6,6,6, 6,6,6, 4,4,4, 4,4,4, 6,6,6));
        CompleteBankMatch(aldricWin);
        Assert.That(Read<string>(aldricWin, "Result"), Is.EqualTo("AldricWin"));

        object tie = NewMatch(6UL, Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6));
        CompleteBankMatch(tie);
        Assert.That(Read<string>(tie, "Result"), Is.EqualTo("Tie"));
    }

    [Test]
    public void SnapshotsResumeExactlyAtChoicePendingAndComplete()
    {
        int[] replay = Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2);
        object choice = NewMatch(7UL, replay);
        Assert.That(Choose(choice, GmBonesChoice.Bank, -1, out _), Is.True);
        object restoredChoice = Restore(Export(choice));
        AssertSameState(choice, restoredChoice);
        Assert.That(Choose(choice, GmBonesChoice.Bank, -1, out _), Is.True);
        Assert.That(Choose(restoredChoice, GmBonesChoice.Bank, -1, out _), Is.True);
        AssertSameState(choice, restoredChoice);

        Assert.That(Choose(choice, GmBonesChoice.Bank, -1, out _), Is.True);
        object restoredPending = Restore(Export(choice));
        AssertSameState(choice, restoredPending);
        Assert.That(Resolve(choice, true, out _), Is.True);
        Assert.That(Resolve(restoredPending, true, out _), Is.True);
        AssertSameState(choice, restoredPending);
        AssertSameState(choice, Restore(Export(choice)));
    }

    [Test]
    public void RestoreRejectsCorruptDiceTotalsRngPhaseFingerprintAndSession()
    {
        object match = NewMatch(8UL, Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6));
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out _), Is.True);
        RejectMutation(match, "currentDice", new[] { 0, 6, 6 });
        RejectMutation(match, "playerTotal", -1);
        RejectMutation(match, "randomState", 0U);
        RejectMutation(match, "round", 3);
        RejectMutation(match, "stateFingerprint", "forged");
        object snapshot = Export(match);
        object session = Field(snapshot, "session");
        session.GetType().GetField("currentFingerprint").SetValue(session, "forged");
        Assert.That(TryRestore(snapshot, out _, out _), Is.False);
    }

    [Test]
    public void RestoreRejectsForgedHonestInterventionTotal()
    {
        object match = NewMatch(9UL, Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2));
        BankThreeTimes(match);
        object snapshot = Export(match);
        snapshot.GetType().GetField("honestAldricTotal").SetValue(snapshot, 35);
        Assert.That(TryRestore(snapshot, out _, out _), Is.False);
    }

    [Test]
    public void ResultIsUnavailableUntilTheMatchCompletes()
    {
        object match = NewMatch(10UL, Dice(6,6,6));
        Assert.That(Read<bool>(match, "HasResult"), Is.False);
        object[] arguments = { GmBonesMatchResult.Tie };
        Assert.That((bool)Invoke(match, "TryGetResult", arguments), Is.False);
        Assert.That(() => Read<string>(match, "Result"), Throws.InvalidOperationException);
    }

    [Test]
    public void RestoreRejectsEveryTerminalOutcomeThatDisagreesWithTotals()
    {
        AssertForgedResults(
            Dice(6,6,6, 4,4,4, 4,4,4, 6,6,6, 6,6,6, 4,4,4),
            "PlayerWin", "AldricWin", "Tie");
        AssertForgedResults(
            Dice(4,4,4, 6,6,6, 6,6,6, 4,4,4, 4,4,4, 6,6,6),
            "AldricWin", "PlayerWin", "Tie");
        AssertForgedResults(
            Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6),
            "Tie", "PlayerWin", "AldricWin");
    }

    [Test]
    public void LoadedSixReceiptIsExactCloneSafeAndPersistsAfterResolution()
    {
        int[] replay = Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2);
        object match = NewMatch(11UL, replay);
        Assert.That(Read<object>(match, "InterventionReceipt"), Is.Null);
        BankThreeTimes(match);

        object receipt = Read<object>(match, "InterventionReceipt");
        Assert.That(Field(receipt, "lockedSlot"), Is.EqualTo(0));
        Assert.That(Field(receipt, "honestReroll"), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(Field(receipt, "displayedReroll"), Is.EqualTo(new[] { 6, 2 }));
        Assert.That(Field(receipt, "changedSlot"), Is.EqualTo(0));
        ((int[])Field(receipt, "honestReroll"))[0] = 5;
        Assert.That(Field(Read<object>(match, "InterventionReceipt"), "honestReroll"),
            Is.EqualTo(new[] { 1, 2 }));

        object restored = Restore(Export(match));
        Assert.That(Field(Read<object>(restored, "InterventionReceipt"), "displayedReroll"),
            Is.EqualTo(new[] { 6, 2 }));
        Assert.That(Resolve(restored, false, out _), Is.True);
        Assert.That(Field(Read<object>(restored, "InterventionReceipt"), "honestReroll"),
            Is.EqualTo(new[] { 1, 2 }));
        AssertSameState(restored, Restore(Export(restored)));
    }

    [Test]
    public void RestoreRejectsForgedOrMissingLoadedSixReceipt()
    {
        int[] replay = Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2);
        object match = NewMatch(12UL, replay);
        BankThreeTimes(match);
        RejectReceiptMutation(match, "lockedSlot", 1);
        RejectReceiptMutation(match, "changedSlot", 1);
        RejectReceiptMutation(match, "honestReroll", new[] { 2, 1 });
        RejectReceiptMutation(match, "displayedReroll", new[] { 5, 2 });
        object missing = Export(match);
        missing.GetType().GetField("interventionReceipt").SetValue(missing, null);
        Assert.That(TryRestore(missing, out _, out _), Is.False);
    }

    [Test]
    public void RestoreRejectsNonCanonicalBonesActionAndInterventionIds()
    {
        object match = NewMatch(13UL, Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6));
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out _), Is.True);
        object badAction = Export(match);
        object session = Field(badAction, "session");
        Array actions = (Array)Field(session, "actions");
        actions.GetValue(0).GetType().GetField("actionId").SetValue(actions.GetValue(0), "round-1:steal");
        Assert.That(TryRestore(badAction, out _, out _), Is.False);

        int[] replay = Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2);
        object pending = NewMatch(14UL, replay);
        BankThreeTimes(pending);
        object badIntervention = Export(pending);
        object intervention = Field(Field(badIntervention, "session"), "intervention");
        intervention.GetType().GetField("interventionId").SetValue(intervention, "generic-cheat");
        Assert.That(TryRestore(badIntervention, out _, out _), Is.False);
    }

    [Test]
    public void RestoreRejectsCoordinatedLoadedSixReceiptForgery()
    {
        int[] replay = Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,5, 6,2,1, 1,2);
        object match = NewMatch(16UL, replay);
        BankThreeTimes(match);
        object snapshot = Export(match);
        object receipt = Field(snapshot, "interventionReceipt");
        receipt.GetType().GetField("honestReroll").SetValue(receipt, new[] { 2, 1 });
        receipt.GetType().GetField("displayedReroll").SetValue(receipt, new[] { 2, 6 });
        receipt.GetType().GetField("changedSlot").SetValue(receipt, 1);
        Assert.That(TryRestore(snapshot, out _, out _), Is.False);
    }

    [Test]
    public void RestoreRejectsCanonicalLookingActionsThatWereNeverPlayed()
    {
        object match = NewMatch(17UL,
            Dice(6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6, 6,6,6));
        CompleteBankMatch(match);
        for (int round = 0; round < 3; round++)
        {
            for (int lockIndex = 0; lockIndex < 3; lockIndex++)
            {
                object snapshot = Export(match);
                object session = Field(snapshot, "session");
                Array actions = (Array)Field(session, "actions");
                object action = actions.GetValue(round);
                action.GetType().GetField("actionId").SetValue(action,
                    $"round-{round + 1}:press:{lockIndex}");
                Assert.That(TryRestore(snapshot, out _, out _), Is.False,
                    $"round {round + 1}, lock {lockIndex}");
            }
        }
    }

    [Test]
    public void RestoreRejectsNullGameCraftActionArrayWithoutThrowing()
    {
        object match = NewMatch(18UL, Dice(6,6,6));
        object snapshot = Export(match);
        object session = Field(snapshot, "session");
        session.GetType().GetField("actions").SetValue(session, null);
        Assert.That(() => TryRestore(snapshot, out _, out _), Throws.Nothing);
        Assert.That(TryRestore(snapshot, out _, out _), Is.False);
    }

    static object NewMatch(ulong seed, int[] replayDice)
    {
        ConstructorInfo constructor = MatchType.GetConstructor(new[] { typeof(ulong), typeof(int[]) });
        Assert.That(constructor, Is.Not.Null, "missing stable-seed/replay constructor");
        return constructor.Invoke(new object[] { seed, replayDice });
    }

    static void AssertCheckpoint(object match, int round, int playerTotal, int aldricTotal)
    {
        Assert.That(Read<string>(match, "Phase"), Is.EqualTo("AwaitingPlayerChoice"));
        Assert.That(Read<int>(match, "Round"), Is.EqualTo(round));
        Assert.That(Read<int>(match, "PlayerTotal"), Is.EqualTo(playerTotal));
        Assert.That(Read<int>(match, "AldricTotal"), Is.EqualTo(aldricTotal));
    }

    static void BankThreeTimes(object match)
    {
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out _), Is.True);
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out _), Is.True);
        Assert.That(Choose(match, GmBonesChoice.Bank, -1, out _), Is.True);
    }

    static void CompleteBankMatch(object match)
    {
        BankThreeTimes(match);
        if (Read<string>(match, "Phase") == "AwaitingIntervention")
            Assert.That(Resolve(match, true, out _), Is.True);
        Assert.That(Read<string>(match, "Phase"), Is.EqualTo("Complete"));
    }

    static bool Choose(object match, GmBonesChoice choice, int lockIndex, out string error)
    {
        object[] arguments = { choice, lockIndex, string.Empty };
        bool result = (bool)Invoke(match, "TryChoose", arguments);
        error = (string)arguments[2];
        return result;
    }

    static bool Resolve(object match, bool challenge, out string error)
    {
        object[] arguments = { challenge, string.Empty };
        bool result = (bool)Invoke(match, "TryResolveIntervention", arguments);
        error = (string)arguments[1];
        return result;
    }

    static object Export(object match) => Invoke(match, "ExportSnapshot", Array.Empty<object>());

    static object Restore(object snapshot)
    {
        Assert.That(TryRestore(snapshot, out object restored, out string error), Is.True, error);
        return restored;
    }

    static bool TryRestore(object snapshot, out object restored, out string error)
    {
        MethodInfo method = MatchType.GetMethod("TryRestore", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "missing TryRestore");
        object[] arguments = { snapshot, null, string.Empty };
        bool result = (bool)method.Invoke(null, arguments);
        restored = arguments[1];
        error = (string)arguments[2];
        return result;
    }

    static void RejectMutation(object match, string fieldName, object value)
    {
        object snapshot = Export(match);
        FieldInfo field = snapshot.GetType().GetField(fieldName);
        Assert.That(field, Is.Not.Null, $"snapshot missing {fieldName}");
        field.SetValue(snapshot, value);
        Assert.That(TryRestore(snapshot, out _, out _), Is.False, fieldName);
    }

    static void AssertSameState(object first, object second)
    {
        Assert.That(Read<string>(first, "StateFingerprint"), Is.EqualTo(Read<string>(second, "StateFingerprint")));
        Assert.That(Read<int[]>(first, "CurrentDice"), Is.EqualTo(Read<int[]>(second, "CurrentDice")));
    }

    static void AssertForgedResults(int[] replay, string actual, params string[] forged)
    {
        object match = NewMatch(15UL, replay);
        CompleteBankMatch(match);
        Assert.That(Read<string>(match, "Result"), Is.EqualTo(actual));
        foreach (string forgedResult in forged)
        {
            object snapshot = Export(match);
            FieldInfo resultField = snapshot.GetType().GetField("result");
            resultField.SetValue(snapshot, Enum.Parse(resultField.FieldType, forgedResult));
            object session = Field(snapshot, "session");
            FieldInfo terminal = session.GetType().GetField("terminalResult");
            string sessionName = forgedResult == "PlayerWin" ? "Win" :
                forgedResult == "AldricWin" ? "Loss" : "Tie";
            terminal.SetValue(session, Enum.Parse(terminal.FieldType, sessionName));
            Assert.That(TryRestore(snapshot, out _, out _), Is.False,
                $"{actual} totals were accepted as {forgedResult}");
        }
    }

    static void RejectReceiptMutation(object match, string fieldName, object value)
    {
        object snapshot = Export(match);
        object receipt = Field(snapshot, "interventionReceipt");
        receipt.GetType().GetField(fieldName).SetValue(receipt, value);
        Assert.That(TryRestore(snapshot, out _, out _), Is.False, fieldName);
    }

    static T Read<T>(object instance, string property)
    {
        PropertyInfo info = instance.GetType().GetProperty(property);
        Assert.That(info, Is.Not.Null, $"missing {property}");
        object value;
        try { value = info.GetValue(instance); }
        catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        if (typeof(T) == typeof(string) && value != null && value.GetType() != typeof(string))
            return (T)(object)value.ToString();
        return (T)value;
    }

    static object Field(object instance, string name) => instance.GetType().GetField(name).GetValue(instance);

    static object Invoke(object instance, string name, object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"missing {name}");
        try { return method.Invoke(instance, arguments); }
        catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
    }

    static int[] Dice(params int[] dice) => dice;
}
