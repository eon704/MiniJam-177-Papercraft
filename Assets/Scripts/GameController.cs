using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class GameController : MonoBehaviour
{
  [SerializeField] private Player playerPrefab;
  [SerializeField] private BoardPrefab boardPrefab;

  private BoardPiece playerPiece;

  private Dictionary<Player.StateType, int> startTransformations = new()
  {
    { Player.StateType.Default, 99 },
    { Player.StateType.Crane, 2 },
    { Player.StateType.Plane, 1 },
    { Player.StateType.Boat, 2 },
    { Player.StateType.Frog, 1 }
  };
  
  Sequence respawnSequence;

  public void ResetMap()
  {
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Lose);
    CellPrefab startCell = this.boardPrefab.GetStartCellPrefab();
    startCell.Cell.FreePiece();
    
    respawnSequence?.Kill();
    respawnSequence = DOTween.Sequence();
    respawnSequence.Append(this.playerPrefab.transform.DOScale(0, 0.5f));
    respawnSequence.AppendCallback(() => this.playerPrefab.SetDefaultState());
    respawnSequence.AppendCallback(() => this.playerPrefab.BoardPiecePrefab.Teleport(startCell));
    respawnSequence.AppendCallback(() => this.playerPrefab.SetTransformationLimits(startTransformations));
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
    this.playerPrefab.SetTransformationLimits(startTransformations);
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