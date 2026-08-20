using System.Collections.Generic;
using UnityEngine;

public sealed class GmStudyPresenter : MonoBehaviour
{
    [SerializeField] GmStudyPieceView whiteKing;
    [SerializeField] GmStudyPieceView blackKing;
    [SerializeField] GmStudyPieceView whiteQueen;
    [SerializeField] GmStudyPieceView whiteRook;
    [SerializeField] GmStudyPieceView blackPawn;
    [SerializeField] Vector3 boardOrigin;
    [SerializeField] float squareSize = 0.05f;
    [SerializeField] float pieceSurfaceY;
    GmStudyController controller;
    bool presentationSuspended;

    public bool IsConfigured => controller != null && whiteKing != null && blackKing != null &&
        whiteQueen != null && whiteRook != null && blackPawn != null;
    public bool SupportsAccessibility => true;

    public void BindPieces(GmStudyPieceView king, GmStudyPieceView blackKingView,
        GmStudyPieceView queen, GmStudyPieceView rook, GmStudyPieceView pawn,
        Vector3 origin, float boardSquareSize, float surfaceY)
    {
        whiteKing = king;
        blackKing = blackKingView;
        whiteQueen = queen;
        whiteRook = rook;
        blackPawn = pawn;
        boardOrigin = origin;
        squareSize = boardSquareSize;
        pieceSurfaceY = surfaceY;
    }

    public bool TryConfigure(GmStudyController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized || whiteKing == null ||
            blackKing == null || whiteQueen == null || whiteRook == null || blackPawn == null)
        {
            error = "Study presenter needs an initialized controller and all five authored pieces";
            return false;
        }
        if (controller != null) controller.OnStateChanged -= Refresh;
        controller = tableController;
        if (!presentationSuspended)
        {
            controller.OnStateChanged += Refresh;
            GmAccessibilitySettings.OnChanged -= Refresh;
            GmAccessibilitySettings.OnChanged += Refresh;
            Refresh();
        }
        error = string.Empty;
        return true;
    }

    public void Refresh()
    {
        if (presentationSuspended || !IsConfigured)
        {
            GmAccessibilitySettings.OnChanged -= Refresh;
            return;
        }
        GmStudyPresentationState state = GmStudyPresentationModel.Project(controller);
        var placed = new HashSet<GmStudyPieceView>();
        foreach (GmChessPiece piece in GmChessFen.ParsePlacement(state.ShownFen))
        {
            GmStudyPieceView view = ViewFor(piece.Type, piece.IsWhite);
            if (view == null) continue;
            bool changed = state.HasArbiterMarker &&
                piece.Type == GmChessPieceType.Pawn && !piece.IsWhite;
            view.PlaceAtSquare(SquareToWorld(piece.File, piece.Rank), changed);
            placed.Add(view);
        }
        foreach (GmStudyPieceView view in AllViews())
            if (!placed.Contains(view)) view.RemoveFromBoard();
    }

    public void SuspendPresentation()
    {
        presentationSuspended = true;
        if (controller != null) controller.OnStateChanged -= Refresh;
        GmAccessibilitySettings.OnChanged -= Refresh;
    }

    public void ResumePresentation()
    {
        presentationSuspended = false;
        if (!IsConfigured) return;
        controller.OnStateChanged -= Refresh;
        controller.OnStateChanged += Refresh;
        GmAccessibilitySettings.OnChanged -= Refresh;
        GmAccessibilitySettings.OnChanged += Refresh;
        Refresh();
    }

    Vector3 SquareToWorld(int file, int rank) => boardOrigin +
        new Vector3((file - 3.5f) * squareSize, pieceSurfaceY, (rank - 3.5f) * squareSize);

    GmStudyPieceView ViewFor(GmChessPieceType type, bool isWhite)
    {
        if (type == GmChessPieceType.King) return isWhite ? whiteKing : blackKing;
        if (isWhite && type == GmChessPieceType.Queen) return whiteQueen;
        if (isWhite && type == GmChessPieceType.Rook) return whiteRook;
        if (!isWhite && type == GmChessPieceType.Pawn) return blackPawn;
        return null;
    }

    IEnumerable<GmStudyPieceView> AllViews()
    {
        yield return whiteKing;
        yield return blackKing;
        yield return whiteQueen;
        yield return whiteRook;
        yield return blackPawn;
    }

    void OnDestroy()
    {
        if (controller != null) controller.OnStateChanged -= Refresh;
        GmAccessibilitySettings.OnChanged -= Refresh;
    }
}
