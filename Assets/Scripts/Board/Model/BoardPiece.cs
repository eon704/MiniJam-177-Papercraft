using UnityEngine;

public class BoardPiece
{
  public Observable<Vector2Int> Coordinate { get; private set; }
    
  public BoardPiece(Vector2Int coordinate)
  {
    this.Coordinate = new Observable<Vector2Int>(coordinate);
  }
}