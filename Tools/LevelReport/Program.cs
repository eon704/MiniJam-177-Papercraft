using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Paperbound.Core;
using Paperbound.Tools;

// Отчёт решателя по всем уровням: dotnet run --project Tools/LevelReport [-- <папка уровней> [<выходной .md>]]
string root = FindRepoRoot();
string levelsDir = args.Length > 0 ? args[0] : Path.Combine(root, "Assets/Data/Levels");
string output = args.Length > 1 ? args[1] : null;

var files = Directory.GetFiles(levelsDir, "*.asset")
    .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out int n) ? n : int.MaxValue)
    .ToList();

var sb = new StringBuilder();
sb.AppendLine("# Отчёт решателя по уровням");
sb.AppendLine();
sb.AppendLine("Правила «новые» — решения плана от 2026-11-10 (скользкий лёд, лодка собирает звёзды). «Старые» — поведение игры до переработки.");
sb.AppendLine();
sb.AppendLine("| Уровень | Правила | Бюджет | Финиш, ходов | Запас | Макс. звёзд | 3 звезды, ходов | Мин. складок | Решений (финиш / 3★) | Сохранённое решение |");
sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

var details = new StringBuilder();

foreach (string file in files)
{
    foreach ((string name, RuleSet rules) in new[] { ("новые", RuleSet.Default), ("старые", RuleSet.Legacy) })
    {
        LevelDef level = LevelAssetReader.Read(file, rules);
        List<string> errors = LevelValidator.Validate(level);
        SolveReport r = Solver.Solve(level);

        List<SolverStep> cached = LevelAssetReader.ReadCachedSolution(file);
        string cachedVerdict;
        if (cached.Count == 0) cachedVerdict = "нет";
        else
        {
            GameState end = Solver.Replay(level, cached);
            cachedVerdict = end == null ? "ход нелегален"
                : end.Status == GameStatus.Won ? $"победа, {end.StarsCollected}★"
                : end.Status == GameStatus.Dead ? "смерть"
                : "не дошёл";
        }

        string budget = string.Join("/", new[] { Form.Crane, Form.Plane, Form.Boat, Form.Frog }.Select(f => level.Budget[(int)f]));
        string count = r.CanFinish ? $"{r.FinishSolutionCount} / {r.ThreeStarSolutionCount}{(r.CountSaturated ? "+" : "")}" : "—";
        sb.AppendLine($"| {level.Name} | {name} | {budget} | {(r.CanFinish ? r.MinMovesToFinish.ToString() : "нет")} | {(r.CanFinish ? r.FinishSlack.ToString() : "—")} | {(r.CanFinish ? r.MaxStarsAtFinish.ToString() : "—")} | {(r.CanThreeStar ? r.MinMovesThreeStars.ToString() : "нет")} | {(r.MinFoldsThreeStars >= 0 ? r.MinFoldsThreeStars.ToString() : "—")} | {count} | {cachedVerdict} |");

        if (name == "новые")
        {
            details.AppendLine($"## Уровень {level.Name}");
            details.AppendLine();
            details.AppendLine("```");
            details.Append(LevelAssetReader.Render(level));
            details.AppendLine("```");
            if (errors.Count > 0) details.AppendLine("Ошибки валидатора: " + string.Join(" ", errors));
            if (r.ThreeStarPath != null) details.AppendLine("Кратчайшее на 3★: " + string.Join(", ", r.ThreeStarPath));
            if (r.MinFoldsPath != null) details.AppendLine($"Минимум складок ({r.MinFoldsThreeStars}): " + string.Join(", ", r.MinFoldsPath));
            if (r.Truncated) details.AppendLine("ВНИМАНИЕ: перебор обрезан по лимиту состояний.");
            details.AppendLine($"Состояний: {r.StatesExplored}");
            details.AppendLine();
        }
    }
}

sb.AppendLine();
sb.AppendLine("Карты: верхняя строка — наибольший y. `S` старт, `E` финиш, `*` звезда, `w` звезда на воде, `f` хрупкая, `~` вода, `R` камень, `F` огонь, `V` вулкан, `I` лёд, `#` пусто.");
sb.AppendLine();
sb.Append(details);

if (output != null) File.WriteAllText(output, sb.ToString());
Console.Write(sb.ToString());

static string FindRepoRoot()
{
    string dir = AppContext.BaseDirectory;
    while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets")))
        dir = Path.GetDirectoryName(dir);
    return dir ?? Directory.GetCurrentDirectory();
}
