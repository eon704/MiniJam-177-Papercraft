using System.Collections.Generic;
using Paperbound.Core;
using UnityEngine;

/// <summary>Мост между Unity-типами (Vector2Int, Player.StateType, LevelData) и чистым ядром правил.</summary>
public static class CoreBridge
{
    public static Pos ToPos(this Vector2Int v) => new(v.x, v.y);
    public static Vector2Int ToVector(this Pos p) => new(p.X, p.Y);
    public static Form ToForm(this Player.StateType s) => (Form)(int)s;
    public static Player.StateType ToStateType(this Form f) => (Player.StateType)(int)f;

    /// <summary>Описание уровня для ядра. budgetOverride — бюджет из инструментов отладки (бесконечные ходы).</summary>
    public static LevelDef ToLevelDef(this LevelData data, IReadOnlyDictionary<Player.StateType, int> budgetOverride = null, RuleSet rules = null)
    {
        var level = new LevelDef(data.MapSize.x, data.MapSize.y)
        {
            Name = data.name,
            Rules = rules ?? RuleSet.Default,
        };

        for (int i = 0; i < level.CellCount && i < data.Map.Length; i++)
        {
            CellData cell = data.Map[i];
            level.Terrain[i] = cell.Terrain;
            level.Fragile[i] = cell.IsFragile;
            if (cell.Item == CellItem.Star)
                level.Stars.Add(level.PosOf(i));
        }

        if (data.StartMovesPerForm != null)
            foreach (MovePerFormEntry entry in data.StartMovesPerForm)
                level.Budget[(int)entry.State] = entry.Moves;

        if (budgetOverride != null)
            foreach (KeyValuePair<Player.StateType, int> kv in budgetOverride)
                level.Budget[(int)kv.Key] = kv.Key == Player.StateType.Default ? 0 : kv.Value;

        if (data.VolcanoConfigs != null)
            foreach (VolcanoConfig v in data.VolcanoConfigs)
                level.Schedules.Add(ScheduleDef.Volcano(v.Position.ToPos(), v.Period, ToPositions(v.LavaSequence)));

        if (data.IceSourceConfigs != null)
            foreach (IceSourceConfig c in data.IceSourceConfigs)
                level.Schedules.Add(ScheduleDef.IceSource(c.Position.ToPos(), c.Period, ToPositions(c.FreezeSequence)));

        return level;
    }

    private static IEnumerable<Pos> ToPositions(List<Vector2Int> list)
    {
        if (list == null) yield break;
        foreach (Vector2Int v in list)
            yield return v.ToPos();
    }
}
