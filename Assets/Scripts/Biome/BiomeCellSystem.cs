using System;
using System.Collections.Generic;
using UnityEngine;

public class BiomeCellSystem
{
    // Raised when a volcano erupts. Lava is already applied to the board at this point.
    // Subscribers play visuals (projectile, explosion) — non-blocking, no callback needed.
    public event Action<Vector2Int, Vector2Int> OnEruptionVisual;
    public event Action OnNextTargetsChanged;
    private readonly Board _board;

    private readonly List<VolcanoConfig> _volcanoConfigs;
    private readonly Dictionary<Vector2Int, int> _volcanoCountdowns = new();
    private readonly Dictionary<Vector2Int, int> _volcanoLavaIndexes = new();

    private readonly List<IceSourceConfig> _iceSourceConfigs;
    private readonly Dictionary<Vector2Int, int> _iceCountdowns = new();
    private readonly Dictionary<Vector2Int, int> _iceFreezeIndexes = new();

    private Dictionary<Vector2Int, TerrainType> _currentDynamicTerrain = new();

    public BiomeCellSystem(Board board, LevelData levelData)
    {
        _board = board;

        _volcanoConfigs = levelData.VolcanoConfigs ?? new List<VolcanoConfig>();
        foreach (var cfg in _volcanoConfigs)
        {
            _volcanoCountdowns[cfg.Position] = cfg.Period;
            _volcanoLavaIndexes[cfg.Position] = 0;
        }

        _iceSourceConfigs = levelData.IceSourceConfigs ?? new List<IceSourceConfig>();
        foreach (var cfg in _iceSourceConfigs)
        {
            _iceCountdowns[cfg.Position] = cfg.Period;
            _iceFreezeIndexes[cfg.Position] = 0;
        }
    }

    public void OnPlayerMoved()
    {
        TickVolcanoes();
        TickIceSources();
        OnNextTargetsChanged?.Invoke();
    }

    /// <summary>Returns the next cell each active volcano will target.</summary>
    public IEnumerable<Vector2Int> GetNextLavaTargets()
    {
        foreach (var cfg in _volcanoConfigs)
        {
            if (cfg.LavaSequence == null || cfg.LavaSequence.Count == 0)
                continue;
            int index = _volcanoLavaIndexes[cfg.Position];
            if (index >= cfg.LavaSequence.Count)
                continue;
            yield return cfg.LavaSequence[index];
        }
    }

    // ── Volcano ────────────────────────────────────────────────────────────

    private void TickVolcanoes()
    {
        foreach (var cfg in _volcanoConfigs)
        {
            if (cfg.LavaSequence == null || cfg.LavaSequence.Count == 0)
                continue;

            _volcanoCountdowns[cfg.Position]--;
            if (_volcanoCountdowns[cfg.Position] <= 0)
            {
                _volcanoCountdowns[cfg.Position] = cfg.Period;
                Erupt(cfg);
            }
        }
    }

    private void Erupt(VolcanoConfig cfg)
    {
        int index = _volcanoLavaIndexes[cfg.Position];
        if (index >= cfg.LavaSequence.Count)
            return;

        Vector2Int targetPos = cfg.LavaSequence[index];
        _volcanoLavaIndexes[cfg.Position] = index + 1;

        Cell targetCell = _board.GetCell(targetPos);
        if (targetCell == null || targetCell.Terrain == TerrainType.Volcano || targetCell.Terrain == TerrainType.Lava)
            return;

        // Apply lava immediately — no waiting for visuals
        _currentDynamicTerrain[targetPos] = TerrainType.Lava;
        targetCell.SetTerrain(TerrainType.Lava);

        // Notify subscribers to play the visual (non-blocking)
        OnEruptionVisual?.Invoke(cfg.Position, targetPos);
    }

    // ── Ice sources ────────────────────────────────────────────────────────

    private void TickIceSources()
    {
        foreach (var cfg in _iceSourceConfigs)
        {
            if (cfg.FreezeSequence == null || cfg.FreezeSequence.Count == 0)
                continue;

            _iceCountdowns[cfg.Position]--;
            if (_iceCountdowns[cfg.Position] <= 0)
            {
                _iceCountdowns[cfg.Position] = cfg.Period;
                FreezeNext(cfg);
            }
        }
    }

    private void FreezeNext(IceSourceConfig cfg)
    {
        int index = _iceFreezeIndexes[cfg.Position];
        if (index >= cfg.FreezeSequence.Count)
            return;

        Vector2Int targetPos = cfg.FreezeSequence[index];
        Cell targetCell = _board.GetCell(targetPos);
        if (targetCell != null && targetCell.Terrain != TerrainType.Ice)
        {
            _currentDynamicTerrain[targetPos] = TerrainType.Ice;
            targetCell.SetTerrain(TerrainType.Ice);
        }

        _iceFreezeIndexes[cfg.Position] = index + 1;
    }

    // ── Snapshot / Undo ────────────────────────────────────────────────────

    public BiomeCellSnapshot TakeSnapshot()
    {
        return new BiomeCellSnapshot
        {
            DynamicTerrainState = new Dictionary<Vector2Int, TerrainType>(_currentDynamicTerrain),
            VolcanoCountdowns   = new Dictionary<Vector2Int, int>(_volcanoCountdowns),
            VolcanoLavaIndexes  = new Dictionary<Vector2Int, int>(_volcanoLavaIndexes),
            IceCountdowns       = new Dictionary<Vector2Int, int>(_iceCountdowns),
            IceFreezeIndexes    = new Dictionary<Vector2Int, int>(_iceFreezeIndexes)
        };
    }

    public void RestoreSnapshot(BiomeCellSnapshot snap)
    {
        foreach (var kvp in _currentDynamicTerrain)
        {
            var cell = _board.GetCell(kvp.Key);
            if (cell != null) cell.SetTerrain(cell.OriginalTerrain);
        }

        _currentDynamicTerrain = snap.DynamicTerrainState != null
            ? new Dictionary<Vector2Int, TerrainType>(snap.DynamicTerrainState)
            : new Dictionary<Vector2Int, TerrainType>();

        foreach (var kvp in _currentDynamicTerrain)
        {
            var cell = _board.GetCell(kvp.Key);
            if (cell != null) cell.SetTerrain(kvp.Value);
        }

        if (snap.VolcanoCountdowns != null)
            foreach (var kvp in snap.VolcanoCountdowns)
                _volcanoCountdowns[kvp.Key] = kvp.Value;

        if (snap.VolcanoLavaIndexes != null)
            foreach (var kvp in snap.VolcanoLavaIndexes)
                _volcanoLavaIndexes[kvp.Key] = kvp.Value;

        if (snap.IceCountdowns != null)
            foreach (var kvp in snap.IceCountdowns)
                _iceCountdowns[kvp.Key] = kvp.Value;

        if (snap.IceFreezeIndexes != null)
            foreach (var kvp in snap.IceFreezeIndexes)
                _iceFreezeIndexes[kvp.Key] = kvp.Value;

        OnNextTargetsChanged?.Invoke();
    }

    public void Reset()
    {
        foreach (var kvp in _currentDynamicTerrain)
        {
            var cell = _board.GetCell(kvp.Key);
            if (cell != null) cell.SetTerrain(cell.OriginalTerrain);
        }
        _currentDynamicTerrain.Clear();

        foreach (var cfg in _volcanoConfigs)
        {
            _volcanoCountdowns[cfg.Position] = cfg.Period;
            _volcanoLavaIndexes[cfg.Position] = 0;
        }

        foreach (var cfg in _iceSourceConfigs)
        {
            _iceCountdowns[cfg.Position] = cfg.Period;
            _iceFreezeIndexes[cfg.Position] = 0;
        }

        OnNextTargetsChanged?.Invoke();
    }
}
