using System.Collections.Generic;
using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  public readonly Cell StartCell;
  public readonly List<Cell> StarCells = new();
  
  public readonly BoardHistory BoardHistory = new();
  
  public Board(Vector2Int size, char[,] map)
  {
    Size = size; 
    CellArray = new Cell[size.x, size.y];
    
    // Parse the board map
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        Cell.TerrainType type = map[x, y] switch
        {
          '0' => Cell.TerrainType.None,
          
          '+' => Cell.TerrainType.Default,
          '1' => Cell.TerrainType.Default,
          
          'W' => Cell.TerrainType.Water,
          '2' => Cell.TerrainType.Water,
          
          'S' => Cell.TerrainType.Stone,
          '3' => Cell.TerrainType.Stone,
          
          'F' => Cell.TerrainType.Fire,
          'x' => Cell.TerrainType.Start,
          'y' => Cell.TerrainType.End,
          _ => Cell.TerrainType.Default
        };

        Cell.CellItem item = map[x, y] switch
        {
          'G' => Cell.CellItem.Star,
          '1' => Cell.CellItem.Star,
          '2' => Cell.CellItem.Star,
          '3' => Cell.CellItem.Star,
          _ => Cell.CellItem.None
        };
        
        CellArray[x, y] = new Cell(new Vector2Int(x, y), type, item);

        if (type == Cell.TerrainType.Start)
        {
          StartCell = CellArray[x, y];
        }
        
        if (item == Cell.CellItem.Star)
        {
          StarCells.Add(CellArray[x, y]);
        }
      }
    }
    
    // Connect neighbours
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        Cell cell = CellArray[x, y];
        List<Cell> neighbours = new();
        
        if (x > 0)
        {
          neighbours.Add(CellArray[x - 1, y]);
        }
        
        if (x < size.x - 1)
        {
          neighbours.Add(CellArray[x + 1, y]);
        }
        
        if (y > 0)
        {
          neighbours.Add(CellArray[x, y - 1]);
        }
        
        if (y < size.y - 1)
        {
          neighbours.Add(CellArray[x, y + 1]);
        }
        
        if (x > 0 && y > 0)
        {
          neighbours.Add(CellArray[x - 1, y - 1]);
        }
        
        if (x < size.x - 1 && y > 0)
        {
          neighbours.Add(CellArray[x + 1, y - 1]);
        }
        
        if (x > 0 && y < size.y - 1)
        {
          neighbours.Add(CellArray[x - 1, y + 1]);
        }
        
        if (x < size.x - 1 && y < size.y - 1)
        {
          neighbours.Add(CellArray[x + 1, y + 1]);
        }
        
        cell.SetNeighbors(neighbours);
      }
    }
  }

  public Cell GetCell(Vector2Int coord)
  {
    int x = coord.x;
    int y = coord.y;
    
    if (x < 0 || x >= Size.x || y < 0 || y >= Size.y)
      return null;
    
    return CellArray[x, y];
  }

  public BoardPiece CreateNewPiece(Vector2Int coord)
  {
    return new BoardPiece(this, CellArray[coord.x, coord.y]);
  }

  public BoardPiece CreatePlayerPiece()
  {
    return new BoardPiece(this, StartCell);
  }
}