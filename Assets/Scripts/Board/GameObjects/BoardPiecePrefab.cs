using DG.Tweening;
using UnityEngine;

public class BoardPiecePrefab : MonoBehaviour
{
  private BoardPiece boardPiece;
    
  public void Initialize(BoardPiece boardPieceData, CellPrefab startCell)
  {
    this.boardPiece = boardPieceData;
    this.Teleport(startCell);
  }

  public void Move(CellPrefab targetCell)
  {
    bool success = this.boardPiece.MoveTo(targetCell.Cell);
     
    this.DOKill();
    if (success)
      this.transform.DOMove(targetCell.transform.position, 0.5f);
    else
      this.transform.DOShakePosition(0.5f, 0.3f);
  }
  
  public void Teleport(CellPrefab targetCell)
  {
    bool success = this.boardPiece.TeleportTo(targetCell.Cell);

    if (!success)
    {
      Debug.LogError("Teleporting failed, targetCell is occupied");
      return;
    }

    this.transform.position = targetCell.transform.position;
  }
}