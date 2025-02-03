using System.Collections;
using DG.Tweening;
using UnityEngine;

public class GameController : MonoBehaviour
{
  [SerializeField] private Player playerPrefab;
  [SerializeField] private BoardPrefab boardPrefab;

  private BoardPiece playerPiece;

  public void ResetMap()
  {
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Lose);
    CellPrefab startCell = this.boardPrefab.GetStartCellPrefab();

    Sequence respawnSequence = DOTween.Sequence();
    respawnSequence.Append(this.playerPrefab.transform.DOScale(0, 0.5f));
    respawnSequence.AppendCallback(() => this.playerPrefab.BoardPiecePrefab.Teleport(startCell));
    respawnSequence.Append(this.playerPrefab.transform.DOScale(0.3f, 0.5f));
  }

  private IEnumerator Start()
  {
    CellPrefab cellPrefab;
    (this.playerPiece, cellPrefab) = this.boardPrefab.CreateNewPlayerPrefab();
    this.playerPrefab.Initialize(this.playerPiece, cellPrefab);
    this.playerPrefab.OnPlayerWon.AddListener(this.OnWin);
    this.playerPrefab.OnPlayerDied.AddListener(this.ResetMap);
    
    yield return null;
  }

  private void Update()
  {
    if (Input.GetKeyDown(KeyCode.R))
    {
      this.ResetMap();
    }
  }

  private void OnWin()
  {
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Win);
  }
}