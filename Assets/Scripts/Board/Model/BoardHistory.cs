using System.Collections.Generic;
using UnityEngine;

public class BoardHistory
{
    private readonly Stack<BoardRecord> _history = new();

    public int Count => _history.Count;

    public void AddRecord(Vector2Int playerPosition, Player.StateType playerState, List<Vector2Int> starsRemaining, Dictionary<Player.StateType, int> movesPerForm, List<Vector2Int> collapsedCells, BiomeCellSnapshot biomeSnapshot = default)
    {
        _history.Push(new BoardRecord(playerPosition, playerState, starsRemaining, movesPerForm, collapsedCells, biomeSnapshot));
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
    public readonly List<Vector2Int> CollapsedCells;
    public readonly BiomeCellSnapshot BiomeSnapshot;

    public BoardRecord(Vector2Int playerPosition, Player.StateType playerState, List<Vector2Int> starsRemaining, Dictionary<Player.StateType, int> movesPerForm, List<Vector2Int> collapsedCells, BiomeCellSnapshot biomeSnapshot = default)
    {
        PlayerPosition = playerPosition;
        PlayerState = playerState;
        StarsRemaining = starsRemaining;
        MovesPerForm = movesPerForm;
        CollapsedCells = collapsedCells;
        BiomeSnapshot = biomeSnapshot;
    }
}

public struct BiomeCellSnapshot
{
    public Dictionary<Vector2Int, TerrainType> DynamicTerrainState;
    public Dictionary<Vector2Int, int> VolcanoCountdowns;
    public Dictionary<Vector2Int, int> VolcanoLavaIndexes;
    public Dictionary<Vector2Int, int> IceCountdowns;
    public Dictionary<Vector2Int, int> IceFreezeIndexes;
}