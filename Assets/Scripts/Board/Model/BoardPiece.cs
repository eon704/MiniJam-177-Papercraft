using System.Collections.Generic;
using UnityEngine;

public class BoardPiece
{
  public readonly Observable<Cell> OccupiedCell;
  private readonly Board board;

  private List<Vector2Int> moveOptions = new()
  {
    new Vector2Int(-1, 0),
    new Vector2Int(1, 0),
    new Vector2Int(0, -1),
    new Vector2Int(0, 1)
  };

  private List<Cell.TerrainType> moveTerrain = new()
  {
    Cell.TerrainType.Default,
    Cell.TerrainType.Start,
    Cell.TerrainType.End,
    Cell.TerrainType.Fire
  };
  
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

  public List<Cell> GetReachableCells()
  {
    Vector2Int currentPosition = this.OccupiedCell.Value.Position;
    List<Cell> reachableCells = new();
    
    foreach (Vector2Int motion in this.moveOptions)
    {
      Vector2Int targetPosition = currentPosition + motion;
      Cell targetCell = this.board.GetCell(targetPosition);
      if (!this.moveTerrain.Contains(targetCell.Terrain))
        continue;
      
      reachableCells.Add(targetCell);
    }

    return reachableCells;
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