using System.Collections.Generic;
using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  public readonly Cell StartCell;
  public readonly List<Cell> StarCells = new();
  
  public Board(Vector2Int size, char[,] map)
  {
    this.Size = size; 
    this.CellArray = new Cell[size.x, size.y];
    
    // Spawn board
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
          'S' => Cell.TerrainType.Start,
          'E' => Cell.TerrainType.End,
          _ => Cell.TerrainType.Default
        };

        Cell.CellItem item = map[x, y] switch
        {
          'G' => Cell.CellItem.Star,
          _ => Cell.CellItem.None
        };
        
        this.CellArray[x, y] = new Cell(new Vector2Int(x, y), type, item);

        if (type == Cell.TerrainType.Start)
        {
          this.StartCell = this.CellArray[x, y];
        }
        
        if (item == Cell.CellItem.Star)
        {
          this.StarCells.Add(this.CellArray[x, y]);
        }
      }
    }
    
    // Connect neighbours
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        Cell cell = this.CellArray[x, y];
        List<Cell> neighbours = new();
        
        if (x > 0)
        {
          neighbours.Add(this.CellArray[x - 1, y]);
        }
        
        if (x < size.x - 1)
        {
          neighbours.Add(this.CellArray[x + 1, y]);
        }
        
        if (y > 0)
        {
          neighbours.Add(this.CellArray[x, y - 1]);
        }
        
        if (y < size.y - 1)
        {
          neighbours.Add(this.CellArray[x, y + 1]);
        }
        
        if (x > 0 && y > 0)
        {
          neighbours.Add(this.CellArray[x - 1, y - 1]);
        }
        
        if (x < size.x - 1 && y > 0)
        {
          neighbours.Add(this.CellArray[x + 1, y - 1]);
        }
        
        if (x > 0 && y < size.y - 1)
        {
          neighbours.Add(this.CellArray[x - 1, y + 1]);
        }
        
        if (x < size.x - 1 && y < size.y - 1)
        {
          neighbours.Add(this.CellArray[x + 1, y + 1]);
        }
        
        cell.SetNeighbors(neighbours);
      }
    }
  }

  public Cell GetCell(Vector2Int coord)
  {
    int x = coord.x;
    int y = coord.y;
    
    if (x < 0 || x >= this.Size.x || y < 0 || y >= this.Size.y)
      return null;
    
    return this.CellArray[x, y];
  }

  public BoardPiece CreateNewPiece(Vector2Int coord)
  {
    return new BoardPiece(this, this.CellArray[coord.x, coord.y]);
  }

  public BoardPiece CreatePlayerPiece()
  {
    return new BoardPiece(this, this.StartCell);
  }
}