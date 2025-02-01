using UnityEngine;

public class Cell
{
    public BoardPiece Piece { get; private set; }
    public Vector2Int Position { get; private set; }
    // terrain: Water, Hills, etc...
    // modifier: Fire, ...
    
    public Cell(Vector2Int position)
    {
        this.Position = position;
    }
}