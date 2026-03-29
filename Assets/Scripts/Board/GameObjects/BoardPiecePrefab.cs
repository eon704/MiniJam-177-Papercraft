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
        transform.DOKill();
    }

    public bool Move(CellPrefab targetCell, UnityAction onComplete = null, bool forceFailMovement = false)
    {
        transform.DOKill();
        bool success = !forceFailMovement && BoardPiece.MoveTo(targetCell.Cell);

        Vector3 targetPos = targetCell.transform.position + Vector3.up * heightOffset;

        if (success)
        {
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
            transform
                .DOShakePosition(0.5f, 0.3f)
                .OnComplete(() => onComplete?.Invoke());
        }

        return success;
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
