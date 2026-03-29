 
using System.Collections.Generic;
using UnityEngine;

public interface IState
{
    public Player.StateType StateType { get; }
    public List<Vector2Int> MoveOptions { get; }
    public List<TerrainType> MoveTerrain { get; }
    public MoveMode MoveMode { get; }

    void Tick();
    void OnEnter();
    void OnExit();
}