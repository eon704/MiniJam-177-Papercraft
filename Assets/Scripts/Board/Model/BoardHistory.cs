using System.Collections.Generic;
using UnityEngine;

public class BoardHistory
{
    private readonly Stack<BoardRecord> _history = new();

    public void AddRecord(Vector2Int playerPosition, Player.StateType playerState, List<Vector2Int> starsRemaining)
    {
        _history.Push(new BoardRecord(playerPosition, playerState, starsRemaining));
    }
    
    public BoardRecord? Undo()
    {
        if (_history.Count <= 1)
        {
            return null;
        }

        _history.Pop();
        return _history.Peek();
    }
}

public struct BoardRecord
{
    public Vector2Int PlayerPosition;
    public Player.StateType PlayerState;
    public List<Vector2Int> StarsRemaining;

    public BoardRecord(Vector2Int playerPosition, Player.StateType playerState, List<Vector2Int> starsRemaining)
    {
        PlayerPosition = playerPosition;
        PlayerState = playerState;
        StarsRemaining = starsRemaining;
    }
}