using System.Collections;
using UnityEngine;

public class GameController : MonoBehaviour
{
  [SerializeField] private Vector2Int playerStartCoord;
  [SerializeField] private Player playerPrefab;
  [SerializeField] private BoardPrefab boardPrefab;

  private BoardPiece playerPiece;

  private IEnumerator Start()
  {
    CellPrefab cellPrefab = this.boardPrefab.GetCellPrefab(this.playerStartCoord);
    this.playerPiece = this.boardPrefab.Board.CreateNewPiece(this.playerStartCoord);
    this.playerPrefab.Initialize(this.playerPiece, cellPrefab);
    
    yield return null;
  }
}