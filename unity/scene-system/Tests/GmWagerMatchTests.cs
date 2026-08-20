using System;
using System.Linq;
using NUnit.Framework;

public sealed class GmWagerMatchTests
{
    // seed 0 -> values [-2, 1, 3] for contracts [0, 1, 2].
    const ulong SeedZero = 0UL;

    [Test]
    public void AcceptingTheNegativeContractImmediatelyLosesWithNoIntervention()
    {
        var match = new GmWagerMatch(SeedZero, 4);
        Assert.That(match.TryAccept(out string error), Is.True, error);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.Complete));
        Assert.That(match.Result, Is.EqualTo(GmWagerMatchResult.AldricWin));
        Assert.That(match.InterventionReceipt, Is.Null);
    }

    [Test]
    public void ReadingCostsScaleByPositionNotByAttemptCount()
    {
        var match = new GmWagerMatch(SeedZero, 4);
        Assert.That(match.CanReadCurrentContract, Is.True);
        Assert.That(match.TryPass(out _), Is.True, "must be able to pass the first contract unread");
        Assert.That(match.Sovereigns, Is.EqualTo(4), "passing costs nothing");

        Assert.That(match.CurrentContractIndex, Is.EqualTo(1));
        Assert.That(match.TryReadCurrentContract(out string error), Is.True, error);
        Assert.That(match.Sovereigns, Is.EqualTo(2), "the second contract's read costs 2, not 1");
        Assert.That(match.TryReadCurrentContract(out error), Is.False,
            "a contract already read cannot be read again");
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void InsufficientFundsRefusesReadWithoutSpendingAnything()
    {
        var match = new GmWagerMatch(SeedZero, 4);
        match.TryReadCurrentContract(out _); // cost 1, sovereigns -> 3
        match.TryPass(out _);
        match.TryReadCurrentContract(out _); // cost 2, sovereigns -> 1
        match.TryPass(out _);
        Assert.That(match.Sovereigns, Is.EqualTo(1));
        Assert.That(match.CanReadCurrentContract, Is.False, "position 2 costs 3, only 1 remains");
        Assert.That(match.TryReadCurrentContract(out string error), Is.False);
        Assert.That(match.Sovereigns, Is.EqualTo(1), "a refused read must not spend sovereigns");
    }

    [Test]
    public void TheFinalContractCannotBePassed()
    {
        var match = new GmWagerMatch(SeedZero, 4);
        match.TryPass(out _);
        match.TryPass(out _);
        Assert.That(match.CurrentContractIndex, Is.EqualTo(2));
        Assert.That(match.CanPass, Is.False);
        Assert.That(match.TryPass(out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void AcceptIsAlwaysLegalAtEveryContractNoSoftLock(int passesFirst)
    {
        // Accept always applies and moves the state machine forward -- either straight to
        // Complete, or into AwaitingIntervention on a raw win pending Challenge/Proceed. Either
        // way it never leaves the match stuck back in AwaitingDecision.
        var match = new GmWagerMatch(SeedZero, 4);
        for (int i = 0; i < passesFirst; i++) Assert.That(match.TryPass(out _), Is.True);
        Assert.That(match.TryAccept(out string error), Is.True, error);
        Assert.That(match.Phase, Is.Not.EqualTo(GmWagerMatchPhase.AwaitingDecision));
        if (match.Phase == GmWagerMatchPhase.AwaitingIntervention)
            Assert.That(match.TryResolveIntervention(true, out string resolveError), Is.True, resolveError);
        Assert.That(match.HasResult, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AcceptingASecondContractWinIsChallengedOrProceededExactly(bool challenge)
    {
        // Pass contract 0 (-2), accept contract 1 (1): wealth 4+1=5, a raw win. The only eligible
        // swap source is contract 0 (-2) -- substituting 3 would still win, so it's ineligible.
        var match = new GmWagerMatch(SeedZero, 4);
        Assert.That(match.TryPass(out _), Is.True);
        Assert.That(match.TryAccept(out string error), Is.True, error);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.AwaitingIntervention));
        var receipt = match.InterventionReceipt;
        Assert.That(receipt, Is.Not.Null);
        Assert.That(receipt.acceptedContractIndex, Is.EqualTo(1));
        Assert.That(receipt.sourceContractIndex, Is.EqualTo(0));
        Assert.That(receipt.honestValue, Is.EqualTo(1));
        Assert.That(receipt.alteredValue, Is.EqualTo(-2));
        Assert.That(match.ContractValue(1), Is.EqualTo(-2), "the accepted slot shows the altered value while pending");
        Assert.That(match.ContractValue(0), Is.EqualTo(1), "both inserts swap, preserving the permutation");

        Assert.That(match.TryResolveIntervention(challenge, out error), Is.True, error);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.Complete));
        if (challenge)
        {
            Assert.That(match.Result, Is.EqualTo(GmWagerMatchResult.PlayerWin));
            Assert.That(match.ContractValue(1), Is.EqualTo(1), "challenge restores the honest value");
            Assert.That(match.ContractValue(0), Is.EqualTo(-2), "challenge restores both inserts");
        }
        else
        {
            Assert.That(match.Result, Is.EqualTo(GmWagerMatchResult.AldricWin));
            Assert.That(match.ContractValue(1), Is.EqualTo(-2), "proceeding keeps the altered value");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForcedAcceptanceOfTheFinalContractStillReachesInterventionWhenItWouldWin(bool challenge)
    {
        // Pass 0 (-2) and 1 (1) unread, forced to accept 2 (3): wealth 4+3=7, a raw win. Only
        // contract 0 (-2) is eligible (substituting 1 gives wealth 5, still a win).
        var match = new GmWagerMatch(SeedZero, 4);
        match.TryPass(out _);
        match.TryPass(out _);
        Assert.That(match.CurrentContractIndex, Is.EqualTo(2));
        Assert.That(match.TryAccept(out string error), Is.True, error);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.AwaitingIntervention));
        Assert.That(match.InterventionReceipt.sourceContractIndex, Is.EqualTo(0));

        Assert.That(match.TryResolveIntervention(challenge, out error), Is.True, error);
        Assert.That(match.Result, Is.EqualTo(challenge ? GmWagerMatchResult.PlayerWin : GmWagerMatchResult.AldricWin));
    }

    [Test]
    public void AcceptingAtExactlyTheTieBoundaryNeverTriggersAnIntervention()
    {
        // Read contract 0 (cost 1, sovereigns 3), pass, accept contract 1 (1): wealth 3+1=4, a
        // tie, not a win -- no intervention should trigger.
        var match = new GmWagerMatch(SeedZero, 4);
        Assert.That(match.TryReadCurrentContract(out _), Is.True);
        Assert.That(match.TryPass(out _), Is.True);
        Assert.That(match.TryAccept(out string error), Is.True, error);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.Complete));
        Assert.That(match.Result, Is.EqualTo(GmWagerMatchResult.Tie));
        Assert.That(match.InterventionReceipt, Is.Null, "a tie is not a win and must not be swapped");
    }

    [Test]
    public void SwapSourcePicksTheLowerValueEvenWhenItSitsAtTheHigherArrayIndex()
    {
        // seed 3 -> values [1, 3, -2]. Read contract 0 (cost 1, sovereigns 3), pass, accept
        // contract 1 (3): wealth 3+3=6, a raw win. BOTH other contracts are eligible here --
        // substituting contract 0 (1, index 0) gives wealth 4 (a tie); substituting contract 2
        // (-2, index 2) gives wealth 1 (a loss). The lower value (-2) sits at the HIGHER index,
        // so a source-selection bug that preferred "first eligible found" or "lowest index"
        // instead of "lowest value" would silently pick the wrong contract.
        var match = new GmWagerMatch(3UL, 4);
        Assert.That(match.TryReadCurrentContract(out _), Is.True);
        Assert.That(match.TryPass(out _), Is.True);
        Assert.That(match.TryAccept(out string error), Is.True, error);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.AwaitingIntervention));
        var receipt = match.InterventionReceipt;
        Assert.That(receipt.sourceContractIndex, Is.EqualTo(2), "the lowest VALUE, not the lowest index, must be chosen");
        Assert.That(receipt.honestValue, Is.EqualTo(3));
        Assert.That(receipt.alteredValue, Is.EqualTo(-2));

        Assert.That(match.TryResolveIntervention(false, out error), Is.True, error);
        Assert.That(match.Result, Is.EqualTo(GmWagerMatchResult.AldricWin));
    }

    [TestCase("payload")]
    [TestCase("journal")]
    [TestCase("result")]
    public void RestoreRejectsAnyForgedField(string field)
    {
        var match = new GmWagerMatch(SeedZero, 4);
        match.TryPass(out _);
        match.TryAccept(out _);
        match.TryResolveIntervention(true, out _);
        GmWagerMatchSnapshot snapshot = match.ExportSnapshot();

        switch (field)
        {
            case "payload": snapshot.contractValues[0] = 999; break;
            case "journal": snapshot.actionJournal = new[] { "pass", "accept", "intervention:proceed" }; break;
            case "result": snapshot.result = GmWagerMatchResult.AldricWin; break;
        }

        Assert.That(GmWagerMatch.TryRestore(snapshot, out GmWagerMatch restored, out string error), Is.False);
        Assert.That(restored, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void RestoreReplaysExactlyAndRejectsAnUnrecognizedAction()
    {
        // Read contract 0 (cost 1, sovereigns 3), pass, pass, forced-accept contract 2 (3):
        // wealth 3+3=6, a raw win, so this genuinely reaches and resolves an intervention --
        // unlike a read-then-accept-contract-1 fixture, which lands exactly on the tie boundary
        // and never opens one at all (TryResolveIntervention would silently no-op).
        var match = new GmWagerMatch(SeedZero, 4);
        Assert.That(match.TryReadCurrentContract(out _), Is.True);
        Assert.That(match.TryPass(out _), Is.True);
        Assert.That(match.TryPass(out _), Is.True);
        Assert.That(match.TryAccept(out _), Is.True);
        Assert.That(match.Phase, Is.EqualTo(GmWagerMatchPhase.AwaitingIntervention));
        Assert.That(match.TryResolveIntervention(false, out _), Is.True);
        Assert.That(match.HasResult, Is.True);
        GmWagerMatchSnapshot snapshot = match.ExportSnapshot();
        Assert.That(snapshot.actionJournal, Does.Contain("intervention:proceed"));

        Assert.That(GmWagerMatch.TryRestore(snapshot, out GmWagerMatch restored, out string error), Is.True, error);
        Assert.That(restored.Result, Is.EqualTo(match.Result));
        Assert.That(restored.Sovereigns, Is.EqualTo(match.Sovereigns));
        Assert.That(restored.ExportSnapshot().stateFingerprint, Is.EqualTo(snapshot.stateFingerprint));

        snapshot.actionJournal = snapshot.actionJournal.Concat(new[] { "bribe" }).ToArray();
        Assert.That(GmWagerMatch.TryRestore(snapshot, out GmWagerMatch corrupted, out string corruptError), Is.False);
        Assert.That(corrupted, Is.Null);
        Assert.That(corruptError, Is.Not.Empty);
    }

    [Test]
    public void NegativeStartingSovereignsIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GmWagerMatch(SeedZero, -1));
    }
}
