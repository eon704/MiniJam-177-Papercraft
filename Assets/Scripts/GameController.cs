using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class GameController : MonoBehaviour
{
  [SerializeField] private Player playerPrefab;
  [SerializeField] private BoardPrefab boardPrefab;
  [SerializeField] private List<PulseImage> nudgeImages;

  private BoardPiece playerPiece;

  private Dictionary<Player.StateType, int> startMovesPerForm = new()
  {
    { Player.StateType.Default, 0 },
    { Player.StateType.Crane, 5 },
    { Player.StateType.Plane, 5 },
    { Player.StateType.Boat, 5 },
    { Player.StateType.Frog, 5 }
  };
  
  Sequence respawnSequence;

  public void ResetMap()
  {
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Lose);
    CellPrefab startCell = this.boardPrefab.GetStartCellPrefab();
    startCell.Cell.FreePiece();
    
    this.nudgeImages.ForEach(image => image.gameObject.SetActive(true));
    
    respawnSequence?.Kill();
    respawnSequence = DOTween.Sequence();
    respawnSequence.Append(this.playerPrefab.transform.DOScale(0, 0.5f));
    respawnSequence.AppendCallback(() => this.playerPrefab.SetDefaultState());
    respawnSequence.AppendCallback(() => this.playerPrefab.BoardPiecePrefab.Teleport(startCell));
    respawnSequence.AppendCallback(() => this.playerPrefab.SetTransformationLimits(this.startMovesPerForm));
    respawnSequence.Append(this.playerPrefab.transform.DOScale(0.3f, 0.5f));
  }

  private IEnumerator Start()
  {
    CellPrefab cellPrefab;
    (this.playerPiece, cellPrefab) = this.boardPrefab.CreateNewPlayerPrefab();
    
    this.playerPrefab.Initialize(this.playerPiece, cellPrefab);

    this.playerPrefab.OnPlayerWon.AddListener(this.OnWin);
    this.playerPrefab.OnPlayerDied.AddListener(this.ResetMap);
    this.playerPrefab.OnTransformation.AddListener(() => nudgeImages.ForEach(image => image.gameObject.SetActive(false)));

    yield return null;
    this.playerPrefab.SetTransformationLimits(this.startMovesPerForm);
    this.nudgeImages.ForEach(image => image.gameObject.SetActive(true));
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