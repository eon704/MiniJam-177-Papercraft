using System.Collections.Generic;

namespace Paperbound.Core
{
    public static class LevelValidator
    {
        public const int MaxSide = 8;
        public const int RequiredStars = 3;

        /// <summary>Ошибки уровня. Пустой список — уровень корректен.</summary>
        public static List<string> Validate(LevelDef level)
        {
            var errors = new List<string>();

            if (level.Width > MaxSide || level.Height > MaxSide)
                errors.Add($"Поле {level.Width}x{level.Height} больше {MaxSide}x{MaxSide}.");

            int starts = 0, ends = 0;
            foreach (TerrainType t in level.Terrain)
            {
                if (t == TerrainType.Start) starts++;
                else if (t == TerrainType.End) ends++;
            }
            if (starts != 1) errors.Add($"Клеток Start: {starts}, нужна ровно одна.");
            if (ends != 1) errors.Add($"Клеток End: {ends}, нужна ровно одна.");

            if (level.Stars.Count != RequiredStars)
                errors.Add($"Звёзд: {level.Stars.Count}, нужно {RequiredStars}.");
            foreach (Pos p in level.Stars)
            {
                if (!level.InBounds(p)) { errors.Add($"Звезда {p} за пределами поля."); continue; }
                TerrainType t = level.Terrain[level.Index(p)];
                if (t == TerrainType.Empty) errors.Add($"Звезда {p} лежит на пустой клетке.");
            }

            if (level.Budget[(int)Form.Default] != 0)
                errors.Add("Бюджет листа (Default) должен быть 0.");
            for (int i = 1; i < Forms.Count; i++)
                if (level.Budget[i] < 0)
                    errors.Add($"Бюджет {(Form)i} = {level.Budget[i]}: отрицательный бюджет (включая -1) не допускается.");
            if (level.TotalBudget == 0)
                errors.Add("Нет ни одного хода в бюджете.");

            // Кувшинка — островок на воде: без соседней воды правило не читается.
            for (int i = 0; i < level.CellCount; i++)
            {
                if (level.Terrain[i] != TerrainType.LilyPad) continue;
                Pos p = level.PosOf(i);
                bool water = false;
                foreach (Pos d in new[] { Pos.Up, Pos.Down, Pos.Left, Pos.Right })
                {
                    Pos n = p + d;
                    if (level.InBounds(n) && level.Terrain[level.Index(n)] == TerrainType.Water) water = true;
                }
                if (!water) errors.Add($"Кувшинка {p} не граничит с водой.");
            }

            if (level.Stars.Count > 31) errors.Add("Больше 31 звезды не поддерживается.");
            if (level.Items.Count > 31) errors.Add("Больше 31 предмета не поддерживается.");
            foreach (ItemDef item in level.Items)
            {
                if (!level.InBounds(item.Position)) { errors.Add($"Предмет {item.Kind} {item.Position} за пределами поля."); continue; }
                if (level.Terrain[level.Index(item.Position)] == TerrainType.Empty) errors.Add($"Предмет {item.Kind} {item.Position} лежит на пустой клетке.");
                if (item.Kind == ItemKind.Leaf && (item.Form == Form.Default || item.Amount <= 0))
                    errors.Add($"Листок {item.Position}: нужна форма (не лист) и положительное число ходов.");
            }

            foreach (EntityDef e in level.Entities)
            {
                if (!level.InBounds(e.Position)) { errors.Add($"Сущность {EntityKind.Name(e.Kind)} {e.Position} за пределами поля."); continue; }
                TerrainType t = level.Terrain[level.Index(e.Position)];
                if (e.Kind == EntityKind.Raft && t != TerrainType.Water && t != TerrainType.Sand)
                    errors.Add($"Плот {e.Position} должен стоять на воде.");
                if (e.Kind == EntityKind.Box && t != TerrainType.Default && t != TerrainType.Sand && t != TerrainType.Plate && t != TerrainType.Ice && t != TerrainType.BoxFloor)
                    errors.Add($"Короб {e.Position} должен стоять на полу.");
            }

            // Два плота не должны спорить за клетку: в одну клетку воды не может вести течение с двух сторон.
            int rafts = 0;
            foreach (EntityDef e in level.Entities) if (e.Kind == EntityKind.Raft || e.Kind == EntityKind.Box) rafts++;
            if (rafts >= 2)
            {
                for (int i = 0; i < level.CellCount; i++)
                {
                    Pos p = level.PosOf(i);
                    int into = 0;
                    foreach (Pos d in new[] { Pos.Up, Pos.Down, Pos.Left, Pos.Right })
                    {
                        Pos n = p - d;
                        if (level.InBounds(n) && level.Flow[level.Index(n)] == d && level.Terrain[level.Index(n)] == TerrainType.Water) into++;
                    }
                    if (into >= 2) errors.Add($"В клетку {p} течение ведёт с двух сторон: плоты могут спорить за неё.");
                }
            }

            bool hasGate = false, hasPlate = false;
            foreach (TerrainType t in level.Terrain) { hasGate |= t == TerrainType.Gate; hasPlate |= t == TerrainType.Plate; }
            if (hasGate && !hasPlate) errors.Add("Есть ворота, но нет ни одной плиты.");
            for (int a = 0; a < level.Entities.Count; a++)
                for (int b = a + 1; b < level.Entities.Count; b++)
                    if (level.Entities[a].Position == level.Entities[b].Position)
                        errors.Add($"Две сущности на клетке {level.Entities[a].Position}.");

            for (int i = 0; i < level.Schedules.Count; i++)
            {
                ScheduleDef s = level.Schedules[i];
                if (s.Period <= 0) errors.Add($"Расписание {i} ({s.Kind}): период {s.Period} должен быть больше 0.");
                if (!level.InBounds(s.Source)) errors.Add($"Расписание {i} ({s.Kind}): источник {s.Source} за пределами поля.");
                foreach (Pos t in s.Targets)
                {
                    if (!level.InBounds(t)) { errors.Add($"Расписание {i} ({s.Kind}): цель {t} за пределами поля."); continue; }
                    TerrainType tt = level.Terrain[level.Index(t)];
                    if (s.Toggle && tt != s.Result && tt != s.Alternate)
                        errors.Add($"Прилив: клетка {t} должна быть водой или песком.");
                }
            }

            // Храм: маршруты стражей и тории.
            for (int r = 0; r < level.Routes.Count; r++)
            {
                List<Pos> route = level.Routes[r];
                if (route.Count < 2) { errors.Add($"Маршрут {r}: нужно хотя бы 2 клетки."); continue; }
                for (int k = 0; k < route.Count; k++)
                {
                    Pos p = route[k];
                    if (!level.InBounds(p)) { errors.Add($"Маршрут {r}: клетка {p} за пределами поля."); continue; }
                    TerrainType t = level.Terrain[level.Index(p)];
                    if (t == TerrainType.Empty || t == TerrainType.Water || t == TerrainType.Stone || t == TerrainType.Start || t == TerrainType.End || t == TerrainType.Torii)
                        errors.Add($"Маршрут {r}: страж не может ходить по клетке {p} ({t}).");
                    if (k > 0 && !Adjacent(route[k - 1], p)) errors.Add($"Маршрут {r}: клетки {route[k - 1]} и {p} не соседние.");
                }
            }
            foreach (EntityDef e in level.Entities)
            {
                if (e.Kind != EntityKind.Guard) continue;
                if (e.Data < 0 || e.Data >= level.Routes.Count) { errors.Add($"Страж {e.Position}: нет маршрута {e.Data}."); continue; }
                if (!level.Routes[e.Data].Contains(e.Position)) errors.Add($"Страж {e.Position} стоит не на своём маршруте.");
                if (e.Direction.X != 1 && e.Direction.X != -1) errors.Add($"Страж {e.Position}: dir должен быть 1,0 или -1,0.");
            }
            int toriiCells = 0;
            foreach (TerrainType t in level.Terrain) if (t == TerrainType.Torii) toriiCells++;
            var paired = new HashSet<Pos>();
            foreach ((Pos a, Pos b) in level.ToriiPairs)
                foreach (Pos p in new[] { a, b })
                {
                    if (!level.InBounds(p) || level.Terrain[level.Index(p)] != TerrainType.Torii) errors.Add($"Тории {p}: клетка не тории.");
                    else if (!paired.Add(p)) errors.Add($"Тории {p} входят в две пары.");
                }
            if (paired.Count != toriiCells) errors.Add("Есть тории без пары.");

            return errors;
        }

        private static bool Adjacent(Pos a, Pos b) => System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) == 1;
    }
}
