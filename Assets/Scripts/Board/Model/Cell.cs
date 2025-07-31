using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public bool IsFree => Piece == null;
    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }
    
    public TerrainType Terrain { get; private set; }
    public Observable<CellItem> Item { get; private set; }
    public Observable<int> IsHintRevealed { get; private set; }
    
    public List<Cell> Neighbors { get; private set; }

    public int HintOrder1 { get; private set; } = -1;
    public int HintOrder2 { get; private set; } = -1;
    public int HintOrder3 { get; private set; } = -1;
    
    public Cell(Vector2Int position, TerrainType type, CellItem item)
    {
        Position = position;
        Terrain = type;
        Item = new Observable<CellItem>(item);
        IsHintRevealed = new Observable<int>(0);
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
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Ding,1f);
        Piece?.OnCollectedStar?.Invoke();
    }
    
    public void AssignPiece(BoardPiece piece)
    {
        Piece = piece;

        if (Item.Value == CellItem.Star)
        {
            CollectStar();
        }
    }

    public void RevealHint(int order)
    {
        int newHintOrder = IsHintRevealed.Value + 1;

        switch (IsHintRevealed)
        {
            case 1:
                HintOrder1 = order;
                break;
            case 2:
                HintOrder2 = order;
                break;
            case 3:
                HintOrder3 = order;
                break;
        }

        IsHintRevealed.Value = newHintOrder;
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