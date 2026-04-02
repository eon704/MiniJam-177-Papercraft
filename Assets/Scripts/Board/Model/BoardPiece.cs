using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardPiece
{
    public readonly Observable<Cell> OccupiedCell;
    private readonly Board board;

    private List<Vector2Int> moveOptions = new();
    private List<TerrainType> moveTerrain = new();
    private MoveMode moveMode = MoveMode.Normal;

    public Action OnCollectedStar;

    public BoardPiece(Board board, Cell startCell)
    {
        this.board = board;
        OccupiedCell = new Observable<Cell>(startCell);
    }

    public bool TeleportTo(Cell targetCell)
    {
        if (!targetCell.IsFree)
            return false;

        OccupiedCell.Value?.FreePiece();
        OccupiedCell.Value = targetCell;
        OccupiedCell.Value.AssignPiece(this);
        return true;
    }

    public bool MoveTo(Cell targetCell)
    {
        if (!CanMoveTo(targetCell))
            return false;

        Cell previousCell = OccupiedCell.Value;
        previousCell.FreePiece();
        OccupiedCell.Value = targetCell;
        OccupiedCell.Value.AssignPiece(this);

        if (previousCell.IsFragile)
            previousCell.Collapse();

        if (moveMode == MoveMode.PlaneSlide)
        {
            foreach (Cell cell in GetPlaneIntermediateCells(previousCell, targetCell))
                cell.Touch(this);
        }

        return true;
    }

    private List<Cell> GetPlaneIntermediateCells(Cell from, Cell to)
    {
        Vector2Int motion = to.Position - from.Position;
        Vector2Int dir = new Vector2Int(motion.x > 0 ? 1 : -1, motion.y > 0 ? 1 : -1);
        var cells = new List<Cell>();
        Vector2Int pos = from.Position + dir;
        while (pos != to.Position)
        {
            Cell c = board.GetCell(pos);
            if (c != null) cells.Add(c);
            pos += dir;
        }
        return cells;
    }

    public List<(Cell, bool)> GetMoveOptionCells()
    {
        Vector2Int currentPosition = OccupiedCell.Value.Position;
        List<(Cell, bool)> result = new();

        switch (moveMode)
        {
            case MoveMode.FrogJump:
                GetFrogMoveOptions(currentPosition, result);
                break;
            case MoveMode.PlaneSlide:
                GetPlaneMoveOptions(currentPosition, result);
                break;
            case MoveMode.BoatSlide:
                GetBoatMoveOptions(currentPosition, result);
                break;
            default:
                GetNormalMoveOptions(currentPosition, result);
                break;
        }

        return result;
    }

    public void SetState(IState state)
    {
        moveOptions = state.MoveOptions;
        moveTerrain = state.MoveTerrain;
        moveMode = state.MoveMode;
    }

    // ─── Normal ──────────────────────────────────────────────────────────────

    private void GetNormalMoveOptions(Vector2Int from, List<(Cell, bool)> result)
    {
        foreach (Vector2Int motion in moveOptions)
        {
            Cell target = board.GetCell(from + motion);
            if (target == null || target.Terrain == TerrainType.Empty || target.IsCollapsed) continue;
            result.Add((target, moveTerrain.Contains(target.Terrain)));
        }
    }

    private bool CanNormalMoveTo(Cell target)
    {
        if (!moveTerrain.Contains(target.Terrain)) return false;
        Vector2Int motion = target.Position - OccupiedCell.Value.Position;
        return moveOptions.Contains(motion);
    }

    // ─── Frog: прыжок ровно на 2 клетки, промежуточная не проверяется ────────

    private void GetFrogMoveOptions(Vector2Int from, List<(Cell, bool)> result)
    {
        foreach (Vector2Int motion in moveOptions)
        {
            Cell target = board.GetCell(from + motion);
            if (target == null || target.Terrain == TerrainType.Empty || target.IsCollapsed) continue;
            result.Add((target, moveTerrain.Contains(target.Terrain)));
        }
    }

    private bool CanFrogMoveTo(Cell target)
    {
        Vector2Int motion = target.Position - OccupiedCell.Value.Position;
        if (!moveOptions.Contains(motion)) return false;
        return moveTerrain.Contains(target.Terrain);
    }

    // ─── Plane: скольжение по диагонали, блокируется не-MoveTerrain ──────────

    private void GetPlaneMoveOptions(Vector2Int from, List<(Cell, bool)> result)
    {
        foreach (Vector2Int dir in moveOptions)
        {
            Vector2Int pos = from + dir;
            while (true)
            {
                Cell cell = board.GetCell(pos);
                if (cell == null || cell.Terrain == TerrainType.Empty || cell.IsCollapsed) break;
                if (!moveTerrain.Contains(cell.Terrain)) break;

                result.Add((cell, true));
                pos += dir;
            }
        }
    }

    private bool CanPlaneMoveTo(Cell target)
    {
        Vector2Int motion = target.Position - OccupiedCell.Value.Position;
        if (motion.x == 0 || motion.y == 0) return false;
        if (Mathf.Abs(motion.x) != Mathf.Abs(motion.y)) return false;

        Vector2Int dir = new Vector2Int(motion.x > 0 ? 1 : -1, motion.y > 0 ? 1 : -1);
        if (!moveOptions.Contains(dir)) return false;

        Vector2Int pos = OccupiedCell.Value.Position + dir;
        while (pos != target.Position)
        {
            Cell cell = board.GetCell(pos);
            if (cell == null || !moveTerrain.Contains(cell.Terrain)) return false;
            pos += dir;
        }

        return moveTerrain.Contains(target.Terrain);
    }

    // ─── Boat: скользит через воду, приземляется на первую сушу ──────────────

    private void GetBoatMoveOptions(Vector2Int from, List<(Cell, bool)> result)
    {
        foreach (Vector2Int dir in moveOptions)
        {
            Cell firstCell = board.GetCell(from + dir);
            if (firstCell == null || firstCell.Terrain != TerrainType.Water) continue;

            Vector2Int pos = from + dir;
            while (true)
            {
                Cell cell = board.GetCell(pos);
                if (cell == null || cell.Terrain != TerrainType.Water)
                {
                    if (cell != null && cell.Terrain != TerrainType.Empty && !cell.IsCollapsed && moveTerrain.Contains(cell.Terrain))
                        result.Add((cell, true));
                    break;
                }
                pos += dir;
            }
        }
    }

    private bool CanBoatMoveTo(Cell target)
    {
        Vector2Int motion = target.Position - OccupiedCell.Value.Position;
        if (motion.x != 0 && motion.y != 0) return false;

        Vector2Int dir = new Vector2Int(Math.Sign(motion.x), Math.Sign(motion.y));
        if (!moveOptions.Contains(dir)) return false;

        Cell firstCell = board.GetCell(OccupiedCell.Value.Position + dir);
        if (firstCell == null || firstCell.Terrain != TerrainType.Water) return false;

        Vector2Int pos = OccupiedCell.Value.Position + dir;
        while (true)
        {
            Cell cell = board.GetCell(pos);
            if (cell == null || cell.Terrain != TerrainType.Water)
                return cell != null && cell == target && moveTerrain.Contains(cell.Terrain);
            pos += dir;
        }
    }

    /// <summary>
    /// Если нажата водяная клетка в направлении, допустимом для лодки — возвращает
    /// клетку приземления (первую не-водяную клетку за водой). Иначе null.
    /// </summary>
    public Cell FindBoatLandingCell(Cell waterCell)
    {
        Vector2Int motion = waterCell.Position - OccupiedCell.Value.Position;
        if (motion == Vector2Int.zero || (motion.x != 0 && motion.y != 0)) return null;

        Vector2Int dir = new Vector2Int(Math.Sign(motion.x), Math.Sign(motion.y));
        if (!moveOptions.Contains(dir)) return null;

        Cell firstCell = board.GetCell(OccupiedCell.Value.Position + dir);
        if (firstCell == null || firstCell.Terrain != TerrainType.Water) return null;

        Vector2Int pos = OccupiedCell.Value.Position + dir;
        while (true)
        {
            Cell cell = board.GetCell(pos);
            if (cell == null || cell.Terrain != TerrainType.Water)
            {
                if (cell != null && cell.Terrain != TerrainType.Empty && !cell.IsCollapsed && moveTerrain.Contains(cell.Terrain))
                    return cell;
                return null;
            }
            pos += dir;
        }
    }

    /// <summary>
    /// Возвращает все клетки пути лодки от текущей позиции до target (через воду),
    /// включая промежуточные водяные клетки и саму целевую клетку.
    /// </summary>
    public List<Cell> GetBoatPathCells(Cell target)
    {
        Vector2Int motion = target.Position - OccupiedCell.Value.Position;
        Vector2Int dir = new Vector2Int(Math.Sign(motion.x), Math.Sign(motion.y));
        var path = new List<Cell>();
        Vector2Int pos = OccupiedCell.Value.Position + dir;
        while (pos != target.Position)
        {
            Cell c = board.GetCell(pos);
            if (c != null) path.Add(c);
            pos += dir;
        }
        path.Add(target);
        return path;
    }

    /// <summary>
    /// Возвращает все клетки пути самолёта от текущей позиции до target (диагональ),
    /// включая промежуточные клетки и саму целевую клетку.
    /// </summary>
    public List<Cell> GetPlanePathCells(Cell target)
    {
        Vector2Int motion = target.Position - OccupiedCell.Value.Position;
        Vector2Int dir = new Vector2Int(motion.x > 0 ? 1 : -1, motion.y > 0 ? 1 : -1);
        var path = new List<Cell>();
        Vector2Int pos = OccupiedCell.Value.Position + dir;
        while (pos != target.Position)
        {
            Cell c = board.GetCell(pos);
            if (c != null) path.Add(c);
            pos += dir;
        }
        path.Add(target);
        return path;
    }

    // ─── Dispatch ─────────────────────────────────────────────────────────────

    private bool CanMoveTo(Cell target)
    {
        if (!target.IsFree) return false;
        if (target.IsCollapsed) return false;

        return moveMode switch
        {
            MoveMode.FrogJump   => CanFrogMoveTo(target),
            MoveMode.PlaneSlide => CanPlaneMoveTo(target),
            MoveMode.BoatSlide  => CanBoatMoveTo(target),
            _                   => CanNormalMoveTo(target)
        };
    }
}
