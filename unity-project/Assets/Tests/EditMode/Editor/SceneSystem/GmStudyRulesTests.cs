using System.Linq;
using NUnit.Framework;

public sealed class GmStudyRulesTests
{
    [Test]
    public void ThreeAuthoredPositionsHaveExactFenOrderAndOverrides()
    {
        GmStudyPosition[] positions = GmStudyRules.Positions;
        Assert.That(positions, Has.Length.EqualTo(3));
        AssertPosition(positions[0], "captured-record", "The Captured Record",
            "2Q5/8/8/8/7K/4p3/3R4/7k w - - 0 1",
            "2Q5/8/8/8/7K/8/3p4/7k w - - 0 1", "e3", "d2");
        AssertPosition(positions[1], "closing-file", "The Closing File",
            "8/8/1R6/8/1p6/k7/7K/1Q6 w - - 0 1",
            "8/8/1R6/8/8/kp6/7K/1Q6 w - - 0 1", "b4", "b3");
        AssertPosition(positions[2], "quiet-rank", "The Quiet Rank",
            "7k/5Q1p/8/8/5R2/8/8/5K2 w - - 0 1",
            "7k/5Q2/7p/8/5R2/8/8/5K2 w - - 0 1", "h7", "h6");
        Assert.That(positions.All(position => position.evidenceIconId == "arbiter-override"), Is.True);
    }

    [Test]
    public void EveryPositionHasExactlyThreePinnedMoveCards()
    {
        AssertCards(0, "captured-record:c8-c1", "Qc1#",
            "Queen c8 to c1 gives mate. The queen checks along the first rank and the rook on d2 covers h2.",
            "captured-record:c8-a6", "Qa6", "captured-record:c8-a8", "Qa8+");
        AssertCards(1, "closing-file:b6-a6", "Ra6#",
            "Rook b6 to a6 gives mate. The rook checks the a-file and the queen on b1 seals the escape squares.",
            "closing-file:b1-a1", "Qa1+", "closing-file:b1-a2", "Qa2+");
        AssertCards(2, "quiet-rank:f7-f8", "Qf8#",
            "Queen f7 to f8 gives mate. The queen checks the eighth rank and the pawn on h7 blocks the king’s remaining flight square.",
            "quiet-rank:f7-a2", "Qa2", "quiet-rank:f7-a7", "Qa7");
    }

    [Test]
    public void OverrideTextAndEvidenceAreExactAndNonColor()
    {
        GmStudyPosition[] positions = GmStudyRules.Positions;
        Assert.That(positions[0].overrideText, Is.EqualTo(
            "Aldric moved the black pawn from e3 to d2 and removed the rook. Qc1 is still legal, but h2 is now an escape square."));
        Assert.That(positions[0].capturedPiece, Is.EqualTo("white rook on d2"));
        Assert.That(positions[1].overrideText, Is.EqualTo(
            "Aldric moved the black pawn from b4 to b3. It frees b4 and blocks the queen’s b-file, so the king can escape to b4."));
        Assert.That(positions[2].overrideText, Is.EqualTo(
            "Aldric moved the black pawn from h7 to h6. Qf8 still gives check, but the king can now escape to h7."));
        Assert.That(positions.All(position => position.evidenceFacts.Length == 3), Is.True);
        Assert.That(GmStudyRules.AccessibilityFacts, Does.Contain(GmStudyRules.ReducedMotionFact));
        Assert.That(GmStudyRules.AccessibilityFacts, Does.Contain(GmStudyRules.NonColorEvidenceFact));
        Assert.That(GmStudyRules.AccessibilityFacts, Does.Contain(GmStudyRules.BoardZoomFact));
        Assert.That(GmStudyRules.AccessibilityFacts, Does.Contain(GmStudyRules.MoveExplanationFact));
    }

    [Test]
    public void DefinitionsAndCardsAreCloneSafe()
    {
        GmStudyPosition first = GmStudyRules.GetPosition(0);
        first.originalFen = "forged";
        first.cards[0].actionId = "forged";
        first.evidenceFacts[0] = "forged";
        GmStudyPosition clean = GmStudyRules.GetPosition(0);
        Assert.That(clean.originalFen, Is.Not.EqualTo("forged"));
        Assert.That(clean.cards[0].actionId, Is.EqualTo("captured-record:c8-c1"));
        Assert.That(clean.evidenceFacts[0], Is.EqualTo("original square e3"));
    }

    [Test]
    public void ScoringIsBinaryAndMatchHasNoTie()
    {
        Assert.That(GmStudyRules.ScoreMove(GmStudyRules.GetPosition(0).cards[0]), Is.EqualTo(1));
        Assert.That(GmStudyRules.ScoreMove(GmStudyRules.GetPosition(0).cards[1]), Is.Zero);
        Assert.That(GmStudyRules.ResolveResult(2), Is.EqualTo(GmStudyMatchResult.PlayerWin));
        Assert.That(GmStudyRules.ResolveResult(1), Is.EqualTo(GmStudyMatchResult.AldricWin));
        Assert.That(GmStudyRules.ResolveResult(0), Is.EqualTo(GmStudyMatchResult.AldricWin));
    }

    static void AssertPosition(GmStudyPosition position, string id, string title,
        string original, string altered, string from, string to)
    {
        Assert.That(position.id, Is.EqualTo(id));
        Assert.That(position.title, Is.EqualTo(title));
        Assert.That(position.originalFen, Is.EqualTo(original));
        Assert.That(position.alteredFen, Is.EqualTo(altered));
        Assert.That(position.overrideFromSquare, Is.EqualTo(from));
        Assert.That(position.overrideToSquare, Is.EqualTo(to));
    }

    static void AssertCards(int positionIndex, string correctId, string correctNotation,
        string correctExplanation, string alternativeAId, string alternativeANotation,
        string alternativeBId, string alternativeBNotation)
    {
        GmStudyMoveCard[] cards = GmStudyRules.GetPosition(positionIndex).cards;
        Assert.That(cards, Has.Length.EqualTo(3));
        Assert.That(cards.Count(card => card.isCorrect), Is.EqualTo(1));
        Assert.That(cards[0].actionId, Is.EqualTo(correctId));
        Assert.That(cards[0].notation, Is.EqualTo(correctNotation));
        Assert.That(cards[0].consequence, Is.EqualTo(correctExplanation));
        Assert.That(cards[1].actionId, Is.EqualTo(alternativeAId));
        Assert.That(cards[1].notation, Is.EqualTo(alternativeANotation));
        Assert.That(cards[2].actionId, Is.EqualTo(alternativeBId));
        Assert.That(cards[2].notation, Is.EqualTo(alternativeBNotation));
        Assert.That(cards.All(card => !string.IsNullOrWhiteSpace(card.consequence)), Is.True);
    }
}
