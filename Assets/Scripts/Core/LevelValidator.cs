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

            for (int i = 0; i < level.Schedules.Count; i++)
            {
                ScheduleDef s = level.Schedules[i];
                if (s.Period <= 0) errors.Add($"Расписание {i} ({s.Kind}): период {s.Period} должен быть больше 0.");
                if (!level.InBounds(s.Source)) errors.Add($"Расписание {i} ({s.Kind}): источник {s.Source} за пределами поля.");
                foreach (Pos t in s.Targets)
                    if (!level.InBounds(t)) errors.Add($"Расписание {i} ({s.Kind}): цель {t} за пределами поля.");
            }

            return errors;
        }
    }
}
