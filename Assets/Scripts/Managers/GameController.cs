using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    [Header("References")]
    [field: SerializeField]
    public Player PlayerPrefab { get; private set; }

    [field: SerializeField]
    public BoardPrefab BoardPrefab { get; private set; }
    [SerializeField] private GameObject winScreen;
    [SerializeField] private VolcanoProjectile volcanoProjectilePrefab;
    [SerializeField] private float volcanoArcHeight = 3f;
    [SerializeField] private float volcanoProjectileDuration = 1f;
    private Camera cameraObject;

#if UNITY_EDITOR
    [Header("Testing Tools")]
    [Tooltip("Only works in the editor")]
    [SerializeField] private bool enableInfiniteMoves;
#endif

    public readonly UnityEvent OnMapReset = new();

    private BoardPiece playerPiece;
    private bool wasLevelWon;
    private BiomeCellSystem _biomeCellSystem;

    private Dictionary<Player.StateType, int> startMovesPerForm;

    private Sequence respawnSequence;

    public void ResetMap()
    {
        if (!FMODEvents.Instance.lose.IsNull) RuntimeManager.PlayOneShot(FMODEvents.Instance.lose);
        BoardPrefab.ResetFragileCells();
        _biomeCellSystem?.Reset();

        CellPrefab startCell = BoardPrefab.GetStartCellPrefab();
        startCell.Cell.FreePiece();

        List<CellPrefab> starCells = BoardPrefab.GetStarCellPrefabs();
        starCells.ForEach(cell => cell.Cell.ReassignStar());

        PlayerPrefab.StarAmount.Value = 0;

        BoardPrefab.Board.BoardHistory.Reset();
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

        if (winScreen != null && winScreen.activeSelf)
            winScreen.SetActive(false);
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
    }

    private IEnumerator Start()
    {
        startMovesPerForm = new Dictionary<Player.StateType, int>();
        foreach (MovePerFormEntry movesPerForm in LevelManager.Instance.CurrentLevel.StartMovesPerForm)
            startMovesPerForm[movesPerForm.State] = movesPerForm.Moves;

#if UNITY_EDITOR
        if (enableInfiniteMoves)
        {
            foreach (Player.StateType state in Enum.GetValues(typeof(Player.StateType)))
                startMovesPerForm[state] = 99;
        }
#endif

        BiomeType biome = LevelManager.Instance.CurrentLevel.Biome;
        yield return BiomeManager.Instance.LoadBiome(biome);
        cameraObject = Camera.main;

        BoardPrefab.Initialize(LevelManager.Instance.CurrentLevel);

        _biomeCellSystem = new BiomeCellSystem(BoardPrefab.Board, LevelManager.Instance.CurrentLevel);
        _biomeCellSystem.OnEruptionRequested += HandleVolcanoEruption;

        CellPrefab cellPrefab;
        (playerPiece, cellPrefab) = BoardPrefab.CreateNewPlayerPrefab();

        PlayerPrefab.Initialize(playerPiece, cellPrefab, BoardPrefab);
        PlayerPrefab.SetBiomeCellSystem(_biomeCellSystem);
        PlayerPrefab.OnPlayerWon.AddListener(OnWin);
        PlayerPrefab.OnPlayerDied.AddListener(ResetMap);
        PlayerPrefab.transform.localScale = Vector3.zero;

        yield return null;
        PlayerPrefab.SetTransformationLimits(startMovesPerForm);

        yield return new WaitUntil(() => BoardPrefab.IsSpawnAnimationComplete);
        PlayerPrefab.isMovementLocked = true;
        yield return PlayerPrefab.transform.DOScale(Vector3.one, 0.5f).WaitForCompletion();
        PlayerPrefab.isMovementLocked = false;
    }

    private void HandleVolcanoEruption(Vector2Int volcanoPos, Vector2Int targetPos, Action applyLava)
    {
        CellPrefab volcanoCell = BoardPrefab.GetCellPrefab(volcanoPos);
        CellPrefab targetCell  = BoardPrefab.GetCellPrefab(targetPos);

        if (volcanoCell == null || targetCell == null)
        {
            applyLava();
            return;
        }

        PlayerPrefab.isMovementLocked = true;

        // Shake the volcano cell before eruption
        volcanoCell.ShakeCell();

        // Delay matches ShakeCell duration (0.5s delay + 0.3s shake)
        float shakeDelay = 0.8f;

        DOVirtual.DelayedCall(shakeDelay, () =>
        {
            if (volcanoProjectilePrefab == null)
            {
                // No prefab assigned — apply lava immediately and unblock
                targetCell.ActivateExplosion(() =>
                {
                    applyLava();
                    PlayerPrefab.isMovementLocked = false;
                });
                return;
            }

            Vector3 from = volcanoCell.VolcanoTop != null
                ? volcanoCell.VolcanoTop.position
                : volcanoCell.transform.position + Vector3.up;

            Vector3 to = targetCell.ExplosionWorldPosition;

            VolcanoProjectile projectile = Instantiate(
                volcanoProjectilePrefab, from, Quaternion.identity);

            projectile.Launch(from, to, volcanoArcHeight, volcanoProjectileDuration, onLanded: () =>
            {
                targetCell.ActivateExplosion(() =>
                {
                    applyLava();
                    PlayerPrefab.isMovementLocked = false;
                });
            });
        });
    }

    private void OnWin(int stars)
    {
        wasLevelWon = true;
        LevelManager.Instance.SetCurrentLevelComplete(stars);
        if (!FMODEvents.Instance.win.IsNull) FMODUnity.RuntimeManager.PlayOneShot(FMODEvents.Instance.win);
    }
}
