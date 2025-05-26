using System.Collections.Generic;
using UnityEngine;

public class BoardPiece
{
  public readonly Observable<Cell> OccupiedCell;
  private readonly Board board;

  private List<Vector2Int> moveOptions = new();

  private List<Cell.TerrainType> moveTerrain = new();
  
  public BoardPiece(Board board, Cell startCell)
  {
    this.board = board;
    OccupiedCell = new Observable<Cell>(startCell);
  }
  
  public bool TeleportTo(Cell targetCell)
  {
    if (!targetCell.IsFree)
      return false;
    
    OccupiedCell.Value?.FreePiece();
    OccupiedCell.Value = targetCell;
    OccupiedCell.Value.AssignPiece(this);
    return true;
  }
  
  public bool MoveTo(Cell targetCell)
  {
    if (!CanMoveTo(targetCell))
      return false;
    
    OccupiedCell.Value.FreePiece();
    OccupiedCell.Value = targetCell;
    OccupiedCell.Value.AssignPiece(this);
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Move);
    return true;
  }

  public List<(Cell, bool)> GetMoveOptionCells()
  {
    Vector2Int currentPosition = OccupiedCell.Value.Position;
    List<(Cell, bool)> moveOptionCells = new();
    
    foreach (Vector2Int motion in moveOptions)
    {
      Vector2Int targetPosition = currentPosition + motion;
      Cell targetCell = board.GetCell(targetPosition);
      if (targetCell == null || targetCell.Terrain == Cell.TerrainType.None)
        continue;

      bool isValidMove = moveTerrain.Contains(targetCell.Terrain); 
      moveOptionCells.Add((targetCell, isValidMove));
    }

    return moveOptionCells;
  }
  
  public void SetState(IState state)
  {
    moveOptions = state.MoveOptions;
    moveTerrain = state.MoveTerrain;
  }

  private bool CanMoveTo(Cell targetCell)
  {
    if (!targetCell.IsFree)
      return false;
    
    if (!moveTerrain.Contains(targetCell.Terrain))
      return false;
    
    Vector2Int motion = targetCell.Position - OccupiedCell.Value.Position;
    return moveOptions.Contains(motion);
  }
}