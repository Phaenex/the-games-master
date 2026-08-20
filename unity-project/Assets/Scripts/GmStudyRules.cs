using System;
using System.Linq;

public enum GmStudyBoardVariant
{
    Original,
    Altered
}

[Serializable]
public sealed class GmStudyMoveCard
{
    public string actionId = string.Empty;
    public string notation = string.Empty;
    public bool isCorrect;
    public string consequence = string.Empty;

    public GmStudyMoveCard DeepCopy() => new GmStudyMoveCard
    {
        actionId = actionId,
        notation = notation,
        isCorrect = isCorrect,
        consequence = consequence
    };
}

[Serializable]
public sealed class GmStudyPosition
{
    public string id = string.Empty;
    public string title = string.Empty;
    public string originalFen = string.Empty;
    public string alteredFen = string.Empty;
    public string overrideFromSquare = string.Empty;
    public string overrideToSquare = string.Empty;
    public string capturedPiece = string.Empty;
    public string overrideText = string.Empty;
    public string[] evidenceFacts = Array.Empty<string>();
    public string evidenceIconId = string.Empty;
    public GmStudyMoveCard[] cards = Array.Empty<GmStudyMoveCard>();

    public GmStudyPosition DeepCopy() => new GmStudyPosition
    {
        id = id,
        title = title,
        originalFen = originalFen,
        alteredFen = alteredFen,
        overrideFromSquare = overrideFromSquare,
        overrideToSquare = overrideToSquare,
        capturedPiece = capturedPiece,
        overrideText = overrideText,
        evidenceFacts = (string[])(evidenceFacts ?? Array.Empty<string>()).Clone(),
        evidenceIconId = evidenceIconId,
        cards = (cards ?? Array.Empty<GmStudyMoveCard>())
            .Select(card => card?.DeepCopy()).ToArray()
    };
}

public static class GmStudyRules
{
    public const string GameId = "seven-debts.study";
    public const string InterventionId = "aldric-arbiter-override";
    public const string EvidenceIconId = "arbiter-override";
    public const string BoardZoomFact = "Board zoom is available for every authored position.";
    public const string MoveExplanationFact =
        "Every move card includes notation and a plain-language tactical consequence.";
    public const string ReducedMotionFact =
        "Original and changed squares remain visible as static markers in reduced-motion mode.";
    public const string NonColorEvidenceFact =
        "Override evidence uses the arbiter-override icon and text, never color alone.";

    static readonly GmStudyPosition[] AuthoredPositions =
    {
        Position(
            "captured-record", "The Captured Record",
            "2Q5/8/8/8/7K/4p3/3R4/7k w - - 0 1",
            "2Q5/8/8/8/7K/8/3p4/7k w - - 0 1",
            "e3", "d2", "white rook on d2",
            "Aldric moved the black pawn from e3 to d2 and removed the rook. Qc1 is still legal, but h2 is now an escape square.",
            new[] { "original square e3", "changed square d2", "captured white rook on d2" },
            Card("captured-record:c8-c1", "Qc1#", true,
                "Queen c8 to c1 gives mate. The queen checks along the first rank and the rook on d2 covers h2."),
            Card("captured-record:c8-a6", "Qa6", false,
                "Queen c8 to a6 is legal and does not give check."),
            Card("captured-record:c8-a8", "Qa8+", false,
                "Queen c8 to a8 gives check, but Black has an escape.")),
        Position(
            "closing-file", "The Closing File",
            "8/8/1R6/8/1p6/k7/7K/1Q6 w - - 0 1",
            "8/8/1R6/8/8/kp6/7K/1Q6 w - - 0 1",
            "b4", "b3", string.Empty,
            "Aldric moved the black pawn from b4 to b3. It frees b4 and blocks the queen’s b-file, so the king can escape to b4.",
            new[] { "original square b4", "changed square b3", "queen b-file path" },
            Card("closing-file:b6-a6", "Ra6#", true,
                "Rook b6 to a6 gives mate. The rook checks the a-file and the queen on b1 seals the escape squares."),
            Card("closing-file:b1-a1", "Qa1+", false,
                "Queen b1 to a1 gives check, but Black has an escape."),
            Card("closing-file:b1-a2", "Qa2+", false,
                "Queen b1 to a2 gives check, but Black has an escape.")),
        Position(
            "quiet-rank", "The Quiet Rank",
            "7k/5Q1p/8/8/5R2/8/8/5K2 w - - 0 1",
            "7k/5Q2/7p/8/5R2/8/8/5K2 w - - 0 1",
            "h7", "h6", string.Empty,
            "Aldric moved the black pawn from h7 to h6. Qf8 still gives check, but the king can now escape to h7.",
            new[] { "original square h7", "changed square h6", "king flight path to h7" },
            Card("quiet-rank:f7-f8", "Qf8#", true,
                "Queen f7 to f8 gives mate. The queen checks the eighth rank and the pawn on h7 blocks the king’s remaining flight square."),
            Card("quiet-rank:f7-a2", "Qa2", false,
                "Queen f7 to a2 is legal, but it is not mate."),
            Card("quiet-rank:f7-a7", "Qa7", false,
                "Queen f7 to a7 is legal, but it is not mate."))
    };

    public static int PositionCount => AuthoredPositions.Length;
    public static string[] AccessibilityFacts => new[]
    {
        BoardZoomFact, MoveExplanationFact, ReducedMotionFact, NonColorEvidenceFact
    };

    public static GmStudyPosition[] Positions =>
        AuthoredPositions.Select(position => position.DeepCopy()).ToArray();

    public static GmStudyPosition GetPosition(int index)
    {
        if (index < 0 || index >= AuthoredPositions.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return AuthoredPositions[index].DeepCopy();
    }

    public static bool TryGetCard(int positionIndex, string actionId,
        out GmStudyMoveCard card)
    {
        card = null;
        if (positionIndex < 0 || positionIndex >= AuthoredPositions.Length ||
            string.IsNullOrWhiteSpace(actionId)) return false;
        GmStudyMoveCard found = AuthoredPositions[positionIndex].cards
            .FirstOrDefault(candidate => candidate.actionId == actionId);
        card = found?.DeepCopy();
        return card != null;
    }

    public static int ScoreMove(GmStudyMoveCard card)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));
        return card.isCorrect ? 1 : 0;
    }

    public static GmStudyMatchResult ResolveResult(int correctCount)
    {
        if (correctCount < 0 || correctCount > PositionCount)
            throw new ArgumentOutOfRangeException(nameof(correctCount));
        return correctCount >= 2 ? GmStudyMatchResult.PlayerWin : GmStudyMatchResult.AldricWin;
    }

    static GmStudyPosition Position(string id, string title, string originalFen,
        string alteredFen, string from, string to, string capturedPiece, string overrideText,
        string[] evidence, params GmStudyMoveCard[] cards) => new GmStudyPosition
    {
        id = id,
        title = title,
        originalFen = originalFen,
        alteredFen = alteredFen,
        overrideFromSquare = from,
        overrideToSquare = to,
        capturedPiece = capturedPiece,
        overrideText = overrideText,
        evidenceFacts = evidence,
        evidenceIconId = EvidenceIconId,
        cards = cards
    };

    static GmStudyMoveCard Card(string actionId, string notation, bool correct,
        string consequence) => new GmStudyMoveCard
    {
        actionId = actionId,
        notation = notation,
        isCorrect = correct,
        consequence = consequence
    };
}
