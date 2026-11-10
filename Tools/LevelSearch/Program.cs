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

if (opt.ContainsKey("autobudget"))
{
    // Бюджет по кратчайшему маршруту на 3★ (щедрый поиск), листки вычитаются; файл переписывается на месте или в out.
    string src = opt["autobudget"];
    var lvl = LevelText.Parse(File.ReadAllText(src));
    var aforms = Get("forms", "crane,frog,plane,boat").Split(',').Select(f => Enum.Parse<Form>(f, true)).ToArray();
    foreach (Form f in aforms) lvl.SetBudget(f, I("fb", 8));
    var st = Solver.FindShortest(lvl, g => g.Status == GameStatus.Won && g.StarsCollected == 3, 2000000);
    if (st == null) { Console.WriteLine("3★ недостижимы"); return; }
    for (int i = 1; i < Forms.Count; i++) lvl.Budget[i] = 0;
    foreach (SolverStep x in st) lvl.Budget[(int)x.Form]++;
    GameState end = Solver.Replay(lvl, st);
    for (int k = 0; k < lvl.Items.Count; k++) if (end.HasItem(k)) lvl.Budget[(int)lvl.Items[k].Form] -= lvl.Items[k].Amount;
    for (int i = 1; i < Forms.Count; i++) lvl.Budget[i] += I("extra" + LevelText.FormName((Form)i), 0);
    string head = string.Join("\n", File.ReadAllLines(src).Where(l => l.StartsWith("# идея:")));
    File.WriteAllText(Get("out", src), (head.Length > 0 ? head + "\n" : "") + LevelText.Write(lvl));
    Console.WriteLine("3★: " + string.Join(", ", st));
    return;
}

if (opt.ContainsKey("evolve"))
{
    // Эволюция уровня к набору ограничений: случайные правки клеток, бюджет заново по маршруту на 3★,
    // принимается правка, не уменьшающая число выполненных ограничений. Ключи: L, slack, maxsol, minfolds, needlily, needfragile.
    var lvl = LevelText.Parse(File.ReadAllText(opt["evolve"]));
    var erng = new Random(I("seed", 1));
    (int lo, int hi) = R("L", "7-9");
    (int s0, int s1) = R("slack", "1-3");
    int maxS = I("maxsol", 2), minF = I("minfolds", 4);
    bool wantLily = Get("needlily", "0") == "1", wantFragile = Get("needfragile", "0") == "1";
    var eforms = Get("forms", "crane,frog,plane,boat").Split(',').Select(f => Enum.Parse<Form>(f, true)).ToArray();
    int Score(LevelDef l, out SolveReport rep)
    {
        rep = null;
        foreach (Form f in eforms) l.SetBudget(f, 6);
        var st = Solver.FindShortest(l, g => g.Status == GameStatus.Won && g.StarsCollected == 3, 60000);
        if (st == null || st.Count > hi + 2) return 0;
        for (int i = 1; i < Forms.Count; i++) l.Budget[i] = 0;
        foreach (SolverStep x in st) l.Budget[(int)x.Form]++;
        GameState end = Solver.Replay(l, st);
        for (int k = 0; k < l.Items.Count; k++) if (end.HasItem(k)) l.Budget[(int)l.Items[k].Form] -= l.Items[k].Amount;
        if (l.Budget.Any(b => b < 0)) return 0;
        rep = Solver.Solve(l, 300000);
        if (rep.Truncated || !rep.CanThreeStar) return 0;
        int sc = 1;
        if (LevelValidator.Validate(l).Count == 0) sc++;
        if (rep.MinMovesThreeStars >= lo && rep.MinMovesThreeStars <= hi) sc++;
        if (rep.FinishSlack >= s0 && rep.FinishSlack <= s1) sc++;
        if (rep.ThreeStarSolutionCount <= maxS) sc++;
        if (rep.MinFoldsThreeStars >= minF) sc++;
        if (LevelQuality.Flags(rep).Count == 0 || s0 == 0) sc++;
        SolveReport r0 = rep;
        bool Changes(Action<LevelDef> strip)
        {
            var c = LevelText.Parse(LevelText.Write(l));
            strip(c);
            var r = Solver.Solve(c, 300000);
            return !(r.CanThreeStar && r.MinMovesThreeStars == r0.MinMovesThreeStars && r.ThreeStarSolutionCount == r0.ThreeStarSolutionCount && r.MinMovesToFinish == r0.MinMovesToFinish);
        }
        if (l.Items.Count == 0 || Changes(c => c.Items.Clear())) sc++;
        if (!wantLily || (l.Terrain.Contains(TerrainType.LilyPad) && Changes(c => { for (int i = 0; i < c.CellCount; i++) if (c.Terrain[i] == TerrainType.LilyPad) c.Terrain[i] = TerrainType.Water; }))) sc++;
        if (!wantFragile || (l.Fragile.Any(b => b) && Changes(c => Array.Clear(c.Fragile, 0, c.Fragile.Length)))) sc++;
        return sc;
    }
    const int MaxScore = 10;
    int cur = Score(lvl, out SolveReport curRep);
    var pal = new[] { TerrainType.Empty, TerrainType.Default, TerrainType.Water, TerrainType.LilyPad, TerrainType.Stone };
    int steps = I("steps", 4000);
    for (int step = 0; step < steps && cur < MaxScore; step++)
    {
        var c = LevelText.Parse(LevelText.Write(lvl));
        int idx = erng.Next(c.CellCount);
        Pos p = c.PosOf(idx);
        double r = erng.NextDouble();
        bool special = c.Terrain[idx] == TerrainType.Start || c.Terrain[idx] == TerrainType.End || c.Stars.Contains(p) || c.Items.Any(x => x.Position == p);
        if (r < 0.1 && c.Terrain[idx] == TerrainType.Default && !special)
        {
            int k = erng.Next(c.Stars.Count); c.Stars[k] = p;
        }
        else if (r < 0.15 && c.Items.Count > 0 && c.Terrain[idx] != TerrainType.Empty && !special)
            c.Items[0].Position = p;
        else if (r < 0.3 && c.Terrain[idx] == TerrainType.Default && !special)
            c.Fragile[idx] = !c.Fragile[idx];
        else if (!special)
        {
            c.Terrain[idx] = pal[erng.Next(pal.Length)];
            c.Fragile[idx] = false;
        }
        else continue;
        int sc = Score(c, out SolveReport rep);
        if (sc > cur || (sc == cur && sc > 0 && erng.NextDouble() < 0.5))
        {
            if (sc > cur) Console.WriteLine($"  step {step}: score {sc}/{MaxScore} F={rep.MinMovesToFinish} slack={rep.FinishSlack} L={rep.MinMovesThreeStars} sol={rep.ThreeStarSolutionCount}");
            lvl = c; cur = sc; curRep = rep;
        }
    }
    Console.WriteLine($"result: score {cur}/{MaxScore}");
    if (cur == MaxScore && opt.ContainsKey("out"))
        File.WriteAllText(opt["out"], LevelText.Write(lvl, $"эволюция: финиш {curRep.MinMovesToFinish}, запас {curRep.FinishSlack}, 3★ {curRep.MinMovesThreeStars}, решений {curRep.ThreeStarSolutionCount}, складок {curRep.MinFoldsThreeStars}"));
    return;
}

if (opt.ContainsKey("enum"))
{
    // Все маршруты на 3★ (до max штук): для проверки обходов.
    var lvl = LevelText.Parse(File.ReadAllText(opt["enum"]));
    int max = I("max", 50);
    int allMask = (1 << lvl.Stars.Count) - 1;
    var path = new List<SolverStep>();
    int n = 0;
    void Dfs(GameState s)
    {
        if (n >= max) return;
        if (s.Status == GameStatus.Won) { if ((s.StarMask & allMask) == allMask) { n++; Console.WriteLine(string.Join(", ", path)); } return; }
        if (s.Status != GameStatus.Playing) return;
        foreach (MoveOption o in Rules.LegalMoves(lvl, s))
        {
            MoveResult r = Rules.Apply(lvl, s, o.Form, o.Target);
            if (!r.Success) continue;
            path.Add(new SolverStep { Form = o.Form, Target = o.Target });
            Dfs(r.After);
            path.RemoveAt(path.Count - 1);
        }
    }
    Dfs(GameState.Initial(lvl));
    Console.WriteLine($"routes: {n}");
    return;
}

if (opt.ContainsKey("clean"))
{
    // Чистка: кувшинки без воды чинятся, мёртвые клетки убираются, затем по одной убираются клетки (камень, кувшинка, вода,
    // с land=1 и суша), без которых метрики не меняются: финиш, 3★, число решений на 3★, складки.
    var lvl = LevelText.Parse(File.ReadAllText(opt["clean"]));
    string header = string.Join("\n", File.ReadAllLines(opt["clean"]).Where(l => l.StartsWith("# идея:")));
    SolveReport baseRep = Solver.Solve(lvl, 300000);
    bool Same(LevelDef l, SolveReport r) => !r.Truncated && r.CanThreeStar && r.MinMovesThreeStars == baseRep.MinMovesThreeStars
        && r.MinMovesToFinish == baseRep.MinMovesToFinish && r.ThreeStarSolutionCount == baseRep.ThreeStarSolutionCount
        && r.MinFoldsThreeStars == baseRep.MinFoldsThreeStars;
    // 1) кувшинки без воды
    for (int i = 0; i < lvl.CellCount; i++)
    {
        if (lvl.Terrain[i] != TerrainType.LilyPad || !LilyInvalid(lvl, i)) continue;
        Pos p = lvl.PosOf(i);
        bool done = false;
        var tries = new List<Action<LevelDef>>();
        foreach (Pos d in new[] { Pos.Up, Pos.Down, Pos.Left, Pos.Right })
        {
            Pos n = p + d;
            if (lvl.InBounds(n) && lvl.Terrain[lvl.Index(n)] == TerrainType.Empty) tries.Add(l => l.Terrain[l.Index(n)] = TerrainType.Water);
        }
        tries.Add(l => l.Terrain[i] = TerrainType.Default);
        tries.Add(l => l.Terrain[i] = TerrainType.Empty);
        tries.Add(l => l.Terrain[i] = TerrainType.Water);
        foreach (var t in tries)
        {
            var copy = LevelText.Parse(LevelText.Write(lvl));
            t(copy);
            if (LilyInvalid(copy, i) && copy.Terrain[i] == TerrainType.LilyPad) continue;
            var r = Solver.Solve(copy, 300000);
            if (Same(copy, r)) { lvl = copy; done = true; Console.WriteLine($"  кувшинка {p}: исправлена"); break; }
        }
        if (!done) Console.WriteLine($"  кувшинка {p}: не исправить без смены метрик");
    }
    // 3) шум
    bool land = Get("land", "0") == "1";
    var removable = new HashSet<TerrainType> { TerrainType.Stone, TerrainType.LilyPad, TerrainType.Water, TerrainType.ThinIce, TerrainType.Bamboo, TerrainType.Ice, TerrainType.Quicksand };
    if (land) removable.Add(TerrainType.Default);
    bool changed = true;
    while (changed)
    {
        changed = false;
        for (int i = 0; i < lvl.CellCount; i++)
        {
            Pos p = lvl.PosOf(i);
            if (!removable.Contains(lvl.Terrain[i]) || lvl.Stars.Contains(p) || lvl.Items.Any(x => x.Position == p)) continue;
            var copy = LevelText.Parse(LevelText.Write(lvl));
            copy.Terrain[i] = TerrainType.Empty; copy.Fragile[i] = false;
            if (LevelValidator.Validate(copy).Count > LevelValidator.Validate(lvl).Count) continue;
            if (!Same(copy, Solver.Solve(copy, 300000))) continue;
            Console.WriteLine($"  убрана {lvl.Terrain[i]} {p}");
            lvl = copy; changed = true;
        }
        // мёртвые клетки (после чистки могли появиться новые)
        foreach (Pos p in LevelMetrics.DeadCells(lvl))
        {
            var copy = LevelText.Parse(LevelText.Write(lvl));
            int i = copy.Index(p);
            copy.Terrain[i] = TerrainType.Empty; copy.Fragile[i] = false;
            if (copy.Stars.Contains(p) || copy.Items.Any(x => x.Position == p)) continue;
            if (Same(copy, Solver.Solve(copy, 300000)) && LevelValidator.Validate(copy).Count <= LevelValidator.Validate(lvl).Count) { lvl = copy; changed = true; }
        }
    }
    if (lvl.Schedules.Count == 0 && lvl.Entities.Count == 0)
    {
        var cropped = Crop(lvl);
        if (Same(cropped, Solver.Solve(cropped, 300000)) && LevelValidator.Validate(cropped).Count == 0) lvl = cropped;
    }
    for (int i = 0; i < lvl.CellCount; i++)
        if (lvl.Terrain[i] == TerrainType.Empty) lvl.Flow[i] = Pos.Zero; // поток над пустотой не нужен
    var fin = Solver.Solve(lvl, 300000);
    Console.WriteLine($"result: F={fin.MinMovesToFinish} slack={fin.FinishSlack} L={fin.MinMovesThreeStars} sol={fin.ThreeStarSolutionCount} dead={LevelMetrics.DeadCells(lvl).Count} forg={LevelMetrics.Forgiveness(lvl):P0} errors={string.Join(" ", LevelValidator.Validate(lvl))}");
    if (opt.ContainsKey("out")) File.WriteAllText(opt["out"], (header.Length > 0 ? header + "\n" : "") + LevelText.Write(lvl));
    return;
}

if (opt.ContainsKey("tighten"))
{
    // Доводка до нулевого запаса: случайные правки клеток (тип, хрупкость, перенос End), пока кратчайший финиш не сравняется с бюджетом.
    // free=1 — бюджет пересчитывается после каждой правки, длина 3★ держится в L=a-b; nofragile=1 — убрать хрупкость, если она не нужна.
    var lvl = LevelText.Parse(File.ReadAllText(opt["tighten"]));
    var trng = new Random(I("seed", 1));
    int maxS = I("maxsol", 2);
    SolveReport cur = Solver.Solve(lvl, 300000);
    int L0 = cur.MinMovesThreeStars;
    var palette = new[] { TerrainType.Empty, TerrainType.Default, TerrainType.Water, TerrainType.Stone, TerrainType.LilyPad };
    int steps = I("steps", 3000);
    bool free = Get("free", "0") == "1";
    (int, int) tl = R("L", "10-12");
    var tforms = Get("forms", "crane,frog,plane,boat").Split(',').Select(f => Enum.Parse<Form>(f, true)).ToArray();
    // Бюджет заново по кратчайшему маршруту на 3★ (как в генераторе), листки вычитаются.
    bool Rebudget(LevelDef l)
    {
        foreach (Form f in tforms) l.SetBudget(f, 8);
        var st = Solver.FindShortest(l, g => g.Status == GameStatus.Won && g.StarsCollected == 3, 60000);
        if (st == null) return false;
        for (int i = 1; i < Forms.Count; i++) l.Budget[i] = 0;
        foreach (SolverStep x in st) l.Budget[(int)x.Form]++;
        GameState end = Solver.Replay(l, st);
        if (end == null) return false;
        for (int k = 0; k < l.Items.Count; k++) if (end.HasItem(k)) l.Budget[(int)l.Items[k].Form] -= l.Items[k].Amount;
        return l.Budget.All(b => b >= 0);
    }
    for (int step = 0; step < steps && cur.FinishSlack > 0; step++)
    {
        var backup = LevelText.Parse(LevelText.Write(lvl));
        int idx = trng.Next(lvl.CellCount);
        Pos p = lvl.PosOf(idx);
        if (lvl.Terrain[idx] == TerrainType.Start || lvl.Stars.Contains(p) || lvl.Items.Any(x => x.Position == p)) continue;
        if (lvl.Terrain[idx] == TerrainType.End) continue;
        if (trng.NextDouble() < 0.15 && lvl.Terrain[idx] == TerrainType.Default)
        {
            int e = Array.IndexOf(lvl.Terrain, TerrainType.End);
            lvl.Terrain[e] = TerrainType.Default; lvl.Terrain[idx] = TerrainType.End; lvl.Fragile[idx] = false;
        }
        else if (trng.NextDouble() < 0.1 && lvl.Terrain[idx] == TerrainType.Default) lvl.Fragile[idx] = !lvl.Fragile[idx];
        else { lvl.Terrain[idx] = palette[trng.Next(palette.Length)]; if (lvl.Terrain[idx] != TerrainType.Default) lvl.Fragile[idx] = false; }
        if (free && !Rebudget(lvl)) { lvl = backup; continue; }
        var r = Solver.Solve(lvl, 300000);
        bool lenOk = free ? r.MinMovesThreeStars >= tl.Item1 && r.MinMovesThreeStars <= tl.Item2 : r.MinMovesThreeStars == L0;
        bool ok = !r.Truncated && r.CanThreeStar && lenOk && r.ThreeStarSolutionCount <= maxS && r.MinFoldsThreeStars >= I("minfolds", 0)
            && LevelValidator.Validate(lvl).Count == 0
            && (r.FinishSlack < cur.FinishSlack || (r.FinishSlack == cur.FinishSlack && trng.NextDouble() < 0.5));
        if (ok)
        {
            if (r.FinishSlack < cur.FinishSlack) Console.WriteLine($"  step {step}: F={r.MinMovesToFinish} slack={r.FinishSlack} sol={r.ThreeStarSolutionCount}");
            cur = r;
        }
        else lvl = backup;
    }
    Console.WriteLine($"result: F={cur.MinMovesToFinish} slack={cur.FinishSlack} L={cur.MinMovesThreeStars} sol={cur.ThreeStarSolutionCount} folds={cur.MinFoldsThreeStars}");
    {
        var pruned = Prune(lvl);
        var rp = Solver.Solve(pruned, 300000);
        if (rp.MinMovesThreeStars == cur.MinMovesThreeStars && rp.MinMovesToFinish == cur.MinMovesToFinish && rp.ThreeStarSolutionCount == cur.ThreeStarSolutionCount)
        { lvl = pruned; cur = rp; Console.WriteLine("pruned"); }
    }
    if (Get("nofragile", "0") == "1")
    {
        var nf = LevelText.Parse(LevelText.Write(lvl));
        Array.Clear(nf.Fragile, 0, nf.Fragile.Length);
        var rn = Solver.Solve(nf, 300000);
        Console.WriteLine($"without fragile: F={rn.MinMovesToFinish} slack={rn.FinishSlack} L={rn.MinMovesThreeStars} sol={rn.ThreeStarSolutionCount}");
        if (rn.CanThreeStar && rn.MinMovesThreeStars == cur.MinMovesThreeStars && rn.FinishSlack == 0 && rn.ThreeStarSolutionCount <= maxS) { lvl = nf; cur = rn; }
    }
    if (cur.FinishSlack == 0 && opt.ContainsKey("out"))
        File.WriteAllText(opt["out"], LevelText.Write(lvl, $"доводка до нулевого запаса: финиш {cur.MinMovesToFinish}, 3★ {cur.MinMovesThreeStars}, решений {cur.ThreeStarSolutionCount}\n3★: {string.Join(", ", cur.ThreeStarPath)}"));
    return;
}

int w = I("w", 5), h = I("h", 5);
Form[] forms = Get("forms", "crane").Split(',').Select(f => Enum.Parse<Form>(f, true)).ToArray();
double pEmpty = D("empty", 0.25), pWater = D("water", 0), pStone = D("stone", 0), pLily = D("lily", 0), pFire = D("fire", 0), pFragile = D("fragile", 0);
double pThinIce = D("thinice", 0), pBamboo = D("bamboo", 0), pIce = D("ice", 0), pQuick = D("quicksand", 0);
int winds = I("winds", 0);
bool needWind = Get("needwind", "0") == "1", needQuick = Get("needquick", "0") == "1";
bool needThinIce = Get("needthinice", "0") == "1", needBamboo = Get("needbamboo", "0") == "1";
int leaves = I("leaves", 0);
(int lMin, int lMax) = R("L", "4-6");
(int sMin, int sMax) = R("slack", "1-3");
int maxSol = I("maxsol", 2);
double minRatio = D("ratio", 0.5);
int iters = I("iters", 200000), want = I("count", 5), seed = I("seed", 1);
bool needLily = Get("needlily", "0") == "1", needFragile = Get("needfragile", "0") == "1";
int minFolds = I("minfolds", forms.Length);
string outDir = Get("out", "candidates");
int searchBudget = I("fb", Math.Min(lMax, lMax / forms.Length + 3));
int stateLimit = I("limit", 60000);
string prefix = Get("name", "cand");
bool waterStars = Get("waterstars", "0") == "1";
double minForg = D("forg", 0), maxForg = D("forgmax", 1);
int streams = I("streams", 0), rafts = I("rafts", 0), tideCells = I("tide", 0), tidePeriodMax = I("tideperiod", 3);
bool needCarry = Get("needcarry", "0") == "1";
int boxes = I("boxes", 0), plates = I("plates", 0), gates = I("gates", 0);
int guards = I("guards", 0), toriiPairs = I("torii", 0);
bool needGuard = Get("needguard", "0") == "1", needTorii = Get("needtorii", "0") == "1";
bool needGatePass = Get("needgatepass", "0") == "1";
bool needBox = Get("needbox", "0") == "1", needGate = Get("needgate", "0") == "1", needPush = Get("needpush", "0") == "1", needPress = Get("needpress", "0") == "1";
bool needBoxPress = Get("needboxpress", "0") == "1"; // короб на плите в момент прохода через ворота
bool needFlow = Get("needflow", "0") == "1", needRaft = Get("needraft", "0") == "1", needTide = Get("needtide", "0") == "1";
Pos[] Dirs = { Pos.Up, Pos.Down, Pos.Left, Pos.Right };
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
        else if ((r -= pThinIce) < 0) t = TerrainType.ThinIce;
        else if ((r -= pBamboo) < 0) t = TerrainType.Bamboo;
        else if ((r -= pIce) < 0) t = TerrainType.Ice;
        else if ((r -= pQuick) < 0) t = TerrainType.Quicksand;
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
    for (int i = 0; i < level.CellCount; i++)
        if (level.Terrain[i] == TerrainType.LilyPad && LilyInvalid(level, i)) level.Terrain[i] = TerrainType.Default;
    if (needLily && !level.Terrain.Contains(TerrainType.LilyPad)) continue;

    // Океан: течения (случайные блуждания по воде), плоты, прилив.
    var water = Enumerable.Range(0, level.CellCount).Where(i => level.Terrain[i] == TerrainType.Water).ToList();
    for (int k = 0; k < streams && water.Count > 0; k++)
    {
        Pos c = level.PosOf(water[rng.Next(water.Count)]);
        Pos d = Dirs[rng.Next(4)];
        int len = 2 + rng.Next(5);
        for (int step = 0; step < len && level.InBounds(c) && level.Terrain[level.Index(c)] == TerrainType.Water; step++)
        {
            if (rng.NextDouble() < 0.3) d = Dirs[rng.Next(4)];
            level.Flow[level.Index(c)] = d;
            c += d;
        }
    }
    // Пустыня: ветер — прямые полосы по суше.
    var plain = Enumerable.Range(0, level.CellCount).Where(i => level.Terrain[i] == TerrainType.Default).ToList();
    for (int k = 0; k < winds && plain.Count > 0; k++)
    {
        Pos c = level.PosOf(plain[rng.Next(plain.Count)]);
        Pos d = Dirs[rng.Next(4)];
        int len = 2 + rng.Next(3);
        for (int step = 0; step < len && level.InBounds(c) && level.Terrain[level.Index(c)] == TerrainType.Default; step++)
        {
            level.Flow[level.Index(c)] = d;
            c += d;
        }
    }
    var freeWater = water.Where(i => level.StarIndex(level.PosOf(i)) < 0).OrderBy(_ => rng.Next()).ToList();
    if (freeWater.Count < rafts) continue;
    for (int k = 0; k < rafts; k++) level.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = level.PosOf(freeWater[k]) });
    if (tideCells > 0)
    {
        var cand = Enumerable.Range(0, level.CellCount)
            .Where(i => (level.Terrain[i] == TerrainType.Water || level.Terrain[i] == TerrainType.Default) && level.Items.All(x => x.Position != level.PosOf(i)))
            .OrderBy(_ => rng.Next()).Take(tideCells).ToList();
        foreach (int i in cand) level.Terrain[i] = rng.NextDouble() < 0.5 ? TerrainType.Water : TerrainType.Sand;
        level.Schedules.Add(ScheduleDef.Tide(1 + rng.Next(tidePeriodMax), cand.Select(level.PosOf)));
    }

    // Пещера: коробы, плиты, ворота на случайной суше.
    {
        var landFree = Enumerable.Range(0, level.CellCount)
            .Where(i => level.Terrain[i] == TerrainType.Default && !level.Fragile[i] && level.StarIndex(level.PosOf(i)) < 0 && level.ItemIndex(level.PosOf(i)) < 0)
            .OrderBy(_ => rng.Next()).ToList();
        if (landFree.Count < boxes + plates + gates) continue;
        int q = 0;
        for (int k = 0; k < boxes; k++) level.Entities.Add(new EntityDef { Kind = EntityKind.Box, Position = level.PosOf(landFree[q++]) });
        for (int k = 0; k < plates; k++) level.Terrain[landFree[q++]] = TerrainType.Plate;
        for (int k = 0; k < gates; k++) level.Terrain[landFree[q++]] = TerrainType.Gate;
    }

    // Храм: стражи на маршрутах (квадрат 2x2 или отрезок) и пары тории.
    {
        bool Free(Pos p) => level.InBounds(p) && level.Terrain[level.Index(p)] == TerrainType.Default && !level.Fragile[level.Index(p)]
            && level.StarIndex(p) < 0 && level.Entities.All(e => e.Position != p) && level.Routes.All(rt => !rt.Contains(p));
        bool okTemple = true;
        for (int k = 0; k < guards && okTemple; k++)
        {
            List<Pos> route = null;
            for (int tries = 0; tries < 30 && route == null; tries++)
            {
                var o = new Pos(rng.Next(w), rng.Next(h));
                List<Pos> cand;
                if (rng.NextDouble() < 0.5) cand = new List<Pos> { o, o + Pos.Right, o + Pos.Right + Pos.Up, o + Pos.Up };
                else { Pos d = Dirs[rng.Next(4)]; int len = 2 + rng.Next(3); cand = Enumerable.Range(0, len).Select(j => new Pos(o.X + d.X * j, o.Y + d.Y * j)).ToList(); }
                if (cand.All(Free)) route = cand;
            }
            if (route == null) { okTemple = false; break; }
            level.Routes.Add(route);
            level.Entities.Add(new EntityDef { Kind = EntityKind.Guard, Position = route[0], Direction = new Pos(rng.Next(2) == 0 ? 1 : -1, 0), Data = level.Routes.Count - 1 });
        }
        if (!okTemple) continue;
        var tc = Enumerable.Range(0, level.CellCount).Select(level.PosOf).Where(Free).OrderBy(_ => rng.Next()).ToList();
        if (tc.Count < toriiPairs * 2) continue;
        for (int k = 0; k < toriiPairs; k++)
        {
            level.Terrain[level.Index(tc[2 * k])] = TerrainType.Torii;
            level.Terrain[level.Index(tc[2 * k + 1])] = TerrainType.Torii;
            level.ToriiPairs.Add((tc[2 * k], tc[2 * k + 1]));
        }
    }

    foreach (Form f in forms) level.SetBudget(f, searchBudget);
    var steps = Solver.FindShortest(level, s => s.Status == GameStatus.Won && s.StarsCollected == 3, stateLimit);
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
    if (LevelValidator.Validate(level).Count > 0) continue;
    if (minForg > 0 || maxForg < 1)
    {
        double fg = LevelMetrics.Forgiveness(level);
        if (fg < minForg || fg > maxForg) continue;
    }
    bool Matters(Action<LevelDef> strip)
    {
        var copy = LevelText.Parse(LevelText.Write(level));
        strip(copy);
        var r2 = Solver.Solve(copy, 300000);
        return !(r2.CanThreeStar && r2.MinMovesThreeStars == rep.MinMovesThreeStars && r2.ThreeStarSolutionCount == rep.ThreeStarSolutionCount && r2.MinMovesToFinish == rep.MinMovesToFinish);
    }
    if (needFlow && !Matters(c => Array.Clear(c.Flow, 0, c.Flow.Length))) continue;
    if (needGuard && !Matters(c => c.Entities.RemoveAll(e => e.Kind == EntityKind.Guard))) continue;
    if (needTorii && !Matters(c => { for (int i = 0; i < c.CellCount; i++) if (c.Terrain[i] == TerrainType.Torii) c.Terrain[i] = TerrainType.Default; c.ToriiPairs.Clear(); })) continue;
    if (needWind && !Matters(c => Array.Clear(c.Flow, 0, c.Flow.Length))) continue;
    if (needQuick && !Matters(c => { for (int i = 0; i < c.CellCount; i++) if (c.Terrain[i] == TerrainType.Quicksand) c.Terrain[i] = TerrainType.Default; })) continue;
    if (needThinIce && !Matters(c => { for (int i = 0; i < c.CellCount; i++) if (c.Terrain[i] == TerrainType.ThinIce) c.Terrain[i] = TerrainType.Default; })) continue;
    if (needBamboo && !Matters(c => { for (int i = 0; i < c.CellCount; i++) if (c.Terrain[i] == TerrainType.Bamboo) c.Terrain[i] = TerrainType.Stone; })) continue;
    if (needRaft && (level.Entities.Count == 0 || !Matters(c => c.Entities.Clear()))) continue;
    if (needBox && !Matters(c => c.Entities.RemoveAll(e => e.Kind == EntityKind.Box))) continue;
    if (needGate && !Matters(c => { for (int i = 0; i < c.CellCount; i++) if (c.Terrain[i] == TerrainType.Gate) c.Terrain[i] = TerrainType.Default; })) continue;
    if (needPush || needPress || needGatePass || needBoxPress)
    {
        bool pushed = false, pressed = false, passedGate = false, boxPass = false;
        GameState gs = GameState.Initial(level);
        foreach (SolverStep st in rep.ThreeStarPath)
        {
            MoveResult m = Rules.Apply(level, gs, st.Form, st.Target);
            foreach (MoveEvent e in m.Events)
                if (e.Type == MoveEventType.EntityMoved && gs.Entities[e.EntityIndex].Kind == EntityKind.Box) pushed = true;
            bool boxHeld = false;
            foreach (EntityState be in gs.Entities)
                if (be.Kind == EntityKind.Box && level.Terrain[level.Index(be.Position)] == TerrainType.Plate) boxHeld = true;
            foreach (Pos pp in m.Path)
                if (level.Terrain[level.Index(pp)] == TerrainType.Gate) { passedGate = true; if (boxHeld) boxPass = true; }
            gs = m.After;
            if (Rules.PlatePressed(level, gs)) pressed = true;
        }
        if (needPush && !pushed) continue;
        if (needPress && !pressed) continue;
        if (needGatePass && !passedGate) continue;
        if (needBoxPress && !boxPass) continue;
    }
    if (needCarry)
    {
        // Маршрут на 3★ должен прокатиться на плоту.
        bool carried = false;
        GameState gs = GameState.Initial(level);
        foreach (SolverStep st in rep.ThreeStarPath)
        {
            MoveResult m = Rules.Apply(level, gs, st.Form, st.Target);
            if (m.Events.Any(e => e.Type == MoveEventType.Carried)) carried = true;
            gs = m.After;
        }
        if (!carried) continue;
    }
    if (needTide && (level.Schedules.Count == 0 || !Matters(c => c.Schedules.Clear()))) continue;
    if (needLily)
    {
        var copy = LevelText.Parse(LevelText.Write(level));
        for (int i = 0; i < copy.CellCount; i++) if (copy.Terrain[i] == TerrainType.LilyPad) copy.Terrain[i] = TerrainType.Water;
        var r2 = Solver.Solve(copy, 300000);
        if (r2.CanThreeStar && r2.MinMovesThreeStars == rep.MinMovesThreeStars && r2.ThreeStarSolutionCount == rep.ThreeStarSolutionCount && r2.MinMovesToFinish == rep.MinMovesToFinish) continue; // кувшинки ничего не меняют
    }
    found++;
    string comment = $"кандидат: финиш {rep.MinMovesToFinish}, запас {rep.FinishSlack}, 3★ {rep.MinMovesThreeStars}, решений {rep.ThreeStarSolutionCount}, складок {rep.MinFoldsThreeStars}\n3★: {string.Join(", ", rep.ThreeStarPath)}";
    File.WriteAllText(Path.Combine(outDir, level.Name + LevelText.Extension), LevelText.Write(level, comment));
    Console.WriteLine($"{level.Name}: it={it} F={rep.MinMovesToFinish} slack={rep.FinishSlack} L={rep.MinMovesThreeStars} sol={rep.ThreeStarSolutionCount} folds={rep.MinFoldsThreeStars}");
}
Console.WriteLine($"found {found}");

// Обрезка поля до прямоугольника непустых клеток.
static LevelDef Crop(LevelDef l)
{
    int x0 = l.Width, y0 = l.Height, x1 = -1, y1 = -1;
    for (int i = 0; i < l.CellCount; i++)
    {
        if (l.Terrain[i] == TerrainType.Empty) continue;
        Pos p = l.PosOf(i);
        x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y);
    }
    if (x1 < 0 || l.Schedules.Count > 0) return l; // расписания не переносим — без обрезки
    var c = new LevelDef(x1 - x0 + 1, y1 - y0 + 1) { Name = l.Name, Rules = l.Rules };
    var off = new Pos(x0, y0);
    for (int i = 0; i < c.CellCount; i++)
    {
        Pos p = c.PosOf(i) + off;
        c.Terrain[i] = l.Terrain[l.Index(p)];
        c.Fragile[i] = l.Fragile[l.Index(p)];
        c.Flow[i] = l.Flow[l.Index(p)];
    }
    foreach (Pos s in l.Stars) c.Stars.Add(s - off);
    foreach (ItemDef it in l.Items) c.Items.Add(new ItemDef { Kind = it.Kind, Position = it.Position - off, Form = it.Form, Amount = it.Amount });
    for (int i = 0; i < l.Budget.Length; i++) c.Budget[i] = l.Budget[i];
    foreach (EntityDef e in l.Entities) c.Entities.Add(new EntityDef { Kind = e.Kind, Position = e.Position - off, Direction = e.Direction, Data = e.Data });
    foreach (List<Pos> rt in l.Routes) c.Routes.Add(rt.Select(p => p - off).ToList());
    foreach ((Pos a, Pos b) in l.ToriiPairs) c.ToriiPairs.Add((a - off, b - off));
    return c;
}

static bool LilyInvalid(LevelDef l, int i)
{
    Pos p = l.PosOf(i);
    foreach (Pos d in new[] { Pos.Up, Pos.Down, Pos.Left, Pos.Right })
    {
        Pos n = p + d;
        if (l.InBounds(n) && l.Terrain[l.Index(n)] == TerrainType.Water) return false;
    }
    return true;
}

// Клетки, на которые фигурка не попадает ни в одном достижимом состоянии, делаются пустыми.
static LevelDef Prune(LevelDef level)
{
    var used = LevelMetrics.UsedCells(level);
    foreach (ScheduleDef sch in level.Schedules) foreach (Pos t in sch.Targets) used.Add(t);
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
