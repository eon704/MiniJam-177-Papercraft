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
    CellPrefab cellPrefab;
    (this.playerPiece, cellPrefab) = this.boardPrefab.CreateNewPlayerPrefab();
    this.playerPrefab.Initialize(this.playerPiece, cellPrefab);
    
    yield return null;
  }
}