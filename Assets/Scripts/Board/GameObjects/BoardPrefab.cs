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

    private const int GridSize = 8;

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
        int offsetX = (GridSize - Size.x) / 2;
        int offsetY = (GridSize - Size.y) / 2;
        float longestDelay = 0f;

        for (int gx = 0; gx < GridSize; gx++)
        {
            for (int gy = 0; gy < GridSize; gy++)
            {
                Vector3 cellPosition = new Vector3(gx * cellSize, 0f, gy * cellSize);
                Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
                CellPrefab cp = Instantiate(cellPrefab, cellPosition, randomRotation, transform);

                int bx = gx - offsetX;
                int by = gy - offsetY;

                if (bx >= 0 && bx < Size.x && by >= 0 && by < Size.y)
                {
                    Cell cell = Board.CellArray[bx, by];
                    const float delay = 0.3f;
                    cp.Initialize(cell, player, delay);
                    cellPrefabs[bx, by] = cp;

                    if (cell.Terrain != TerrainType.Empty)
                        longestDelay = delay;
                }
                else
                {
                    cp.gameObject.SetActive(false);
                }
            }
        }

        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxBoardSpawn);
        Invoke(nameof(SetAnimationComplete), longestDelay + 0.5f);
    }

    private void SetAnimationComplete()
    {
        IsSpawnAnimationComplete = true;
    }

    private void ComputeBoardCenterPosition()
    {
        float center = (GridSize - 1) * cellSize / 2f;
        WorldCenter = new Vector3(center, 0f, center);
    }
}
