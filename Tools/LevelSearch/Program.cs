using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Paperbound.Core;

// Поиск уровней по ограничениям: случайная карта → решатель с щедрым бюджетом → бюджет по кратчайшему маршруту на 3★
// → повторная проверка метрик. Кандидаты пишутся в .pbl для ручного отбора и доводки.
// dotnet run --project Tools/LevelSearch -- w=5 h=5 forms=crane,frog L=6-9 out=dir [ключ=значение ...]
var opt = args.Select(a => a.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
string Get(string k, string d) => opt.TryGetValue(k, out string v) ? v : d;
double D(string k, double d) => double.Parse(Get(k, d.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture);
int I(string k, int d) => int.Parse(Get(k, d.ToString()));
(int, int) R(string k, string d) { var p = Get(k, d).Split('-'); return (int.Parse(p[0]), int.Parse(p[^1])); }

int w = I("w", 5), h = I("h", 5);
Form[] forms = Get("forms", "crane").Split(',').Select(f => Enum.Parse<Form>(f, true)).ToArray();
double pEmpty = D("empty", 0.25), pWater = D("water", 0), pStone = D("stone", 0), pLily = D("lily", 0), pFire = D("fire", 0), pFragile = D("fragile", 0);
int leaves = I("leaves", 0);
(int lMin, int lMax) = R("L", "4-6");
(int sMin, int sMax) = R("slack", "1-3");
int maxSol = I("maxsol", 2);
double minRatio = D("ratio", 0.5);
int iters = I("iters", 200000), want = I("count", 5), seed = I("seed", 1);
bool needLily = Get("needlily", "0") == "1", needFragile = Get("needfragile", "0") == "1";
int minFolds = I("minfolds", forms.Length);
string outDir = Get("out", "candidates");
string prefix = Get("name", "cand");
bool waterStars = Get("waterstars", "0") == "1";
Directory.CreateDirectory(outDir);
var rng = new Random(seed);
int found = 0;

for (int it = 0; it < iters && found < want; it++)
{
    var level = new LevelDef(w, h) { Name = $"{prefix}-{found + 1}" };
    for (int i = 0; i < level.CellCount; i++)
    {
        double r = rng.NextDouble();
        TerrainType t = TerrainType.Default;
        if ((r -= pEmpty) < 0) t = TerrainType.Empty;
        else if ((r -= pWater) < 0) t = TerrainType.Water;
        else if ((r -= pStone) < 0) t = TerrainType.Stone;
        else if ((r -= pLily) < 0) t = TerrainType.LilyPad;
        else if ((r -= pFire) < 0) t = TerrainType.Fire;
        level.Terrain[i] = t;
        level.Fragile[i] = t == TerrainType.Default && rng.NextDouble() < pFragile;
    }
    var land = Enumerable.Range(0, level.CellCount).Where(i => level.Terrain[i] == TerrainType.Default).OrderBy(_ => rng.Next()).ToList();
    if (land.Count < 5 + leaves) continue;
    level.Terrain[land[0]] = TerrainType.Start; level.Fragile[land[0]] = false;
    level.Terrain[land[1]] = TerrainType.End; level.Fragile[land[1]] = false;
    var starCells = land.Skip(2).ToList();
    if (waterStars) starCells.AddRange(Enumerable.Range(0, level.CellCount).Where(i => level.Terrain[i] == TerrainType.Water));
    starCells = starCells.OrderBy(_ => rng.Next()).ToList();
    for (int k = 0; k < 3; k++) level.Stars.Add(level.PosOf(starCells[k]));
    level.Stars.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
    for (int k = 0; k < leaves; k++)
    {
        Pos p = level.PosOf(land[2 + 3 + k < land.Count ? 2 + 3 + k : 2]);
        if (level.Stars.Contains(p)) continue;
        level.Items.Add(new ItemDef { Position = p, Form = forms[rng.Next(forms.Length)] });
    }
    if (needLily && !level.Terrain.Contains(TerrainType.LilyPad)) continue;

    foreach (Form f in forms) level.SetBudget(f, lMax);
    var steps = Solver.FindShortest(level, s => s.Status == GameStatus.Won && s.StarsCollected == 3, 300000);
    if (steps == null || steps.Count < lMin || steps.Count > lMax) continue;
    for (int i = 1; i < Forms.Count; i++) level.Budget[i] = 0;
    foreach (SolverStep st in steps) level.Budget[(int)st.Form]++;
    // Листки: бюджет выдан с учётом листков → уменьшаем на подобранные.
    GameState end = Solver.Replay(level, steps);
    if (end == null) continue;
    foreach (var (item, k) in level.Items.Select((x, k) => (x, k)))
        if (end.HasItem(k)) level.Budget[(int)item.Form] -= item.Amount;
    if (level.Budget.Any(b => b < 0)) continue;
    if (forms.Any(f => level.Budget[(int)f] + level.Items.Count(x => x.Form == f) == 0)) continue;

    SolveReport rep = Solver.Solve(level, 300000);
    if (rep.Truncated || !rep.CanThreeStar || rep.MinMovesThreeStars != steps.Count) continue;
    if (rep.FinishSlack < sMin || rep.FinishSlack > sMax) continue;
    if (rep.ThreeStarSolutionCount < 1 || rep.ThreeStarSolutionCount > maxSol) continue;
    if ((double)rep.MinMovesToFinish / rep.MinMovesThreeStars < minRatio) continue;
    if (LevelQuality.Flags(rep).Count > 0 && sMin > 0) continue;
    if (rep.MinFoldsThreeStars < minFolds) continue;
    if (LevelValidator.Validate(level).Count > 0) continue;
    if (needFragile)
    {
        var copy = LevelText.Parse(LevelText.Write(level));
        Array.Clear(copy.Fragile, 0, copy.Fragile.Length);
        var r2 = Solver.Solve(copy, 300000, countSolutions: true);
        if (r2.ThreeStarSolutionCount == rep.ThreeStarSolutionCount && r2.FinishSlack == rep.FinishSlack) continue; // хрупкость ничего не меняет
    }
    if (leaves > 0)
    {
        var copy = LevelText.Parse(LevelText.Write(level));
        copy.Items.Clear();
        if (Solver.Solve(copy, 300000, countSolutions: false).CanThreeStar) continue; // листок обязателен
    }
    if (Get("prune", "1") == "1")
    {
        var pruned = Prune(level);
        var rp = Solver.Solve(pruned, 300000);
        if (rp.MinMovesThreeStars == rep.MinMovesThreeStars && rp.MinMovesToFinish == rep.MinMovesToFinish
            && rp.ThreeStarSolutionCount == rep.ThreeStarSolutionCount && rp.MinFoldsThreeStars == rep.MinFoldsThreeStars)
        { level = pruned; rep = rp; }
    }
    found++;
    string comment = $"кандидат: финиш {rep.MinMovesToFinish}, запас {rep.FinishSlack}, 3★ {rep.MinMovesThreeStars}, решений {rep.ThreeStarSolutionCount}, складок {rep.MinFoldsThreeStars}\n3★: {string.Join(", ", rep.ThreeStarPath)}";
    File.WriteAllText(Path.Combine(outDir, level.Name + LevelText.Extension), LevelText.Write(level, comment));
    Console.WriteLine($"{level.Name}: it={it} F={rep.MinMovesToFinish} slack={rep.FinishSlack} L={rep.MinMovesThreeStars} sol={rep.ThreeStarSolutionCount} folds={rep.MinFoldsThreeStars}");
}
Console.WriteLine($"found {found}");

// Клетки, на которые фигурка не попадает ни в одном достижимом состоянии, делаются пустыми.
static LevelDef Prune(LevelDef level)
{
    var used = new HashSet<Pos>();
    var seen = new HashSet<GameState>(StateComparer.IgnoreForm);
    var q = new Queue<GameState>();
    var start = GameState.Initial(level);
    q.Enqueue(start); seen.Add(start); used.Add(start.Player);
    while (q.Count > 0 && seen.Count < 300000)
    {
        var s = q.Dequeue();
        foreach (var o in Rules.LegalMoves(level, s))
        {
            foreach (var p in o.Path) used.Add(p);
            var r = Rules.Apply(level, s, o.Form, o.Target);
            if (r.Success && r.After.Status == GameStatus.Playing && seen.Add(r.After)) q.Enqueue(r.After);
        }
    }
    var copy = LevelText.Parse(LevelText.Write(level));
    for (int i = 0; i < copy.CellCount; i++)
    {
        Pos p = copy.PosOf(i);
        if (used.Contains(p) || copy.Terrain[i] == TerrainType.Start) continue;
        // Вода рядом с используемой водой оставляется ради вида? Нет — только используемые клетки.
        copy.Terrain[i] = TerrainType.Empty;
        copy.Fragile[i] = false;
    }
    copy.Name = level.Name;
    return copy;
}
