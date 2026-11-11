using System;
using System.Collections.Generic;
using Paperbound.Core;

/// <summary>
/// Фигурка на поле. Правила хода здесь не живут: варианты и пути берутся из Paperbound.Core.Rules
/// по текущему состоянию Board.State, а модель клеток синхронизирует Player после Rules.Apply.
/// </summary>
public class BoardPiece
{
    public readonly Observable<Cell> OccupiedCell;
    private readonly Board board;

    private Form form = Form.Default;

    public Action OnCollectedStar;

    public BoardPiece(Board board, Cell startCell)
    {
        this.board = board;
        OccupiedCell = new Observable<Cell>(startCell);
    }

    public Form Form => form;

    public bool TeleportTo(Cell targetCell)
    {
        if (!targetCell.IsFree)
            return false;

        OccupiedCell.Value?.FreePiece();
        OccupiedCell.Value = targetCell;
        OccupiedCell.Value.AssignPieceSilently(this);
        return true;
    }

    public void SetState(IState state)
    {
        form = state.StateType.ToForm();
    }

    /// <summary>Варианты хода текущей формой: клетка и допустима ли она (недопустимые подсвечиваются красным).</summary>
    public List<(Cell, bool)> GetMoveOptionCells()
    {
        var result = new List<(Cell, bool)>();
        foreach (MoveOption option in Rules.GetOptions(board.Def, board.State, form))
        {
            Cell cell = board.GetCell(option.Target.ToVector());
            if (cell != null)
                result.Add((cell, option.Legal));
        }
        return result;
    }

    /// <summary>Ход, который имеет в виду клик по клетке (вода для лодки, первый лёд для журавля). null — хода нет.</summary>
    public MoveOption ResolveClick(Cell clicked)
    {
        return Rules.ResolveClick(board.Def, board.State, form, clicked.Position.ToPos());
    }

    /// <summary>Клетки пути варианта хода (без стартовой).</summary>
    public List<Cell> GetPathCells(MoveOption option)
    {
        var cells = new List<Cell>(option.Path.Count);
        foreach (Pos p in option.Path)
        {
            Cell c = board.GetCell(p.ToVector());
            if (c != null) cells.Add(c);
        }
        return cells;
    }
}
