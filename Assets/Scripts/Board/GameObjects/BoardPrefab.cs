using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class BoardPrefab : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Player player;
    [SerializeField] private CellPrefab cellPrefab;
    [SerializeField] private float cellSize = 1.1f;

    public Vector2Int Size { get; private set; }
    public Vector3 WorldCenter { get; private set; }

    public Board Board { get; private set; }
    public LevelData LevelData { get; private set; }
    public bool IsSpawnAnimationComplete { get; private set; }

    private CellPrefab[,] cellPrefabs;

    public void Initialize(LevelData levelData)
    {
        LevelData = levelData;
        Size = levelData.MapSize;
        Board = new Board(Size, levelData.Map, levelData);
        cellPrefabs = new CellPrefab[Size.x, Size.y];

        ComputeBoardCenterPosition();
        InstantiateBoard();
    }

    public List<CellPrefab> GetCellPrefabs(List<Cell> cells)
    {
        return cells.Select(cell => GetCellPrefab(cell.Position)).ToList();
    }

    public List<CellPrefab> GetCellPrefabs(List<Vector2Int> coords)
    {
        return coords.Select(GetCellPrefab).ToList();
    }

    public CellPrefab GetCellPrefab(Cell cell)
    {
        return GetCellPrefab(cell.Position);
    }

    public CellPrefab GetCellPrefab(Vector2Int coord)
    {
        return cellPrefabs[coord.x, coord.y];
    }

    public CellPrefab GetStartCellPrefab()
    {
        return GetCellPrefab(Board.StartCell.Position);
    }

    public List<CellPrefab> GetStarCellPrefabs()
    {
        return GetCellPrefabs(Board.StarCells);
    }

    public (BoardPiece, CellPrefab) CreateNewPlayerPrefab()
    {
        BoardPiece playerPiece = Board.CreatePlayerPiece();
        CellPrefab startCell = GetCellPrefab(playerPiece.OccupiedCell.Value.Position);
        return (playerPiece, startCell);
    }

    public void ResetFragileCells()
    {
        Board.ResetFragileCells();
    }

    private void InstantiateBoard()
    {
        int centerX = Size.x / 2;
        int centerY = Size.y / 2;
        float longestDelay = 0f;

        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
            {
                Cell cell = Board.CellArray[x, y];
                Vector3 cellPosition = new Vector3(x * cellSize, 0f, y * cellSize);
                int distanceFromCenter = Mathf.Abs(centerX - x) + Mathf.Abs(centerY - y);
                float delay = distanceFromCenter * 0.1f + 0.5f;
                Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
                cellPrefabs[x, y] = Instantiate(cellPrefab, cellPosition, randomRotation, transform);
                cellPrefabs[x, y].Initialize(cell, player, delay);

                if (cell.Terrain == TerrainType.Empty || delay < longestDelay)
                    continue;

                longestDelay = delay;
            }
        }

        FMODAudioManager.Instance.PlayBoardSpawn();
        Invoke(nameof(SetAnimationComplete), longestDelay + 0.5f);
    }

    private void SetAnimationComplete()
    {
        IsSpawnAnimationComplete = true;
    }

    private void ComputeBoardCenterPosition()
    {
        float centerX = (Size.x - 1) * cellSize / 2f;
        float centerZ = (Size.y - 1) * cellSize / 2f;
        WorldCenter = new Vector3(centerX, 0f, centerZ);
    }
}
