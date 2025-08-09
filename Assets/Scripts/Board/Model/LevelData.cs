using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Level", menuName = "Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Map Data")]
    public Vector2Int MapSize;
    [TextArea(3, 10)]
    public string Map;

    [Header("Player data")]
    public List<MovePerFormEntry> StartMovesPerForm;

    public static LevelData DefaultLevel 
    {
        get
        {
            LevelData defaultLevel = CreateInstance<LevelData>();
            defaultLevel.MapSize = new Vector2Int(5, 1);
            defaultLevel.Map = "SoGoE";
            defaultLevel.StartMovesPerForm = new List<MovePerFormEntry>()
            {
                new() {State = Player.StateType.Default, Moves = -1 },
                new() {State = Player.StateType.Crane, Moves = -1 },
                new() {State = Player.StateType.Plane, Moves = -1 },
                new() {State = Player.StateType.Boat, Moves = -1 },
                new() {State = Player.StateType.Frog, Moves = -1 }
            };
            return defaultLevel;
        }
    }
}

[Serializable]
public struct MovePerFormEntry
{
    public Player.StateType State;
    public int Moves;
}