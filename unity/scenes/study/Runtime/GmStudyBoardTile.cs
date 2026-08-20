using UnityEngine;

public sealed class GmStudyBoardTile : MonoBehaviour
{
    [SerializeField] int file;
    [SerializeField] int rank;

    public int File => file;
    public int Rank => rank;
    public bool IsLightSquare => (file + rank) % 2 == 0;

    public void Configure(int squareFile, int squareRank)
    {
        file = squareFile;
        rank = squareRank;
    }
}
