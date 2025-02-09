using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class BoardPiecePrefab : MonoBehaviour
{
  public BoardPiece BoardPiece { get; private set; }
  public CellPrefab CurrentCell { get; private set; }
  
  private BoardPrefab boardPrefab;
    
  public void Initialize(BoardPiece boardPieceData, CellPrefab startCell, BoardPrefab initBoardPrefab)
  {
    this.boardPrefab = initBoardPrefab;
    this.BoardPiece = boardPieceData;
    this.BoardPiece.OccupiedCell.OnChanged += (_, _, newCell) =>
      this.CurrentCell = this.boardPrefab.GetCellPrefab(newCell); 
    this.Teleport(startCell);
  }

  public List<CellPrefab> GetReachableCellPrefabs()
  {
    List<Cell> reachableCells = this.BoardPiece.GetReachableCells();
    List<CellPrefab> reachableCellPrefabs = this.boardPrefab.GetCellPrefabs(reachableCells);
    

    return reachableCellPrefabs;
  }

  public bool Move(CellPrefab targetCell, UnityAction onComplete = null, bool forceFailMovement = false)
  {
    bool success = !forceFailMovement && this.BoardPiece.MoveTo(targetCell.Cell);
    
    if (success)
    {
      this.transform
          .DOMove(targetCell.transform.position, 0.5f)
          .OnComplete(() => onComplete?.Invoke());
    }
    else
    {
      this.transform
          .DOShakePosition(0.5f, 0.3f)
          .OnComplete(() => onComplete?.Invoke());
    }

    return success;
  }

  public void Teleport(CellPrefab targetCell)
  {
    bool success = this.BoardPiece.TeleportTo(targetCell.Cell);

    if (!success)
    {
      Debug.LogError("Teleporting failed, targetCell is occupied");
      return;
    }

    this.transform.position = targetCell.transform.position;
  }
}