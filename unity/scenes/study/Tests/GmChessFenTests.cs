using System;
using System.Linq;
using NUnit.Framework;

public sealed class GmChessFenTests
{
    [Test]
    public void CapturedRecordOriginalPlacesFivePiecesOnExactSquares()
    {
        var pieces = GmChessFen.ParsePlacement(GmStudyRules.GetPosition(0).originalFen);
        Assert.That(pieces.Count, Is.EqualTo(5));
        AssertSquare(pieces, GmChessPieceType.Queen, true, "c8");
        AssertSquare(pieces, GmChessPieceType.King, true, "h4");
        AssertSquare(pieces, GmChessPieceType.Pawn, false, "e3");
        AssertSquare(pieces, GmChessPieceType.Rook, true, "d2");
        AssertSquare(pieces, GmChessPieceType.King, false, "h1");
    }

    [Test]
    public void CapturedRecordAlteredRemovesTheCapturedRookAndMovesThePawn()
    {
        var pieces = GmChessFen.ParsePlacement(GmStudyRules.GetPosition(0).alteredFen);
        Assert.That(pieces.Count, Is.EqualTo(4), "the captured white rook must not appear");
        Assert.That(pieces.Any(piece => piece.Type == GmChessPieceType.Rook), Is.False);
        AssertSquare(pieces, GmChessPieceType.Pawn, false, "d2");
    }

    [Test]
    public void ClosingFileOriginalAndAlteredMoveOnlyThePawn()
    {
        var original = GmChessFen.ParsePlacement(GmStudyRules.GetPosition(1).originalFen);
        AssertSquare(original, GmChessPieceType.Rook, true, "b6");
        AssertSquare(original, GmChessPieceType.Pawn, false, "b4");
        AssertSquare(original, GmChessPieceType.King, false, "a3");
        AssertSquare(original, GmChessPieceType.King, true, "h2");
        AssertSquare(original, GmChessPieceType.Queen, true, "b1");

        var altered = GmChessFen.ParsePlacement(GmStudyRules.GetPosition(1).alteredFen);
        Assert.That(altered.Count, Is.EqualTo(5), "closing-file never captures a piece");
        AssertSquare(altered, GmChessPieceType.Pawn, false, "b3");
        AssertSquare(altered, GmChessPieceType.Rook, true, "b6");
    }

    [Test]
    public void QuietRankOriginalAndAlteredMoveOnlyThePawn()
    {
        var original = GmChessFen.ParsePlacement(GmStudyRules.GetPosition(2).originalFen);
        AssertSquare(original, GmChessPieceType.King, false, "h8");
        AssertSquare(original, GmChessPieceType.Queen, true, "f7");
        AssertSquare(original, GmChessPieceType.Pawn, false, "h7");
        AssertSquare(original, GmChessPieceType.Rook, true, "f4");
        AssertSquare(original, GmChessPieceType.King, true, "f1");

        var altered = GmChessFen.ParsePlacement(GmStudyRules.GetPosition(2).alteredFen);
        Assert.That(altered.Count, Is.EqualTo(5), "quiet-rank never captures a piece");
        AssertSquare(altered, GmChessPieceType.Pawn, false, "h6");
        AssertSquare(altered, GmChessPieceType.Queen, true, "f7");
    }

    [Test]
    public void MissingOrMalformedFenIsRejected()
    {
        Assert.Throws<ArgumentException>(() => GmChessFen.ParsePlacement(null));
        Assert.Throws<ArgumentException>(() => GmChessFen.ParsePlacement(""));
        Assert.Throws<ArgumentException>(() => GmChessFen.ParsePlacement("8/8/8/8/8/8/8 w - - 0 1"));
        Assert.Throws<ArgumentException>(() => GmChessFen.ParsePlacement("9/8/8/8/8/8/8/8 w - - 0 1"));
        Assert.Throws<ArgumentException>(() => GmChessFen.ParsePlacement("8/8/8/8/8/8/8/7 w - - 0 1"));
        Assert.Throws<ArgumentException>(() => GmChessFen.ParsePlacement("zzzzzzzz/8/8/8/8/8/8/8 w - - 0 1"));
    }

    static void AssertSquare(System.Collections.Generic.IReadOnlyList<GmChessPiece> pieces,
        GmChessPieceType type, bool isWhite, string square)
    {
        var matches = pieces.Where(piece => piece.Type == type && piece.IsWhite == isWhite).ToList();
        Assert.That(matches, Has.Count.EqualTo(1),
            $"expected exactly one {(isWhite ? "white" : "black")} {type} on the board");
        Assert.That(matches[0].Square, Is.EqualTo(square),
            $"{(isWhite ? "white" : "black")} {type} expected at {square}");
    }
}
