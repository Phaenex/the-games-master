using System;
using System.Linq;

public enum GmBonesChoice
{
    Bank,
    Press
}

public enum GmBonesMatchResult
{
    PlayerWin,
    AldricWin,
    Tie
}

/// <summary>
/// Pure rules for Seven Debts table three. Dice are explicit inputs so tests, replays, and saves do
/// not depend on Unity frame time or a global random source.
/// </summary>
public static class GmBonesRules
{
    public static int ScoreBank(int[] dice)
    {
        ValidateDice(dice, 3, nameof(dice));
        return dice.Sum();
    }

    public static int ScorePress(int lockedDie, int[] rerolledDice)
    {
        ValidateDie(lockedDie, nameof(lockedDie));
        ValidateDice(rerolledDice, 2, nameof(rerolledDice));
        if (rerolledDice[0] == 1 || rerolledDice[1] == 1) return 0;
        return lockedDie + rerolledDice[0] + rerolledDice[1] + 3;
    }

    public static GmBonesChoice ChooseAldricAction(int round, bool playerFinishedRound,
        int playerTotal, int aldricTotal, int initialSum)
    {
        ValidateRound(round);
        if (playerTotal < 0) throw new ArgumentOutOfRangeException(nameof(playerTotal));
        if (aldricTotal < 0) throw new ArgumentOutOfRangeException(nameof(aldricTotal));
        if (initialSum < 3 || initialSum > 18) throw new ArgumentOutOfRangeException(nameof(initialSum));
        if (round == 3 && playerFinishedRound && aldricTotal + initialSum < playerTotal)
            return GmBonesChoice.Press;
        return initialSum <= 9 ? GmBonesChoice.Press : GmBonesChoice.Bank;
    }

    public static int ChooseLockIndex(int[] dice)
    {
        ValidateDice(dice, 3, nameof(dice));
        int best = 0;
        for (int index = 1; index < dice.Length; index++)
            if (dice[index] > dice[best]) best = index;
        return best;
    }

    public static bool IsPlayerFirst(int round)
    {
        ValidateRound(round);
        return round == 1 || round == 3;
    }

    public static bool TryLoadedSixIntervention(bool isFinalAldricTurn,
        int playerFinalTotal, int aldricTotalBeforeTurn, int lockedDie,
        int rerolledA, int rerolledB, out int alteredTurnScore)
    {
        if (playerFinalTotal < 0) throw new ArgumentOutOfRangeException(nameof(playerFinalTotal));
        if (aldricTotalBeforeTurn < 0) throw new ArgumentOutOfRangeException(nameof(aldricTotalBeforeTurn));
        ValidateDie(lockedDie, nameof(lockedDie));
        ValidateDie(rerolledA, nameof(rerolledA));
        ValidateDie(rerolledB, nameof(rerolledB));
        alteredTurnScore = 0;
        if (!isFinalAldricTurn || aldricTotalBeforeTurn >= playerFinalTotal) return false;
        if (ScorePress(lockedDie, new[] { rerolledA, rerolledB }) != 0) return false;

        int changedA = rerolledA == 1 ? 6 : rerolledA;
        int changedB = rerolledA != 1 && rerolledB == 1 ? 6 : rerolledB;
        int candidate = ScorePress(lockedDie, new[] { changedA, changedB });
        if (candidate == 0 || aldricTotalBeforeTurn + candidate < playerFinalTotal) return false;
        alteredTurnScore = candidate;
        return true;
    }

    public static GmBonesMatchResult ResolveMatch(int playerTotal, int aldricTotal)
    {
        if (playerTotal < 0) throw new ArgumentOutOfRangeException(nameof(playerTotal));
        if (aldricTotal < 0) throw new ArgumentOutOfRangeException(nameof(aldricTotal));
        if (playerTotal > aldricTotal) return GmBonesMatchResult.PlayerWin;
        if (aldricTotal > playerTotal) return GmBonesMatchResult.AldricWin;
        return GmBonesMatchResult.Tie;
    }

    static void ValidateRound(int round)
    {
        if (round < 1 || round > 3) throw new ArgumentOutOfRangeException(nameof(round));
    }

    static void ValidateDice(int[] dice, int expectedCount, string parameter)
    {
        if (dice == null || dice.Length != expectedCount)
            throw new ArgumentException($"exactly {expectedCount} dice are required", parameter);
        for (int index = 0; index < dice.Length; index++) ValidateDie(dice[index], parameter);
    }

    static void ValidateDie(int die, string parameter)
    {
        if (die < 1 || die > 6) throw new ArgumentOutOfRangeException(parameter, "die must be between 1 and 6");
    }
}
