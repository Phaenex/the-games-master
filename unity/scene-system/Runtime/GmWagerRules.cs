using System;

public enum GmWagerMatchResult { PlayerWin, AldricWin, Tie }

public static class GmWagerRules
{
    public const string GameId = "seven-debts.wager";
    public const string InterventionId = "aldric-clause-swap";
    public const string EvidenceIconId = "clause-swap";
    public const int ContractCount = 3;
    // Completing Flames, Shut the Box, Bones and Study grants one sovereign each -- Wager always
    // begins with exactly four. See docs/SEVEN-DEBTS-CANON-2026-08-19.md "Game 5: Wager".
    public const int StartingSovereigns = 4;
    public const int WinThreshold = 5;
    public const int TieWealth = 4;
    public static readonly int[] ReadCost = { 1, 2, 3 };
    public static readonly int[] NetValues = { -2, 1, 3 };

    // All 3! permutations of {-2, 1, 3} across the three contract IDs. "assigned by the run seed"
    // -- the seed selects one permutation deterministically; SD4's test bar is every permutation.
    static readonly int[][] Permutations =
    {
        new[] { -2, 1, 3 },
        new[] { -2, 3, 1 },
        new[] { 1, -2, 3 },
        new[] { 1, 3, -2 },
        new[] { 3, -2, 1 },
        new[] { 3, 1, -2 },
    };

    public static int PermutationCount => Permutations.Length;

    public static int[] ValuesForSeed(ulong seed)
    {
        int index = (int)(seed % (ulong)Permutations.Length);
        return (int[])Permutations[index].Clone();
    }

    public static GmWagerMatchResult ResolveResult(int wealth)
    {
        if (wealth >= WinThreshold) return GmWagerMatchResult.PlayerWin;
        if (wealth == TieWealth) return GmWagerMatchResult.Tie;
        return GmWagerMatchResult.AldricWin;
    }

    public static void ValidateContractIndex(int contractIndex)
    {
        if (contractIndex < 0 || contractIndex >= ContractCount)
            throw new ArgumentOutOfRangeException(nameof(contractIndex));
    }
}
