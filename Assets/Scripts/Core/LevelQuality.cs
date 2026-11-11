using System.Collections.Generic;

namespace Paperbound.Core
{
    /// <summary>Флаги качества уровня по отчёту решателя (политика бюджета из плана дизайна от 2026-11-10).</summary>
    public static class LevelQuality
    {
        public const int MaxSlack = 3;

        public const string Unsolvable = "нет решения на 3★";
        public const string FinishTooCheap = "финиш слишком дешёвый";
        public const string NoSlack = "запаса нет";
        public const string Truncated = "перебор обрезан";

        /// <summary>
        /// «финиш слишком дешёвый» — до финиша короче трети маршрута на 3★ или запас больше MaxSlack;
        /// «запаса нет» — бюджет ровно на кратчайший финиш.
        /// </summary>
        public static List<string> Flags(SolveReport r)
        {
            var flags = new List<string>();
            if (r.Truncated) flags.Add(Truncated);
            if (!r.CanThreeStar) flags.Add(Unsolvable);
            if (r.CanFinish)
            {
                bool shortFinish = r.CanThreeStar && r.MinMovesToFinish * 3 < r.MinMovesThreeStars;
                if (shortFinish || r.FinishSlack > MaxSlack) flags.Add(FinishTooCheap);
                if (r.FinishSlack == 0) flags.Add(NoSlack);
            }
            return flags;
        }
    }
}
