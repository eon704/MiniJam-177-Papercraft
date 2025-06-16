using System.Collections.Generic;
using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  public readonly Cell StartCell;
  public readonly List<Cell> StarCells = new();
  
  public readonly BoardHistory BoardHistory = new();
  
  public Board(Vector2Int size, CellData[,] map)
  {
    Size = size; 
    CellArray = new Cell[size.x, size.y];
    
    // Parse the board map
    for (int x = 0; x < size.x; x++)
    {
      for (int y = 0; y < size.y; y++)
      {
        CellData cellData = map[x, y];
        CellArray[x, y] = new Cell(new Vector2Int(x, y), cellData.Terrain, cellData.Item);

        if (cellData.Terrain == TerrainType.Start)
        {
          StartCell = CellArray[x, y];
        }
        
        if (cellData.Item == CellItem.Star)
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