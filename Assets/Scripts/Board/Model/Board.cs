using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Board
{
  public readonly Vector2Int Size;
  public readonly Cell[,] CellArray;
  public readonly Cell StartCell;
  public readonly List<Cell> StarCells = new();
  public readonly LevelData LevelData;
  
  public readonly BoardHistory BoardHistory = new();
  
  private int currentHintStep = 1;
  
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
  public (Cell, int) RevealNextHint(out bool areAllHintsRevealed)
  {
    areAllHintsRevealed = false;

    if (currentHintStep >= LevelData.CachedSolution.Count)
    {
      return(null, -1);
    }

    // Get the next step in the solution
    SolutionStep nextStep = LevelData.CachedSolution[currentHintStep];
    Cell cellToReveal = GetCell(nextStep.Position);
    int revealDepth = cellToReveal.RevealHint();
    currentHintStep++;

    // No need to reveal the turn to finish tile
    if (currentHintStep >= LevelData.CachedSolution.Count - 1)
    {
      areAllHintsRevealed = true;
    }

    return (cellToReveal, revealDepth);
  }

  /// <summary>
  /// Reveals a specific hint by step number (1-based index, excluding start position).
  /// This allows players to choose which hint they want to reveal.
  /// </summary>
  public (Cell, int) RevealSpecificHint(int hintStepNumber, out bool areAllHintsRevealed)
  {
    areAllHintsRevealed = false;

    // Validate the hint step number (1-based, excluding start position)
    if (hintStepNumber < 1 || hintStepNumber >= LevelData.CachedSolution.Count - 1)
    {
      return (null, -1);
    }

    // Get the specific step in the solution (convert to 0-based index)
    SolutionStep targetStep = LevelData.CachedSolution[hintStepNumber];
    Cell cellToReveal = GetCell(targetStep.Position);
    
    // Check if this hint is already revealed
    if (cellToReveal.IsHintRevealed.Value > 0)
    {
      return (cellToReveal, -1);
    }

    int revealDepth = cellToReveal.RevealHint();

    // Check if all valid hints are now revealed
    bool allRevealed = true;
    for (int i = 1; i < LevelData.CachedSolution.Count - 1; i++)
    {
      Cell checkCell = GetCell(LevelData.CachedSolution[i].Position);
      if (checkCell.IsHintRevealed.Value <= 0)
      {
        allRevealed = false;
        break;
      }
    }
    areAllHintsRevealed = allRevealed;

    return (cellToReveal, revealDepth);
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

  /// <summary>
  /// Checks if there are any hints that haven't been revealed yet.
  /// This is different from HasMoreHints which only checks sequential progression.
  /// </summary>
  public bool HasUnrevealedHints()
  {
    // Check if any steps in the solution (excluding start and end) are not revealed
    for (int i = 1; i < LevelData.CachedSolution.Count - 1; i++)
    {
      Cell checkCell = GetCell(LevelData.CachedSolution[i].Position);
      if (checkCell.IsHintRevealed.Value <= 0)
      {
        return true;
      }
    }
    return false;
  }
}