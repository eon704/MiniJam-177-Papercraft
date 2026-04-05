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

    [field: SerializeField]
    public BoardPrefab BoardPrefab { get; private set; }
    [SerializeField] private GameObject winScreen;
    [SerializeField] private VolcanoProjectile volcanoProjectilePrefab;
    [SerializeField] private float volcanoArcHeight = 3f;
    [SerializeField] private float volcanoProjectileDuration = 1f;
    [Tooltip("Unlit/Transparent material for the dashed arc preview. Leave null to disable arc.")]
    [SerializeField] private Material volcanoArcMaterial;
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
    private readonly HashSet<Vector2Int> _volcanoWarningCells = new();
    private readonly List<VolcanoArcPreview> _arcPreviews = new();
    private bool _eruptionActive;
    private bool _boardReady;

    private Dictionary<Player.StateType, int> startMovesPerForm;

    private Sequence respawnSequence;

    public void ResetMap()
    {
        PlayerPrefab.CancelPendingEruptions();
        BoardPrefab.ResetFragileCells();
        _biomeCellSystem?.Reset();

        CellPrefab startCell = BoardPrefab.GetStartCellPrefab();
        startCell.Cell.FreePiece();

        List<CellPrefab> starCells = BoardPrefab.GetStarCellPrefabs();
        starCells.ForEach(cell => cell.Cell.ReassignStar());

        PlayerPrefab.StarAmount.Value = 0;

        BoardPrefab.Board.BoardHistory.Reset();
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxReset);
        OnMapReset?.Invoke();

        PlayerPrefab.isMovementLocked = true;

        respawnSequence?.Kill();
        respawnSequence = DOTween.Sequence();
        respawnSequence.Append(PlayerPrefab.transform.DOScale(0, 0.5f));
        respawnSequence.AppendCallback(() => PlayerPrefab.SetDefaultState());
        respawnSequence.AppendCallback(() => PlayerPrefab.BoardPiecePrefab.Teleport(startCell));
        respawnSequence.AppendCallback(() => PlayerPrefab.SetTransformationLimits(startMovesPerForm));
        respawnSequence.Append(PlayerPrefab.transform.DOScale(1f, 0.5f));
        respawnSequence.AppendCallback(() => { PlayerPrefab.isMovementLocked = false; PlayerPrefab.isStateChangeLocked = false; });

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
        FMODAudioManager.Instance.PlayMusicForBiome(biome);
        cameraObject = Camera.main;

        BoardPrefab.Initialize(LevelManager.Instance.CurrentLevel);

        _biomeCellSystem = new BiomeCellSystem(BoardPrefab.Board, LevelManager.Instance.CurrentLevel);
        _biomeCellSystem.OnEruptionVisual += HandleVolcanoEruptionVisual;
        _biomeCellSystem.OnNextTargetsChanged += RefreshVolcanoWarnings;

        CellPrefab cellPrefab;
        (playerPiece, cellPrefab) = BoardPrefab.CreateNewPlayerPrefab();

        PlayerPrefab.Initialize(playerPiece, cellPrefab, BoardPrefab);
        PlayerPrefab.SetBiomeCellSystem(_biomeCellSystem);
        PlayerPrefab.OnPlayerWon.AddListener(OnWin);
        PlayerPrefab.OnPlayerDied.AddListener(ResetMap);
        PlayerPrefab.OnPlayerWon.AddListener(_ => { FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxWin); FMODAudioManager.Instance.StopMusic(); });
        PlayerPrefab.OnPlayerDied.AddListener(() => FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxLose));
        PlayerPrefab.transform.localScale = Vector3.zero;

        yield return null;
        PlayerPrefab.SetTransformationLimits(startMovesPerForm);

        yield return new WaitUntil(() => BoardPrefab.IsSpawnAnimationComplete);
        PlayerPrefab.isMovementLocked = true;
        yield return PlayerPrefab.transform.DOScale(Vector3.one, 0.5f).WaitForCompletion();
        _boardReady = true;
        RefreshVolcanoWarnings();
        PlayerPrefab.isMovementLocked = false;
        PlayerPrefab.isStateChangeLocked = false;
    }

    private void RefreshVolcanoWarnings()
    {
        foreach (var pos in _volcanoWarningCells)
            BoardPrefab.GetCellPrefab(pos)?.SetVolcanoWarning(false);
        _volcanoWarningCells.Clear();

        foreach (var preview in _arcPreviews)
            if (preview != null) Destroy(preview.gameObject);
        _arcPreviews.Clear();

        if (_biomeCellSystem == null || !_boardReady) return;
        foreach (var (volcanoPos, targetPos, countdown) in _biomeCellSystem.GetNextLavaData())
        {
            _volcanoWarningCells.Add(targetPos);
            BoardPrefab.GetCellPrefab(targetPos)?.SetVolcanoWarning(true, countdown);

            if (volcanoArcMaterial == null) continue;
            CellPrefab volcanoCell = BoardPrefab.GetCellPrefab(volcanoPos);
            CellPrefab targetCell  = BoardPrefab.GetCellPrefab(targetPos);
            if (volcanoCell == null || targetCell == null) continue;

            Vector3 from = volcanoCell.VolcanoTop != null
                ? volcanoCell.VolcanoTop.position
                : volcanoCell.transform.position + Vector3.up;
            Vector3 to = targetCell.ExplosionWorldPosition;

            var go = new GameObject("VolcanoArcPreview");
            var preview = go.AddComponent<VolcanoArcPreview>();
            preview.Draw(from, to, volcanoArcHeight, countdown, volcanoArcMaterial);
            if (_eruptionActive) go.SetActive(false);
            _arcPreviews.Add(preview);
        }
    }

    // Visual handler — lava is applied when the projectile lands, not before.
    private void HandleVolcanoEruptionVisual(Vector2Int volcanoPos, Vector2Int targetPos)
    {
        _eruptionActive = true;
        foreach (var p in _arcPreviews)
            if (p != null) p.gameObject.SetActive(false);

        PlayerPrefab.StartEruption();

        CellPrefab volcanoCell = BoardPrefab.GetCellPrefab(volcanoPos);
        CellPrefab targetCell  = BoardPrefab.GetCellPrefab(targetPos);

        if (volcanoCell == null || targetCell == null)
        {
            _eruptionActive = false;
            foreach (var p in _arcPreviews)
                if (p != null) p.gameObject.SetActive(true);
            PlayerPrefab.EndEruption();
            return;
        }

        volcanoCell.ShakeCell();

        DOVirtual.DelayedCall(0.8f, () =>
        {
            if (volcanoProjectilePrefab == null)
            {
                targetCell.ActivateExplosion(() =>
                {
                    OnProjectileLanded(targetPos, targetCell);
                });
                return;
            }

            Vector3 from = volcanoCell.VolcanoTop != null
                ? volcanoCell.VolcanoTop.position
                : volcanoCell.transform.position + Vector3.up;

            Vector3 to = targetCell.ExplosionWorldPosition;

            VolcanoProjectile projectile = Instantiate(
                volcanoProjectilePrefab, from, Quaternion.identity);

            FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxVolcanoLaunch);
            projectile.Launch(from, to, volcanoArcHeight, volcanoProjectileDuration,
                onLanded: () => targetCell.ActivateExplosion(() =>
                {
                    OnProjectileLanded(targetPos, targetCell);
                }));
        });
    }

    private void OnProjectileLanded(Vector2Int targetPos, CellPrefab targetCell)
    {
        // Apply lava to model now that the projectile has visually arrived
        _biomeCellSystem?.ApplyLava(targetPos);

        // Kill player if they were standing on the impact cell
        if (PlayerPrefab.BoardPiecePrefab.CurrentCell?.Cell?.Position == targetPos)
        {
            targetCell.ActivateFireSplash();
            PlayerPrefab.OnPlayerDied.Invoke();
        }

        _eruptionActive = false;
        foreach (var p in _arcPreviews)
            if (p != null) p.gameObject.SetActive(true);
        PlayerPrefab.EndEruption();
    }

    private void OnWin(int stars)
    {
        wasLevelWon = true;
        LevelManager.Instance.SetCurrentLevelComplete(stars);
    }
}
