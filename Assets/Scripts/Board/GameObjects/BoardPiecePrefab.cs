using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class BoardPiecePrefab : MonoBehaviour
{
  public BoardPiece BoardPiece { get; private set; }
  public CellPrefab CurrentCell { get; private set; }
    
  public void Initialize(BoardPiece boardPieceData, CellPrefab startCell)
  {
    this.BoardPiece = boardPieceData;
    this.Teleport(startCell);
  }

  public void Move(CellPrefab targetCell, UnityAction onComplete = null)
  {
    bool success = this.BoardPiece.MoveTo(targetCell.Cell);
    
    if (success)
    {
      this.transform
          .DOMove(targetCell.transform.position, 0.5f)
          .OnComplete(() =>
          {
            this.CurrentCell = targetCell;
            onComplete?.Invoke();
          });
    }
    else
    {
      this.transform
          .DOShakePosition(0.5f, 0.3f)
          .OnComplete(() => onComplete?.Invoke());
    }
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
    this.CurrentCell = targetCell;
  }
}