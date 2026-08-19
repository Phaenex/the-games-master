using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Nyx.GameCraft;

public enum GmBonesMatchPhase
{
    AwaitingPlayerChoice,
    AwaitingIntervention,
    Complete
}

[Serializable]
public sealed class GmBonesInterventionReceipt
{
    public int round;
    public int lockedSlot;
    public int[] honestReroll = Array.Empty<int>();
    public int[] displayedReroll = Array.Empty<int>();
    public int changedSlot;
    public int aldricTotalBeforeTurn;
    public int honestAldricTotal;
    public int alteredAldricTotal;

    public GmBonesInterventionReceipt DeepCopy() => new GmBonesInterventionReceipt
    {
        round = round,
        lockedSlot = lockedSlot,
        honestReroll = (int[])(honestReroll ?? Array.Empty<int>()).Clone(),
        displayedReroll = (int[])(displayedReroll ?? Array.Empty<int>()).Clone(),
        changedSlot = changedSlot,
        aldricTotalBeforeTurn = aldricTotalBeforeTurn,
        honestAldricTotal = honestAldricTotal,
        alteredAldricTotal = alteredAldricTotal
    };
}

[Serializable]
public sealed class GmBonesMatchSnapshot
{
    public int schemaVersion = 1;
    public ulong seed;
    public uint randomState;
    public int[] replayDice = Array.Empty<int>();
    public int replayIndex;
    public int[] currentDice = Array.Empty<int>();
    public int round;
    public int playerTotal;
    public int aldricTotal;
    public int playerDecisionCount;
    public GmBonesMatchPhase phase;
    public GmBonesMatchResult result;
    public bool hasResult;
    public int honestAldricTotal;
    public GmBonesInterventionReceipt interventionReceipt;
    public string stateFingerprint = string.Empty;
    public GameSessionSnapshot session;
}

public sealed class GmBonesMatch
{
    const string GameId = "seven-debts.bones";

    readonly ulong seed;
    readonly int[] replayDice;
    DeterministicGameSession session;
    uint randomState;
    int replayIndex;
    int[] currentDice;
    int round;
    int playerTotal;
    int aldricTotal;
    int playerDecisionCount;
    int honestAldricTotal;
    GmBonesInterventionReceipt interventionReceipt;
    GmBonesMatchPhase phase;
    GmBonesMatchResult result;
    bool hasResult;

    public GmBonesMatch(ulong seed, int[] replayDice)
    {
        this.seed = seed;
        this.replayDice = replayDice == null ? Array.Empty<int>() : (int[])replayDice.Clone();
        ValidateDiceValues(this.replayDice, nameof(replayDice));
        randomState = FoldSeed(seed);
        round = 1;
        phase = GmBonesMatchPhase.AwaitingPlayerChoice;
        currentDice = Roll(3);
        session = new DeterministicGameSession(GameId, seed, ComputeFingerprint());
    }

    GmBonesMatch(GmBonesMatchSnapshot snapshot, DeterministicGameSession restoredSession)
    {
        seed = snapshot.seed;
        randomState = snapshot.randomState;
        replayDice = (int[])snapshot.replayDice.Clone();
        replayIndex = snapshot.replayIndex;
        currentDice = (int[])snapshot.currentDice.Clone();
        round = snapshot.round;
        playerTotal = snapshot.playerTotal;
        aldricTotal = snapshot.aldricTotal;
        playerDecisionCount = snapshot.playerDecisionCount;
        phase = snapshot.phase;
        result = snapshot.result;
        hasResult = snapshot.hasResult;
        honestAldricTotal = snapshot.honestAldricTotal;
        interventionReceipt = snapshot.interventionReceipt?.DeepCopy();
        session = restoredSession;
    }

    public GmBonesMatchPhase Phase => phase;
    public bool HasResult => hasResult;
    public GmBonesMatchResult Result => hasResult
        ? result
        : throw new InvalidOperationException("the Bones match has not completed");
    public int Round => round;
    public int PlayerTotal => playerTotal;
    public int AldricTotal => aldricTotal;
    public int PlayerDecisionCount => playerDecisionCount;
    public int[] CurrentDice => (int[])currentDice.Clone();
    public GmBonesInterventionReceipt InterventionReceipt => interventionReceipt?.DeepCopy();
    public string StateFingerprint => ComputeFingerprint();

    public bool TryGetResult(out GmBonesMatchResult matchResult)
    {
        matchResult = result;
        return hasResult;
    }

    public bool TryChoose(GmBonesChoice choice, int lockIndex, out string error)
    {
        if (phase != GmBonesMatchPhase.AwaitingPlayerChoice)
            return Refuse("the match is not waiting for a player choice", out error);
        if (!Enum.IsDefined(typeof(GmBonesChoice), choice)) return Refuse("choice is invalid", out error);
        if (choice == GmBonesChoice.Bank && lockIndex != -1)
            return Refuse("bank does not accept a lock", out error);
        if (choice == GmBonesChoice.Press && (lockIndex < 0 || lockIndex >= currentDice.Length))
            return Refuse("press requires a legal die lock", out error);

        string before = ComputeFingerprint();
        int decisionRound = round;
        int turnScore = choice == GmBonesChoice.Bank
            ? GmBonesRules.ScoreBank(currentDice)
            : GmBonesRules.ScorePress(currentDice[lockIndex], Roll(2));
        playerTotal += turnScore;
        playerDecisionCount++;

        bool openedIntervention = AdvanceAfterPlayerTurn();
        string honestAfter = openedIntervention ? FingerprintWithAldricTotal(honestAldricTotal) : ComputeFingerprint();
        string actionId = choice == GmBonesChoice.Bank
            ? $"round-{decisionRound}:bank"
            : $"round-{decisionRound}:press:{lockIndex}";
        if (!session.TryRecordDecision(playerDecisionCount - 1, actionId, before, honestAfter, out error))
            throw new InvalidOperationException($"Bones and session state diverged: {error}");

        if (openedIntervention)
        {
            if (!session.TryOpenIntervention("aldric-loaded-six", ComputeFingerprint(), out error))
                throw new InvalidOperationException($"Bones intervention could not be recorded: {error}");
        }
        else if (phase == GmBonesMatchPhase.Complete &&
                 !session.TryComplete(ToTerminalResult(result), out error))
        {
            throw new InvalidOperationException($"Bones result could not be recorded: {error}");
        }

        error = string.Empty;
        return true;
    }

    public bool TryResolveIntervention(bool challenge, out string error)
    {
        if (phase != GmBonesMatchPhase.AwaitingIntervention)
            return Refuse("no loaded-six intervention is waiting", out error);
        if (!session.TryResolveIntervention(challenge, out string selectedFingerprint, out error)) return false;

        if (challenge) aldricTotal = interventionReceipt.honestAldricTotal;
        if (ComputeFingerprint() != selectedFingerprint)
            throw new InvalidOperationException("Bones intervention result disagrees with its session fingerprint");

        CompleteMatch();
        if (!session.TryComplete(ToTerminalResult(result), out error))
            throw new InvalidOperationException($"Bones result could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    public GmBonesMatchSnapshot ExportSnapshot() => new GmBonesMatchSnapshot
    {
        seed = seed,
        randomState = randomState,
        replayDice = (int[])replayDice.Clone(),
        replayIndex = replayIndex,
        currentDice = (int[])currentDice.Clone(),
        round = round,
        playerTotal = playerTotal,
        aldricTotal = aldricTotal,
        playerDecisionCount = playerDecisionCount,
        phase = phase,
        result = result,
        hasResult = hasResult,
        honestAldricTotal = honestAldricTotal,
        interventionReceipt = interventionReceipt?.DeepCopy(),
        stateFingerprint = ComputeFingerprint(),
        session = session.ExportSnapshot()
    };

    public static bool TryRestore(GmBonesMatchSnapshot snapshot, out GmBonesMatch match, out string error)
    {
        match = null;
        if (snapshot == null) return Refuse("snapshot is missing", out error);
        if (snapshot.schemaVersion != 1) return Refuse("snapshot schema is unsupported", out error);
        if (snapshot.randomState == 0) return Refuse("snapshot RNG state is invalid", out error);
        if (snapshot.replayDice == null || snapshot.currentDice == null)
            return Refuse("snapshot dice are missing", out error);
        if (!HasValidDice(snapshot.replayDice) || snapshot.currentDice.Length != 3 || !HasValidDice(snapshot.currentDice))
            return Refuse("snapshot dice are invalid", out error);
        if (snapshot.replayIndex < 0 || snapshot.replayIndex > snapshot.replayDice.Length)
            return Refuse("snapshot replay position is invalid", out error);
        if (snapshot.round < 1 || snapshot.round > 3 || snapshot.playerTotal < 0 || snapshot.aldricTotal < 0)
            return Refuse("snapshot round or totals are invalid", out error);
        if (snapshot.playerDecisionCount < 0 || snapshot.playerDecisionCount > 3)
            return Refuse("snapshot decision count is invalid", out error);
        if (!Enum.IsDefined(typeof(GmBonesMatchPhase), snapshot.phase) ||
            !Enum.IsDefined(typeof(GmBonesMatchResult), snapshot.result))
            return Refuse("snapshot phase or result is invalid", out error);
        if (snapshot.phase == GmBonesMatchPhase.AwaitingPlayerChoice &&
            (snapshot.playerDecisionCount >= 3 || snapshot.round != snapshot.playerDecisionCount + 1))
            return Refuse("snapshot choice phase disagrees with round progress", out error);
        if (snapshot.phase != GmBonesMatchPhase.AwaitingPlayerChoice &&
            (snapshot.round != 3 || snapshot.playerDecisionCount != 3))
            return Refuse("snapshot terminal phase disagrees with round progress", out error);
        if ((snapshot.phase == GmBonesMatchPhase.Complete) != snapshot.hasResult)
            return Refuse("snapshot result and phase disagree", out error);
        if (snapshot.phase == GmBonesMatchPhase.Complete &&
            snapshot.result != GmBonesRules.ResolveMatch(snapshot.playerTotal, snapshot.aldricTotal))
            return Refuse("snapshot result disagrees with its totals", out error);
        if (!ValidateReceiptShape(snapshot, out error)) return false;
        if (!DeterministicGameSession.TryRestore(snapshot.session, out DeterministicGameSession restored, out error))
            return false;

        var candidate = new GmBonesMatch(snapshot, restored);
        if (candidate.ComputeFingerprint() != snapshot.stateFingerprint || restored.CurrentFingerprint != snapshot.stateFingerprint)
            return Refuse("snapshot fingerprint does not match match and session state", out error);
        if (!ValidateSessionContract(snapshot, candidate, out error)) return false;
        if (restored.GameId != GameId || restored.RunSeed != snapshot.seed ||
            restored.DecisionIndex != snapshot.playerDecisionCount)
            return Refuse("snapshot session identity or progress disagrees", out error);
        if (!PhaseAgrees(snapshot.phase, restored.Phase)) return Refuse("snapshot session phase disagrees", out error);
        if (snapshot.hasResult && restored.TerminalResult != ToTerminalResult(snapshot.result))
            return Refuse("snapshot terminal result disagrees", out error);
        if (!snapshot.hasResult && restored.TerminalResult != GameTerminalResult.None)
            return Refuse("active snapshot has a terminal session result", out error);

        match = candidate;
        error = string.Empty;
        return true;
    }

    bool AdvanceAfterPlayerTurn()
    {
        if (round == 1)
        {
            PlayAldricTurn(false);
            round = 2;
            PlayAldricTurn(false);
            currentDice = Roll(3);
            return false;
        }
        if (round == 2)
        {
            round = 3;
            currentDice = Roll(3);
            return false;
        }
        return PlayAldricTurn(true);
    }

    bool PlayAldricTurn(bool finalTurn)
    {
        currentDice = Roll(3);
        GmBonesChoice choice = GmBonesRules.ChooseAldricAction(round, playerDecisionCount == round,
            playerTotal, aldricTotal, GmBonesRules.ScoreBank(currentDice));
        if (choice == GmBonesChoice.Bank)
        {
            aldricTotal += GmBonesRules.ScoreBank(currentDice);
            if (finalTurn) CompleteMatch();
            return false;
        }

        int lockIndex = GmBonesRules.ChooseLockIndex(currentDice);
        int[] rerolled = Roll(2);
        int honestScore = GmBonesRules.ScorePress(currentDice[lockIndex], rerolled);
        honestAldricTotal = aldricTotal + honestScore;
        if (GmBonesRules.TryLoadedSixIntervention(finalTurn, playerTotal, aldricTotal,
            currentDice[lockIndex], rerolled[0], rerolled[1], out int alteredScore))
        {
            int beforeTurn = aldricTotal;
            int changedSlot = rerolled[0] == 1 ? 0 : 1;
            int[] displayed = (int[])rerolled.Clone();
            displayed[changedSlot] = 6;
            aldricTotal += alteredScore;
            interventionReceipt = new GmBonesInterventionReceipt
            {
                round = round,
                lockedSlot = lockIndex,
                honestReroll = (int[])rerolled.Clone(),
                displayedReroll = displayed,
                changedSlot = changedSlot,
                aldricTotalBeforeTurn = beforeTurn,
                honestAldricTotal = honestAldricTotal,
                alteredAldricTotal = aldricTotal
            };
            phase = GmBonesMatchPhase.AwaitingIntervention;
            return true;
        }

        aldricTotal = honestAldricTotal;
        honestAldricTotal = 0;
        if (finalTurn) CompleteMatch();
        return false;
    }

    void CompleteMatch()
    {
        phase = GmBonesMatchPhase.Complete;
        result = GmBonesRules.ResolveMatch(playerTotal, aldricTotal);
        hasResult = true;
        honestAldricTotal = 0;
    }

    int[] Roll(int count)
    {
        var dice = new int[count];
        for (int index = 0; index < count; index++)
            dice[index] = replayIndex < replayDice.Length ? replayDice[replayIndex++] : NextRandomDie();
        return dice;
    }

    int NextRandomDie()
    {
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        return (int)(randomState % 6U) + 1;
    }

    string FingerprintWithAldricTotal(int value)
    {
        int altered = aldricTotal;
        aldricTotal = value;
        string fingerprint = ComputeFingerprint();
        aldricTotal = altered;
        return fingerprint;
    }

    string ComputeFingerprint()
    {
        var data = new StringBuilder(160 + replayDice.Length * 2);
        data.Append(seed.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(randomState.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(replayIndex).Append('|').Append(round).Append('|')
            .Append(playerTotal).Append('|').Append(aldricTotal).Append('|')
            .Append(playerDecisionCount).Append('|').Append(string.Join(",", currentDice)).Append('|')
            .Append(string.Join(",", replayDice));
        ulong hash = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(data.ToString()))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    static GameTerminalResult ToTerminalResult(GmBonesMatchResult value)
    {
        if (value == GmBonesMatchResult.PlayerWin) return GameTerminalResult.Win;
        if (value == GmBonesMatchResult.AldricWin) return GameTerminalResult.Loss;
        return GameTerminalResult.Tie;
    }

    static bool PhaseAgrees(GmBonesMatchPhase matchPhase, GameSessionPhase sessionPhase)
    {
        if (matchPhase == GmBonesMatchPhase.AwaitingPlayerChoice)
            return sessionPhase == GameSessionPhase.AwaitingDecision;
        if (matchPhase == GmBonesMatchPhase.AwaitingIntervention)
            return sessionPhase == GameSessionPhase.AwaitingInterventionResponse;
        return sessionPhase == GameSessionPhase.Complete;
    }

    static bool ValidateReceiptShape(GmBonesMatchSnapshot snapshot, out string error)
    {
        GmBonesInterventionReceipt receipt = snapshot.interventionReceipt;
        if (receipt == null)
        {
            if (snapshot.phase == GmBonesMatchPhase.AwaitingIntervention)
                return Refuse("pending intervention has no receipt", out error);
            if (snapshot.honestAldricTotal != 0)
                return Refuse("snapshot retains intervention-only state", out error);
            error = string.Empty;
            return true;
        }
        if (snapshot.phase == GmBonesMatchPhase.AwaitingPlayerChoice || snapshot.round != 3 ||
            snapshot.playerDecisionCount != 3)
            return Refuse("intervention receipt appears outside the final turn", out error);
        if (receipt.round != 3 || receipt.lockedSlot < 0 || receipt.lockedSlot > 2 ||
            receipt.changedSlot < 0 || receipt.changedSlot > 1 ||
            receipt.honestReroll == null || receipt.honestReroll.Length != 2 ||
            receipt.displayedReroll == null || receipt.displayedReroll.Length != 2 ||
            !HasValidDice(receipt.honestReroll) || !HasValidDice(receipt.displayedReroll) ||
            receipt.lockedSlot != GmBonesRules.ChooseLockIndex(snapshot.currentDice))
            return Refuse("intervention receipt dice or slots are invalid", out error);
        for (int index = 0; index < 2; index++)
        {
            int expected = index == receipt.changedSlot ? 6 : receipt.honestReroll[index];
            if (receipt.displayedReroll[index] != expected)
                return Refuse("intervention displayed reroll is invalid", out error);
        }
        int canonicalChangedSlot = receipt.honestReroll[0] == 1 ? 0 : 1;
        if (receipt.changedSlot != canonicalChangedSlot || receipt.honestReroll[receipt.changedSlot] != 1 ||
            GmBonesRules.ScorePress(snapshot.currentDice[receipt.lockedSlot], receipt.honestReroll) != 0 ||
            receipt.aldricTotalBeforeTurn < 0 ||
            receipt.honestAldricTotal != receipt.aldricTotalBeforeTurn)
            return Refuse("intervention honest receipt is invalid", out error);
        int alteredScore = GmBonesRules.ScorePress(snapshot.currentDice[receipt.lockedSlot], receipt.displayedReroll);
        if (!GmBonesRules.TryLoadedSixIntervention(true, snapshot.playerTotal,
                receipt.aldricTotalBeforeTurn, snapshot.currentDice[receipt.lockedSlot],
                receipt.honestReroll[0], receipt.honestReroll[1], out int expectedAlteredScore) ||
            alteredScore != expectedAlteredScore ||
            receipt.alteredAldricTotal != receipt.aldricTotalBeforeTurn + alteredScore)
            return Refuse("intervention altered receipt is invalid", out error);
        if (snapshot.phase == GmBonesMatchPhase.AwaitingIntervention &&
            (snapshot.honestAldricTotal != receipt.honestAldricTotal ||
             snapshot.aldricTotal != receipt.alteredAldricTotal))
            return Refuse("pending intervention totals disagree with its receipt", out error);
        if (snapshot.phase == GmBonesMatchPhase.Complete && snapshot.honestAldricTotal != 0)
            return Refuse("completed snapshot retains pending intervention state", out error);
        error = string.Empty;
        return true;
    }

    static bool ValidateSessionContract(GmBonesMatchSnapshot snapshot, GmBonesMatch candidate, out string error)
    {
        GameSessionSnapshot gameSession = snapshot.session;
        for (int index = 0; index < gameSession.actions.Length; index++)
        {
            string actionId = gameSession.actions[index].actionId;
            string bank = $"round-{index + 1}:bank";
            string pressPrefix = $"round-{index + 1}:press:";
            bool legalPress = actionId != null && actionId.StartsWith(pressPrefix, StringComparison.Ordinal) &&
                actionId.Length == pressPrefix.Length + 1 && actionId[actionId.Length - 1] >= '0' &&
                actionId[actionId.Length - 1] <= '2';
            if (actionId != bank && !legalPress)
                return Refuse("snapshot contains a non-canonical Bones action", out error);
        }

        GameInterventionRecord intervention = gameSession.intervention;
        if ((intervention == null) != (snapshot.interventionReceipt == null))
            return Refuse("snapshot receipt and session intervention disagree", out error);
        if (intervention == null)
        {
            error = string.Empty;
            return true;
        }
        if (intervention.interventionId != "aldric-loaded-six" || intervention.decisionIndex != 2 ||
            snapshot.phase == GmBonesMatchPhase.AwaitingPlayerChoice ||
            intervention.resolved != (snapshot.phase == GmBonesMatchPhase.Complete))
            return Refuse("snapshot contains an illegal Bones intervention", out error);
        GmBonesInterventionReceipt receipt = snapshot.interventionReceipt;
        if (candidate.FingerprintWithAldricTotal(receipt.honestAldricTotal) != intervention.beforeFingerprint ||
            candidate.FingerprintWithAldricTotal(receipt.alteredAldricTotal) != intervention.alteredFingerprint)
            return Refuse("snapshot receipt fingerprints disagree with the session intervention", out error);
        int selectedTotal = intervention.resolved && intervention.challenged
            ? receipt.honestAldricTotal
            : receipt.alteredAldricTotal;
        if (snapshot.aldricTotal != selectedTotal)
            return Refuse("snapshot selected intervention total is invalid", out error);
        error = string.Empty;
        return true;
    }

    static uint FoldSeed(ulong value)
    {
        uint folded = (uint)(value ^ (value >> 32));
        return folded == 0 ? 0x9E3779B9U : folded;
    }

    static bool HasValidDice(int[] dice) => dice.All(value => value >= 1 && value <= 6);

    static void ValidateDiceValues(int[] dice, string parameter)
    {
        if (!HasValidDice(dice)) throw new ArgumentOutOfRangeException(parameter, "dice must be between 1 and 6");
    }

    static bool Refuse(string message, out string error)
    {
        error = message;
        return false;
    }
}
