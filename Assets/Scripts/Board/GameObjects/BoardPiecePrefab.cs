using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class BoardPiecePrefab : MonoBehaviour
{
  public BoardPiece BoardPiece { get; private set; }
  public CellPrefab CurrentCell { get; private set; }
  
  private BoardPrefab boardPrefab;

  private Tween _moveTween; 
    
  public void Initialize(BoardPiece boardPieceData, CellPrefab startCell, BoardPrefab initBoardPrefab)
  {
    boardPrefab = initBoardPrefab;
    BoardPiece = boardPieceData;
    BoardPiece.OccupiedCell.OnChanged += (_, oldCell, newCell) =>
    {
      boardPrefab.GetCellPrefab(oldCell).ResetPulse();
      CurrentCell = boardPrefab.GetCellPrefab(newCell);
    };
    Teleport(startCell);
  }

  public List<CellPrefab> GetMoveOptionCellPrefabs()
  {
    List<(Cell, bool)> moveOptionCells = BoardPiece.GetMoveOptionCells();
    List<CellPrefab> moveOptionCellPrefabs = new();
    foreach (var (cell, isValidMove) in moveOptionCells)
    {
        CellPrefab cellPrefab = boardPrefab.GetCellPrefab(cell);
        cellPrefab.SetIsValidMoveOption(isValidMove);
        
        if (isValidMove)
          moveOptionCellPrefabs.Add(cellPrefab);
    }
    
    return moveOptionCellPrefabs;
  }

  public bool Move(CellPrefab targetCell, UnityAction onComplete = null, bool forceFailMovement = false)
  {
    //transform.DOComplete(true);
    transform.DOKill();
    bool success = !forceFailMovement && BoardPiece.MoveTo(targetCell.Cell);

    if (success)
    {
      var path = new[]
      {
        transform.position,
        (transform.position + targetCell.transform.position) / 2 + Vector3.up * 0.4f, // Midpoint with an upward offset
        targetCell.transform.position
      };

      _moveTween = transform
        .DOPath(path, 0.5f, PathType.CatmullRom)
        .SetEase(Ease.InOutQuad)
        .OnKill(() => onComplete?.Invoke());
    }
    else
    {
      _moveTween = transform
        .DOShakePosition(0.5f, 0.3f)
        .OnKill(() => onComplete?.Invoke());
    }

    return success;
  }

  public void Teleport(CellPrefab targetCell, bool tweenMovement = false)
  {
    bool success = BoardPiece.TeleportTo(targetCell.Cell);

    if (!success)
    {
      Debug.LogError("Teleporting failed, targetCell is occupied");
      return;
    }

    if (tweenMovement)
    {
      transform.DOKill();
      
      var path = new[]
      {
        transform.position,
        (transform.position + targetCell.transform.position) / 2 + Vector3.up * 0.4f, // Midpoint with an upward offset
        targetCell.transform.position
      };

      transform
        .DOPath(path, 0.5f, PathType.CatmullRom)
        .SetEase(Ease.InOutQuad);
    }
    else
    {
      transform.position = targetCell.transform.position;
    }
  }

  private void OnDestroy()
  {
    transform.DOKill();
  }
}