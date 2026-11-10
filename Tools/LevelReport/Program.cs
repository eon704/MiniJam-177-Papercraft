using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Paperbound.Core;
using Paperbound.Tools;

// Команды:
//   dotnet run --project Tools/LevelReport -- report [<папка .asset>] [<выход .md>]   отчёт по уровням игры (новые и старые правила)
//   dotnet run --project Tools/LevelReport -- check <файл.pbl|.asset> ...           проверить уровень(и)
//   dotnet run --project Tools/LevelReport -- export <папка .asset> <папка Json> <выход>   экспорт в .pbl + сводная таблица
string root = FindRepoRoot();
string command = args.Length > 0 ? args[0] : "report";
string[] rest = args.Skip(1).ToArray();

switch (command)
{
    case "report":
        Report(rest.Length > 0 ? rest[0] : Path.Combine(root, "Assets/Data/Levels"), rest.Length > 1 ? rest[1] : null);
        return 0;
    case "check":
        if (rest.Length == 0) { Console.Error.WriteLine("check: укажите файл(ы) .pbl или .asset"); return 2; }
        bool ok = true;
        foreach (string f in rest) ok &= Check(f);
        return ok ? 0 : 1;
    case "export":
        if (rest.Length < 3) { Console.Error.WriteLine("export <папка .asset> <папка Json> <выход>"); return 2; }
        Export(rest[0], rest[1], rest[2]);
        return 0;
    case "summary":
        if (rest.Length < 1) { Console.Error.WriteLine("summary <папка .pbl> [<выход .md>]"); return 2; }
        Summary(rest[0], rest.Length > 1 ? rest[1] : null);
        return 0;
    case "trace":
        if (rest.Length < 1) { Console.Error.WriteLine("trace <файл .pbl> — проиграть маршрут на 3★ по шагам"); return 2; }
        Trace(rest[0]);
        return 0;
    default:
        Console.Error.WriteLine($"Неизвестная команда «{command}». Есть: report, check, export, summary, trace.");
        return 2;
}

static LevelDef Load(string path) =>
    path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
        ? LevelAssetReader.Read(path)
        : LevelText.Parse(File.ReadAllText(path));

static bool Check(string path)
{
    LevelDef level;
    try { level = Load(path); }
    catch (Exception e) { Console.WriteLine($"{path}: ошибка чтения: {e.Message}"); return false; }

    Console.WriteLine($"== {path} ({level.Width}x{level.Height})");
    Console.Write(LevelAssetReader.Render(level));
    List<string> errors = LevelValidator.Validate(level);
    foreach (string e in errors) Console.WriteLine("  ошибка: " + e);

    SolveReport r = Solver.Solve(level);
    Console.WriteLine($"  бюджет: {level.TotalBudget} ({string.Join(" ", Forms.Movable.Select(f => $"{LevelText.FormName(f)}={level.Budget[(int)f]}"))})");
    Console.WriteLine(r.CanFinish
        ? $"  финиш: {r.MinMovesToFinish} ход., запас {r.FinishSlack}, макс. звёзд на финише {r.MaxStarsAtFinish}"
        : "  финиш: недостижим");
    Console.WriteLine(r.CanThreeStar
        ? $"  3★: {r.MinMovesThreeStars} ход., минимум складок {r.MinFoldsThreeStars}"
        : "  3★: невозможно");
    if (r.CanFinish)
        Console.WriteLine($"  решений: {r.FinishSolutionCount} до финиша, {r.ThreeStarSolutionCount} на 3★{(r.CountSaturated ? " (больше лимита)" : "")}");
    if (r.FinishPath != null) Console.WriteLine("  кратчайшее до финиша: " + string.Join(", ", r.FinishPath));
    if (r.ThreeStarPath != null) Console.WriteLine("  кратчайшее на 3★: " + string.Join(", ", r.ThreeStarPath));
    if (r.MinFoldsPath != null) Console.WriteLine("  меньше всего складок: " + string.Join(", ", r.MinFoldsPath));
    List<string> flags = LevelQuality.Flags(r);
    Console.WriteLine(flags.Count == 0 ? "  качество: ок" : "  качество: " + string.Join("; ", flags));
    List<Pos> dead = LevelMetrics.DeadCells(level);
    Console.WriteLine($"  мёртвые клетки: {dead.Count} ({LevelMetrics.DeadShare(level):P0}){(dead.Count > 0 ? " " + string.Join(" ", dead) : "")}");
    double forg = LevelMetrics.Forgiveness(level, out int good, out int total);
    Console.WriteLine($"  прощение (первые два хода): {forg:P0} ({good}/{total})");
    Console.WriteLine($"  состояний: {r.StatesExplored}");
    return errors.Count == 0 && r.CanThreeStar;
}

static void Trace(string path)
{
    LevelDef level = Load(path);
    SolveReport r = Solver.Solve(level);
    if (r.ThreeStarPath == null) { Console.WriteLine("3★ недостижимы"); return; }
    GameState s = GameState.Initial(level);
    Console.Write(LevelAssetReader.Render(level, s));
    foreach (SolverStep st in r.ThreeStarPath)
    {
        MoveResult m = Rules.Apply(level, s, st.Form, st.Target);
        s = m.After;
        Console.WriteLine($"-- {st}  [{string.Join(" ", m.Events.Where(e => e.Type != MoveEventType.Enter))}]");
        Console.Write(LevelAssetReader.Render(level, s));
    }
}

static string Row(LevelDef level, SolveReport r, string extra)
{
    string budget = string.Join("/", new[] { Form.Crane, Form.Plane, Form.Boat, Form.Frog }.Select(f => level.Budget[(int)f]));
    string count = r.CanFinish ? $"{r.FinishSolutionCount} / {r.ThreeStarSolutionCount}{(r.CountSaturated ? "+" : "")}" : "—";
    List<string> flags = LevelQuality.Flags(r);
    List<string> errors = LevelValidator.Validate(level);
    if (errors.Count > 0) flags.Insert(0, "невалиден");
    return $"| {level.Name} | {level.Width}x{level.Height} | {budget} | {(r.CanFinish ? r.MinMovesToFinish.ToString() : "нет")} | {(r.CanFinish ? r.FinishSlack.ToString() : "—")} | {(r.CanFinish ? r.MaxStarsAtFinish.ToString() : "—")} | {(r.CanThreeStar ? r.MinMovesThreeStars.ToString() : "нет")} | {(r.MinFoldsThreeStars >= 0 ? r.MinFoldsThreeStars.ToString() : "—")} | {count} | {(flags.Count == 0 ? "ок" : string.Join("; ", flags))} |{extra}";
}

const string Header = "| Уровень | Поле | Бюджет C/P/B/F | Финиш, ходов | Запас | Макс. ★ на финише | 3★, ходов | Мин. складок | Решений (финиш / 3★) | Качество |";
const string Divider = "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |";

void Report(string levelsDir, string output)
{
    var files = AssetFiles(levelsDir);
    var sb = new StringBuilder();
    sb.AppendLine("# Отчёт решателя по уровням");
    sb.AppendLine();
    sb.AppendLine("«Новые» правила — решения плана от 2026-11-10 (скользкий лёд, лодка собирает звёзды). «Старые» — поведение игры до переработки.");
    sb.AppendLine($"Флаги качества: «{LevelQuality.FinishTooCheap}» — финиш короче трети маршрута на 3★ или запас больше {LevelQuality.MaxSlack}; «{LevelQuality.NoSlack}» — бюджет ровно на кратчайший финиш.");
    sb.AppendLine();
    sb.AppendLine(Header.Replace("| Поле |", "| Правила |").TrimEnd() + " Сохранённое решение |");
    sb.AppendLine(Divider + " --- |");

    var details = new StringBuilder();
    foreach (string file in files)
    {
        foreach ((string name, RuleSet rules) in new[] { ("новые", RuleSet.Default), ("старые", RuleSet.Legacy) })
        {
            LevelDef level = LevelAssetReader.Read(file, rules);
            SolveReport r = Solver.Solve(level);
            List<SolverStep> cached = LevelAssetReader.ReadCachedSolution(file);
            string cachedVerdict;
            if (cached.Count == 0) cachedVerdict = "нет";
            else
            {
                GameState end = Solver.Replay(level, cached);
                cachedVerdict = end == null ? "ход нелегален"
                    : end.Status == GameStatus.Won ? $"победа, {end.StarsCollected}★"
                    : end.Status == GameStatus.Dead ? "смерть" : "не дошёл";
            }
            string row = Row(level, r, $" {cachedVerdict} |");
            // В основной таблице вместо размера поля — набор правил.
            row = row.Replace($"| {level.Width}x{level.Height} |", $"| {name} |");
            sb.AppendLine(row);

            if (name == "новые")
            {
                details.AppendLine($"## Уровень {level.Name}");
                details.AppendLine();
                details.AppendLine("```");
                details.Append(LevelAssetReader.Render(level));
                details.AppendLine("```");
                if (r.ThreeStarPath != null) details.AppendLine("Кратчайшее на 3★: " + string.Join(", ", r.ThreeStarPath));
                if (r.MinFoldsPath != null) details.AppendLine($"Минимум складок ({r.MinFoldsThreeStars}): " + string.Join(", ", r.MinFoldsPath));
                details.AppendLine($"Состояний: {r.StatesExplored}");
                details.AppendLine();
            }
        }
    }
    sb.AppendLine();
    sb.AppendLine("Карты: верхняя строка — наибольший y. `S` старт, `E` финиш, `*` звезда, `w` звезда на воде, `f` хрупкая, `~` вода, `R` камень, `F` огонь, `V` вулкан, `I` лёд, `P` кувшинка, `#` пусто.");
    sb.AppendLine();
    sb.Append(details);
    if (output != null) File.WriteAllText(output, sb.ToString());
    Console.Write(sb.ToString());
}

void Export(string assetsDir, string jsonDir, string outDir)
{
    Directory.CreateDirectory(Path.Combine(outDir, "game"));
    Directory.CreateDirectory(Path.Combine(outDir, "archive"));
    var sb = new StringBuilder();
    sb.AppendLine("# Сводка: уровни игры и архивные карты в формате .pbl");
    sb.AppendLine();
    sb.AppendLine("Новые правила. Файлы — рядом, в `game/` и `archive/`. Проверка одного файла: `dotnet run --project Tools/LevelReport -- check <файл>`.");
    sb.AppendLine($"Флаги: «{LevelQuality.FinishTooCheap}» — финиш короче трети маршрута на 3★ или запас больше {LevelQuality.MaxSlack}; «{LevelQuality.NoSlack}» — бюджет ровно на кратчайший финиш; «невалиден» — не проходит валидатор (например, не 3 звезды).");
    sb.AppendLine();

    sb.AppendLine("## Уровни игры (Assets/Data/Levels)");
    sb.AppendLine();
    sb.AppendLine(Header);
    sb.AppendLine(Divider);
    foreach (string file in AssetFiles(assetsDir))
    {
        LevelDef level = LevelAssetReader.Read(file);
        level.Name = "game-" + level.Name;
        File.WriteAllText(Path.Combine(outDir, "game", level.Name + LevelText.Extension),
            LevelText.Write(level, $"Экспорт из Assets/Data/Levels/{Path.GetFileName(file)}"));
        sb.AppendLine(Row(level, Solver.Solve(level), ""));
    }

    sb.AppendLine();
    sb.AppendLine("## Архив джем-версии (Assets/Data/Levels/Json)");
    sb.AppendLine();
    sb.AppendLine(Header);
    sb.AppendLine(Divider);
    var jsonFiles = Directory.GetFiles(jsonDir, "*.json")
        .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f).Replace("Level_", ""), out int n) ? n : int.MaxValue);
    foreach (string file in jsonFiles)
    {
        LevelDef level;
        try { level = ReadLegacyJson(file); }
        catch (Exception e) { sb.AppendLine($"| {Path.GetFileName(file)} | ошибка чтения: {e.Message} |"); continue; }
        File.WriteAllText(Path.Combine(outDir, "archive", level.Name + LevelText.Extension),
            LevelText.Write(level, $"Экспорт из Assets/Data/Levels/Json/{Path.GetFileName(file)}"));
        sb.AppendLine(Row(level, Solver.Solve(level), ""));
    }

    File.WriteAllText(Path.Combine(outDir, "summary.md"), sb.ToString());
    Console.Write(sb.ToString());
}

static void Summary(string dir, string output)
{
    var sb = new StringBuilder();
    sb.AppendLine("| Уровень | Поле | Бюджет C/P/B/F | Финиш | Запас | 3★ | Финиш/3★ | Решений 3★ | Складок | Хрупкие | Кувшинки | Листки | Мёртвые | Прощение | Качество |");
    sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
    var cards = new StringBuilder();
    foreach (string file in Directory.GetFiles(dir, "*" + LevelText.Extension).OrderBy(f => f))
    {
        string text = File.ReadAllText(file);
        LevelDef level = LevelText.Parse(text);
        SolveReport r = Solver.Solve(level);
        List<string> flags = LevelQuality.Flags(r);
        List<string> errs = LevelValidator.Validate(level);
        if (errs.Count > 0) flags.Insert(0, "невалиден: " + string.Join(" ", errs));
        string budget = string.Join("/", new[] { Form.Crane, Form.Plane, Form.Boat, Form.Frog }.Select(f => level.Budget[(int)f]));
        int fragile = level.Fragile.Count(b => b);
        int lily = level.Terrain.Count(t => t == TerrainType.LilyPad);
        string ratio = r.CanThreeStar && r.CanFinish ? $"{(double)r.MinMovesToFinish / r.MinMovesThreeStars:0.00}" : "—";
        sb.AppendLine($"| {level.Name} | {level.Width}x{level.Height} | {budget} | {(r.CanFinish ? r.MinMovesToFinish : -1)} | {r.FinishSlack} | {(r.CanThreeStar ? r.MinMovesThreeStars : -1)} | {ratio} | {r.ThreeStarSolutionCount} | {r.MinFoldsThreeStars} | {fragile} | {lily} | {level.Items.Count} | {LevelMetrics.DeadCells(level).Count} | {LevelMetrics.Forgiveness(level):P0} | {(flags.Count == 0 ? "ок" : string.Join("; ", flags))} |");
        string idea = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("# идея:"));
        cards.AppendLine($"### {level.Name}");
        cards.AppendLine();
        if (idea != null) cards.AppendLine(idea.Substring(2).Trim());
        cards.AppendLine();
        cards.AppendLine("```");
        cards.Append(LevelAssetReader.Render(level));
        cards.AppendLine("```");
        if (r.ThreeStarPath != null) cards.AppendLine("Решение на 3★: " + string.Join(", ", r.ThreeStarPath));
        cards.AppendLine();
    }
    string result = sb + "\n" + cards;
    if (output != null) File.WriteAllText(output, result);
    Console.Write(sb.ToString());
}

static LevelDef ReadLegacyJson(string file)
{
    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
    JsonElement rootEl = doc.RootElement;
    int w = rootEl.GetProperty("MapSize").GetProperty("x").GetInt32();
    int h = rootEl.GetProperty("MapSize").GetProperty("y").GetInt32();
    string map = rootEl.GetProperty("Map").GetString();
    var budget = new int[Forms.Count];
    if (rootEl.TryGetProperty("StartMovesPerForm", out JsonElement moves))
        foreach (JsonElement e in moves.EnumerateArray())
            budget[e.GetProperty("State").GetInt32()] = e.GetProperty("Moves").GetInt32();
    string name = "archive-" + Path.GetFileNameWithoutExtension(file).Replace("Level_", "").ToLowerInvariant();
    return LevelText.FromLegacyMap(w, h, map, budget, name);
}

static List<string> AssetFiles(string dir) => Directory.GetFiles(dir, "*.asset")
    .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out int n) ? n : int.MaxValue)
    .ToList();

static string FindRepoRoot()
{
    string dir = AppContext.BaseDirectory;
    while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets")))
        dir = Path.GetDirectoryName(dir);
    return dir ?? Directory.GetCurrentDirectory();
}
