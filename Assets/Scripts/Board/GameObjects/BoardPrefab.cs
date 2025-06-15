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
  public Vector2Int Size { get; private set; }
  public Vector3 WorldCenter { get; private set; }
  
  public Board Board { get; private set; }
  public bool IsSpawnAnimationComplete { get; private set; }
  
  private CellPrefab[,] cellPrefabs;
  
  public void Initialize(string newMap, Vector2Int newSize)
  {
    map = newMap;
    Size = newSize;
    Board = new Board(Size, ParseMap());
    cellPrefabs = new CellPrefab[Size.x, Size.y];
    
    InstantiateBoard();
    UpdateBorder();
    ComputeBoardCenterPosition();
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
    
    char[,] parsedMap = new char[Size.x, Size.y];
    int rowLength = Size.x + 1;
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
    for (int x = 0; x < Size.x; x++)
    {
      for (int y = 0; y < Size.y / 2; y++)
      {
        (parsedMap[x, y], parsedMap[x, Size.y - y - 1]) = (parsedMap[x, Size.y - y - 1], parsedMap[x, y]);
      }
    }

    return parsedMap;
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
        Vector3 cellPosition = worldGrid.GetCellCenterWorld(new Vector3Int(x, y, 0));
        int distanceFromCenter = Mathf.Abs(centerX - x) + Mathf.Abs(centerY - y);
        float delay = distanceFromCenter * 0.1f + 0.5f;
        cellPrefabs[x, y] = Instantiate(cellPrefab, cellPosition, Quaternion.identity, transform);
        cellPrefabs[x, y].Initialize(cell, player, delay);
        
        if (cell.Terrain == TerrainType.Empty || delay < longestDelay)
          continue;
        
        longestDelay = delay;
      }
    }
    
    Invoke(nameof(SetAnimationComplete), longestDelay + 0.5f);
  }
  
  private void SetAnimationComplete()
  {
    IsSpawnAnimationComplete = true;
  }


  private void UpdateBorder()
  {
    boardBorder.gameObject.SetActive(true);
    boardBorder.localScale = new Vector3(Size.x + 0.1f, Size.y + 0.1f, 1);
    
    Vector3 borderPosition = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2, Size.y / 2, 0));
    
    if (Size.x % 2 == 0)
      borderPosition.x -= worldGrid.cellSize.x / 2;
    
    if (Size.y % 2 == 0)
      borderPosition.y -= worldGrid.cellSize.y / 2;

    boardBorder.position = borderPosition;
  }

  private void ComputeBoardCenterPosition()
  {
    bool isOddX = Size.x % 2 != 0;
    bool isOddY = Size.y % 2 != 0;

    float xPos;
    if (isOddX)
    {
      xPos = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2, 0, 0)).x;  
    }
    else
    {
      float x1 = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2 - 1, 0, 0)).x;
      float x2 = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2, 0, 0)).x;
      xPos = (x1 + x2) / 2;
    }
    
    float yPos;
    if (isOddY)
    {
      yPos = worldGrid.GetCellCenterWorld(new Vector3Int(0, Size.y / 2, 0)).y;  
    }
    else
    {
      float y1 = worldGrid.GetCellCenterWorld(new Vector3Int(0, Size.y / 2 - 1, 0)).y;
      float y2 = worldGrid.GetCellCenterWorld(new Vector3Int(0, Size.y / 2, 0)).y;
      yPos = (y1 + y2) / 2;
    }
    
    WorldCenter = new Vector3(xPos, yPos, -10);
  }
}