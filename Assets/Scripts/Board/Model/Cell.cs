using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public bool IsFree => this.Piece == null;
    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }

    public enum TerrainType
    {
        None,
        Default,
        Start,
        End,
        Water,
        Stone,
        Fire
    }
    
    public enum CellItem
    {
        None,
        Star,
        RandomPowerup
    }
    
    public TerrainType Terrain { get; private set; }
    public Observable<CellItem> Item { get; private set; }
    
    public List<Cell> Neighbors { get; private set; }
    
    public Cell(Vector2Int position, TerrainType type, CellItem item)
    {
        this.Position = position;
        this.Terrain = type;
        this.Item = new Observable<CellItem>(item);
    }
    
    public void SetNeighbors(List<Cell> neighbors)
    {
        this.Neighbors = neighbors;
    }
    
    public void FreePiece()
    {
        this.Piece = null;
    }

    public void ReassignStar()
    {
        this.Item.Value = CellItem.Star;
    }

    public void CollectStar()
    {
        if (this.Item.Value == CellItem.Star)
        {
            this.Item.Value = CellItem.None;
        }
    }
    
    public void AssignPiece(BoardPiece piece)
    {
        this.Piece = piece;
    }

    public static int Distance(Cell cell1, Cell cell2)
    {
        return Mathf.Abs(cell1.Position.x - cell2.Position.x) + Mathf.Abs(cell1.Position.y - cell2.Position.y);
    }
}