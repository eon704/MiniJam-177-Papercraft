using System.Collections.Generic;
using UnityEngine;

public static class StateModelInfo
{
    public static Dictionary<Player.StateType, StateModel> StateModels = new()
    {
        {
            Player.StateType.Default,
            new StateModel(
                Player.StateType.Default,
                new List<Vector2Int>(),
                new List<TerrainType>(),
                MoveMode.Normal
            )
        },
        {
            Player.StateType.Crane,
            new StateModel(
                Player.StateType.Crane,
                new List<Vector2Int>
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(0, -1),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 0)
                },
                new List<TerrainType>
                {
                    TerrainType.Default,
                    TerrainType.Stone,
                    TerrainType.Fire,
                    TerrainType.Lava,
                    TerrainType.Ice,
                    TerrainType.Start,
                    TerrainType.End
                },
                MoveMode.Normal
            )
        },
        {
            Player.StateType.Frog,
            new StateModel(
                Player.StateType.Frog,
                new List<Vector2Int>
                {
                    new Vector2Int(0, 2),
                    new Vector2Int(0, -2),
                    new Vector2Int(2, 0),
                    new Vector2Int(-2, 0)
                },
                new List<TerrainType>
                {
                    TerrainType.Default,
                    TerrainType.Fire,
                    TerrainType.Lava,
                    TerrainType.Ice,
                    TerrainType.Start,
                    TerrainType.End
                },
                MoveMode.FrogJump
            )
        },
        {
            Player.StateType.Plane,
            new StateModel(
                Player.StateType.Plane,
                new List<Vector2Int>
                {
                    new Vector2Int(-1, 1),
                    new Vector2Int(-1, -1),
                    new Vector2Int(1, 1),
                    new Vector2Int(1, -1)
                },
                new List<TerrainType>
                {
                    TerrainType.Default,
                    TerrainType.Fire,
                    TerrainType.Lava,
                    TerrainType.Start,
                    TerrainType.End
                },
                MoveMode.PlaneSlide
            )
        },
        {
            Player.StateType.Boat,
            new StateModel(
                Player.StateType.Boat,
                new List<Vector2Int>
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, -1),
                    new Vector2Int(0, 1)
                },
                new List<TerrainType>
                {
                    TerrainType.Default,
                    TerrainType.Start,
                    TerrainType.End,
                    TerrainType.Fire,
                    TerrainType.Lava
                },
                MoveMode.BoatSlide
            )
        }
    };
}

public struct StateModel
{
    public Player.StateType StateType { get; private set; }

    public List<Vector2Int> MoveOptions { get; private set; }

    public List<TerrainType> MoveTerrain { get; private set; }

    public MoveMode MoveMode { get; private set; }

    public StateModel(Player.StateType stateType, List<Vector2Int> moveOptions, List<TerrainType> moveTerrain, MoveMode moveMode)
    {
        StateType = stateType;
        MoveOptions = moveOptions;
        MoveTerrain = moveTerrain;
        MoveMode = moveMode;
    }
}