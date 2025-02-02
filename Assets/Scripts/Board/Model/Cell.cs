using UnityEngine;

public class Cell
{
    public bool IsFree => this.Piece == null;
    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }

    public enum TerrainType
    {
        Default,
        Water,
        Stone,
        Fire
    }
    
    public TerrainType Terrain { get; private set; }
    
    public Cell(Vector2Int position, TerrainType type)
    {
        this.Position = position;
        this.Terrain = type;
    }
    
    public void FreePiece()
    {
        this.Piece = null;
    }
    
    public void AssignPiece(BoardPiece piece)
    {
        this.Piece = piece;
    }
}