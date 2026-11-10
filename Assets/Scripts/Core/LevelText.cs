using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Paperbound.Core
{
    /// <summary>
    /// Текстовый формат уровня (.pbl) — расширение старого формата Assets/Data/Levels/Json.
    /// Уровни проектируются в нём и проверяются решателем; в .asset импортируются в Unity.
    ///
    /// <code>
    /// # комментарий
    /// name: forest-03
    /// size: 5 3
    /// budget: crane=3 plane=1 boat=0 frog=2
    /// rules: slippery_ice=on boat_stars=on plane_over_water=off   (необязательно)
    /// map:                 верхняя строка — наибольший y
    /// x+W+y
    /// +0P0+
    /// +++++
    /// marks:               необязательно; '.' пусто, '*' звезда, 'f' хрупкая, 'x' хрупкая со звездой
    /// .*...
    /// ..f..
    /// .x..*
    /// leaf: 2,0 crane +1
    /// volcano: 0,2 period=2 -> 1,1 2,1
    /// ice: 4,1 period=1 -> 3,1 3,0
    /// entity: kind=1 at=2,1 dir=1,0 data=0
    /// </code>
    ///
    /// Террейн (как в старом формате): 0 пусто, + обычная, W вода, S камень, F огонь, x старт, y финиш,
    /// V вулкан, I лёд; новое: L лава, P кувшинка. При чтении принимаются и старые символы со звездой:
    /// G (обычная), 1 (обычная), 2 (вода), 3 (камень).
    /// </summary>
    public static class LevelText
    {
        public const string Extension = ".pbl";

        private static readonly Form[] BudgetOrder = { Form.Crane, Form.Plane, Form.Boat, Form.Frog };

        // ── Чтение ────────────────────────────────────────────────────────────

        public static LevelDef Parse(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            string name = "";
            int w = -1, h = -1;
            var budget = new int[Forms.Count];
            RuleSet rules = RuleSet.Default;
            List<string> map = null, marks = null;
            var leaves = new List<ItemDef>();
            var schedules = new List<ScheduleDef>();
            var entities = new List<EntityDef>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]);
                if (line.Length == 0) continue;
                int colon = line.IndexOf(':');
                if (colon < 0) throw Error(i, $"ожидалось «ключ: значение», получено «{line}»");
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim();

                switch (key)
                {
                    case "name": name = value; break;
                    case "size":
                    {
                        string[] p = Words(value);
                        if (p.Length != 2) throw Error(i, "size: нужно «ширина высота»");
                        w = Int(p[0], i); h = Int(p[1], i);
                        break;
                    }
                    case "budget":
                        foreach (string kv in Words(value))
                        {
                            (string k, string v) = Pair(kv, i);
                            budget[(int)ParseForm(k, i)] = Int(v, i);
                        }
                        break;
                    case "rules":
                        rules = RuleSet.Default;
                        foreach (string kv in Words(value))
                        {
                            (string k, string v) = Pair(kv, i);
                            bool on = v == "on" || v == "1" || v == "true";
                            switch (k)
                            {
                                case "slippery_ice": rules.SlipperyIce = on; break;
                                case "boat_stars": rules.BoatCollectsStars = on; break;
                                case "plane_over_water": rules.PlaneFliesOverWaterAndEmpty = on; break;
                                default: throw Error(i, $"неизвестное правило «{k}»");
                            }
                        }
                        break;
                    case "map":
                    case "marks":
                    {
                        if (h <= 0) throw Error(i, $"{key}: сначала укажите size");
                        var grid = new List<string>();
                        for (int r = 0; r < h; r++)
                        {
                            i++;
                            if (i >= lines.Length) throw Error(i, $"{key}: не хватает строк");
                            string row = StripComment(lines[i]);
                            if (row.Length != w) throw Error(i, $"{key}: строка длины {row.Length}, нужно {w}");
                            grid.Add(row);
                        }
                        if (key == "map") map = grid; else marks = grid;
                        break;
                    }
                    case "leaf":
                    {
                        // leaf: x,y form [+N]
                        string[] p = Words(value);
                        if (p.Length < 2) throw Error(i, "leaf: нужно «x,y форма [+N]»");
                        var leaf = new ItemDef { Kind = ItemKind.Leaf, Position = ParsePos(p[0], i), Form = ParseForm(p[1], i) };
                        if (p.Length > 2) leaf.Amount = Int(p[2].TrimStart('+'), i);
                        leaves.Add(leaf);
                        break;
                    }
                    case "volcano":
                    case "ice":
                    {
                        // volcano: x,y period=N -> x,y x,y ...
                        int arrow = value.IndexOf("->", StringComparison.Ordinal);
                        if (arrow < 0) throw Error(i, $"{key}: нужно «x,y period=N -> цели»");
                        string[] head = Words(value.Substring(0, arrow));
                        Pos source = ParsePos(head[0], i);
                        int period = 1;
                        bool loop = false;
                        for (int k = 1; k < head.Length; k++)
                        {
                            (string a, string b) = Pair(head[k], i);
                            if (a == "period") period = Int(b, i);
                            else if (a == "loop") loop = b == "on" || b == "1" || b == "true";
                            else throw Error(i, $"{key}: неизвестный параметр «{a}»");
                        }
                        var targets = new List<Pos>();
                        foreach (string t in Words(value.Substring(arrow + 2)))
                            targets.Add(ParsePos(t, i));
                        ScheduleDef s = key == "volcano" ? ScheduleDef.Volcano(source, period, targets) : ScheduleDef.IceSource(source, period, targets);
                        s.Loop = loop;
                        schedules.Add(s);
                        break;
                    }
                    case "entity":
                    {
                        var e = new EntityDef();
                        foreach (string kv in Words(value))
                        {
                            (string a, string b) = Pair(kv, i);
                            switch (a)
                            {
                                case "kind": e.Kind = Int(b, i); break;
                                case "at": e.Position = ParsePos(b, i); break;
                                case "dir": e.Direction = ParsePos(b, i); break;
                                case "data": e.Data = Int(b, i); break;
                                default: throw Error(i, $"entity: неизвестный параметр «{a}»");
                            }
                        }
                        entities.Add(e);
                        break;
                    }
                    default:
                        throw Error(i, $"неизвестный ключ «{key}»");
                }
            }

            if (w <= 0 || h <= 0) throw new FormatException("Нет строки size");
            if (map == null) throw new FormatException("Нет блока map");

            var level = new LevelDef(w, h) { Name = name, Rules = rules };
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row;
                for (int x = 0; x < w; x++)
                {
                    int idx = level.Index(new Pos(x, y));
                    (TerrainType t, bool star) = ParseTerrain(map[row][x], row);
                    level.Terrain[idx] = t;
                    char m = marks != null ? marks[row][x] : '.';
                    if (m != '.' && m != '*' && m != 'f' && m != 'x')
                        throw new FormatException($"marks: неизвестный символ «{m}» в строке {row + 1}");
                    level.Fragile[idx] = m == 'f' || m == 'x';
                    if (star || m == '*' || m == 'x') level.Stars.Add(new Pos(x, y));
                }
            }
            level.Stars.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            Array.Copy(budget, level.Budget, budget.Length);
            level.Items.AddRange(leaves);
            level.Schedules.AddRange(schedules);
            level.Entities.AddRange(entities);
            return level;
        }

        // ── Запись ────────────────────────────────────────────────────────────

        public static string Write(LevelDef level, string comment = null)
        {
            var sb = new StringBuilder();
            sb.Append("# Paperbound level v1\n");
            if (!string.IsNullOrEmpty(comment))
                foreach (string c in comment.Replace("\r", "").Split('\n'))
                    sb.Append("# ").Append(c).Append('\n');
            if (!string.IsNullOrEmpty(level.Name)) sb.Append("name: ").Append(level.Name).Append('\n');
            sb.Append("size: ").Append(level.Width).Append(' ').Append(level.Height).Append('\n');
            sb.Append("budget:");
            foreach (Form f in BudgetOrder)
                sb.Append(' ').Append(FormName(f)).Append('=').Append(level.Budget[(int)f]);
            sb.Append('\n');

            RuleSet d = RuleSet.Default, r = level.Rules;
            if (r.SlipperyIce != d.SlipperyIce || r.BoatCollectsStars != d.BoatCollectsStars || r.PlaneFliesOverWaterAndEmpty != d.PlaneFliesOverWaterAndEmpty)
                sb.Append($"rules: slippery_ice={OnOff(r.SlipperyIce)} boat_stars={OnOff(r.BoatCollectsStars)} plane_over_water={OnOff(r.PlaneFliesOverWaterAndEmpty)}\n");

            bool anyMarks = false;
            sb.Append("map:\n");
            for (int y = level.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    int idx = level.Index(new Pos(x, y));
                    sb.Append(TerrainChar(level.Terrain[idx]));
                    if (level.Fragile[idx] || level.StarIndex(new Pos(x, y)) >= 0) anyMarks = true;
                }
                sb.Append('\n');
            }
            if (anyMarks)
            {
                sb.Append("marks:\n");
                for (int y = level.Height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < level.Width; x++)
                    {
                        var p = new Pos(x, y);
                        bool star = level.StarIndex(p) >= 0, fragile = level.Fragile[level.Index(p)];
                        sb.Append(star && fragile ? 'x' : star ? '*' : fragile ? 'f' : '.');
                    }
                    sb.Append('\n');
                }
            }

            foreach (ItemDef item in level.Items)
                if (item.Kind == ItemKind.Leaf)
                    sb.Append($"leaf: {P(item.Position)} {FormName(item.Form)} +{item.Amount}\n");

            foreach (ScheduleDef s in level.Schedules)
            {
                string kind = s.Kind == "volcano" ? "volcano" : s.Kind == "ice" ? "ice" : null;
                if (kind == null) continue;
                sb.Append($"{kind}: {P(s.Source)} period={s.Period}{(s.Loop ? " loop=on" : "")} ->");
                foreach (Pos t in s.Targets) sb.Append(' ').Append(P(t));
                sb.Append('\n');
            }

            foreach (EntityDef e in level.Entities)
                sb.Append($"entity: kind={e.Kind} at={P(e.Position)} dir={P(e.Direction)} data={e.Data}\n");

            return sb.ToString();
        }

        /// <summary>Карта старого формата Json («Map»: строка символов, индекс y*w+x).</summary>
        public static LevelDef FromLegacyMap(int width, int height, string map, int[] budgetByState, string name = "")
        {
            if (map.Length != width * height) throw new FormatException($"Map length {map.Length} != {width}x{height}");
            var level = new LevelDef(width, height) { Name = name };
            for (int i = 0; i < map.Length; i++)
            {
                (TerrainType t, bool star) = ParseTerrain(map[i], 0);
                level.Terrain[i] = t;
                if (star) level.Stars.Add(level.PosOf(i));
            }
            for (int i = 0; i < Forms.Count && i < budgetByState.Length; i++)
                level.Budget[i] = budgetByState[i];
            return level;
        }

        // ── Символы ───────────────────────────────────────────────────────────

        public static char TerrainChar(TerrainType t) => t switch
        {
            TerrainType.Empty => '0',
            TerrainType.Default => '+',
            TerrainType.Water => 'W',
            TerrainType.Stone => 'S',
            TerrainType.Fire => 'F',
            TerrainType.Start => 'x',
            TerrainType.End => 'y',
            TerrainType.Volcano => 'V',
            TerrainType.Ice => 'I',
            TerrainType.Lava => 'L',
            TerrainType.LilyPad => 'P',
            _ => '+',
        };

        private static (TerrainType, bool) ParseTerrain(char c, int row) => c switch
        {
            '0' => (TerrainType.Empty, false),
            '+' => (TerrainType.Default, false),
            'G' or '1' => (TerrainType.Default, true),
            'W' => (TerrainType.Water, false),
            '2' => (TerrainType.Water, true),
            'S' => (TerrainType.Stone, false),
            '3' => (TerrainType.Stone, true),
            'F' => (TerrainType.Fire, false),
            'x' => (TerrainType.Start, false),
            'y' => (TerrainType.End, false),
            'V' => (TerrainType.Volcano, false),
            'I' => (TerrainType.Ice, false),
            'L' => (TerrainType.Lava, false),
            'P' => (TerrainType.LilyPad, false),
            _ => throw new FormatException($"map: неизвестный символ «{c}» в строке {row + 1}"),
        };

        public static string FormName(Form f) => f.ToString().ToLowerInvariant();

        private static Form ParseForm(string s, int line)
        {
            foreach (Form f in Forms.Movable)
                if (FormName(f) == s.ToLowerInvariant()) return f;
            throw Error(line, $"неизвестная форма «{s}»");
        }

        private static string P(Pos p) => $"{p.X},{p.Y}";
        private static string OnOff(bool b) => b ? "on" : "off";

        private static Pos ParsePos(string s, int line)
        {
            string[] p = s.Split(',');
            if (p.Length != 2) throw Error(line, $"ожидались координаты «x,y», получено «{s}»");
            return new Pos(Int(p[0], line), Int(p[1], line));
        }

        private static (string, string) Pair(string kv, int line)
        {
            int eq = kv.IndexOf('=');
            if (eq <= 0) throw Error(line, $"ожидалось «ключ=значение», получено «{kv}»");
            return (kv.Substring(0, eq).ToLowerInvariant(), kv.Substring(eq + 1).ToLowerInvariant());
        }

        private static int Int(string s, int line)
        {
            if (!int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                throw Error(line, $"ожидалось число, получено «{s}»");
            return v;
        }

        private static string[] Words(string s) => s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        private static string StripComment(string line)
        {
            int hash = line.IndexOf('#');
            return (hash >= 0 ? line.Substring(0, hash) : line).Trim();
        }

        private static FormatException Error(int line, string message) => new($"Строка {line + 1}: {message}");
    }
}
