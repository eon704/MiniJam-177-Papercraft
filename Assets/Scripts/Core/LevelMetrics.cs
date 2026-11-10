using System.Collections.Generic;

namespace Paperbound.Core
{
    /// <summary>Метрики уровня сверх отчёта решателя: мёртвые клетки и прощение первых ходов.</summary>
    public static class LevelMetrics
    {
        public const int DefaultStateLimit = 300_000;

        /// <summary>
        /// Клетки, которые участвуют в игре: фигурка встаёт на них или проходит по ним (путь самолёта, скольжение)
        /// хотя бы в одном состоянии, достижимом со старта в пределах бюджета.
        /// </summary>
        public static HashSet<Pos> UsedCells(LevelDef level, int stateLimit = DefaultStateLimit)
        {
            var used = new HashSet<Pos>();
            if (level.Find(TerrainType.Start) == null) return used;
            var start = GameState.Initial(level);
            var seen = new HashSet<GameState>(StateComparer.IgnoreForm) { start };
            var queue = new Queue<GameState>();
            queue.Enqueue(start);
            used.Add(start.Player);
            while (queue.Count > 0 && seen.Count < stateLimit)
            {
                GameState s = queue.Dequeue();
                foreach (MoveOption o in Rules.LegalMoves(level, s))
                {
                    foreach (Pos p in o.Path) used.Add(p);
                    used.Add(o.Target);
                    MoveResult r = Rules.Apply(level, s, o.Form, o.Target);
                    if (!r.Success) continue;
                    used.Add(r.After.Player); // плот мог перевезти
                    foreach (EntityState e in r.After.Entities) used.Add(e.Position);
                    if (r.After.Status == GameStatus.Playing && seen.Add(r.After)) queue.Enqueue(r.After);
                }
            }
            foreach (EntityState e in start.Entities) used.Add(e.Position);
            return used;
        }

        /// <summary>
        /// Непустые клетки, на которые нельзя попасть ни в одном достижимом состоянии. Вода рядом с кувшинкой не считается:
        /// она нужна, чтобы кувшинка читалась (правило валидатора).
        /// </summary>
        public static List<Pos> DeadCells(LevelDef level, int stateLimit = DefaultStateLimit)
        {
            HashSet<Pos> used = UsedCells(level, stateLimit);
            var dead = new List<Pos>();
            for (int i = 0; i < level.CellCount; i++)
            {
                Pos p = level.PosOf(i);
                if (level.Terrain[i] == TerrainType.Empty || used.Contains(p)) continue;
                if (level.Terrain[i] == TerrainType.Water && NextToLily(level, p)) continue;
                dead.Add(p);
            }
            return dead;
        }

        private static bool NextToLily(LevelDef level, Pos p)
        {
            foreach (Pos d in new[] { Pos.Up, Pos.Down, Pos.Left, Pos.Right })
            {
                Pos n = p + d;
                if (level.InBounds(n) && level.Terrain[level.Index(n)] == TerrainType.LilyPad) return true;
            }
            return false;
        }

        /// <summary>Доля мёртвых клеток среди непустых.</summary>
        public static double DeadShare(LevelDef level, int stateLimit = DefaultStateLimit)
        {
            int nonEmpty = 0;
            foreach (TerrainType t in level.Terrain) if (t != TerrainType.Empty) nonEmpty++;
            return nonEmpty == 0 ? 0 : (double)DeadCells(level, stateLimit).Count / nonEmpty;
        }

        /// <summary>
        /// Прощение: доля последовательностей из двух легальных ходов с начала уровня, после которых 3★ ещё достижимы.
        /// Если первый ход заканчивает игру, последовательность короче и считается одна. Гибель — не прощает
        /// (хотя Undo её отменяет: метрика про то, сколько первых решений не ведут в тупик).
        /// </summary>
        public static double Forgiveness(LevelDef level, out int good, out int total, int stateLimit = DefaultStateLimit)
        {
            good = 0; total = 0;
            if (level.Find(TerrainType.Start) == null) return 0;
            var memo = new Dictionary<GameState, bool>(StateComparer.IgnoreForm);
            GameState start = GameState.Initial(level);
            foreach (MoveOption a in Rules.LegalMoves(level, start))
            {
                MoveResult r1 = Rules.Apply(level, start, a.Form, a.Target);
                if (!r1.Success) continue;
                if (r1.After.Status != GameStatus.Playing)
                {
                    total++;
                    if (CanThreeStarFrom(level, r1.After, memo, stateLimit)) good++;
                    continue;
                }
                foreach (MoveOption b in Rules.LegalMoves(level, r1.After))
                {
                    MoveResult r2 = Rules.Apply(level, r1.After, b.Form, b.Target);
                    if (!r2.Success) continue;
                    total++;
                    if (CanThreeStarFrom(level, r2.After, memo, stateLimit)) good++;
                }
            }
            return total == 0 ? 0 : (double)good / total;
        }

        public static double Forgiveness(LevelDef level, int stateLimit = DefaultStateLimit) =>
            Forgiveness(level, out _, out _, stateLimit);

        /// <summary>Можно ли из состояния дойти до финиша со всеми звёздами в оставшийся бюджет.</summary>
        public static bool CanThreeStarFrom(LevelDef level, GameState from, Dictionary<GameState, bool> memo = null, int stateLimit = DefaultStateLimit)
        {
            int allMask = level.Stars.Count >= 31 ? -1 : (1 << level.Stars.Count) - 1;
            if (from.Status == GameStatus.Won) return (from.StarMask & allMask) == allMask;
            if (from.Status != GameStatus.Playing) return false;
            if (memo != null && memo.TryGetValue(from, out bool cached)) return cached;
            var seen = new HashSet<GameState>(StateComparer.IgnoreForm) { from };
            var queue = new Queue<GameState>();
            queue.Enqueue(from);
            bool found = false;
            while (queue.Count > 0 && !found && seen.Count < stateLimit)
            {
                GameState s = queue.Dequeue();
                foreach (MoveOption o in Rules.LegalMoves(level, s))
                {
                    MoveResult r = Rules.Apply(level, s, o.Form, o.Target);
                    if (!r.Success) continue;
                    GameState n = r.After;
                    if (n.Status == GameStatus.Won && (n.StarMask & allMask) == allMask) { found = true; break; }
                    if (n.Status == GameStatus.Playing && seen.Add(n)) queue.Enqueue(n);
                }
            }
            if (memo != null) memo[from] = found;
            return found;
        }

        /// <summary>Сколько ходов маршрута фигурку везёт плот (событие Carried).</summary>
        public static int RidesInPath(LevelDef level, IEnumerable<SolverStep> path)
        {
            int rides = 0;
            GameState s = GameState.Initial(level);
            foreach (SolverStep st in path)
            {
                MoveResult r = Rules.Apply(level, s, st.Form, st.Target);
                if (!r.Success) break;
                foreach (MoveEvent e in r.Events)
                    if (e.Type == MoveEventType.Carried) { rides++; break; }
                s = r.After;
            }
            return rides;
        }

        /// <summary>
        /// Обязательна ли поездка на плоту: нет ни одного маршрута на 3★ в пределах бюджета, где фигурку ни разу не везёт плот.
        /// Для уровня без плотов и без 3★ — false.
        /// </summary>
        public static bool RideRequired(LevelDef level, int stateLimit = DefaultStateLimit)
        {
            if (level.Find(TerrainType.Start) == null || !level.Entities.Exists(e => e.Kind == EntityKind.Raft)) return false;
            int allMask = level.Stars.Count >= 31 ? -1 : (1 << level.Stars.Count) - 1;
            var start = GameState.Initial(level);
            var seen = new HashSet<GameState>(StateComparer.IgnoreForm) { start };
            var queue = new Queue<GameState>();
            queue.Enqueue(start);
            while (queue.Count > 0 && seen.Count < stateLimit)
            {
                GameState s = queue.Dequeue();
                foreach (MoveOption o in Rules.LegalMoves(level, s))
                {
                    MoveResult r = Rules.Apply(level, s, o.Form, o.Target);
                    if (!r.Success || r.Events.Exists(e => e.Type == MoveEventType.Carried)) continue;
                    GameState n = r.After;
                    if (n.Status == GameStatus.Won && (n.StarMask & allMask) == allMask) return false;
                    if (n.Status == GameStatus.Playing && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return Solver.Solve(level, stateLimit, false).CanThreeStar;
        }

        /// <summary>Плоты, которые не сдвигаются ни в одном достижимом состоянии: для игрока это обманчивый островок.</summary>
        public static List<int> StaticRafts(LevelDef level, int stateLimit = DefaultStateLimit)
        {
            var moved = new HashSet<int>();
            var result = new List<int>();
            if (level.Find(TerrainType.Start) == null) return result;
            var start = GameState.Initial(level);
            var seen = new HashSet<GameState>(StateComparer.IgnoreForm) { start };
            var queue = new Queue<GameState>();
            queue.Enqueue(start);
            while (queue.Count > 0 && seen.Count < stateLimit)
            {
                GameState s = queue.Dequeue();
                foreach (MoveOption o in Rules.LegalMoves(level, s))
                {
                    MoveResult r = Rules.Apply(level, s, o.Form, o.Target);
                    if (!r.Success) continue;
                    foreach (MoveEvent e in r.Events)
                        if (e.Type == MoveEventType.EntityMoved && level.Entities[e.EntityIndex].Kind == EntityKind.Raft) moved.Add(e.EntityIndex);
                    if (r.After.Status == GameStatus.Playing && seen.Add(r.After)) queue.Enqueue(r.After);
                }
            }
            for (int i = 0; i < level.Entities.Count; i++)
                if (level.Entities[i].Kind == EntityKind.Raft && !moved.Contains(i)) result.Add(i);
            return result;
        }
    }
}
