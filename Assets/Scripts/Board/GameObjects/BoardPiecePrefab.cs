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

    public bool Move(CellPrefab targetCell, UnityAction onComplete = null, bool forceFailMovement = false,
        List<CellPrefab> boatPath = null, float arcHeight = 1.5f, List<CellPrefab> planePath = null,
        System.Action onStarCollected = null, System.Action<CellPrefab> onFireCell = null,
        System.Action onLastHopStart = null)
    {
        transform.DOKill();
        // Snap back to current cell in case a previous fail-shake was interrupted mid-animation
        if (CurrentCell != null)
            transform.position = CurrentCell.transform.position + Vector3.up * heightOffset;
        if (_boatCoroutine != null)
        {
            StopCoroutine(_boatCoroutine);
            _boatCoroutine = null;
        }

        CellPrefab startCell = CurrentCell;

        // Defer star visuals BEFORE model update consumes them
        if (targetCell.Cell.Item.Value == CellItem.Star) targetCell.SetStarVisualDeferred(onStarCollected);
        if (boatPath != null)
            foreach (var c in boatPath)
                if (c.Cell.Item.Value == CellItem.Star) c.SetStarVisualDeferred(onStarCollected);
        if (planePath != null)
            foreach (var c in planePath)
                if (c.Cell.Item.Value == CellItem.Star) c.SetStarVisualDeferred(onStarCollected);

        // Defer collapse visuals for multi-hop moves so they sync with the animation
        if (boatPath != null && boatPath.Count > 0)
        {
            if (startCell != null && startCell.Cell.IsFragile && !startCell.Cell.IsCollapsed)
                startCell.DeferCollapseVisual();
        }
        if (planePath != null && planePath.Count > 0)
        {
            if (startCell != null && startCell.Cell.IsFragile && !startCell.Cell.IsCollapsed)
                startCell.DeferCollapseVisual();
            for (int i = 0; i < planePath.Count - 1; i++)
                if (planePath[i].Cell.IsFragile && !planePath[i].Cell.IsCollapsed)
                    planePath[i].DeferCollapseVisual();
        }

        bool success = !forceFailMovement && BoardPiece.MoveTo(targetCell.Cell);

        if (!success)
        {
            targetCell.ResetStarVisualDeferred();
            if (boatPath != null)
                foreach (var c in boatPath) c.ResetStarVisualDeferred();
            if (planePath != null)
                foreach (var c in planePath) c.ResetStarVisualDeferred();
        }

        Vector3 targetPos = targetCell.transform.position + Vector3.up * heightOffset;

        if (success)
        {
            if (boatPath != null && boatPath.Count > 0)
            {
                _boatCoroutine = StartCoroutine(AnimateBoatPath(boatPath, onComplete, startCell, onLastHopStart));
            }
            else if (planePath != null && planePath.Count > 0)
            {
                _boatCoroutine = StartCoroutine(AnimatePlanePath(planePath, onComplete, startCell, onFireCell, onLastHopStart));
            }
            else
            {
                startCell?.PlayStepDust();

                var path = new[]
                {
                    transform.position,
                    (transform.position + targetPos) / 2 + Vector3.up * arcHeight,
                    targetPos
                };

                transform
                    .DOPath(path, 0.5f, PathType.CatmullRom)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() =>
                    {
                        targetCell.TriggerStarPickupVisual();
                        targetCell.PlayStepDust();
                        onComplete?.Invoke();
                    });
            }
        }
        else
        {
            transform
                .DOShakePosition(0.5f, 0.3f)
                .OnComplete(() => onComplete?.Invoke());
        }

        return success;
    }

    private IEnumerator AnimateBoatPath(List<CellPrefab> path, UnityAction onComplete, CellPrefab startCell,
        System.Action onLastHopStart = null)
    {
        const float hopDuration = 0.44f;
        bool isFirstHop = true;
        CellPrefab lastCell = path[path.Count - 1];
        foreach (var cell in path)
        {
            // Start cell: collapse and dust as the player jumps off it
            if (isFirstHop)
            {
                startCell?.TriggerDeferredCollapseVisual();
                startCell?.PlayStepDust();
                isFirstHop = false;
            }

            if (cell == lastCell && path.Count > 1)
                onLastHopStart?.Invoke();

            Vector3 from = transform.position;
            Vector3 to = cell.transform.position + Vector3.up * heightOffset;
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
            cell.TriggerStarPickupVisual();
            cell.PlayStepDust();
        }

        _boatCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator AnimatePlanePath(List<CellPrefab> path, UnityAction onComplete, CellPrefab startCell,
        System.Action<CellPrefab> onFireCell = null, System.Action onLastHopStart = null)
    {
        const float hopDuration = 0.44f;
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

            if (cell == lastCell && path.Count > 1)
                onLastHopStart?.Invoke();

            Vector3 from = transform.position;
            Vector3 to = cell.transform.position + Vector3.up * heightOffset;
            Vector3 mid = (from + to) / 2f + Vector3.up * 0.6f;

            FMODAudioManager.Instance.PlayStepSound(cell.Cell.Terrain, cell.Cell.IsFragile);
            bool hopDone = false;
            transform
                .DOPath(new[] { from, mid, to }, hopDuration, PathType.CatmullRom)
                .SetEase(Ease.InOutQuad)
                .OnComplete(() => hopDone = true);

            yield return new WaitUntil(() => hopDone);

            if (cell != lastCell)
                cell.TriggerDeferredCollapseVisual();
            cell.TriggerStarPickupVisual();
            cell.PlayStepDust();

            // Промежуточная клетка с огнём/лавой — убиваем игрока и прерываем путь
            if (cell != lastCell &&
                (cell.Cell.Terrain == TerrainType.Fire || cell.Cell.Terrain == TerrainType.Lava))
            {
                _boatCoroutine = null;
                onFireCell?.Invoke(cell);
                yield break;
            }
        }

        _boatCoroutine = null;
        onComplete?.Invoke();
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

        Vector3 targetPos = targetCell.transform.position + Vector3.up * heightOffset;

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
