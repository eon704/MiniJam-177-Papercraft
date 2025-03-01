using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
  [Header("References")]
  [SerializeField] private Player playerPrefab;
  [SerializeField] private BoardPrefab boardPrefab;
  [SerializeField] private List<PulseImage> nudgeImages;
  [SerializeField] private GameObject winScreen;
  [SerializeField] private GameObject finalScreen;
  
  [Header("Testing Tools")]
  [Tooltip("Only works in the editor")]
  [SerializeField] private bool enableInfiniteMoves;

  private BoardPiece playerPiece;

  private Dictionary<Player.StateType, int> startMovesPerForm;
  
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
    respawnSequence.Append(this.playerPrefab.transform.DOScale(1f, 0.5f));
  }

  public void LoadNextLevel()
  {
    LevelManager.Instance.PrepareNextLevel();
    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
  }

  private IEnumerator Start()
  {
    this.startMovesPerForm = new Dictionary<Player.StateType, int>();
    foreach (MovePerFormEntry movesPerForm in LevelManager.Instance.CurrentLevel.StartMovesPerForm)
    {
      this.startMovesPerForm[movesPerForm.State] = movesPerForm.Moves;
    }
    
    #if UNITY_EDITOR

    if (this.enableInfiniteMoves)
    {
      foreach (Player.StateType state in System.Enum.GetValues(typeof(Player.StateType)))
      {
          this.startMovesPerForm[state] = 99;
      }
    }

    #endif
    
    this.boardPrefab.Initialize(LevelManager.Instance.CurrentLevel.Map, LevelManager.Instance.CurrentLevel.MapSize);
    CellPrefab cellPrefab;
    (this.playerPiece, cellPrefab) = this.boardPrefab.CreateNewPlayerPrefab();
    
    this.playerPrefab.Initialize(this.playerPiece, cellPrefab, this.boardPrefab);

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
    if (LevelManager.Instance.IsLastLevel())
    {
      this.finalScreen.SetActive(true);
    }
    else
    {
      this.winScreen.SetActive(true);
    }
    
    LevelManager.Instance.SetCurrentLevelComplete();
    GlobalSoundManager.PlayRandomSoundByType(SoundType.Win);
  }
}