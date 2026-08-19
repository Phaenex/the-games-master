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
        session = restoredSession;
    }

    public GmBonesMatchPhase Phase => phase;
    public GmBonesMatchResult Result => result;
    public int Round => round;
    public int PlayerTotal => playerTotal;
    public int AldricTotal => aldricTotal;
    public int PlayerDecisionCount => playerDecisionCount;
    public int[] CurrentDice => (int[])currentDice.Clone();
    public string StateFingerprint => ComputeFingerprint();

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

        if (challenge) aldricTotal = honestAldricTotal;
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
        if (snapshot.phase == GmBonesMatchPhase.AwaitingIntervention &&
            (snapshot.honestAldricTotal < 0 || snapshot.honestAldricTotal == snapshot.aldricTotal))
            return Refuse("snapshot intervention totals are invalid", out error);
        if (snapshot.phase != GmBonesMatchPhase.AwaitingIntervention && snapshot.honestAldricTotal != 0)
            return Refuse("snapshot retains intervention-only state", out error);
        if (!DeterministicGameSession.TryRestore(snapshot.session, out DeterministicGameSession restored, out error))
            return false;

        var candidate = new GmBonesMatch(snapshot, restored);
        if (candidate.ComputeFingerprint() != snapshot.stateFingerprint || restored.CurrentFingerprint != snapshot.stateFingerprint)
            return Refuse("snapshot fingerprint does not match match and session state", out error);
        if (snapshot.phase == GmBonesMatchPhase.AwaitingIntervention &&
            candidate.FingerprintWithAldricTotal(snapshot.honestAldricTotal) !=
            snapshot.session.intervention.beforeFingerprint)
            return Refuse("snapshot honest intervention state disagrees with its session", out error);
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
            aldricTotal += alteredScore;
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
