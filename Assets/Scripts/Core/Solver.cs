using System.Collections.Generic;

namespace Paperbound.Core
{
    public struct SolverStep
    {
        public Form Form;
        public Pos Target;
        public int StarsAfter;

        public override string ToString() => $"{Form}->{Target}";
    }

    public sealed class SolveReport
    {
        public bool CanFinish;
        public int MinMovesToFinish = -1;
        public List<SolverStep> FinishPath;

        public bool CanThreeStar;
        public int MinMovesThreeStars = -1;
        public List<SolverStep> ThreeStarPath;

        public int MinFoldsThreeStars = -1;
        public List<SolverStep> MinFoldsPath;

        /// <summary>Наибольшее число звёзд, с которым можно дойти до финиша.</summary>
        public int MaxStarsAtFinish = -1;

        /// <summary>Число различных последовательностей ходов (форма, цель), ведущих к финишу с любым числом звёзд.</summary>
        public long FinishSolutionCount;
        /// <summary>То же с тремя звёздами.</summary>
        public long ThreeStarSolutionCount;
        public bool CountSaturated;

        public int TotalBudget;
        public int StatesExplored;
        public bool Truncated;

        /// <summary>Запас бюджета на пути к финишу.</summary>
        public int FinishSlack => CanFinish ? TotalBudget - MinMovesToFinish : -1;
    }

    /// <summary>Перебор состояний на общих правилах <see cref="Rules"/>.</summary>
    public static class Solver
    {
        public const int DefaultStateLimit = 2_000_000;
        private const long CountCap = 1_000_000_000_000L;

        public static SolveReport Solve(LevelDef level, int stateLimit = DefaultStateLimit, bool countSolutions = true)
        {
            int allStars = level.Stars.Count;
            int allMask = allStars >= 31 ? -1 : (1 << allStars) - 1;
            var report = new SolveReport { TotalBudget = level.TotalBudget };
            GameState start = GameState.Initial(level);

            // 1) BFS по числу ходов (форма не важна: складка бесплатна по бюджету).
            var parent = new Dictionary<GameState, (GameState prev, SolverStep step)>(StateComparer.IgnoreForm);
            var queue = new Queue<GameState>();
            parent[start] = (null, default);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GameState cur = queue.Dequeue();
                if (cur.Status == GameStatus.Won)
                {
                    int stars = cur.StarsCollected;
                    if (stars > report.MaxStarsAtFinish) report.MaxStarsAtFinish = stars;
                    if (!report.CanFinish)
                    {
                        report.CanFinish = true;
                        report.MinMovesToFinish = cur.MovesMade;
                        report.FinishPath = Rebuild(parent, cur);
                    }
                    if (!report.CanThreeStar && (cur.StarMask & allMask) == allMask)
                    {
                        report.CanThreeStar = true;
                        report.MinMovesThreeStars = cur.MovesMade;
                        report.ThreeStarPath = Rebuild(parent, cur);
                    }
                    continue;
                }
                if (cur.Status != GameStatus.Playing) continue;
                if (parent.Count >= stateLimit) { report.Truncated = true; break; }

                foreach (MoveOption o in Rules.LegalMoves(level, cur))
                {
                    MoveResult res = Rules.Apply(level, cur, o.Form, o.Target);
                    if (!res.Success || res.After.Status == GameStatus.Dead) continue;
                    if (parent.ContainsKey(res.After)) continue;
                    parent[res.After] = (cur, new SolverStep { Form = o.Form, Target = o.Target, StarsAfter = res.After.StarsCollected });
                    queue.Enqueue(res.After);
                }
            }
            report.StatesExplored = parent.Count;

            // 2) Минимум складок на три звезды: 0-1 BFS, форма входит в состояние.
            if (report.CanThreeStar && !report.Truncated)
                SolveMinFolds(level, start, allMask, stateLimit, report);

            // 3) Число решений: граф ацикличен (каждый ход тратит бюджет), считаем с мемоизацией.
            if (countSolutions && report.CanFinish && !report.Truncated)
            {
                var memo = new Dictionary<GameState, (long any, long three)>(StateComparer.IgnoreForm);
                bool saturated = false;
                (long any, long three) = Count(level, start, allMask, memo, ref saturated);
                report.FinishSolutionCount = any;
                report.ThreeStarSolutionCount = three;
                report.CountSaturated = saturated;
            }

            return report;
        }

        private static (long, long) Count(LevelDef level, GameState s, int allMask,
            Dictionary<GameState, (long, long)> memo, ref bool saturated)
        {
            if (s.Status == GameStatus.Won)
                return (1, (s.StarMask & allMask) == allMask ? 1 : 0);
            if (s.Status != GameStatus.Playing) return (0, 0);
            if (memo.TryGetValue(s, out var cached)) return cached;

            long any = 0, three = 0;
            foreach (MoveOption o in Rules.LegalMoves(level, s))
            {
                MoveResult res = Rules.Apply(level, s, o.Form, o.Target);
                if (!res.Success) continue;
                (long a, long t) = Count(level, res.After, allMask, memo, ref saturated);
                any = Add(any, a, ref saturated);
                three = Add(three, t, ref saturated);
            }
            memo[s] = (any, three);
            return (any, three);
        }

        private static long Add(long a, long b, ref bool saturated)
        {
            long sum = a + b;
            if (sum > CountCap || sum < 0) { saturated = true; return CountCap; }
            return sum;
        }

        private static void SolveMinFolds(LevelDef level, GameState start, int allMask, int stateLimit, SolveReport report)
        {
            var dist = new Dictionary<GameState, int>(StateComparer.WithForm);
            var parent = new Dictionary<GameState, (GameState prev, SolverStep step)>(StateComparer.WithForm);
            var deque = new LinkedList<GameState>();
            dist[start] = 0;
            parent[start] = (null, default);
            deque.AddFirst(start);

            while (deque.Count > 0)
            {
                GameState cur = deque.First.Value;
                deque.RemoveFirst();
                int d = dist[cur];
                if (cur.Folds > d) continue; // устаревшая запись

                if (cur.Status == GameStatus.Won)
                {
                    if ((cur.StarMask & allMask) == allMask)
                    {
                        report.MinFoldsThreeStars = d;
                        report.MinFoldsPath = Rebuild(parent, cur);
                        return;
                    }
                    continue;
                }
                if (dist.Count >= stateLimit) { report.Truncated = true; return; }

                foreach (MoveOption o in Rules.LegalMoves(level, cur))
                {
                    MoveResult res = Rules.Apply(level, cur, o.Form, o.Target);
                    if (!res.Success || res.After.Status == GameStatus.Dead) continue;
                    GameState next = res.After;
                    int nd = next.Folds;
                    if (dist.TryGetValue(next, out int old) && old <= nd) continue;
                    dist[next] = nd;
                    parent[next] = (cur, new SolverStep { Form = o.Form, Target = o.Target, StarsAfter = next.StarsCollected });
                    if (nd == d) deque.AddFirst(next); else deque.AddLast(next);
                }
            }
        }

        private static List<SolverStep> Rebuild(Dictionary<GameState, (GameState prev, SolverStep step)> parent, GameState end)
        {
            var steps = new List<SolverStep>();
            GameState cur = end;
            while (true)
            {
                (GameState prev, SolverStep step) = parent[cur];
                if (prev == null) break;
                steps.Add(step);
                cur = prev;
            }
            steps.Reverse();
            return steps;
        }

        /// <summary>Кратчайшая (по ходам) последовательность до состояния, удовлетворяющего goal. null — нет.</summary>
        public static List<SolverStep> FindShortest(LevelDef level, System.Func<GameState, bool> goal, int stateLimit = DefaultStateLimit)
        {
            GameState start = GameState.Initial(level);
            var parent = new Dictionary<GameState, (GameState prev, SolverStep step)>(StateComparer.IgnoreForm);
            var queue = new Queue<GameState>();
            parent[start] = (null, default);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GameState cur = queue.Dequeue();
                if (goal(cur)) return Rebuild(parent, cur);
                if (cur.Status != GameStatus.Playing || parent.Count >= stateLimit) continue;
                foreach (MoveOption o in Rules.LegalMoves(level, cur))
                {
                    MoveResult res = Rules.Apply(level, cur, o.Form, o.Target);
                    if (!res.Success || res.After.Status == GameStatus.Dead || parent.ContainsKey(res.After)) continue;
                    parent[res.After] = (cur, new SolverStep { Form = o.Form, Target = o.Target, StarsAfter = res.After.StarsCollected });
                    queue.Enqueue(res.After);
                }
            }
            return null;
        }

        /// <summary>Все конечные бюджеты (больше нуля на старте) израсходованы.</summary>
        public static bool BudgetExhausted(LevelDef level, GameState s)
        {
            for (int i = 1; i < Forms.Count; i++)
                if (level.Budget[i] > 0 && s.Budget[i] > 0) return false;
            return true;
        }

        /// <summary>Проиграть последовательность ходов с начала уровня. null — какой-то ход нелегален.</summary>
        public static GameState Replay(LevelDef level, IEnumerable<SolverStep> steps)
        {
            GameState s = GameState.Initial(level);
            foreach (SolverStep step in steps)
            {
                MoveResult r = Rules.Apply(level, s, step.Form, step.Target);
                if (!r.Success) return null;
                s = r.After;
            }
            return s;
        }
    }
}
