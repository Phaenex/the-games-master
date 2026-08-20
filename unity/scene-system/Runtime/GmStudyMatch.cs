using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Nyx.GameCraft;

public enum GmStudyMatchPhase
{
    AwaitingMove,
    AwaitingIntervention,
    Complete
}

public enum GmStudyMatchResult
{
    PlayerWin,
    AldricWin
}

[Serializable]
public sealed class GmStudyInterventionReceipt
{
    public string interventionId = string.Empty;
    public int positionIndex;
    public string positionId = string.Empty;
    public string actionId = string.Empty;
    public string originalFen = string.Empty;
    public string alteredFen = string.Empty;
    public string overrideFromSquare = string.Empty;
    public string overrideToSquare = string.Empty;
    public string capturedPiece = string.Empty;
    public string overrideText = string.Empty;
    public string[] evidenceFacts = Array.Empty<string>();
    public string evidenceIconId = string.Empty;
    public string honestFingerprint = string.Empty;
    public string alteredFingerprint = string.Empty;
    public string response = string.Empty;

    public GmStudyInterventionReceipt DeepCopy() => new GmStudyInterventionReceipt
    {
        interventionId = interventionId,
        positionIndex = positionIndex,
        positionId = positionId,
        actionId = actionId,
        originalFen = originalFen,
        alteredFen = alteredFen,
        overrideFromSquare = overrideFromSquare,
        overrideToSquare = overrideToSquare,
        capturedPiece = capturedPiece,
        overrideText = overrideText,
        evidenceFacts = (string[])(evidenceFacts ?? Array.Empty<string>()).Clone(),
        evidenceIconId = evidenceIconId,
        honestFingerprint = honestFingerprint,
        alteredFingerprint = alteredFingerprint,
        response = response
    };
}

[Serializable]
public sealed class GmStudyMatchSnapshot
{
    public int schemaVersion = 1;
    public ulong seed;
    public int positionIndex;
    public int correctCount;
    public int playerDecisionCount;
    public bool interventionUsed;
    public GmStudyMatchPhase phase;
    public GmStudyMatchResult result;
    public bool hasResult;
    public GmStudyBoardVariant currentBoardVariant;
    public string currentFen = string.Empty;
    public string[] actionJournal = Array.Empty<string>();
    public GmStudyInterventionReceipt interventionReceipt;
    public string stateFingerprint = string.Empty;
    public GameSessionSnapshot session;

    public GmStudyMatchSnapshot DeepCopy() => new GmStudyMatchSnapshot
    {
        schemaVersion = schemaVersion,
        seed = seed,
        positionIndex = positionIndex,
        correctCount = correctCount,
        playerDecisionCount = playerDecisionCount,
        interventionUsed = interventionUsed,
        phase = phase,
        result = result,
        hasResult = hasResult,
        currentBoardVariant = currentBoardVariant,
        currentFen = currentFen,
        actionJournal = (string[])(actionJournal ?? Array.Empty<string>()).Clone(),
        interventionReceipt = interventionReceipt?.DeepCopy(),
        stateFingerprint = stateFingerprint,
        session = session?.DeepCopy()
    };
}

public sealed class GmStudyMatch
{
    const string ChallengeResponse = "intervention:challenge";
    const string ProceedResponse = "intervention:proceed";

    readonly ulong seed;
    readonly List<string> actionJournal = new List<string>();
    DeterministicGameSession session;
    int positionIndex;
    int correctCount;
    int playerDecisionCount;
    bool interventionUsed;
    GmStudyMatchPhase phase;
    GmStudyMatchResult result;
    bool hasResult;
    GmStudyBoardVariant currentBoardVariant;
    string currentFen;
    GmStudyInterventionReceipt interventionReceipt;

    public GmStudyMatch(ulong seed)
    {
        this.seed = seed;
        phase = GmStudyMatchPhase.AwaitingMove;
        currentBoardVariant = GmStudyBoardVariant.Original;
        currentFen = GmStudyRules.GetPosition(0).originalFen;
        session = new DeterministicGameSession(GmStudyRules.GameId, seed, ComputeFingerprint());
    }

    public ulong Seed => seed;
    public int PositionIndex => positionIndex;
    public int CorrectCount => correctCount;
    public int PlayerDecisionCount => playerDecisionCount;
    public bool InterventionUsed => interventionUsed;
    public GmStudyMatchPhase Phase => phase;
    public bool HasResult => hasResult;
    public GmStudyMatchResult Result => hasResult
        ? result
        : throw new InvalidOperationException("the Study match has not completed");
    public GmStudyBoardVariant CurrentBoardVariant => currentBoardVariant;
    public string CurrentFen => currentFen;
    public GmStudyMoveCard[] CurrentMoveCards => phase == GmStudyMatchPhase.Complete
        ? Array.Empty<GmStudyMoveCard>()
        : GmStudyRules.GetPosition(positionIndex).cards;
    public string[] ActionJournal => actionJournal.ToArray();
    public GmStudyInterventionReceipt InterventionReceipt => interventionReceipt?.DeepCopy();
    public string StateFingerprint => ComputeFingerprint();

    public bool TryGetResult(out GmStudyMatchResult matchResult)
    {
        matchResult = result;
        return hasResult;
    }

    public bool TryChoose(string actionId, out string error)
    {
        if (phase != GmStudyMatchPhase.AwaitingMove)
            return Refuse("the Study match is not waiting for a move", out error);
        if (!GmStudyRules.TryGetCard(positionIndex, actionId, out GmStudyMoveCard card))
            return Refuse("the move card is not legal for this position", out error);
        string before = ComputeFingerprint();
        if (before != session.CurrentFingerprint)
            throw new InvalidOperationException("Study and session state diverged before the move");

        playerDecisionCount++;
        actionJournal.Add(actionId);
        bool opensIntervention = card.isCorrect && correctCount == 1 && !interventionUsed;
        if (opensIntervention)
        {
            OpenIntervention(card, before, out error);
            return true;
        }

        correctCount += GmStudyRules.ScoreMove(card);
        AdvanceOrComplete();
        string after = ComputeFingerprint();
        if (!session.TryRecordDecision(playerDecisionCount - 1, actionId, before, after, out error))
            throw new InvalidOperationException($"Study decision could not be recorded: {error}");
        if (phase == GmStudyMatchPhase.Complete &&
            !session.TryComplete(ToTerminalResult(result), out error))
            throw new InvalidOperationException($"Study result could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    public bool TryResolveIntervention(bool challenge, out string error)
    {
        if (phase != GmStudyMatchPhase.AwaitingIntervention || interventionReceipt == null)
            return Refuse("no Study intervention is waiting", out error);
        if (!session.TryResolveIntervention(challenge, out string selectedFingerprint, out error))
            return false;

        actionJournal.Add(challenge ? ChallengeResponse : ProceedResponse);
        interventionReceipt.response = challenge ? "challenge" : "proceed";
        if (challenge)
        {
            correctCount++;
            currentBoardVariant = GmStudyBoardVariant.Original;
            currentFen = GmStudyRules.GetPosition(positionIndex).originalFen;
            Complete(GmStudyMatchResult.PlayerWin);
        }
        else
        {
            AdvanceOrComplete();
        }

        if (ComputeFingerprint() != selectedFingerprint)
            throw new InvalidOperationException("Study intervention response disagrees with its authenticated branch");
        if (phase == GmStudyMatchPhase.Complete &&
            !session.TryComplete(ToTerminalResult(result), out error))
            throw new InvalidOperationException($"Study result could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    void OpenIntervention(GmStudyMoveCard card, string before, out string error)
    {
        GmStudyPosition position = GmStudyRules.GetPosition(positionIndex);
        correctCount++;
        string honestFingerprint = ComputeFingerprint();
        correctCount--;
        interventionUsed = true;
        currentBoardVariant = GmStudyBoardVariant.Altered;
        currentFen = position.alteredFen;
        phase = GmStudyMatchPhase.AwaitingIntervention;
        string alteredFingerprint = ComputeFingerprint();
        interventionReceipt = new GmStudyInterventionReceipt
        {
            interventionId = GmStudyRules.InterventionId,
            positionIndex = positionIndex,
            positionId = position.id,
            actionId = card.actionId,
            originalFen = position.originalFen,
            alteredFen = position.alteredFen,
            overrideFromSquare = position.overrideFromSquare,
            overrideToSquare = position.overrideToSquare,
            capturedPiece = position.capturedPiece,
            overrideText = position.overrideText,
            evidenceFacts = (string[])position.evidenceFacts.Clone(),
            evidenceIconId = position.evidenceIconId,
            honestFingerprint = honestFingerprint,
            alteredFingerprint = alteredFingerprint
        };

        if (!session.TryRecordDecision(playerDecisionCount - 1, card.actionId,
                before, honestFingerprint, out error))
            throw new InvalidOperationException($"Study decision could not be recorded: {error}");
        if (!session.TryOpenIntervention(GmStudyRules.InterventionId,
                alteredFingerprint, out error))
            throw new InvalidOperationException($"Study intervention could not be recorded: {error}");
        error = string.Empty;
    }

    void AdvanceOrComplete()
    {
        if (correctCount >= 2)
        {
            Complete(GmStudyMatchResult.PlayerWin);
            return;
        }
        if (positionIndex >= GmStudyRules.PositionCount - 1)
        {
            Complete(GmStudyMatchResult.AldricWin);
            return;
        }
        positionIndex++;
        phase = GmStudyMatchPhase.AwaitingMove;
        currentBoardVariant = GmStudyBoardVariant.Original;
        currentFen = GmStudyRules.GetPosition(positionIndex).originalFen;
    }

    void Complete(GmStudyMatchResult terminalResult)
    {
        result = terminalResult;
        hasResult = true;
        phase = GmStudyMatchPhase.Complete;
    }

    public GmStudyMatchSnapshot ExportSnapshot() => new GmStudyMatchSnapshot
    {
        seed = seed,
        positionIndex = positionIndex,
        correctCount = correctCount,
        playerDecisionCount = playerDecisionCount,
        interventionUsed = interventionUsed,
        phase = phase,
        result = result,
        hasResult = hasResult,
        currentBoardVariant = currentBoardVariant,
        currentFen = currentFen,
        actionJournal = actionJournal.ToArray(),
        interventionReceipt = interventionReceipt?.DeepCopy(),
        stateFingerprint = ComputeFingerprint(),
        session = session.ExportSnapshot()
    };

    public static bool TryRestore(GmStudyMatchSnapshot snapshot,
        out GmStudyMatch match, out string error)
    {
        match = null;
        if (snapshot == null) return Refuse("snapshot is missing", out error);
        if (snapshot.schemaVersion != 1) return Refuse("snapshot schema is unsupported", out error);
        if (snapshot.actionJournal == null || snapshot.currentFen == null || snapshot.session == null)
            return Refuse("snapshot state is missing", out error);
        if (!Enum.IsDefined(typeof(GmStudyMatchPhase), snapshot.phase) ||
            !Enum.IsDefined(typeof(GmStudyMatchResult), snapshot.result) ||
            !Enum.IsDefined(typeof(GmStudyBoardVariant), snapshot.currentBoardVariant))
            return Refuse("snapshot enum state is invalid", out error);

        GmStudyMatch replay = new GmStudyMatch(snapshot.seed);
        foreach (string action in snapshot.actionJournal)
        {
            bool applied;
            if (action == ChallengeResponse) applied = replay.TryResolveIntervention(true, out _);
            else if (action == ProceedResponse) applied = replay.TryResolveIntervention(false, out _);
            else applied = replay.TryChoose(action, out _);
            if (!applied) return Refuse("snapshot action journal cannot be replayed", out error);
        }

        GmStudyMatchSnapshot canonical = replay.ExportSnapshot();
        if (!SnapshotsEqual(canonical, snapshot))
            return Refuse("snapshot does not match deterministic Study replay", out error);
        match = replay;
        error = string.Empty;
        return true;
    }

    string ComputeFingerprint()
    {
        // Position/FEN transitions are authenticated by full deterministic replay. The session
        // fingerprint follows scoring and player decisions so Proceed can advance without inventing
        // a fourth player decision that the shared GameCraft session never received.
        var data = new StringBuilder(160);
        data.Append(seed.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(correctCount).Append('|').Append(playerDecisionCount).Append('|');
        foreach (string action in actionJournal)
            if (action != ChallengeResponse && action != ProceedResponse)
                data.Append(action).Append(';');
        ulong hash = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(data.ToString()))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    static bool SnapshotsEqual(GmStudyMatchSnapshot left, GmStudyMatchSnapshot right)
    {
        if (left.schemaVersion != right.schemaVersion || left.seed != right.seed ||
            left.positionIndex != right.positionIndex || left.correctCount != right.correctCount ||
            left.playerDecisionCount != right.playerDecisionCount ||
            left.interventionUsed != right.interventionUsed || left.phase != right.phase ||
            left.result != right.result || left.hasResult != right.hasResult ||
            left.currentBoardVariant != right.currentBoardVariant || left.currentFen != right.currentFen ||
            left.stateFingerprint != right.stateFingerprint ||
            !left.actionJournal.SequenceEqual(right.actionJournal) ||
            !ReceiptsEqual(left.interventionReceipt, right.interventionReceipt)) return false;
        return SessionsEqual(left.session, right.session);
    }

    static bool ReceiptsEqual(GmStudyInterventionReceipt left, GmStudyInterventionReceipt right)
    {
        if (left == null || right == null) return left == right;
        if (left.evidenceFacts == null || right.evidenceFacts == null) return false;
        return left.interventionId == right.interventionId &&
            left.positionIndex == right.positionIndex && left.positionId == right.positionId &&
            left.actionId == right.actionId && left.originalFen == right.originalFen &&
            left.alteredFen == right.alteredFen && left.overrideFromSquare == right.overrideFromSquare &&
            left.overrideToSquare == right.overrideToSquare && left.capturedPiece == right.capturedPiece &&
            left.overrideText == right.overrideText && left.evidenceIconId == right.evidenceIconId &&
            left.honestFingerprint == right.honestFingerprint &&
            left.alteredFingerprint == right.alteredFingerprint && left.response == right.response &&
            left.evidenceFacts.SequenceEqual(right.evidenceFacts);
    }

    static bool SessionsEqual(GameSessionSnapshot left, GameSessionSnapshot right)
    {
        if (left == null || right == null || left.schemaVersion != right.schemaVersion ||
            left.gameId != right.gameId || left.runSeed != right.runSeed ||
            left.initialFingerprint != right.initialFingerprint ||
            left.currentFingerprint != right.currentFingerprint || left.decisionIndex != right.decisionIndex ||
            left.phase != right.phase || left.terminalResult != right.terminalResult ||
            left.actions == null || right.actions == null || left.actions.Length != right.actions.Length)
            return false;
        for (int index = 0; index < left.actions.Length; index++)
        {
            GameActionRecord a = left.actions[index];
            GameActionRecord b = right.actions[index];
            if (a == null || b == null || a.decisionIndex != b.decisionIndex ||
                a.actionId != b.actionId || a.beforeFingerprint != b.beforeFingerprint ||
                a.afterFingerprint != b.afterFingerprint) return false;
        }
        GameInterventionRecord x = left.intervention;
        GameInterventionRecord y = right.intervention;
        if (x == null || y == null) return x == y;
        return x.decisionIndex == y.decisionIndex && x.interventionId == y.interventionId &&
            x.beforeFingerprint == y.beforeFingerprint && x.alteredFingerprint == y.alteredFingerprint &&
            x.resolved == y.resolved && x.challenged == y.challenged &&
            x.resolvedFingerprint == y.resolvedFingerprint;
    }

    static GameTerminalResult ToTerminalResult(GmStudyMatchResult value) =>
        value == GmStudyMatchResult.PlayerWin ? GameTerminalResult.Win : GameTerminalResult.Loss;

    static bool Refuse(string message, out string error)
    {
        error = message;
        return false;
    }
}
