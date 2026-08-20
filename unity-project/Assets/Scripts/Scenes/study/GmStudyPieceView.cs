using UnityEngine;

public sealed class GmStudyPieceView : MonoBehaviour
{
    [SerializeField] GmChessPieceType pieceType;
    [SerializeField] bool isWhite;
    [SerializeField] GameObject changedEvidence;

    public GmChessPieceType PieceType => pieceType;
    public bool IsWhite => isWhite;
    public bool IsOnBoard { get; private set; }
    public bool IsChangedEvidenceVisible => changedEvidence != null && changedEvidence.activeSelf;
    public int ChangedEvidenceRendererCount => changedEvidence == null
        ? 0 : changedEvidence.GetComponentsInChildren<Renderer>(true).Length;

    public void Configure(GmChessPieceType type, bool white, GameObject evidence)
    {
        pieceType = type;
        isWhite = white;
        changedEvidence = evidence;
    }

    public void PlaceAtSquare(Vector3 worldPosition, bool changed)
    {
        transform.position = worldPosition;
        IsOnBoard = true;
        gameObject.SetActive(true);
        if (changedEvidence != null) changedEvidence.SetActive(changed);
    }

    public void RemoveFromBoard()
    {
        IsOnBoard = false;
        if (changedEvidence != null) changedEvidence.SetActive(false);
        gameObject.SetActive(false);
    }
}
