using System.Collections.Generic;
using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  public readonly Cell StartCell;
  public readonly List<Cell> StarCells = new();
  public readonly LevelData LevelData;
  
  public readonly BoardHistory BoardHistory = new();
  
  private int currentHintStep = 0;
  
  public Board(Vector2Int size, CellData[] map, LevelData levelData)
  {
    Size = size;
    CellArray = new Cell[size.x, size.y];
    LevelData = levelData;
    
    // Populate the board with cells
    for (int y = 0; y < size.y; y++)
    {
      for (int x = 0; x < size.x; x++)
      {
        int index = y * size.x + x;
        CellData cellData = map[index];
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

  /// <summary>
  /// Reveals the next cell in the cached solution as a hint.
  /// Returns true if a hint was revealed, false if no more hints available.
  /// </summary>
  public void RevealNextHint()
  {
    // Check if we have a cached solution
    if (LevelData.CachedSolution == null || LevelData.CachedSolution.Count == 0)
    {
      Debug.LogWarning("No cached solution available for hints.");
      return;
    }

    // Check if we've already revealed all steps
    if (currentHintStep >= LevelData.CachedSolution.Count)
    {
      Debug.Log("All solution steps have been revealed.");
      return;
    }

    // Get the next step in the solution
    SolutionStep nextStep = LevelData.CachedSolution[currentHintStep];
    Cell cellToReveal = GetCell(nextStep.Position);
    
    if (cellToReveal != null)
    {
      cellToReveal.RevealHint();
      currentHintStep++;
      Debug.Log($"Revealed hint step {currentHintStep}/{LevelData.CachedSolution.Count} at position {nextStep.Position}");
    }
    else
    {
      Debug.LogError($"Invalid position in solution step: {nextStep.Position}");
    }
  }

  /// <summary>
  /// Clears all revealed hints and resets the hint step counter.
  /// </summary>
  public void ClearAllHints()
  {
    // Clear all cell hints
    for (int x = 0; x < Size.x; x++)
    {
      for (int y = 0; y < Size.y; y++)
      {
        CellArray[x, y].HideHint();
      }
    }
    
    // Reset hint step counter
    currentHintStep = 0;
    Debug.Log("All hints cleared.");
  }

  /// <summary>
  /// Gets the current hint step (0-based index).
  /// </summary>
  public int CurrentHintStep => currentHintStep;

  /// <summary>
  /// Gets the total number of steps in the cached solution.
  /// </summary>
  public int TotalSolutionSteps => LevelData.CachedSolution?.Count ?? 0;

  /// <summary>
  /// Checks if there are more hints available to reveal.
  /// </summary>
  public bool HasMoreHints => currentHintStep < TotalSolutionSteps;
}