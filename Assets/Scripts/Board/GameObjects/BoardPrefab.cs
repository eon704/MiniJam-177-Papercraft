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
    this.map = newMap;
    this.size = newSize;
    this.Board = new Board(this.size, this.ParseMap());
    this.cellPrefabs = new CellPrefab[this.size.x, this.size.y];
    
    this.InstantiateBoard();
    this.UpdateBorder();
  }
  
  public List<CellPrefab> GetCellPrefabs(List<Cell> cells)
  {
    return cells.Select(cell => this.GetCellPrefab(cell.Position)).ToList();
  }
  
  public List<CellPrefab> GetCellPrefabs(List<Vector2Int> coords)
  {
    return coords.Select(this.GetCellPrefab).ToList();
  }

  public CellPrefab GetCellPrefab(Cell cell)
  {
    return this.GetCellPrefab(cell.Position);
  }
  
  public CellPrefab GetCellPrefab(Vector2Int coord)
  {
    return this.cellPrefabs[coord.x, coord.y];
  }
  
  public CellPrefab GetStartCellPrefab()
  {
    return this.GetCellPrefab(this.Board.StartCell.Position);
  }

  public List<CellPrefab> GetStarCellPrefabs()
  {
    return this.GetCellPrefabs(this.Board.StarCells);
  }
  
  public (BoardPiece, CellPrefab) CreateNewPlayerPrefab()
  {
    BoardPiece playerPiece = this.Board.CreatePlayerPiece();
    CellPrefab startCell = this.GetCellPrefab(playerPiece.OccupiedCell.Value.Position);
    return (playerPiece, startCell);
  }

  private void Awake()
  {
    this.worldGrid = this.GetComponent<Grid>();
  }

  private char[,] ParseMap()
  {
    // Normalize newlines to '\n'
    this.map = this.map.Replace("\r\n", "\n");
    
    char[,] parsedMap = new char[this.size.x, this.size.y];
    int rowLength = this.size.x + 1;
    for (int i = 0; i < this.map.Length; i++)
    {
      char key = this.map[i];
      if (key == '\n')
        continue;
      
      int x = i % rowLength;
      int y = i / rowLength;
      parsedMap[x, y] = key;
    }
    
    // Y-flip the map
    for (int x = 0; x < this.size.x; x++)
    {
      for (int y = 0; y < this.size.y / 2; y++)
      {
        (parsedMap[x, y], parsedMap[x, this.size.y - y - 1]) = (parsedMap[x, this.size.y - y - 1], parsedMap[x, y]);
      }
    }

    return parsedMap;
  }

  private void InstantiateBoard()
  {
    for (int x = 0; x < this.size.x; x++)
    {
      for (int y = 0; y < this.size.y; y++)
      {
        Cell cell = this.Board.CellArray[x, y];
        Vector3 cellPosition = this.worldGrid.GetCellCenterWorld(new Vector3Int(x, y, 0));
        this.cellPrefabs[x, y] = Instantiate(this.cellPrefab, cellPosition, Quaternion.identity, this.transform);
        this.cellPrefabs[x, y].Initialize(cell, this.player);
      }
    }
  }


  private void UpdateBorder()
  {
    this.boardBorder.gameObject.SetActive(true);
    this.boardBorder.localScale = new Vector3(this.size.x + 0.1f, this.size.y + 0.1f, 1);
    
    Vector3 borderPosition = this.worldGrid.GetCellCenterWorld(new Vector3Int(this.size.x / 2, this.size.y / 2, 0));
    
    if (this.size.x % 2 == 0)
      borderPosition.x -= this.worldGrid.cellSize.x / 2;
    
    if (this.size.y % 2 == 0)
      borderPosition.y -= this.worldGrid.cellSize.y / 2;

    this.boardBorder.position = borderPosition;
  }
}