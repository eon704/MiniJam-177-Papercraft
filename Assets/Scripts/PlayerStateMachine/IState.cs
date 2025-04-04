﻿ 
using System.Collections.Generic;
using UnityEngine;

public interface IState
{
    public Player.StateType StateType { get; }
    public List<Vector2Int> MoveOptions { get; }
    public List<Cell.TerrainType> MoveTerrain { get; }
    
    void Tick();
    void OnEnter();
    void OnExit();
}