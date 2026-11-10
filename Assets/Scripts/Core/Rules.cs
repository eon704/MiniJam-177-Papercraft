using System.Collections.Generic;

namespace Paperbound.Core
{
    /// <summary>Вариант хода для подсветки в UI и для решателя.</summary>
    public sealed class MoveOption
    {
        public Form Form;
        public Pos Target;
        /// <summary>Террейн цели допустим для формы. Недопустимые варианты показываются серыми.</summary>
        public bool Legal;
        /// <summary>Клетки пути без стартовой, последняя — Target.</summary>
        public List<Pos> Path = new();
    }

    public enum MoveFailure
    {
        None,
        GameOver,
        NoForm,
        NoBudget,
        Illegal,
    }

    public enum MoveEventType
    {
        /// <summary>Фигурка оказалась на клетке пути (Index — номер клетки в Path).</summary>
        Enter,
        StarCollected,
        /// <summary>Подобран предмет (ItemIndex), например листок +1 ход.</summary>
        ItemCollected,
        Collapsed,
        /// <summary>Кувшинка утонула и стала водой.</summary>
        Sank,
        Died,
        Won,
        /// <summary>Расписание изменило клетку (Source — источник, Pos — цель).</summary>
        TerrainChanged,
    }

    public struct MoveEvent
    {
        public MoveEventType Type;
        public Pos Pos;
        /// <summary>Номер клетки пути, на которой произошло событие; -1 — до первого шага (обрушение старта), Path.Count — после хода (расписания).</summary>
        public int PathIndex;
        public int StarIndex;
        public int ItemIndex;
        public int ScheduleIndex;
        public Pos Source;
        public TerrainType OldTerrain;
        public TerrainType NewTerrain;
        public DeathCause Death;

        public override string ToString() => $"{Type}@{Pos}#{PathIndex}";
    }

    public sealed class MoveResult
    {
        public bool Success;
        public MoveFailure Failure;
        public GameState Before;
        public GameState After;
        public Form Form;
        public Pos From;
        public Pos Target;
        public List<Pos> Path = new();
        public List<MoveEvent> Events = new();
    }

    /// <summary>
    /// Единственный источник правил хода. Им пользуются игра (Player/BoardPiece), решатель и редактор уровней.
    /// </summary>
    public static class Rules
    {
        // ── Варианты хода ─────────────────────────────────────────────────────

        public static List<MoveOption> GetOptions(LevelDef level, GameState s, Form form)
        {
            var result = new List<MoveOption>();
            FormDef def = Forms.Get(form);
            switch (def.Kind)
            {
                case MoveKind.Step: StepOptions(level, s, def, result); break;
                case MoveKind.Jump: JumpOptions(level, s, def, result); break;
                case MoveKind.Slide: SlideOptions(level, s, def, result); break;
                case MoveKind.Sail: SailOptions(level, s, def, result); break;
            }
            return result;
        }

        private static bool IsHole(LevelDef level, GameState s, Pos p) =>
            s.Terrain[level.Index(p)] == TerrainType.Empty || s.Collapsed[level.Index(p)];

        private static void StepOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            foreach (Pos dir in def.Directions)
            {
                Pos t = s.Player + dir;
                if (!level.InBounds(t) || IsHole(level, s, t)) continue;
                TerrainType terrain = s.TerrainAt(level, t);
                var option = new MoveOption { Form = def.Form, Target = t, Legal = def.CanStandOn(terrain) };
                option.Path.Add(t);

                if (option.Legal && level.Rules.SlipperyIce && terrain == TerrainType.Ice)
                {
                    Pos c = t;
                    while (s.TerrainAt(level, c) == TerrainType.Ice)
                    {
                        Pos n = c + dir;
                        if (!level.InBounds(n) || IsHole(level, s, n) || !def.CanStandOn(s.TerrainAt(level, n))) break;
                        c = n;
                        option.Path.Add(c);
                    }
                    option.Target = c;
                }
                result.Add(option);
            }
        }

        private static void JumpOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            foreach (Pos offset in def.Directions)
            {
                Pos t = s.Player + offset;
                if (!level.InBounds(t) || IsHole(level, s, t)) continue;
                var option = new MoveOption { Form = def.Form, Target = t, Legal = def.CanStandOn(s.TerrainAt(level, t)) };
                option.Path.Add(t);
                result.Add(option);
            }
        }

        private static void SlideOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            bool overWater = level.Rules.PlaneFliesOverWaterAndEmpty;
            foreach (Pos dir in def.Directions)
            {
                var path = new List<Pos>();
                Pos c = s.Player + dir;
                while (level.InBounds(c))
                {
                    TerrainType terrain = s.TerrainAt(level, c);
                    bool hole = IsHole(level, s, c);
                    if (hole || TerrainInfo.IsWaterLike(terrain))
                    {
                        if (!overWater) break;
                        path.Add(c);
                        c += dir;
                        continue;
                    }
                    if (!def.CanStandOn(terrain)) break;
                    path.Add(c);
                    result.Add(new MoveOption { Form = def.Form, Target = c, Legal = true, Path = new List<Pos>(path) });
                    c += dir;
                }
            }
        }

        private static void SailOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            foreach (Pos dir in def.Directions)
            {
                Pos c = s.Player + dir;
                if (!level.InBounds(c) || s.TerrainAt(level, c) != TerrainType.Water || s.IsCollapsed(level, c)) continue;
                var path = new List<Pos>();
                while (level.InBounds(c) && s.TerrainAt(level, c) == TerrainType.Water && !s.IsCollapsed(level, c))
                {
                    path.Add(c);
                    c += dir;
                }
                if (!level.InBounds(c) || IsHole(level, s, c) || !def.CanStandOn(s.TerrainAt(level, c))) continue;
                path.Add(c);
                result.Add(new MoveOption { Form = def.Form, Target = c, Legal = true, Path = path });
            }
        }

        /// <summary>
        /// Какой ход имеет в виду игрок, кликнув по клетке: сама цель, либо клетка на пути легального хода
        /// (вода для лодки, первая клетка льда для журавля). null — такого хода нет.
        /// </summary>
        public static MoveOption ResolveClick(LevelDef level, GameState s, Form form, Pos clicked)
        {
            List<MoveOption> options = GetOptions(level, s, form);
            foreach (MoveOption o in options)
                if (o.Target == clicked) return o;
            foreach (MoveOption o in options)
                if (o.Legal && o.Path.Contains(clicked)) return o;
            return null;
        }

        // ── Применение ────────────────────────────────────────────────────────

        /// <summary>Сменить форму. Смена бесплатна по бюджету, но считается складкой.</summary>
        public static GameState Fold(GameState s, Form form)
        {
            if (s.Status != GameStatus.Playing || s.Form == form) return s;
            GameState n = s.Clone();
            n.Form = form;
            n.Folds++;
            return n;
        }

        /// <summary>
        /// Применить ход формой form к клетке target. Если форма отличается от текущей — сначала складка.
        /// Возвращает новое состояние и упорядоченный список событий для анимации. Исходное состояние не меняется.
        /// </summary>
        public static MoveResult Apply(LevelDef level, GameState s, Form form, Pos target)
        {
            var r = new MoveResult { Before = s, After = s, Form = form, From = s.Player, Target = target };

            if (s.Status != GameStatus.Playing) return Fail(r, MoveFailure.GameOver);
            if (Forms.Get(form).Kind == MoveKind.None) return Fail(r, MoveFailure.NoForm);
            if (s.Budget[(int)form] <= 0) return Fail(r, MoveFailure.NoBudget);

            MoveOption option = null;
            foreach (MoveOption o in GetOptions(level, s, form))
                if (o.Legal && o.Target == target) { option = o; break; }
            if (option == null) return Fail(r, MoveFailure.Illegal);

            GameState n = s.Clone();
            if (n.Form != form)
            {
                n.Form = form;
                n.Folds++;
            }
            n.Budget[(int)form]--;
            n.MovesMade++;
            r.Path = option.Path;
            r.Success = true;
            r.After = n;

            MoveKind kind = Forms.Get(form).Kind;

            // Уход с клетки: хрупкая рушится, кувшинка тонет.
            Collapse(level, n, s.Player, -1, r);
            Sink(level, n, s.Player, r);

            for (int i = 0; i < option.Path.Count; i++)
            {
                Pos c = option.Path[i];
                bool last = i == option.Path.Count - 1;
                int idx = level.Index(c);
                TerrainType terrain = n.Terrain[idx];
                r.Events.Add(new MoveEvent { Type = MoveEventType.Enter, Pos = c, PathIndex = i });

                if (!last)
                {
                    bool overHole = terrain == TerrainType.Empty || TerrainInfo.IsWaterLike(terrain) || n.Collapsed[idx];
                    switch (kind)
                    {
                        case MoveKind.Slide:
                            if (overHole) break;
                            if (TerrainInfo.IsDeadly(terrain))
                            {
                                n.Player = c;
                                Die(n, r, c, i, DeathCause.FlewOverFire);
                                return r;
                            }
                            CollectStar(level, n, c, i, r);
                            CollectItem(level, n, c, i, r);
                            Collapse(level, n, c, i, r);
                            break;
                        case MoveKind.Sail:
                            if (level.Rules.BoatCollectsStars) CollectStar(level, n, c, i, r);
                            CollectItem(level, n, c, i, r);
                            break;
                        case MoveKind.Step:
                            // Скольжение по льду: фигурка проходит клетку целиком.
                            CollectStar(level, n, c, i, r);
                            CollectItem(level, n, c, i, r);
                            Collapse(level, n, c, i, r);
                            break;
                    }
                    continue;
                }

                n.Player = c;
                CollectStar(level, n, c, i, r);
                CollectItem(level, n, c, i, r);
                if (terrain == TerrainType.End)
                {
                    n.Status = GameStatus.Won;
                    r.Events.Add(new MoveEvent { Type = MoveEventType.Won, Pos = c, PathIndex = i });
                    return r;
                }
                if (TerrainInfo.IsDeadly(terrain))
                {
                    Die(n, r, c, i, DeathCause.Terrain);
                    return r;
                }
            }

            TickSchedules(level, n, r.Path.Count, r);
            return r;
        }

        private static MoveResult Fail(MoveResult r, MoveFailure failure)
        {
            r.Success = false;
            r.Failure = failure;
            return r;
        }

        private static void Die(GameState n, MoveResult r, Pos at, int pathIndex, DeathCause cause)
        {
            n.Status = GameStatus.Dead;
            n.Death = cause;
            n.DeathPos = at;
            r.Events.Add(new MoveEvent { Type = MoveEventType.Died, Pos = at, PathIndex = pathIndex, Death = cause });
        }

        private static void CollectStar(LevelDef level, GameState n, Pos c, int pathIndex, MoveResult r)
        {
            int star = level.StarIndex(c);
            if (star < 0 || n.HasStar(star)) return;
            n.StarMask |= 1 << star;
            r.Events.Add(new MoveEvent { Type = MoveEventType.StarCollected, Pos = c, PathIndex = pathIndex, StarIndex = star });
        }

        private static void CollectItem(LevelDef level, GameState n, Pos c, int pathIndex, MoveResult r)
        {
            for (int k = 0; k < level.Items.Count; k++)
            {
                ItemDef item = level.Items[k];
                if (item.Position != c || n.HasItem(k)) continue;
                n.ItemMask |= 1 << k;
                if (item.Kind == ItemKind.Leaf)
                    n.Budget[(int)item.Form] += item.Amount;
                r.Events.Add(new MoveEvent { Type = MoveEventType.ItemCollected, Pos = c, PathIndex = pathIndex, ItemIndex = k });
            }
        }

        private static void Sink(LevelDef level, GameState n, Pos c, MoveResult r)
        {
            int idx = level.Index(c);
            if (n.Terrain[idx] != TerrainType.LilyPad) return;
            n.Terrain[idx] = TerrainType.Water;
            r.Events.Add(new MoveEvent
            {
                Type = MoveEventType.Sank, Pos = c, PathIndex = -1,
                OldTerrain = TerrainType.LilyPad, NewTerrain = TerrainType.Water,
            });
        }

        private static void Collapse(LevelDef level, GameState n, Pos c, int pathIndex, MoveResult r)
        {
            int idx = level.Index(c);
            if (!level.Fragile[idx] || n.Collapsed[idx]) return;
            n.Collapsed[idx] = true;
            r.Events.Add(new MoveEvent { Type = MoveEventType.Collapsed, Pos = c, PathIndex = pathIndex });
        }

        /// <summary>Тик расписаний после хода игрока. Сущности будут тикать здесь же, после расписаний.</summary>
        private static void TickSchedules(LevelDef level, GameState n, int pathIndex, MoveResult r)
        {
            for (int i = 0; i < level.Schedules.Count; i++)
            {
                ScheduleDef sch = level.Schedules[i];
                if (sch.Targets.Count == 0 || sch.Period <= 0) continue;

                n.ScheduleCountdown[i]--;
                if (n.ScheduleCountdown[i] > 0) continue;
                n.ScheduleCountdown[i] = sch.Period;

                int q = n.ScheduleIndex[i];
                if (q >= sch.Targets.Count)
                {
                    if (!sch.Loop) continue;
                    q = 0;
                }
                Pos t = sch.Targets[q];
                n.ScheduleIndex[i] = q + 1;

                if (!level.InBounds(t)) continue;
                int idx = level.Index(t);
                TerrainType old = n.Terrain[idx];
                if (sch.Immune.Contains(old) || old == sch.Result) continue;

                n.Terrain[idx] = sch.Result;
                r.Events.Add(new MoveEvent
                {
                    Type = MoveEventType.TerrainChanged, Pos = t, PathIndex = pathIndex, ScheduleIndex = i,
                    Source = sch.Source, OldTerrain = old, NewTerrain = sch.Result,
                });

                if (sch.Lethal && n.Player == t && n.Status == GameStatus.Playing)
                    Die(n, r, t, pathIndex, DeathCause.Schedule);
            }
        }

        /// <summary>Следующая цель каждого расписания и сколько ходов до срабатывания (для дуг и рамок вулкана).</summary>
        public static List<(int schedule, Pos source, Pos target, int countdown)> UpcomingEffects(LevelDef level, GameState s)
        {
            var list = new List<(int, Pos, Pos, int)>();
            for (int i = 0; i < level.Schedules.Count; i++)
            {
                ScheduleDef sch = level.Schedules[i];
                if (sch.Targets.Count == 0) continue;
                int q = s.ScheduleIndex[i];
                if (q >= sch.Targets.Count)
                {
                    if (!sch.Loop) continue;
                    q = 0;
                }
                list.Add((i, sch.Source, sch.Targets[q], s.ScheduleCountdown[i]));
            }
            return list;
        }

        /// <summary>Все легальные ходы всех форм с ненулевым бюджетом (для решателя).</summary>
        public static IEnumerable<MoveOption> LegalMoves(LevelDef level, GameState s)
        {
            if (s.Status != GameStatus.Playing) yield break;
            foreach (Form f in Forms.Movable)
            {
                if (s.Budget[(int)f] <= 0) continue;
                foreach (MoveOption o in GetOptions(level, s, f))
                    if (o.Legal) yield return o;
            }
        }
    }
}
