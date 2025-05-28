using System.Collections.Generic;
using UnityEngine;

public class BoardHistory
{
    private readonly Stack<BoardRecord> _history = new();

    public void AddRecord(Vector2Int playerPosition, Player.StateType playerState, List<Vector2Int> starsRemaining, Dictionary<Player.StateType, int> movesPerForm)
    {
        _history.Push(new BoardRecord(playerPosition, playerState, starsRemaining, movesPerForm));
    }
    
    public BoardRecord? Undo()
    {
        if (_history.Count == 1)
        {
            return null;
        }
        
        if (_history.Count > 1)
        {
            _history.Pop();
        }
        
        return _history.Peek();
    }

    public void Reset()
    {
        while (_history.Count > 1)
        {
            _history.Pop();
        }
    }
}

public struct BoardRecord
{
    public readonly Vector2Int PlayerPosition;
    public readonly Player.StateType PlayerState;
    public readonly List<Vector2Int> StarsRemaining;
    public readonly Dictionary<Player.StateType, int> MovesPerForm;

    public BoardRecord(Vector2Int playerPosition, Player.StateType playerState, List<Vector2Int> starsRemaining, Dictionary<Player.StateType, int> movesPerForm)
    {
        PlayerPosition = playerPosition;
        PlayerState = playerState;
        StarsRemaining = starsRemaining;
        MovesPerForm = movesPerForm;
    }
}