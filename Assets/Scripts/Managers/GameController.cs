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
    [SerializeField] private List<PulseImage> nudgeImages;
    [SerializeField] private GameObject winScreen;
    [SerializeField] private Camera cameraObject;
    [Tooltip("Camera offset from board center for isometric view. Tune in Inspector.")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(-7f, 10f, -7f);
    [SerializeField] private float cameraOrthographicSize = 8f;

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
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Lose);
        CellPrefab startCell = BoardPrefab.GetStartCellPrefab();
        startCell.Cell.FreePiece();

        List<CellPrefab> starCells = BoardPrefab.GetStarCellPrefabs();
        starCells.ForEach(cell => cell.Cell.ReassignStar());

        PlayerPrefab.StarAmount.Value = 0;

        nudgeImages.ForEach(image => image.gameObject.SetActive(true));
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

        if (winScreen != null)
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
    }

    private IEnumerator Start()
    {
        Debug.Log("[GC] ── GameController.Start BEGIN ──");

        // --- null checks ---
        if (PlayerPrefab == null)  Debug.LogError("[GC] PlayerPrefab is NULL — assign in Inspector");
        if (BoardPrefab == null)   Debug.LogError("[GC] BoardPrefab is NULL — assign in Inspector");
        if (cameraObject == null)  Debug.LogError("[GC] cameraObject is NULL — assign in Inspector");
        if (LevelManager.Instance == null) Debug.LogError("[GC] LevelManager.Instance is NULL");
        if (BiomeManager.Instance == null) Debug.LogError("[GC] BiomeManager.Instance is NULL — add BiomeManager to scene");

        startMovesPerForm = new Dictionary<Player.StateType, int>();
        foreach (MovePerFormEntry movesPerForm in LevelManager.Instance.CurrentLevel.StartMovesPerForm)
        {
            startMovesPerForm[movesPerForm.State] = movesPerForm.Moves;
        }
        Debug.Log($"[GC] StartMovesPerForm loaded: {startMovesPerForm.Count} entries");

#if UNITY_EDITOR
        if (enableInfiniteMoves)
        {
            foreach (Player.StateType state in Enum.GetValues(typeof(Player.StateType)))
            {
                startMovesPerForm[state] = 99;
            }
            Debug.Log("[GC] InfiniteMoves ON (editor only)");
        }
#endif

        // --- biome ---
        BiomeType biome = LevelManager.Instance.CurrentLevel.Biome;
        Debug.Log($"[GC] Loading biome: {biome}");
        yield return BiomeManager.Instance.LoadBiome(biome);
        Debug.Log($"[GC] Biome load done");

        // --- board ---
        Debug.Log($"[GC] Initializing board, map size: {LevelManager.Instance.CurrentLevel.MapSize}");
        BoardPrefab.Initialize(LevelManager.Instance.CurrentLevel);
        Debug.Log($"[GC] Board WorldCenter: {BoardPrefab.WorldCenter}  Size: {BoardPrefab.Size}");

        CellPrefab cellPrefab;
        (playerPiece, cellPrefab) = BoardPrefab.CreateNewPlayerPrefab();
        Debug.Log($"[GC] Player start cell position: {cellPrefab.transform.position}");

        PlayerPrefab.Initialize(playerPiece, cellPrefab, BoardPrefab);
        PlayerPrefab.OnPlayerWon.AddListener(OnWin);
        PlayerPrefab.OnPlayerDied.AddListener(ResetMap);
        PlayerPrefab.OnTransformation.AddListener(_ =>
            nudgeImages.ForEach(image => image.gameObject.SetActive(false)));
        PlayerPrefab.transform.localScale = Vector3.zero;

        // --- camera ---
        Vector3 camPos = BoardPrefab.WorldCenter + cameraOffset;
        cameraObject.transform.position = camPos;
        cameraObject.transform.LookAt(BoardPrefab.WorldCenter);
        cameraObject.orthographicSize = cameraOrthographicSize;
        Debug.Log($"[GC] Camera pos: {camPos}  rot: {cameraObject.transform.eulerAngles}  orthoSize: {cameraOrthographicSize}");
        Debug.Log($"[GC] Camera projection: {(cameraObject.orthographic ? "Orthographic" : "Perspective")}");

        // --- check PhysicsRaycaster ---
        var raycaster = cameraObject.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
        if (raycaster == null)
            Debug.LogWarning("[GC] PhysicsRaycaster NOT found on camera — cell clicks will not work!");
        else
            Debug.Log("[GC] PhysicsRaycaster found on camera ✓");

        yield return null;
        PlayerPrefab.SetTransformationLimits(startMovesPerForm);
        nudgeImages.ForEach(image => image.gameObject.SetActive(true));

        yield return new WaitUntil(() => BoardPrefab.IsSpawnAnimationComplete);
        PlayerPrefab.transform.DOScale(Vector3.one, 0.5f);
        Debug.Log("[GC] ── GameController.Start COMPLETE ──");
    }

    private void OnWin(int stars)
    {
        Debug.Log($"[GC] OnWin — stars: {stars}");
        wasLevelWon = true;
        LevelManager.Instance.SetCurrentLevelComplete(stars);
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Win);
    }
}
