using System.Collections.Generic;
using UnityEngine;

public class BoardPiece
{
  public readonly Observable<Cell> Cell;
  private readonly Board board;

  private readonly List<Vector2Int> moveOptions = new()
  {
    new Vector2Int(-1, 0),
    new Vector2Int(1, 0),
    new Vector2Int(0, -1),
    new Vector2Int(0, 1)
  };
  
  public BoardPiece(Board board, Cell startCell)
  {
    this.board = board;
    this.Cell = new Observable<Cell>(startCell);
  }
  
  public bool TeleportTo(Cell targetCell)
  {
    if (!targetCell.IsFree)
      return false;
    
    this.Cell.Value = targetCell;
    return true;
  }
  
  public bool MoveTo(Cell targetCell)
  {
    if (!this.CanMoveTo(targetCell))
      return false;
    
    this.Cell.Value = targetCell;
    return true;
  }

  private bool CanMoveTo(Cell targetCell)
  {
    if (!targetCell.IsFree)
      return false;
    
    Vector2Int motion = targetCell.Position - this.Cell.Value.Position;
    return this.moveOptions.Contains(motion);
  }
}