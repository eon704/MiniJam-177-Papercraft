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
    [SerializeField] private Camera cameraObject;

#if UNITY_EDITOR    
    [Header("Testing Tools")] 
    
    [Tooltip("Only works in the editor")] 
    [SerializeField] private bool enableInfiniteMoves;
#endif
    
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
        CellPrefab startCell = boardPrefab.GetStartCellPrefab();
        startCell.Cell.FreePiece();

        List<CellPrefab> starCells = boardPrefab.GetStarCellPrefabs();
        starCells.ForEach(cell => cell.Cell.ReassignStar());

        PlayerPrefab.StarAmount.Value = 0;

        nudgeImages.ForEach(image => image.gameObject.SetActive(true));
        boardPrefab.Board.BoardHistory.Reset();
        OnMapReset?.Invoke();

        PlayerPrefab.isMovementLocked = true;
        
        respawnSequence?.Kill();
        respawnSequence = DOTween.Sequence();
        respawnSequence.Append(PlayerPrefab.transform.DOScale(0, 0.5f));
        respawnSequence.AppendCallback(() => PlayerPrefab.SetDefaultState());
        respawnSequence.AppendCallback(() => PlayerPrefab.BoardPiecePrefab.Teleport(startCell));
        respawnSequence.AppendCallback(() => PlayerPrefab.SetTransformationLimits(startMovesPerForm));
        respawnSequence.Append(PlayerPrefab.transform.DOScale(1f, 0.5f));
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
        startMovesPerForm = new Dictionary<Player.StateType, int>();
        foreach (MovePerFormEntry movesPerForm in LevelManager.Instance.CurrentLevel.StartMovesPerForm)
        {
            startMovesPerForm[movesPerForm.State] = movesPerForm.Moves;
        }

#if UNITY_EDITOR
        if (enableInfiniteMoves)
        {
            foreach (Player.StateType state in Enum.GetValues(typeof(Player.StateType)))
            {
                startMovesPerForm[state] = 99;
            }
        }
#endif

        boardPrefab.Initialize(LevelManager.Instance.CurrentLevel);
        CellPrefab cellPrefab;
        (playerPiece, cellPrefab) = boardPrefab.CreateNewPlayerPrefab();

        PlayerPrefab.Initialize(playerPiece, cellPrefab, boardPrefab);
        PlayerPrefab.OnPlayerWon.AddListener(OnWin);
        PlayerPrefab.OnPlayerDied.AddListener(ResetMap);
        PlayerPrefab.OnTransformation.AddListener(_ =>
            nudgeImages.ForEach(image => image.gameObject.SetActive(false)));
        PlayerPrefab.transform.localScale = Vector3.zero;

        cameraObject.transform.position = boardPrefab.WorldCenter;
        cameraObject.orthographicSize = boardPrefab.Size.x switch
        {
            <= 6 => 10f,
            7 => 12f,
            8 => 13.5f,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        yield return null;
        PlayerPrefab.SetTransformationLimits(startMovesPerForm);
        nudgeImages.ForEach(image => image.gameObject.SetActive(true));

        yield return new WaitUntil(() => boardPrefab.IsSpawnAnimationComplete);
        PlayerPrefab.transform.DOScale(Vector3.one, 0.5f);
        
        yield return new WaitUntil(() => UgsManager.Instance.IsInitialized);
        UgsManager.Instance.RecordNewLevelAttemptEvent(LevelManager.Instance.CurrentLevelIndex, attemptsCount);
    }

    private void OnWin(int stars)
    {
        if (LevelManager.Instance.IsLastLevel())
        {
            finalScreen.SetActive(true);
        }

        wasLevelWon = true;
        UgsManager.Instance.RecordLevelPassedEvent(LevelManager.Instance.CurrentLevelIndex, attemptsCount, stars);
        LevelManager.Instance.SetCurrentLevelComplete(stars);
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Win);
    }
}