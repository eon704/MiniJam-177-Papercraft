using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public bool IsFree => this.Piece == null;
    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }

    public enum TerrainType
    {
        Default,
        Start,
        End,
        Water,
        Stone,
        Fire
    }
    
    public TerrainType Terrain { get; private set; }
    
    public List<Cell> Neighbors { get; private set; }
    
    public Cell(Vector2Int position, TerrainType type)
    {
        this.Position = position;
        this.Terrain = type;
    }
    
    public void SetNeighbors(List<Cell> neighbors)
    {
        this.Neighbors = neighbors;
    }
    
    public void FreePiece()
    {
        this.Piece = null;
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