using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class BoardPiecePrefab : MonoBehaviour
{
    public BoardPiece BoardPiece { get; private set; }
    public CellPrefab CurrentCell { get; private set; }

    [Tooltip("How high above cell surface the piece sits. Tune to match cube height.")]
    [SerializeField] private float heightOffset = 0.3f;

    private BoardPrefab boardPrefab;
    private Coroutine _boatCoroutine;

    public void Initialize(BoardPiece boardPieceData, CellPrefab startCell, BoardPrefab initBoardPrefab)
    {
        boardPrefab = initBoardPrefab;
        BoardPiece = boardPieceData;
        BoardPiece.OccupiedCell.OnChanged += (_, oldCell, newCell) =>
        {
            boardPrefab.GetCellPrefab(oldCell).ResetPulse();
            CurrentCell = boardPrefab.GetCellPrefab(newCell);
        };
        Teleport(startCell);
    }

    public List<CellPrefab> GetMoveOptionCellPrefabs()
    {
        List<(Cell, bool)> moveOptionCells = BoardPiece.GetMoveOptionCells();
        List<CellPrefab> moveOptionCellPrefabs = new();
        foreach (var (cell, isValidMove) in moveOptionCells)
        {
            CellPrefab cellPrefab = boardPrefab.GetCellPrefab(cell);
            cellPrefab.SetIsValidMoveOption(isValidMove);

            if (isValidMove)
                moveOptionCellPrefabs.Add(cellPrefab);
        }
        return moveOptionCellPrefabs;
    }

    public void CancelMove()
    {
        if (_boatCoroutine != null)
        {
            StopCoroutine(_boatCoroutine);
            _boatCoroutine = null;
        }
        transform.DOKill();
    }

    /// <summary>Анимация отказа: фигурка вздрагивает на месте.</summary>
    public void PlayFailShake(UnityAction onComplete = null)
    {
        transform.DOKill();
        if (CurrentCell != null)
            transform.position = CurrentCell.FinalWorldPosition + Vector3.up * heightOffset;
        transform
            .DOShakePosition(0.5f, 0.3f)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// Проиграть уже применённый к модели ход по клеткам пути (без стартовой).
    /// Один шаг — прыжок дугой; несколько — по клетке за раз (лодка, самолёт, скольжение по льду).
    /// Отложенные визуалы звёзд и обрушений клетки запускают сами, когда фигурка до них долетает.
    /// </summary>
    public void PlayMove(CellPrefab startCell, List<CellPrefab> path, UnityAction onComplete = null,
        float arcHeight = 1.5f, System.Action onLastHopStart = null)
    {
        transform.DOKill();
        if (startCell != null)
            transform.position = startCell.FinalWorldPosition + Vector3.up * heightOffset;
        if (_boatCoroutine != null)
        {
            StopCoroutine(_boatCoroutine);
            _boatCoroutine = null;
        }

        if (path == null || path.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (path.Count > 1)
        {
            _boatCoroutine = StartCoroutine(AnimateHopPath(path, onComplete, startCell, onLastHopStart));
            return;
        }

        CellPrefab targetCell = path[0];
        Vector3 targetPos = targetCell.FinalWorldPosition + Vector3.up * heightOffset;
        startCell?.TriggerDeferredCollapseVisual();
        startCell?.PlayStepDust();

        var arc = new[]
        {
            transform.position,
            (transform.position + targetPos) / 2 + Vector3.up * arcHeight,
            targetPos
        };

        transform
            .DOPath(arc, 0.5f, PathType.CatmullRom)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                targetCell.TriggerStarPickupVisual();
                targetCell.PlayStepDust();
                onComplete?.Invoke();
            });
    }

    private IEnumerator AnimateHopPath(List<CellPrefab> path, UnityAction onComplete, CellPrefab startCell,
        System.Action onLastHopStart = null)
    {
        const float hopDuration = HopDuration;
        bool isFirstHop = true;
        CellPrefab lastCell = path[path.Count - 1];
        foreach (var cell in path)
        {
            if (isFirstHop)
            {
                startCell?.TriggerDeferredCollapseVisual();
                startCell?.PlayStepDust();
                isFirstHop = false;
            }

            if (cell == lastCell)
                onLastHopStart?.Invoke();

            Vector3 from = transform.position;
            Vector3 to = cell.FinalWorldPosition + Vector3.up * heightOffset;
            Vector3 mid = (from + to) / 2f + Vector3.up * 0.6f;

            FMODAudioManager.Instance.PlayStepSound(cell.Cell.Terrain, cell.Cell.IsFragile);
            bool hopDone = false;
            transform
                .DOPath(new[] { from, mid, to }, hopDuration, PathType.CatmullRom)
                .SetEase(Ease.InOutQuad)
                .OnComplete(() => hopDone = true);

            yield return new WaitUntil(() => hopDone);

            if (cell.Cell.Terrain == TerrainType.Water)
                cell.ActivateSplash();
            if (cell != lastCell)
                cell.TriggerDeferredCollapseVisual();
            cell.TriggerStarPickupVisual();
            cell.PlayStepDust();
        }

        _boatCoroutine = null;
        onComplete?.Invoke();
    }

    public const float HopDuration = 0.44f;

    /// <summary>Плавно переместить фигурку на клетку без прыжка (плот везёт). Модель уже стоит на этой клетке.</summary>
    public Tween Drift(CellPrefab targetCell, float duration)
    {
        Vector3 targetPos = targetCell.FinalWorldPosition + Vector3.up * heightOffset;
        return transform.DOMove(targetPos, duration).SetEase(Ease.InOutSine);
    }

    public void Teleport(CellPrefab targetCell, bool tweenMovement = false, UnityAction onComplete = null)
    {
        bool success = BoardPiece.TeleportTo(targetCell.Cell);

        if (!success)
        {
            Debug.LogError($"[Piece] Teleport FAILED — targetCell {targetCell.name} is occupied");
            onComplete?.Invoke();
            return;
        }

        Vector3 targetPos = targetCell.FinalWorldPosition + Vector3.up * heightOffset;

        if (tweenMovement)
        {
            transform.DOKill();

            var path = new[]
            {
                transform.position,
                (transform.position + targetPos) / 2 + Vector3.up * 1.5f,
                targetPos
            };

            transform
                .DOPath(path, 0.5f, PathType.CatmullRom)
                .SetEase(Ease.InOutQuad)
                .OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            transform.position = targetPos;
            onComplete?.Invoke();
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
