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
    this.OccupiedCell = new Observable<Cell>(startCell);
  }
  
  public bool TeleportTo(Cell targetCell)
  {
    if (!targetCell.IsFree)
      return false;
    
    this.OccupiedCell.Value?.FreePiece();
    this.OccupiedCell.Value = targetCell;
    this.OccupiedCell.Value.AssignPiece(this);
    return true;
  }
  
  public bool MoveTo(Cell targetCell)
  {
    if (!this.CanMoveTo(targetCell))
      return false;
    
    this.OccupiedCell.Value.FreePiece();
    this.OccupiedCell.Value = targetCell;
    this.OccupiedCell.Value.AssignPiece(this);
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Move);
    return true;
  }

  public List<(Cell, bool)> GetMoveOptionCells()
  {
    Vector2Int currentPosition = this.OccupiedCell.Value.Position;
    List<(Cell, bool)> moveOptionCells = new();
    
    foreach (Vector2Int motion in this.moveOptions)
    {
      Vector2Int targetPosition = currentPosition + motion;
      Cell targetCell = this.board.GetCell(targetPosition);
      if (targetCell == null || targetCell.Terrain == Cell.TerrainType.None)
        continue;

      bool isValidMove = moveTerrain.Contains(targetCell.Terrain); 
      moveOptionCells.Add((targetCell, isValidMove));
    }

    return moveOptionCells;
  }
  
  public void SetState(IState state)
  {
    this.moveOptions = state.MoveOptions;
    this.moveTerrain = state.MoveTerrain;
  }

  private bool CanMoveTo(Cell targetCell)
  {
    if (!targetCell.IsFree)
      return false;
    
    if (!this.moveTerrain.Contains(targetCell.Terrain))
      return false;
    
    Vector2Int motion = targetCell.Position - this.OccupiedCell.Value.Position;
    return this.moveOptions.Contains(motion);
  }
}