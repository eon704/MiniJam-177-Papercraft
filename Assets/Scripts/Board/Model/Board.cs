using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  
  public Board(Vector2Int size, char[,] map)
  {
    this.Size = size; 
    this.CellArray = new Cell[size.x, size.y];
    
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        Cell.TerrainType type = map[x, y] switch
        {
          'o' => Cell.TerrainType.Default,
          '-' => Cell.TerrainType.Water,
          '+' => Cell.TerrainType.Stone,
          'x' => Cell.TerrainType.Fire,
          'S' => Cell.TerrainType.Default,
          'E' => Cell.TerrainType.Default,
          _ => Cell.TerrainType.Default
        };
        
        this.CellArray[x, y] = new Cell(new Vector2Int(x, y), type);
      }
    }
  }

  public BoardPiece CreateNewPiece(Vector2Int coord)
  {
    return new BoardPiece(this, this.CellArray[coord.x, coord.y]);
  }
}