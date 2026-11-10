using System.Collections.Generic;
using System.Linq;
using Paperbound.Core;
using UnityEngine;

/// <summary>
/// Направления и проходимые клетки форм для UI и генератора уровней.
/// Источник — таблица Paperbound.Core.Forms; сами правила хода живут в Paperbound.Core.Rules.
/// </summary>
public static class StateModelInfo
{
    public static readonly Dictionary<Player.StateType, StateModel> StateModels = Build();

    private static Dictionary<Player.StateType, StateModel> Build()
    {
        var models = new Dictionary<Player.StateType, StateModel>();
        for (int i = 0; i < Forms.Count; i++)
        {
            FormDef def = Forms.Get((Form)i);
            var type = (Player.StateType)i;
            models[type] = new StateModel(
                type,
                def.Directions.Select(d => new Vector2Int(d.X, d.Y)).ToList(),
                def.WalkableTerrain.ToList(),
                ToMoveMode(def.Kind));
        }
        return models;
    }

    private static MoveMode ToMoveMode(MoveKind kind) => kind switch
    {
        MoveKind.Jump => MoveMode.FrogJump,
        MoveKind.Slide => MoveMode.PlaneSlide,
        MoveKind.Sail => MoveMode.BoatSlide,
        _ => MoveMode.Normal,
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
