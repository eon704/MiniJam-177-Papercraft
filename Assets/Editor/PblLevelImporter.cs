using System.Collections.Generic;
using System.IO;
using Paperbound.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Импорт уровней из текстового формата .pbl (Tools/Levels, проверяются решателем вне Unity) в ассеты LevelData.
/// Существующий ассет с тем же именем обновляется на месте (GUID и ссылки на него сохраняются).
/// </summary>
public static class PblLevelImporter
{
    private const string TargetRoot = "Assets/Data/Levels";

    [MenuItem("Tools/Import .pbl levels")]
    public static void ImportFolder()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string start = Path.Combine(projectRoot, "Tools", "Levels");
        string dir = EditorUtility.OpenFolderPanel("Папка с уровнями .pbl", Directory.Exists(start) ? start : projectRoot, "");
        if (string.IsNullOrEmpty(dir)) return;

        string target = $"{TargetRoot}/{new DirectoryInfo(dir).Name}";
        EnsureFolder(target);

        int imported = 0, failed = 0;
        var report = new List<string>();
        foreach (string file in Directory.GetFiles(dir, "*" + LevelText.Extension))
        {
            if (Import(file, target, out string message)) imported++;
            else failed++;
            report.Add(message);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (string line in report) Debug.Log(line);
        EditorUtility.DisplayDialog("Импорт .pbl",
            $"Импортировано: {imported}\nС ошибками: {failed}\nПапка: {target}\n\nПодробности — в консоли.", "OK");
    }

    /// <summary>Импортировать один файл. false — файл не прочитан или уровень невалиден (ассет не создаётся).</summary>
    public static bool Import(string file, string targetFolder, out string message)
    {
        LevelDef def;
        try
        {
            def = LevelText.Parse(File.ReadAllText(file));
        }
        catch (System.Exception e)
        {
            message = $"✗ {Path.GetFileName(file)}: ошибка чтения: {e.Message}";
            return false;
        }

        string name = string.IsNullOrEmpty(def.Name) ? Path.GetFileNameWithoutExtension(file) : def.Name;
        List<string> errors = LevelValidator.Validate(def);
        if (errors.Count > 0)
        {
            message = $"✗ {name}: {string.Join(" ", errors)}";
            return false;
        }
        foreach (EntityDef e in def.Entities)
            if (e.Kind != EntityKind.Raft)
            {
                message = $"✗ {name}: сущности «{e.Kind}» в LevelData пока не поддерживаются (есть только плоты).";
                return false;
            }

        foreach (ScheduleDef s in def.Schedules)
            if (s.Kind != "volcano" && s.Kind != "ice")
            {
                message = $"✗ {name}: расписание «{s.Kind}» (например, прилив) в игре пока не поддерживается.";
                return false;
            }

        foreach (TerrainType t in def.Terrain)
            if (t > TerrainType.LilyPad)
            {
                message = $"✗ {name}: клетки новых миров ({t}) в игре пока не поддерживаются.";
                return false;
            }

        // Течение на воде (Океан) поддерживается видом OceanView. Ветер на суше (Пустыня) — пока нет.
        for (int i = 0; i < def.CellCount; i++)
            if (def.Flow[i] != Pos.Zero && def.Terrain[i] != TerrainType.Water)
            {
                message = $"✗ {name}: ветер на суше ({def.PosOf(i)}) в игре пока не поддерживается.";
                return false;
            }

        string assetPath = $"{targetFolder}/{name}.asset";
        LevelData data = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<LevelData>();

        Fill(data, def);

        if (isNew) AssetDatabase.CreateAsset(data, assetPath);

        // Решение для подсказок считается тем же решателем, что и в игре.
        List<LevelEditorWindow.TurnInfo> solution = LevelEditorWindow.SolveLevel(data);
        data.CachedSolution = solution != null ? LevelDataTools.ConvertToSolutionSteps(solution) : new List<SolutionStep>();
        EditorUtility.SetDirty(data);

        string warn = solution == null ? " (решение на 3★ не найдено — подсказок не будет)" : "";
        message = $"✓ {name} → {assetPath}{(isNew ? "" : " (обновлён)")}{warn}";
        return true;
    }

    private static void Fill(LevelData data, LevelDef def)
    {
        data.MapSize = new Vector2Int(def.Width, def.Height);
        data.Map = new CellData[def.CellCount];
        for (int i = 0; i < def.CellCount; i++)
        {
            CellItem item = def.Stars.Contains(def.PosOf(i)) ? CellItem.Star : CellItem.None;
            data.Map[i] = new CellData(def.Terrain[i], item, def.Fragile[i]);
        }

        data.StartMovesPerForm = new List<MovePerFormEntry>();
        for (int f = 0; f < Forms.Count; f++)
        {
            // Формы новых миров (сюрикен, бабочка) в игре пока не реализованы.
            if (!System.Enum.IsDefined(typeof(Player.StateType), f)) continue;
            data.StartMovesPerForm.Add(new MovePerFormEntry { State = ((Form)f).ToStateType(), Moves = def.Budget[f] });
        }

        data.Biome = BiomeType.Forest;

        data.VolcanoConfigs = new List<VolcanoConfig>();
        data.IceSourceConfigs = new List<IceSourceConfig>();
        foreach (ScheduleDef s in def.Schedules)
        {
            var targets = new List<Vector2Int>();
            foreach (Pos t in s.Targets) targets.Add(t.ToVector());
            if (s.Kind == "volcano")
                data.VolcanoConfigs.Add(new VolcanoConfig { Position = s.Source.ToVector(), Period = s.Period, LavaSequence = targets });
            else if (s.Kind == "ice")
                data.IceSourceConfigs.Add(new IceSourceConfig { Position = s.Source.ToVector(), Period = s.Period, FreezeSequence = targets });
            else
                Debug.LogWarning($"{def.Name}: расписание вида «{s.Kind}» в LevelData не поддерживается и пропущено.");
        }

        data.Flow = new List<FlowConfig>();
        for (int i = 0; i < def.CellCount; i++)
            if (def.Flow[i] != Pos.Zero)
                data.Flow.Add(new FlowConfig { Position = def.PosOf(i).ToVector(), Direction = def.Flow[i].ToVector() });

        data.Rafts = new List<RaftConfig>();
        foreach (EntityDef e in def.Entities)
            if (e.Kind == EntityKind.Raft)
                data.Rafts.Add(new RaftConfig { Position = e.Position.ToVector() });

        data.Leaves = new List<LeafConfig>();
        foreach (ItemDef item in def.Items)
            if (item.Kind == ItemKind.Leaf)
                data.Leaves.Add(new LeafConfig { Position = item.Position.ToVector(), Form = item.Form.ToStateType(), Amount = item.Amount });
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
