using UnityEngine;

public class BoardPiecePrefab : MonoBehaviour
{
  private BoardPiece boardPiece;
    
  public void Initialize(BoardPiece boardPieceData)
  {
    this.boardPiece = boardPieceData;
  }
}