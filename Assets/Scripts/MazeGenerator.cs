using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a simple Pac-Man style maze from a text layout.
/// Legend:  # = wall, . = pellet, o = power pellet, ' ' = empty path,
///          P = player start, G = ghost start
/// Attach this to an empty GameObject called "MazeGenerator".
/// </summary>
public class MazeGenerator : MonoBehaviour
{
    [Header("Prefabs (assign in Inspector)")]
    public GameObject wallPrefab;
    public GameObject pelletPrefab;
    public GameObject powerPelletPrefab;
    public GameObject playerPrefab;
    public GameObject ghostPrefabDefault;
    public GameObject ghostPrefabSentis;

    [Header("Layout")]
    public float cellSize = 1f;

    // Simple 15x11 layout. Feel free to expand this — keep the border walled in.
    private readonly string[] layout =
    {
        "###############",
        "#......#......#",
        "#o##.#.#.#.##o#",
        "#.............#",
        "#.##.#.###.##.#",
        "#....#..G#....#",
        "###.####.####.#",
        "#....#P..#....#",
        "#.##.#.#.#.##.#",
        "#o.#.....#..o.#",
        "###############"
    };

    public List<Vector2Int> PelletPositions { get; private set; } = new List<Vector2Int>();
    public Vector3 PlayerStart { get; private set; }
    public List<Vector3> GhostStarts { get; private set; } = new List<Vector3>();

    // Track which grid cells are walkable (not walls) for movement/AI logic.
    public HashSet<Vector2Int> WalkableCells { get; private set; } = new HashSet<Vector2Int>();

    void Awake()
    {
        BuildMaze();
    }

    void BuildMaze()
    {
        int rows = layout.Length;
        int cols = layout[0].Length;

        // Center the maze around the origin.
        float xOffset = -(cols - 1) * cellSize / 2f;
        float yOffset = (rows - 1) * cellSize / 2f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                char c = layout[row][col];
                Vector3 pos = new Vector3(xOffset + col * cellSize, yOffset - row * cellSize, 0f);
                Vector2Int grid = new Vector2Int(col, row);

                if (c == '#')
                {
                    if (wallPrefab != null)
                        Instantiate(wallPrefab, pos, Quaternion.identity, transform);
                    continue; // walls are not walkable
                }

                WalkableCells.Add(grid);

                if (c == '.')
                {
                    if (pelletPrefab != null)
                        Instantiate(pelletPrefab, pos, Quaternion.identity, transform);
                    PelletPositions.Add(grid);
                }
                else if (c == 'o')
                {
                    if (powerPelletPrefab != null)
                        Instantiate(powerPelletPrefab, pos, Quaternion.identity, transform);
                    PelletPositions.Add(grid);
                }
                else if (c == 'P')
                {
                    PlayerStart = pos;
                }
                else if (c == 'G')
                {
                    GhostStarts.Add(pos);
                }
            }
        }

        if (playerPrefab != null)
            Instantiate(playerPrefab, PlayerStart, Quaternion.identity);

        if (ghostPrefabSentis != null && ghostPrefabDefault != null)
        {
            foreach (var gpos in GhostStarts)
            {
                Instantiate(ghostPrefabSentis, gpos, Quaternion.identity);
                Instantiate(ghostPrefabDefault, gpos, Quaternion.identity);
            }
        }
    }

    public bool IsWalkable(Vector2Int cell) => WalkableCells.Contains(cell);
}
