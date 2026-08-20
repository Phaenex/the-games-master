using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class GmWagerRulesTests
{
    [Test]
    public void StartingSovereignsMatchesTheFourCompletedTableGames()
    {
        Assert.That(GmWagerRules.StartingSovereigns, Is.EqualTo(4));
    }

    [Test]
    public void ReadCostsSumToMoreThanStartingSovereignsSoAllThreeCannotBeAfforded()
    {
        Assert.That(GmWagerRules.ReadCost.Sum(), Is.GreaterThan(GmWagerRules.StartingSovereigns));
        Assert.That(GmWagerRules.ReadCost, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [TestCase(5, GmWagerMatchResult.PlayerWin)]
    [TestCase(6, GmWagerMatchResult.PlayerWin)]
    [TestCase(4, GmWagerMatchResult.Tie)]
    [TestCase(3, GmWagerMatchResult.AldricWin)]
    [TestCase(0, GmWagerMatchResult.AldricWin)]
    [TestCase(-2, GmWagerMatchResult.AldricWin)]
    public void WealthThresholdsMatchCanon(int wealth, GmWagerMatchResult expected)
    {
        Assert.That(GmWagerRules.ResolveResult(wealth), Is.EqualTo(expected));
    }

    [Test]
    public void EveryPermutationContainsExactlyTheThreeAuthoredValuesOnce()
    {
        for (ulong seed = 0; seed < (ulong)GmWagerRules.PermutationCount; seed++)
        {
            int[] values = GmWagerRules.ValuesForSeed(seed);
            Assert.That(values, Has.Length.EqualTo(3));
            CollectionAssert.AreEquivalent(new[] { -2, 1, 3 }, values);
        }
    }

    [Test]
    public void AllSixPermutationsAreDistinctAndSeedWrapsDeterministically()
    {
        var seen = new HashSet<string>();
        for (ulong seed = 0; seed < (ulong)GmWagerRules.PermutationCount; seed++)
            seen.Add(string.Join(",", GmWagerRules.ValuesForSeed(seed)));
        Assert.That(seen.Count, Is.EqualTo(GmWagerRules.PermutationCount));

        // The seed space is far larger than 6 permutations; the mapping must wrap deterministically
        // rather than throw or produce an out-of-range permutation.
        Assert.That(GmWagerRules.ValuesForSeed(0), Is.EqualTo(GmWagerRules.ValuesForSeed((ulong)GmWagerRules.PermutationCount)));
        Assert.That(GmWagerRules.ValuesForSeed(ulong.MaxValue), Is.Not.Null);
    }
}
