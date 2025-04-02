using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    [Header("References")]
    [field: SerializeField]
    public Player PlayerPrefab { get; private set; }

    [SerializeField] private BoardPrefab boardPrefab;
    [SerializeField] private List<PulseImage> nudgeImages;
    [SerializeField] private GameObject finalScreen;
    [SerializeField] private GameObject winScreen;

    [Header("Testing Tools")] [Tooltip("Only works in the editor")] [SerializeField]
    private bool enableInfiniteMoves;

    public readonly UnityEvent OnMapReset = new();

    private BoardPiece playerPiece;
    private bool wasLevelWon;

    private Dictionary<Player.StateType, int> startMovesPerForm;
    private int attemptsCount = 1;
    
    Sequence respawnSequence;

    public void ResetMap()
    {
        UgsManager.Instance.RecordNewLevelAttemptEvent(LevelManager.Instance.CurrentLevelIndex, attemptsCount);
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Lose);
        CellPrefab startCell = this.boardPrefab.GetStartCellPrefab();
        startCell.Cell.FreePiece();

        List<CellPrefab> starCells = this.boardPrefab.GetStarCellPrefabs();
        starCells.ForEach(cell => cell.Cell.ReassignStar());

        this.PlayerPrefab.StarAmount.Value = 0;

        this.nudgeImages.ForEach(image => image.gameObject.SetActive(true));
        this.OnMapReset?.Invoke();

        PlayerPrefab.isMovementLocked = true;
        
        respawnSequence?.Kill();
        respawnSequence = DOTween.Sequence();
        respawnSequence.Append(this.PlayerPrefab.transform.DOScale(0, 0.5f));
        respawnSequence.AppendCallback(() => this.PlayerPrefab.SetDefaultState());
        respawnSequence.AppendCallback(() => this.PlayerPrefab.BoardPiecePrefab.Teleport(startCell));
        respawnSequence.AppendCallback(() => this.PlayerPrefab.SetTransformationLimits(this.startMovesPerForm));
        respawnSequence.Append(this.PlayerPrefab.transform.DOScale(1f, 0.5f));
        respawnSequence.AppendCallback(() => PlayerPrefab.isMovementLocked = false);

        if (winScreen !=null)
        {
            if (winScreen.activeSelf)
            {
                winScreen.SetActive(false);
            }
        }
    }

    public void LoadNextLevel()
    {
        LevelManager.Instance.PrepareNextLevel();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnLoadingMainMenu()
    {
        if (wasLevelWon)
            return;
        
        UgsManager.Instance.RecordLevelQuitEvent(LevelManager.Instance.CurrentLevelIndex, attemptsCount);
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
            foreach (Player.StateType state in Enum.GetValues(typeof(Player.StateType)))
            {
                this.startMovesPerForm[state] = 99;
            }
        }

#endif

        this.boardPrefab.Initialize(LevelManager.Instance.CurrentLevel.Map, LevelManager.Instance.CurrentLevel.MapSize);
        CellPrefab cellPrefab;
        (this.playerPiece, cellPrefab) = this.boardPrefab.CreateNewPlayerPrefab();

        this.PlayerPrefab.Initialize(this.playerPiece, cellPrefab, this.boardPrefab);
        this.PlayerPrefab.OnPlayerWon.AddListener(this.OnWin);
        this.PlayerPrefab.OnPlayerDied.AddListener(this.ResetMap);
        this.PlayerPrefab.OnTransformation.AddListener(_ =>
            nudgeImages.ForEach(image => image.gameObject.SetActive(false)));
        this.PlayerPrefab.transform.localScale = Vector3.zero;
        
        yield return null;
        this.PlayerPrefab.SetTransformationLimits(this.startMovesPerForm);
        this.nudgeImages.ForEach(image => image.gameObject.SetActive(true));

        yield return new WaitUntil(() => this.boardPrefab.IsSpawnAnimationComplete);
        this.PlayerPrefab.transform.DOScale(Vector3.one, 0.5f);
        
        yield return new WaitUntil(() => UgsManager.Instance.IsInitialized);
        UgsManager.Instance.RecordNewLevelAttemptEvent(LevelManager.Instance.CurrentLevelIndex, attemptsCount);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            this.ResetMap();
        }
    }

    private void OnWin(int stars)
    {
        if (LevelManager.Instance.IsLastLevel())
        {
            this.finalScreen.SetActive(true);
        }

        wasLevelWon = true;
        UgsManager.Instance.RecordLevelPassedEvent(LevelManager.Instance.CurrentLevelIndex, attemptsCount, stars);
        LevelManager.Instance.SetCurrentLevelComplete(stars);
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Win);
    }
}