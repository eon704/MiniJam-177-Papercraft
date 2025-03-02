using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelData
{
    [Header("Map Data")]
    public Vector2Int MapSize;
    [TextArea(3, 10)]
    public string Map;

    [Header("Player data")] 
    public List<MovePerFormEntry> StartMovesPerForm;

    public static LevelData DefaultLevel => new()
    {
        MapSize = new Vector2Int(5, 5),
        Map = "ooooo\nooooo\nSoooE\nooooo\nooooo\n",
        StartMovesPerForm = new List<MovePerFormEntry>()
        {
            new() {State = Player.StateType.Default, Moves = -1 },
            new() {State = Player.StateType.Crane, Moves = -1 },
            new() {State = Player.StateType.Plane, Moves = -1 },
            new() {State = Player.StateType.Boat, Moves = -1 },
            new() {State = Player.StateType.Frog, Moves = -1 }
        }
    };
}

[Serializable]
public struct MovePerFormEntry
{
    public Player.StateType State;
    public int Moves;
}