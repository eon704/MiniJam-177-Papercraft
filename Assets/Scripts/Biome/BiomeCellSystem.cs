using System;
using System.Collections.Generic;
using Paperbound.Core;
using UnityEngine;

/// <summary>
/// Вид расписаний биома (вулкан, источник льда и будущие). Логика и счётчики — в ядре (GameState),
/// здесь только проигрывание событий хода и данные для предупреждений.
/// </summary>
public class BiomeCellSystem
{
    public event Action<Vector2Int, Vector2Int> OnEruptionVisual;
    public event Action OnNextTargetsChanged;

    private readonly Board _board;

    public BiomeCellSystem(Board board)
    {
        _board = board;
    }

    /// <summary>Проиграть изменения клеток от расписаний после анимации хода.</summary>
    public void PlayEvents(MoveResult result)
    {
        foreach (MoveEvent e in result.Events)
        {
            if (e.Type != MoveEventType.TerrainChanged) continue;
            ScheduleDef schedule = _board.Def.Schedules[e.ScheduleIndex];

            if (schedule.Kind == "volcano")
            {
                // Лава применяется к клеткам, когда снаряд долетит (ApplyLava из GameController).
                OnEruptionVisual?.Invoke(e.Source.ToVector(), e.Pos.ToVector());
                continue;
            }

            Cell cell = _board.GetCell(e.Pos.ToVector());
            cell?.SetTerrain(e.NewTerrain);
            if (e.NewTerrain == TerrainType.Ice)
                FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxIceFreeze);
        }

        OnNextTargetsChanged?.Invoke();
    }

    /// <summary>Снаряд вулкана долетел: показать террейн, который уже стоит в состоянии.</summary>
    public void ApplyLava(Vector2Int targetPos)
    {
        Cell cell = _board.GetCell(targetPos);
        if (cell == null) return;
        TerrainType terrain = _board.State.TerrainAt(_board.Def, targetPos.ToPos());
        if (cell.Terrain != terrain)
            cell.SetTerrain(terrain);
    }

    /// <summary>Returns (volcanoPos, targetCell, movesUntilEruption) for each active volcano.</summary>
    public IEnumerable<(Vector2Int volcanoPos, Vector2Int target, int countdown)> GetNextLavaData()
    {
        foreach (var (schedule, source, target, countdown) in Rules.UpcomingEffects(_board.Def, _board.State))
        {
            if (_board.Def.Schedules[schedule].Kind != "volcano") continue;
            yield return (source.ToVector(), target.ToVector(), countdown);
        }
    }

    /// <summary>Состояние сменилось целиком (Undo, рестарт) — обновить предупреждения.</summary>
    public void Refresh()
    {
        OnNextTargetsChanged?.Invoke();
    }
}
