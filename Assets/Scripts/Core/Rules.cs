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
        /// <summary>Журавль толкает короб, стоящий на Target, на клетку Target + направление.</summary>
        public bool Push;
        public Pos PushTo;
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
        /// <summary>Сущность (EntityIndex) переместилась из Source в Pos после хода игрока.</summary>
        EntityMoved,
        /// <summary>Фигурку перевезла сущность (плот) из Source в Pos.</summary>
        Carried,
        /// <summary>Сущность (EntityIndex) исчезла или сменила вид: короб сгорел, стал полом или плотом (NewTerrain — что стало с клеткой).</summary>
        EntityChanged,
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
        public int EntityIndex;
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
                case MoveKind.Rush: RushOptions(level, s, def, result); break;
                case MoveKind.Flutter: FlutterOptions(level, s, def, result); break;
            }
            // Зыбучий песок: стоящий на нём (кроме бабочки) не может сложиться — уходит той же формой.
            if (form != s.Form && s.Form != Form.Butterfly && s.Status == GameStatus.Playing
                && level.InBounds(s.Player) && s.TerrainAt(level, s.Player) == TerrainType.Quicksand)
                foreach (MoveOption o in result) o.Legal = false;
            return result;
        }

        /// <summary>Ветер (Пустыня): направление слоя Flow на клетке суши. На воде тот же слой — течение.</summary>
        public static Pos WindAt(LevelDef level, GameState s, Pos p)
        {
            Pos f = level.FlowAt(p);
            if (f == Pos.Zero) return f;
            TerrainType t = s.TerrainAt(level, p);
            return t == TerrainType.Water || t == TerrainType.LilyPad || t == TerrainType.Empty ? Pos.Zero : f;
        }

        private static bool IsHole(LevelDef level, GameState s, Pos p) =>
            s.Terrain[level.Index(p)] == TerrainType.Empty || s.Collapsed[level.Index(p)];

        // ── Пещера: плиты и ворота ────────────────────────────────────────────

        /// <summary>Плиту жмут журавль, лягушка и короб.</summary>
        public static bool PlatePressed(LevelDef level, GameState s)
        {
            for (int i = 0; i < level.CellCount; i++)
            {
                if (s.Terrain[i] != TerrainType.Plate) continue;
                Pos p = level.PosOf(i);
                if (s.Player == p && (s.Form == Form.Crane || s.Form == Form.Frog) && s.Status == GameStatus.Playing) return true;
                if (s.EntityAt(p, EntityKind.Box) >= 0) return true;
            }
            return false;
        }

        /// <summary>Ворота открыты, пока нажата хоть одна плита; занятые ворота не закрываются.</summary>
        public static bool GateOpen(LevelDef level, GameState s, Pos p) =>
            PlatePressed(level, s) || s.Player == p || s.EntityAt(p) >= 0;

        /// <summary>Стена для движения: закрытые ворота или короб.</summary>
        private static bool Walled(LevelDef level, GameState s, Pos p) =>
            s.EntityAt(p, EntityKind.Box) >= 0 || (s.TerrainAt(level, p) == TerrainType.Gate && !GateOpen(level, s, p));

        /// <summary>Журавль, лягушка и лодка могут стоять на плоту.</summary>
        private static bool RidesRaft(Form f) => f == Form.Crane || f == Form.Frog || f == Form.Boat;

        /// <summary>Форма может остановиться на клетке: подходящий террейн или плот.</summary>
        public static bool CanStop(LevelDef level, GameState s, Form form, Pos p)
        {
            if (!level.InBounds(p) || IsHole(level, s, p) || Walled(level, s, p)) return false;
            if (RidesRaft(form) && s.EntityAt(p, EntityKind.Raft) >= 0) return true;
            return Forms.Get(form).CanStandOn(s.TerrainAt(level, p));
        }

        /// <summary>Куда можно втолкнуть короб: пол, вода/кувшинка (станет плотом), провал (станет полом), огонь/лава (сгорит).</summary>
        private static bool CanReceiveBox(LevelDef level, GameState s, Pos p)
        {
            if (!level.InBounds(p) || s.EntityAt(p) >= 0 || s.Player == p) return false;
            if (IsHole(level, s, p)) return true;
            switch (s.TerrainAt(level, p))
            {
                case TerrainType.Default: case TerrainType.Sand: case TerrainType.Ice: case TerrainType.BoxFloor:
                case TerrainType.Plate: case TerrainType.Water: case TerrainType.LilyPad:
                case TerrainType.Fire: case TerrainType.Lava:
                    return true;
                case TerrainType.Gate:
                    return GateOpen(level, s, p);
                default:
                    return false; // камень, старт, финиш, вулкан
            }
        }

        private static void StepOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            foreach (Pos dir in def.Directions)
            {
                Pos t = s.Player + dir;
                if (!level.InBounds(t) || IsHole(level, s, t)) continue;
                TerrainType terrain = s.TerrainAt(level, t);
                if (s.EntityAt(t, EntityKind.Box) >= 0)
                {
                    // Журавль толкает короб; другие шагающие формы — нет.
                    if (def.Form != Form.Crane) continue;
                    Pos to = t + dir;
                    var push = new MoveOption { Form = def.Form, Target = t, Push = true, PushTo = to,
                        Legal = def.CanStandOn(terrain) && CanReceiveBox(level, s, to) };
                    push.Path.Add(t);
                    result.Add(push);
                    continue;
                }
                if (Walled(level, s, t)) continue;
                var option = new MoveOption { Form = def.Form, Target = t, Legal = CanStop(level, s, def.Form, t) };
                option.Path.Add(t);

                if (option.Legal && level.Rules.SlipperyIce && terrain == TerrainType.Ice && s.EntityAt(t, EntityKind.Raft) < 0)
                {
                    Pos c = t;
                    while (s.TerrainAt(level, c) == TerrainType.Ice)
                    {
                        Pos n = c + dir;
                        if (!level.InBounds(n) || IsHole(level, s, n) || Walled(level, s, n) || !def.CanStandOn(s.TerrainAt(level, n))) break;
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
                if (!level.InBounds(t) || IsHole(level, s, t) || Walled(level, s, t)) continue;
                var option = new MoveOption { Form = def.Form, Target = t, Legal = CanStop(level, s, def.Form, t) };
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
                var gusts = new HashSet<Pos>();
                Pos c = s.Player + dir;
                while (level.InBounds(c))
                {
                    if (Walled(level, s, c)) break; // короб и закрытые ворота останавливают самолёт
                    // Ветер: каждая клетка ветра на пути сносит самолёт на 1 клетку по ветру; на ней не сесть.
                    Pos wind = WindAt(level, s, c);
                    if (wind != Pos.Zero)
                    {
                        if (!gusts.Add(c)) break; // кольцо ветра
                        Pos to = c + wind;
                        if (!level.InBounds(to) || Walled(level, s, to)) break; // сносит в стену или за край — полёта нет
                        path.Add(c);
                        c = to;
                        continue;
                    }
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

        /// <summary>
        /// Сюрикен: летит по прямой, пока следующая клетка проходима (суша, бамбук), и останавливается перед препятствием
        /// (край, провал, вода, камень, короб, плот, закрытые ворота). Бамбук на пути срезается. Хотя бы одна клетка пути.
        /// </summary>
        private static void RushOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            foreach (Pos dir in def.Directions)
            {
                var path = new List<Pos>();
                Pos c = s.Player + dir;
                while (level.InBounds(c) && !IsHole(level, s, c) && !Walled(level, s, c) && s.EntityAt(c) < 0)
                {
                    TerrainType t = s.TerrainAt(level, c);
                    if (t != TerrainType.Bamboo && !def.CanStandOn(t)) break;
                    path.Add(c);
                    c += dir;
                }
                if (path.Count == 0) continue;
                result.Add(new MoveOption { Form = def.Form, Target = path[path.Count - 1], Legal = true, Path = path });
            }
        }

        /// <summary>
        /// Бабочка: 1 клетка по диагонали на сушу. Если клетка с ветром, ветер несёт бабочку по стрелкам до конца потока
        /// (пока следующая клетка — суша, на которую бабочка может сесть). Невесомая: не рушит, не топит, не жмёт.
        /// </summary>
        private static void FlutterOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            foreach (Pos dir in def.Directions)
            {
                Pos c = s.Player + dir;
                if (!ButterflyCanLand(level, s, def, c)) continue;
                var path = new List<Pos> { c };
                var seen = new HashSet<Pos> { c };
                while (true)
                {
                    Pos wind = WindAt(level, s, c);
                    if (wind == Pos.Zero) break;
                    Pos to = c + wind;
                    if (!ButterflyCanLand(level, s, def, to) || !seen.Add(to)) break;
                    c = to;
                    path.Add(c);
                }
                result.Add(new MoveOption { Form = def.Form, Target = c, Legal = true, Path = path });
            }
        }

        private static bool ButterflyCanLand(LevelDef level, GameState s, FormDef def, Pos p) =>
            level.InBounds(p) && !IsHole(level, s, p) && !Walled(level, s, p) && s.EntityAt(p) < 0 && def.CanStandOn(s.TerrainAt(level, p));

        private static bool OpenWater(LevelDef level, GameState s, Pos p) =>
            level.InBounds(p) && s.TerrainAt(level, p) == TerrainType.Water && !s.IsCollapsed(level, p) && s.EntityAt(p) < 0;

        /// <summary>
        /// Лодка: плывёт по воде до первой суши (или плота) и причаливает. Течение (LevelDef.Flow на воде) поворачивает
        /// лодку по своему направлению; войти в клетку против течения нельзя; кольцо течения без выхода — хода нет.
        /// Лодка, стоящая на воде (на плоту или на залитой клетке), может причалить к соседней суше одним шагом.
        /// </summary>
        private static void SailOptions(LevelDef level, GameState s, FormDef def, List<MoveOption> result)
        {
            bool afloat = s.TerrainAt(level, s.Player) == TerrainType.Water;
            foreach (Pos dir in def.Directions)
            {
                Pos c = s.Player + dir;
                if (!level.InBounds(c) || IsHole(level, s, c)) continue;
                if (!OpenWater(level, s, c))
                {
                    // Соседний плот или (если лодка на воде) соседняя суша — причал одним шагом.
                    bool raft = s.EntityAt(c, EntityKind.Raft) >= 0;
                    if (raft || (afloat && s.TerrainAt(level, c) != TerrainType.Water && def.CanStandOn(s.TerrainAt(level, c))))
                        result.Add(new MoveOption { Form = def.Form, Target = c, Legal = true, Path = new List<Pos> { c } });
                    continue;
                }
                var path = new List<Pos>();
                var seen = new HashSet<(Pos, Pos)>();
                Pos d = dir;
                bool blocked = false;
                while (OpenWater(level, s, c))
                {
                    Pos flow = level.FlowAt(c);
                    if (flow != Pos.Zero)
                    {
                        if (flow == Pos.Zero - d) { blocked = true; break; } // против течения
                        d = flow;
                    }
                    if (!seen.Add((c, d))) { blocked = true; break; } // кольцо течения
                    path.Add(c);
                    c += d;
                }
                if (blocked || !level.InBounds(c) || !CanStop(level, s, def.Form, c)) continue;
                if (s.TerrainAt(level, c) == TerrainType.Water && s.EntityAt(c, EntityKind.Raft) < 0) continue;
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

            // Уход с клетки: хрупкая рушится, кувшинка тонет. Бабочка невесома и клетку не трогает.
            if (!(s.Form == Form.Butterfly && form == Form.Butterfly))
            {
                Collapse(level, n, s.Player, -1, r);
                Sink(level, n, s.Player, r);
            }

            if (option.Push) PushBox(level, n, option.Target, option.PushTo, r);

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
                        case MoveKind.Flutter:
                            // Ветер несёт бабочку: звёзды и предметы по пути подбираются, клетки не рушатся.
                            CollectStar(level, n, c, i, r);
                            CollectItem(level, n, c, i, r);
                            break;
                        case MoveKind.Sail:
                            if (level.Rules.BoatCollectsStars) CollectStar(level, n, c, i, r);
                            CollectItem(level, n, c, i, r);
                            break;
                        case MoveKind.Rush:
                            // Сюрикен проходит огонь, срезает бамбук, собирает звёзды, рушит хрупкие клетки.
                            CutBamboo(level, n, c, i, r);
                            CollectStar(level, n, c, i, r);
                            CollectItem(level, n, c, i, r);
                            Collapse(level, n, c, i, r);
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
                if (kind == MoveKind.Rush) { CutBamboo(level, n, c, i, r); terrain = n.Terrain[idx]; }
                CollectStar(level, n, c, i, r);
                CollectItem(level, n, c, i, r);
                if (form == Form.Frog && terrain == TerrainType.ThinIce)
                {
                    // Под лягушкой тонкий лёд трескается; когда с клетки уйдут, она станет водой (Sink).
                    // Гипотеза RuleSet.FrogSwims (выключена): лёд сразу проламывается, лягушка плавает в полынье.
                    TerrainType now = level.Rules.FrogSwims ? TerrainType.Water : TerrainType.CrackedIce;
                    n.Terrain[idx] = now;
                    r.Events.Add(new MoveEvent { Type = MoveEventType.TerrainChanged, Pos = c, PathIndex = i, Source = c, OldTerrain = TerrainType.ThinIce, NewTerrain = now });
                }
                if (form == Form.Frog && terrain == TerrainType.Quicksand)
                {
                    Die(n, r, c, i, DeathCause.Quicksand);
                    return r;
                }
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
            if (n.Status == GameStatus.Playing) TickEntities(level, n, r.Path.Count, r);
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

        private static void CutBamboo(LevelDef level, GameState n, Pos c, int pathIndex, MoveResult r)
        {
            int idx = level.Index(c);
            if (n.Terrain[idx] != TerrainType.Bamboo) return;
            n.Terrain[idx] = TerrainType.Default;
            r.Events.Add(new MoveEvent { Type = MoveEventType.TerrainChanged, Pos = c, PathIndex = pathIndex, Source = c, OldTerrain = TerrainType.Bamboo, NewTerrain = TerrainType.Default });
        }

        private static void PushBox(LevelDef level, GameState n, Pos from, Pos to, MoveResult r)
        {
            int k = n.EntityAt(from, EntityKind.Box);
            if (k < 0) return;
            int ti = level.Index(to);
            n.Entities[k].Position = to;
            r.Events.Add(new MoveEvent { Type = MoveEventType.EntityMoved, Pos = to, Source = from, PathIndex = 0, EntityIndex = k });
            TerrainType t = n.Terrain[ti];
            if (t == TerrainType.Empty || n.Collapsed[ti])
            {
                // Короб заполняет провал и становится полом.
                n.Terrain[ti] = TerrainType.BoxFloor;
                n.Collapsed[ti] = false;
                n.Entities[k].Kind = EntityKind.None;
                r.Events.Add(new MoveEvent { Type = MoveEventType.EntityChanged, Pos = to, PathIndex = 0, EntityIndex = k, OldTerrain = t, NewTerrain = TerrainType.BoxFloor });
            }
            else if (t == TerrainType.Water || t == TerrainType.LilyPad)
            {
                n.Terrain[ti] = TerrainType.Water;
                n.Entities[k].Kind = EntityKind.Raft;
                r.Events.Add(new MoveEvent { Type = MoveEventType.EntityChanged, Pos = to, PathIndex = 0, EntityIndex = k, OldTerrain = t, NewTerrain = TerrainType.Water });
            }
            else if (TerrainInfo.IsDeadly(t))
            {
                n.Entities[k].Kind = EntityKind.None;
                r.Events.Add(new MoveEvent { Type = MoveEventType.EntityChanged, Pos = to, PathIndex = 0, EntityIndex = k, OldTerrain = t, NewTerrain = t });
            }
        }

        private static void Sink(LevelDef level, GameState n, Pos c, MoveResult r)
        {
            int idx = level.Index(c);
            TerrainType was = n.Terrain[idx];
            if (was != TerrainType.LilyPad && was != TerrainType.CrackedIce) return;
            n.Terrain[idx] = TerrainType.Water;
            r.Events.Add(new MoveEvent
            {
                Type = MoveEventType.Sank, Pos = c, PathIndex = -1,
                OldTerrain = was, NewTerrain = TerrainType.Water,
            });
        }

        private static void Collapse(LevelDef level, GameState n, Pos c, int pathIndex, MoveResult r)
        {
            int idx = level.Index(c);
            if (!level.Fragile[idx] || n.Collapsed[idx] || n.Terrain[idx] == TerrainType.BoxFloor) return;
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

                if (sch.Toggle)
                {
                    foreach (Pos tp in sch.Targets)
                    {
                        if (!level.InBounds(tp)) continue;
                        int ti = level.Index(tp);
                        TerrainType was = n.Terrain[ti];
                        TerrainType now = was == sch.Result ? sch.Alternate : was == sch.Alternate ? sch.Result : was;
                        if (now == was) continue;
                        n.Terrain[ti] = now;
                        r.Events.Add(new MoveEvent
                        {
                            Type = MoveEventType.TerrainChanged, Pos = tp, PathIndex = pathIndex, ScheduleIndex = i,
                            Source = sch.Source, OldTerrain = was, NewTerrain = now,
                        });
                    }
                    // Прилив залил клетку: на плаву остаются лодка и стоящие на плоту (и лягушка при RuleSet.FrogSwims).
                    if (n.Status == GameStatus.Playing && n.Terrain[level.Index(n.Player)] == TerrainType.Water
                        && n.Form != Form.Boat && !(n.Form == Form.Frog && level.Rules.FrogSwims) && n.EntityAt(n.Player, EntityKind.Raft) < 0)
                        Die(n, r, n.Player, pathIndex, DeathCause.Drowned);
                    continue;
                }

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

        /// <summary>
        /// Сущности после хода игрока и расписаний. Плот: если стоит на воде с течением и следующая клетка — свободная вода,
        /// сдвигается на 1; стоящего на нём везёт (звёзды и предметы по пути подбираются).
        /// </summary>
        private static void TickEntities(LevelDef level, GameState n, int pathIndex, MoveResult r)
        {
            for (int i = 0; i < n.Entities.Length; i++)
            {
                EntityState e = n.Entities[i];
                if (e.Kind != EntityKind.Raft) continue;
                int idx = level.Index(e.Position);
                if (n.Terrain[idx] != TerrainType.Water) continue;
                Pos flow = level.Flow[idx];
                if (flow == Pos.Zero) continue;
                Pos t = e.Position + flow;
                if (!level.InBounds(t) || n.Terrain[level.Index(t)] != TerrainType.Water || n.Collapsed[level.Index(t)] || n.EntityAt(t) >= 0) continue;
                bool rider = n.Player == e.Position;
                if (!rider && n.Player == t) continue; // на пути лодка на воде
                n.Entities[i].Position = t;
                r.Events.Add(new MoveEvent { Type = MoveEventType.EntityMoved, Pos = t, Source = e.Position, PathIndex = pathIndex, EntityIndex = i });
                if (rider)
                {
                    n.Player = t;
                    r.Events.Add(new MoveEvent { Type = MoveEventType.Carried, Pos = t, Source = e.Position, PathIndex = pathIndex, EntityIndex = i });
                    CollectStar(level, n, t, pathIndex, r);
                    CollectItem(level, n, t, pathIndex, r);
                }
            }
        }

        /// <summary>Следующая цель каждого расписания и сколько ходов до срабатывания (для дуг и рамок вулкана).</summary>
        public static List<(int schedule, Pos source, Pos target, int countdown)> UpcomingEffects(LevelDef level, GameState s)
        {
            var list = new List<(int, Pos, Pos, int)>();
            for (int i = 0; i < level.Schedules.Count; i++)
            {
                ScheduleDef sch = level.Schedules[i];
                if (sch.Targets.Count == 0 || sch.Toggle) continue;
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
