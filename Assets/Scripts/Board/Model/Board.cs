using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  
  public Board(Vector2Int size)
  {
    this.Size = size; 
    this.CellArray = new Cell[size.x, size.y];
    
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        this.CellArray[x, y] = new Cell(new Vector2Int(x, y), (Cell.TerrainType)Random.Range(0, 4));
      }
    }
  }

  public BoardPiece CreateNewPiece(Vector2Int coord)
  {
    return new BoardPiece(this, this.CellArray[coord.x, coord.y]);
  }
}