using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Paperbound.Core;

namespace Paperbound.Tools
{
    /// <summary>
    /// Читает LevelData (.asset, Unity YAML) без Unity. Понимает только поля, которые пишет LevelData:
    /// MapSize, Map, StartMovesPerForm, VolcanoConfigs, IceSourceConfigs.
    /// </summary>
    public static class LevelAssetReader
    {
        private static readonly Regex Vec = new(@"\{x:\s*(-?\d+),\s*y:\s*(-?\d+)\}");

        public static LevelDef Read(string path, RuleSet rules = null)
        {
            string[] lines = File.ReadAllLines(path);
            int w = 0, h = 0;
            var cells = new List<(int terrain, int item, bool fragile)>();
            var budget = new List<(int state, int moves)>();
            var volcanoes = new List<(Pos pos, int period, List<Pos> seq)>();
            var ices = new List<(Pos pos, int period, List<Pos> seq)>();

            string section = "";
            (Pos pos, int period, List<Pos> seq) current = default;
            bool inSequence = false;
            int pendingState = -1;

            void FlushSource()
            {
                if (current.seq == null) return;
                if (section == "VolcanoConfigs") volcanoes.Add(current);
                else if (section == "IceSourceConfigs") ices.Add(current);
                current = default;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string t = line.Trim();
                bool topLevel = line.StartsWith("  ") && !line.StartsWith("   ") && !t.StartsWith("-");

                if (topLevel)
                {
                    FlushSource();
                    inSequence = false;
                    int colon = t.IndexOf(':');
                    section = colon > 0 ? t.Substring(0, colon) : t;
                    if (section == "MapSize")
                    {
                        Match m = Vec.Match(t);
                        w = int.Parse(m.Groups[1].Value);
                        h = int.Parse(m.Groups[2].Value);
                    }
                    continue;
                }

                switch (section)
                {
                    case "Map":
                        if (t.StartsWith("- Terrain:"))
                        {
                            int terrain = int.Parse(t.Substring("- Terrain:".Length).Trim());
                            int item = int.Parse(lines[++i].Trim().Substring("Item:".Length).Trim());
                            bool fragile = lines[++i].Trim().Substring("IsFragile:".Length).Trim() == "1";
                            cells.Add((terrain, item, fragile));
                        }
                        break;
                    case "StartMovesPerForm":
                        if (t.StartsWith("- State:")) pendingState = int.Parse(t.Substring("- State:".Length).Trim());
                        else if (t.StartsWith("Moves:")) budget.Add((pendingState, int.Parse(t.Substring("Moves:".Length).Trim())));
                        break;
                    case "VolcanoConfigs":
                    case "IceSourceConfigs":
                        if (t.StartsWith("- Position:"))
                        {
                            FlushSource();
                            Match m = Vec.Match(t);
                            current = (new Pos(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)), 1, new List<Pos>());
                            inSequence = false;
                        }
                        else if (t.StartsWith("Period:"))
                            current.period = int.Parse(t.Substring("Period:".Length).Trim());
                        else if (t.StartsWith("LavaSequence:") || t.StartsWith("FreezeSequence:"))
                            inSequence = true;
                        else if (inSequence && t.StartsWith("- {x:"))
                        {
                            Match m = Vec.Match(t);
                            current.seq.Add(new Pos(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
                        }
                        break;
                }
            }
            FlushSource();

            if (cells.Count != w * h)
                throw new InvalidDataException($"{path}: map has {cells.Count} cells, expected {w}x{h}");

            var level = new LevelDef(w, h) { Name = Path.GetFileNameWithoutExtension(path), Rules = rules ?? RuleSet.Default };
            for (int i = 0; i < cells.Count; i++)
            {
                level.Terrain[i] = (TerrainType)cells[i].terrain;
                level.Fragile[i] = cells[i].fragile;
                if (cells[i].item == (int)CellItem.Star) level.Stars.Add(level.PosOf(i));
            }
            foreach ((int state, int moves) in budget)
                level.Budget[state] = moves;
            foreach (var v in volcanoes) level.Schedules.Add(ScheduleDef.Volcano(v.pos, v.period, v.seq));
            foreach (var c in ices) level.Schedules.Add(ScheduleDef.IceSource(c.pos, c.period, c.seq));
            return level;
        }

        /// <summary>Сохранённое в ассете решение (cachedSolution) как ходы: шаг 0 — старт.</summary>
        public static List<SolverStep> ReadCachedSolution(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var steps = new List<SolverStep>();
            bool inSection = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (lines[i].StartsWith("  ") && !lines[i].StartsWith("   ") && !t.StartsWith("-"))
                {
                    inSection = t.StartsWith("cachedSolution:");
                    continue;
                }
                if (!inSection || !t.StartsWith("- Position:")) continue;
                Match m = Vec.Match(t);
                int state = int.Parse(lines[++i].Trim().Substring("State:".Length).Trim());
                int stars = int.Parse(lines[++i].Trim().Substring("StarsCollected:".Length).Trim());
                steps.Add(new SolverStep { Target = new Pos(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)), Form = (Form)state, StarsAfter = stars });
            }
            if (steps.Count > 0) steps.RemoveAt(0);
            return steps;
        }

        public static string Render(LevelDef level, GameState s = null)
        {
            var sb = new System.Text.StringBuilder();
            for (int y = level.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    var p = new Pos(x, y);
                    int i = level.Index(p);
                    TerrainType t = s != null ? s.Terrain[i] : level.Terrain[i];
                    char c = t switch
                    {
                        TerrainType.Empty => '#', TerrainType.Start => 'S', TerrainType.End => 'E',
                        TerrainType.Water => '~', TerrainType.Stone => 'R', TerrainType.Fire => 'F',
                        TerrainType.Volcano => 'V', TerrainType.Lava => 'L', TerrainType.Ice => 'I', TerrainType.LilyPad => 'P', TerrainType.Sand => 'D', TerrainType.BoxFloor => 'B', TerrainType.Plate => 'T', TerrainType.Gate => 'H', TerrainType.ThinIce => 'n', TerrainType.CrackedIce => 'k', TerrainType.Quicksand => 'q', TerrainType.Torii => 'O', TerrainType.Bamboo => 'Z', _ => '.',
                    };
                    int star = level.StarIndex(p);
                    if (star >= 0 && (s == null || !s.HasStar(star))) c = t == TerrainType.Water ? 'w' : '*';
                    else if (level.Fragile[i] && c == '.') c = 'f';
                    if (c == '~' && level.Flow[i] != Pos.Zero) c = LevelText.FlowChar(level.Flow[i]);
                    else if (c == '.' && level.Flow[i] != Pos.Zero) c = WindChar(level.Flow[i]); // ветер на суше
                    if (s != null && s.Collapsed[i]) c = '#';
                    var ents = s != null ? s.Entities : null;
                    if (ents == null)
                    {
                        foreach (EntityDef e in level.Entities) if (e.Position == p) c = e.Kind == EntityKind.Raft ? 'r' : e.Kind == EntityKind.Box ? 'b' : e.Kind == EntityKind.Guard ? 'g' : e.Kind == EntityKind.None ? c : 'e';
                    }
                    else foreach (EntityState e in ents) if (e.Position == p) c = e.Kind == EntityKind.Raft ? 'r' : e.Kind == EntityKind.Box ? 'b' : e.Kind == EntityKind.Guard ? 'g' : e.Kind == EntityKind.None ? c : 'e';
                    if (s != null && s.Player == p) c = '@';
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
        private static char WindChar(Pos f) => f == Pos.Up ? '↑' : f == Pos.Down ? '↓' : f == Pos.Left ? '←' : '→';
}
}
