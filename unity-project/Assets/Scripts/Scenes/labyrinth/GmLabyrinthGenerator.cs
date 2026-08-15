using UnityEngine;

public sealed class GmLabyrinthGenerator : MonoBehaviour
{
    public const int GridSize = 7;
    public const float CellSpacing = 5.0f; // 35m x 35m total area

    public static Vector2Int EntranceCell => new Vector2Int(0, 0);
    public static Vector2Int CenterShrineCell => new Vector2Int(3, 3);
    public static Vector2Int ExitCell => new Vector2Int(6, 6);

    public bool[,] WallGrid { get; private set; } = new bool[GridSize, GridSize];

    // The builder places its hedges from this grid, so this method is the only description of the
    // maze layout. The layout is fixed today: seed is accepted and unused, and no caller varies it.
    // Whether the maze may vary per run is Nick's call, not something to invent here.
    public void GenerateDeterministicMaze(int seed = 42)
    {
        // Clear all cells initially (passable)
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                WallGrid[x, y] = false;
            }
        }

        // Add internal maze obstructions ensuring a valid path from (0,0) -> (3,3) -> (6,6)
        for (int x = 1; x < GridSize - 1; x += 2)
        {
            for (int y = 1; y < GridSize - 1; y += 2)
            {
                if (x == 3 && y == 3) continue; // Keep center shrine clear
                WallGrid[x, y] = true;
            }
        }
    }

    public static Vector3 CellToWorldPos(Vector2Int cell)
    {
        float origin = -((GridSize - 1) * CellSpacing) / 2.0f;
        return new Vector3(origin + cell.x * CellSpacing, 0f, origin + cell.y * CellSpacing);
    }
}
