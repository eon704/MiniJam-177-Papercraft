using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelData : ScriptableObject
{
    [Header("Map Data")]
    public Vector2Int MapSize;
    public CellData[] Map;

    [Header("Player data")]
    public List<MovePerFormEntry> StartMovesPerForm;

    [Header("Biome")]
    public BiomeType Biome;

    [Header("Special Biome Cells")]
    public List<VolcanoConfig> VolcanoConfigs = new();
    public List<IceSourceConfig> IceSourceConfigs = new();

    [Header("Solution Data")]
    [SerializeField] private List<SolutionStep> cachedSolution;

    public List<SolutionStep> CachedSolution
    {
        get => cachedSolution ?? new List<SolutionStep>();
        set => cachedSolution = value;
    }

    public bool HasCachedSolution => cachedSolution != null && cachedSolution.Count > 0;

    public static LevelData DefaultLevel
    {
        get
        {
            LevelData defaultLevel = CreateInstance<LevelData>();
            defaultLevel.MapSize = new Vector2Int(5, 1);
            defaultLevel.Map = new CellData[5 * 1];
            for (int x = 0; x < 5; x++)
                defaultLevel.Map[x] = new CellData(TerrainType.Default, CellItem.None);

            defaultLevel.StartMovesPerForm = new List<MovePerFormEntry>()
            {
                new() { State = Player.StateType.Default, Moves = -1 },
                new() { State = Player.StateType.Crane,   Moves = -1 },
                new() { State = Player.StateType.Plane,   Moves = -1 },
                new() { State = Player.StateType.Boat,    Moves = -1 },
                new() { State = Player.StateType.Frog,    Moves = -1 }
            };
            defaultLevel.cachedSolution = new List<SolutionStep>();
            return defaultLevel;
        }
    }

    public bool IsValid()
    {
        if (Map == null || Map.Length != MapSize.x * MapSize.y)
        {
            Debug.LogError("Invalid map size or map data.");
            return false;
        }

        int startCellCount = 0;
        int endCellCount = 0;
        foreach (var cell in Map)
        {
            if (cell.Terrain == TerrainType.Start) startCellCount++;
            else if (cell.Terrain == TerrainType.End) endCellCount++;
        }

        if (startCellCount != 1)
        {
            Debug.LogError($"Invalid number of start cells: {startCellCount}. There should be exactly one start cell.");
            return false;
        }

        if (endCellCount != 1)
        {
            Debug.LogError($"Invalid number of end cells: {endCellCount}. There should be exactly one end cell.");
            return false;
        }

        int starCount = 0;
        foreach (var cell in Map)
        {
            if (cell.Item == CellItem.Star)
                starCount++;
        }

        if (starCount != 3)
        {
            Debug.LogError($"Invalid number of stars: {starCount}. There should be exactly 3 stars in the level.");
            return false;
        }

        if (StartMovesPerForm == null || StartMovesPerForm.Count == 0)
        {
            Debug.LogError("No starting moves defined for player forms.");
            return false;
        }

        foreach (var entry in StartMovesPerForm)
        {
            if (entry.State == Player.StateType.Default && entry.Moves > 0)
            {
                Debug.LogError("Default moves must be 0.");
                return false;
            }

            if (entry.Moves < -1)
            {
                Debug.LogError($"Invalid moves count for state {entry.State}: {entry.Moves}. Must be either -1 or a non-negative integer.");
                return false;
            }
        }

        return true;
    }
}

[Serializable]
public class VolcanoConfig
{
    public Vector2Int Position;
    public int Period = 2;
    public List<Vector2Int> LavaSequence = new();
}

[Serializable]
public class IceSourceConfig
{
    public Vector2Int Position;
    public int Period = 3;
    public List<Vector2Int> FreezeSequence = new();
}

[Serializable]
public struct MovePerFormEntry
{
    public Player.StateType State;
    public int Moves;
}

[Serializable]
public struct SolutionStep
{
    public Vector2Int Position;
    public Player.StateType State;
    public int StarsCollected;

    public SolutionStep(Vector2Int position, Player.StateType state, int starsCollected)
    {
        Position = position;
        State = state;
        StarsCollected = starsCollected;
    }
}
