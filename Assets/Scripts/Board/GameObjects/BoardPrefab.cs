using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardPrefab : MonoBehaviour
{
  [Header("Setup")]
  [SerializeField] private Player player;
  [SerializeField] private Transform boardBorder;
  [SerializeField] private CellPrefab cellPrefab;
  
  private Grid worldGrid;
  private string map;
  private Vector2Int size;
  
  public Board Board { get; private set; }
  
  private CellPrefab[,] cellPrefabs;
  
  public void Initialize(string newMap, Vector2Int newSize)
  {
    map = newMap;
    size = newSize;
    Board = new Board(size, ParseMap());
    cellPrefabs = new CellPrefab[size.x, size.y];
    
    InstantiateBoard();
    UpdateBorder();
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

  private void Awake()
  {
    worldGrid = GetComponent<Grid>();
  }

  private char[,] ParseMap()
  {
    // Normalize newlines to '\n'
    map = map.Replace("\r\n", "\n");
    
    char[,] parsedMap = new char[size.x, size.y];
    int rowLength = size.x + 1;
    for (int i = 0; i < map.Length; i++)
    {
      char key = map[i];
      if (key == '\n')
        continue;
      
      int x = i % rowLength;
      int y = i / rowLength;
      parsedMap[x, y] = key;
    }
    
    // Y-flip the map
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y / 2; y++)
      {
        (parsedMap[x, y], parsedMap[x, size.y - y - 1]) = (parsedMap[x, size.y - y - 1], parsedMap[x, y]);
      }
    }

    return parsedMap;
  }

  private void InstantiateBoard()
  {
    int centerX = size.x / 2;
    int centerY = size.y / 2;
    
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        Cell cell = Board.CellArray[x, y];
        Vector3 cellPosition = worldGrid.GetCellCenterWorld(new Vector3Int(x, y, 0));
        int distanceFromCenter = Mathf.Abs(centerX - x) + Mathf.Abs(centerY - y);
        cellPrefabs[x, y] = Instantiate(cellPrefab, cellPosition, Quaternion.identity, transform);
        cellPrefabs[x, y].Initialize(cell, player, distanceFromCenter * 0.1f + 0.5f);
      }
    }
  }


  private void UpdateBorder()
  {
    boardBorder.gameObject.SetActive(true);
    boardBorder.localScale = new Vector3(size.x + 0.1f, size.y + 0.1f, 1);
    
    Vector3 borderPosition = worldGrid.GetCellCenterWorld(new Vector3Int(size.x / 2, size.y / 2, 0));
    
    if (size.x % 2 == 0)
      borderPosition.x -= worldGrid.cellSize.x / 2;
    
    if (size.y % 2 == 0)
      borderPosition.y -= worldGrid.cellSize.y / 2;

    boardBorder.position = borderPosition;
  }
}