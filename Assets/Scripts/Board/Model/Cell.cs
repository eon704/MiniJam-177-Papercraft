using System;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public class Cell
{
    public bool IsFree => Piece == null;
    public bool IsFragile { get; private set; }
    public bool IsCollapsed { get; private set; }

    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }

    public TerrainType Terrain { get; private set; }
    public TerrainType OriginalTerrain { get; private set; }
    public Observable<CellItem> Item { get; private set; }
    public Observable<int> IsHintRevealed { get; private set; }

    public List<Cell> Neighbors { get; private set; }

    public event Action OnCollapsed;
    public event Action OnCollapsedInstant;
    public event Action OnCollapseReset;
    public event Action<TerrainType, TerrainType> OnTerrainChanged;

    public Cell(Vector2Int position, TerrainType type, CellItem item, bool isFragile = false)
    {
        Position = position;
        Terrain = type;
        OriginalTerrain = type;
        Item = new Observable<CellItem>(item);
        IsHintRevealed = new Observable<int>(-1);
        IsFragile = isFragile;
    }

    public void SetTerrain(TerrainType newTerrain)
    {
        var old = Terrain;
        Terrain = newTerrain;
        OnTerrainChanged?.Invoke(old, newTerrain);
    }

    public void SetNeighbors(List<Cell> neighbors)
    {
        Neighbors = neighbors;
    }

    public void FreePiece()
    {
        Piece = null;
    }

    public void ReassignStar()
    {
        Item.Value = CellItem.Star;
    }

    public void CollectStar()
    {
        Item.Value = CellItem.None;
        if (!FMODEvents.Instance.ding.IsNull) RuntimeManager.PlayOneShot(FMODEvents.Instance.ding);
        Piece?.OnCollectedStar?.Invoke();
    }

    public void AssignPiece(BoardPiece piece)
    {
        Piece = piece;

        if (Item.Value == CellItem.Star)
            CollectStar();
    }

    public void Collapse()
    {
        if (IsCollapsed) return;
        IsCollapsed = true;
        OnCollapsed?.Invoke();
    }

    public void CollapseInstant()
    {
        if (IsCollapsed) return;
        IsCollapsed = true;
        OnCollapsedInstant?.Invoke();
    }

    public void ResetCollapse()
    {
        if (!IsCollapsed) return;
        IsCollapsed = false;
        OnCollapseReset?.Invoke();
    }

    public int RevealHint()
    {
        int newRevealDepth = IsHintRevealed.Value + 1;
        IsHintRevealed.Value = newRevealDepth;
        return newRevealDepth;
    }

    public void HideHint()
    {
        IsHintRevealed.Value = 0;
    }

    public static int Distance(Cell cell1, Cell cell2)
    {
        return Mathf.Abs(cell1.Position.x - cell2.Position.x) + Mathf.Abs(cell1.Position.y - cell2.Position.y);
    }
}
