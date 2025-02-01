using UnityEngine;

public class BoardPrefab : MonoBehaviour
{
  [SerializeField] private Player player;
  [SerializeField] private Vector2Int size;
  [SerializeField] private Transform boardBorder;
  [SerializeField] private CellPrefab cellPrefab;
    
  private Grid worldGrid;
  public Board Board { get; private set; }
  
  private CellPrefab[,] cellPrefabs;

  public CellPrefab GetCellPrefab(Vector2Int coord)
  {
    return this.cellPrefabs[coord.x, coord.y];
  }
  
  private void Awake()
  {
    this.worldGrid = this.GetComponent<Grid>();
    
    this.Board = new Board(this.size);
    this.cellPrefabs = new CellPrefab[this.size.x, this.size.y];
    
    this.InstantiateBoard();
    this.boardBorder.gameObject.SetActive(true);
    this.boardBorder.localScale = new Vector3(this.size.x + 0.1f, this.size.y + 0.1f, 1);
    
    Vector3 borderPosition = this.worldGrid.GetCellCenterWorld(new Vector3Int(this.size.x / 2, this.size.y / 2, 0));
    
    if (this.size.x % 2 == 0)
      borderPosition.x -= this.worldGrid.cellSize.x / 2;
    
    if (this.size.y % 2 == 0)
      borderPosition.y -= this.worldGrid.cellSize.y / 2;

    this.boardBorder.position = borderPosition;
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
}