using System;
using System.Collections.Generic;

public enum GmChessPieceType { King, Queen, Rook, Bishop, Knight, Pawn }

public readonly struct GmChessPiece
{
    public readonly GmChessPieceType Type;
    public readonly bool IsWhite;
    public readonly int File;
    public readonly int Rank;

    public GmChessPiece(GmChessPieceType type, bool isWhite, int file, int rank)
    {
        Type = type;
        IsWhite = isWhite;
        File = file;
        Rank = rank;
    }

    public string Square => ((char)('a' + File)).ToString() + (Rank + 1);
}

/// <summary>Minimal FEN board-placement reader. Not a chess engine: no move legality, no turn
/// tracking beyond the placement field. Three authored Study positions only need their pieces
/// placed on an 8x8 grid.</summary>
public static class GmChessFen
{
    public static IReadOnlyList<GmChessPiece> ParsePlacement(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen)) throw new ArgumentException("FEN is missing", nameof(fen));
        string placement = fen.Split(' ')[0];
        string[] ranks = placement.Split('/');
        if (ranks.Length != 8)
            throw new ArgumentException("FEN placement must have 8 ranks", nameof(fen));
        var pieces = new List<GmChessPiece>();
        for (int rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            int rank = 7 - rankIndex;
            int file = 0;
            foreach (char symbol in ranks[rankIndex])
            {
                if (char.IsDigit(symbol))
                {
                    file += symbol - '0';
                    continue;
                }
                if (file >= 8)
                    throw new ArgumentException($"FEN rank {rankIndex} overflows past file h", nameof(fen));
                pieces.Add(new GmChessPiece(TypeFor(symbol), char.IsUpper(symbol), file, rank));
                file++;
            }
            if (file != 8)
                throw new ArgumentException($"FEN rank {rankIndex} does not sum to 8 files", nameof(fen));
        }
        return pieces;
    }

    static GmChessPieceType TypeFor(char symbol)
    {
        switch (char.ToLowerInvariant(symbol))
        {
            case 'k': return GmChessPieceType.King;
            case 'q': return GmChessPieceType.Queen;
            case 'r': return GmChessPieceType.Rook;
            case 'b': return GmChessPieceType.Bishop;
            case 'n': return GmChessPieceType.Knight;
            case 'p': return GmChessPieceType.Pawn;
            default: throw new ArgumentException($"'{symbol}' is not a legal FEN piece letter", nameof(symbol));
        }
    }
}
