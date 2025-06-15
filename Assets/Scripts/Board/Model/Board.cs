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
        TerrainType type = map[x, y] switch
        {
          '0' => TerrainType.Empty,
          
          '+' => TerrainType.Default,
          '1' => TerrainType.Default,
          
          'W' => TerrainType.Water,
          '2' => TerrainType.Water,
          
          'S' => TerrainType.Stone,
          '3' => TerrainType.Stone,
          
          'F' => TerrainType.Fire,
          'x' => TerrainType.Start,
          'y' => TerrainType.End,
          _ => TerrainType.Default
        };

        CellItem item = map[x, y] switch
        {
          'G' => CellItem.Star,
          '1' => CellItem.Star,
          '2' => CellItem.Star,
          '3' => CellItem.Star,
          _ => CellItem.None
        };
        
        CellArray[x, y] = new Cell(new Vector2Int(x, y), type, item);

        if (type == TerrainType.Start)
        {
          StartCell = CellArray[x, y];
        }
        
        if (item == CellItem.Star)
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