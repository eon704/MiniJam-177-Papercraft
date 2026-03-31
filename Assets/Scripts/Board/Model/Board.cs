using System.Collections.Generic;
using UnityEngine;

public class Board
{
    public readonly Vector2Int Size;
    public readonly Cell[,] CellArray;
    public readonly Cell StartCell;
    public readonly List<Cell> StarCells = new();
    public readonly List<Cell> FragileCells = new();
    public readonly LevelData LevelData;

    public readonly BoardHistory BoardHistory = new();

    private int currentHintStep = 1;

    public Board(Vector2Int size, CellData[] map, LevelData levelData)
    {
        Size = size;
        CellArray = new Cell[size.x, size.y];
        LevelData = levelData;

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int index = y * size.x + x;
                CellData cellData = map[index];
                CellArray[x, y] = new Cell(new Vector2Int(x, y), cellData.Terrain, cellData.Item, cellData.IsFragile);

                if (cellData.Terrain == TerrainType.Start)
                    StartCell = CellArray[x, y];

                if (cellData.Item == CellItem.Star)
                    StarCells.Add(CellArray[x, y]);

                if (cellData.IsFragile)
                    FragileCells.Add(CellArray[x, y]);
            }
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Cell cell = CellArray[x, y];
                List<Cell> neighbours = new();

                if (x > 0) neighbours.Add(CellArray[x - 1, y]);
                if (x < size.x - 1) neighbours.Add(CellArray[x + 1, y]);
                if (y > 0) neighbours.Add(CellArray[x, y - 1]);
                if (y < size.y - 1) neighbours.Add(CellArray[x, y + 1]);
                if (x > 0 && y > 0) neighbours.Add(CellArray[x - 1, y - 1]);
                if (x < size.x - 1 && y > 0) neighbours.Add(CellArray[x + 1, y - 1]);
                if (x > 0 && y < size.y - 1) neighbours.Add(CellArray[x - 1, y + 1]);
                if (x < size.x - 1 && y < size.y - 1) neighbours.Add(CellArray[x + 1, y + 1]);

                cell.SetNeighbors(neighbours);
            }
        }
    }

    public Cell GetCell(Vector2Int coord)
    {
        int x = coord.x;
        int y = coord.y;

        if (x < 0 || x >= Size.x || y < 0 || y >= Size.y)
            return null;

        return CellArray[x, y];
    }

    public BoardPiece CreateNewPiece(Vector2Int coord)
    {
        return new BoardPiece(this, CellArray[coord.x, coord.y]);
    }

    public BoardPiece CreatePlayerPiece()
    {
        return new BoardPiece(this, StartCell);
    }

    public void ResetFragileCells()
    {
        foreach (Cell cell in FragileCells)
            cell.ResetCollapse();
    }

    public (Cell, int) RevealNextHint(out bool areAllHintsRevealed)
    {
        areAllHintsRevealed = false;

        if (currentHintStep >= LevelData.CachedSolution.Count)
            return (null, -1);

        SolutionStep nextStep = LevelData.CachedSolution[currentHintStep];
        Cell cellToReveal = GetCell(nextStep.Position);
        int revealDepth = cellToReveal.RevealHint();
        currentHintStep++;

        if (currentHintStep >= LevelData.CachedSolution.Count - 1)
            areAllHintsRevealed = true;

        return (cellToReveal, revealDepth);
    }

    public (Cell, int) RevealSpecificHint(int hintStepNumber, out bool areAllHintsRevealed)
    {
        areAllHintsRevealed = false;

        if (hintStepNumber < 1 || hintStepNumber >= LevelData.CachedSolution.Count - 1)
            return (null, -1);

        SolutionStep targetStep = LevelData.CachedSolution[hintStepNumber];
        Cell cellToReveal = GetCell(targetStep.Position);

        if (cellToReveal.IsHintRevealed.Value > 0)
            return (cellToReveal, -1);

        int revealDepth = cellToReveal.RevealHint();

        bool allRevealed = true;
        for (int i = 1; i < LevelData.CachedSolution.Count - 1; i++)
        {
            Cell checkCell = GetCell(LevelData.CachedSolution[i].Position);
            if (checkCell.IsHintRevealed.Value <= 0)
            {
                allRevealed = false;
                break;
            }
        }
        areAllHintsRevealed = allRevealed;

        return (cellToReveal, revealDepth);
    }

    public void ClearAllHints()
    {
        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
                CellArray[x, y].HideHint();
        }

        currentHintStep = 0;
    }

    public int CurrentHintStep => currentHintStep;
    public int TotalSolutionSteps => LevelData.CachedSolution?.Count ?? 0;
    public bool HasMoreHints => currentHintStep < TotalSolutionSteps;

    public bool HasUnrevealedHints()
    {
        for (int i = 1; i < LevelData.CachedSolution.Count - 1; i++)
        {
            Cell checkCell = GetCell(LevelData.CachedSolution[i].Position);
            if (checkCell.IsHintRevealed.Value <= 0)
                return true;
        }
        return false;
    }
}
