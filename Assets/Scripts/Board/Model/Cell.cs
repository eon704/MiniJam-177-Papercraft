using UnityEngine;

public class Cell
{
    public bool IsFree => this.Piece == null;
    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }
    
    // terrain: Water, Hills, etc...
    // modifier: Fire, ...
    
    public Cell(Vector2Int position)
    {
        this.Position = position;
    }
    
    public void AssignPiece(BoardPiece piece)
    {
        this.Piece = piece;
    }
}