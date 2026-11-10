using System;
using System.Collections.Generic;

namespace Paperbound.Core
{
    /// <summary>Переключаемые правила. Значения по умолчанию — решения по плану дизайна от 2026-11-10.</summary>
    public sealed class RuleSet
    {
        /// <summary>Журавль, зайдя на лёд, едет в том же направлении до первой нелёдяной клетки. Лягушка не скользит.</summary>
        public bool SlipperyIce = true;

        /// <summary>Лодка собирает звёзды на воде по пути.</summary>
        public bool BoatCollectsStars = true;

        /// <summary>Гипотеза: самолёт пролетает над водой и пустотой (и обрушенными клетками), садится только на сушу.</summary>
        public bool PlaneFliesOverWaterAndEmpty = false;

        /// <summary>
        /// Гипотеза (не утверждена штабом): лягушка переживает воду, появившуюся под ней (прилив, тонкий лёд), и следующим ходом
        /// выпрыгивает. По умолчанию выключено: лягушка на такой клетке тонет, как журавль.
        /// </summary>
        public bool FrogSwims = false;

        public static RuleSet Default => new();

        /// <summary>Правила игры до переработки ядра (для сверки старых уровней).</summary>
        public static RuleSet Legacy => new() { SlipperyIce = false, BoatCollectsStars = false, PlaneFliesOverWaterAndEmpty = false };

        public RuleSet Clone() => (RuleSet)MemberwiseClone();
    }

    /// <summary>
    /// Расписание общего вида: каждые Period ходов игрока источник меняет следующую клетку очереди на Result.
    /// Вулкан: Result = Lava, Lethal = true. Источник льда: Result = Ice.
    /// Прилив, ветер и т.п. добавляются как новые расписания без изменения состояния.
    /// </summary>
    public sealed class ScheduleDef
    {
        public string Kind = "generic";
        public Pos Source;
        public int Period = 1;
        public List<Pos> Targets = new();
        public TerrainType Result;
        /// <summary>Игрок на клетке в момент срабатывания погибает.</summary>
        public bool Lethal;
        /// <summary>Очередь начинается заново после последней цели.</summary>
        public bool Loop;
        /// <summary>Клетки с этим террейном не меняются (индекс очереди всё равно сдвигается).</summary>
        public List<TerrainType> Immune = new();
        /// <summary>
        /// Переключатель (прилив): при срабатывании все цели сразу меняются Result ↔ Alternate. Очередь не используется.
        /// </summary>
        public bool Toggle;
        public TerrainType Alternate;

        /// <summary>Прилив: каждые period ходов клетки targets меняются вода ↔ песок.</summary>
        public static ScheduleDef Tide(int period, IEnumerable<Pos> targets) => new()
        {
            Kind = "tide", Period = period, Targets = new List<Pos>(targets),
            Result = TerrainType.Sand, Alternate = TerrainType.Water, Toggle = true,
        };

        public static ScheduleDef Volcano(Pos source, int period, IEnumerable<Pos> targets) => new()
        {
            Kind = "volcano", Source = source, Period = period, Targets = new List<Pos>(targets),
            Result = TerrainType.Lava, Lethal = true,
            Immune = new List<TerrainType> { TerrainType.Volcano, TerrainType.Lava },
        };

        public static ScheduleDef IceSource(Pos source, int period, IEnumerable<Pos> targets) => new()
        {
            Kind = "ice", Source = source, Period = period, Targets = new List<Pos>(targets),
            Result = TerrainType.Ice, Lethal = false,
            Immune = new List<TerrainType> { TerrainType.Ice },
        };
    }

    public enum ItemKind
    {
        /// <summary>Листок: +Amount ходов форме Form. Подбирается как звезда, самолёт и лодка берут на пролёте.</summary>
        Leaf,
    }

    /// <summary>Предмет на клетке (кроме звёзд). Индекс в LevelDef.Items — бит в GameState.ItemMask.</summary>
    public sealed class ItemDef
    {
        public ItemKind Kind = ItemKind.Leaf;
        public Pos Position;
        public Form Form = Form.Crane;
        public int Amount = 1;
    }

    /// <summary>Движущаяся сущность на поле (короб, плот, страж). Типы появятся вместе с механиками биомов.</summary>
    /// <summary>Виды сущностей (EntityDef.Kind).</summary>
    public static class EntityKind
    {
        /// <summary>Плот (Океан): плывёт по течению на 1 клетку после хода игрока; журавль и лягушка стоят и едут, лодка причаливает.</summary>
        public const int Raft = 1;
        /// <summary>
        /// Короб (Пещера): журавль толкает на 1. В воде становится плотом-причалом, в провале — полом (BoxFloor),
        /// в огне и лаве сгорает. Останавливает самолёт, жмёт плиты. Нельзя втолкнуть в камень, старт, финиш, закрытые ворота, другую сущность.
        /// </summary>
        public const int Box = 2;
        /// <summary>Убранная сущность (сгоревший короб, короб, ставший полом). Не участвует в правилах.</summary>
        public const int None = 0;

        public static string Name(int kind) => kind switch { Raft => "raft", Box => "box", None => "none", _ => kind.ToString() };

        public static int Parse(string s) => s switch { "raft" => Raft, "box" => Box, _ => int.Parse(s) };
    }

    public sealed class EntityDef
    {
        public int Kind;
        public Pos Position;
        public Pos Direction;
        public int Data;
    }

    /// <summary>Неизменяемое описание уровня для ядра. Строится из LevelData (Unity) или из текста (тесты).</summary>
    public sealed class LevelDef
    {
        public readonly int Width;
        public readonly int Height;
        public readonly TerrainType[] Terrain;
        public readonly bool[] Fragile;
        /// <summary>
        /// Направление потока клетки (Pos.Zero — нет). На воде это течение (лодка поворачивает с ним, плот дрейфует),
        /// на суше — ветер (Пустыня).
        /// </summary>
        public readonly Pos[] Flow;
        /// <summary>Позиции звёзд; индекс в списке — бит в GameState.StarMask.</summary>
        public readonly List<Pos> Stars = new();
        /// <summary>Бюджет ходов по формам, индекс — (int)Form.</summary>
        public readonly int[] Budget = new int[Forms.Count];
        public readonly List<ScheduleDef> Schedules = new();
        public readonly List<EntityDef> Entities = new();
        public readonly List<ItemDef> Items = new();
        public RuleSet Rules = RuleSet.Default;
        public string Name = "";

        public LevelDef(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Level size must be positive");
            Width = width;
            Height = height;
            Terrain = new TerrainType[width * height];
            Fragile = new bool[width * height];
            Flow = new Pos[width * height];
        }

        public int CellCount => Width * Height;
        public bool InBounds(Pos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        public int Index(Pos p) => p.Y * Width + p.X;
        public Pos PosOf(int index) => new(index % Width, index / Width);

        public int StarIndex(Pos p)
        {
            for (int i = 0; i < Stars.Count; i++)
                if (Stars[i] == p) return i;
            return -1;
        }

        public int ItemIndex(Pos p)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Position == p) return i;
            return -1;
        }

        public int TotalBudget
        {
            get
            {
                int sum = 0;
                for (int i = 1; i < Forms.Count; i++)
                    if (Budget[i] > 0) sum += Budget[i];
                return sum;
            }
        }

        public Pos? Find(TerrainType t)
        {
            for (int i = 0; i < Terrain.Length; i++)
                if (Terrain[i] == t) return PosOf(i);
            return null;
        }

        public Pos Start => Find(TerrainType.Start) ?? throw new InvalidOperationException("Level has no Start cell");

        public Pos FlowAt(Pos p) => Flow[Index(p)];

        public void SetBudget(Form form, int moves) => Budget[(int)form] = moves;

        /// <summary>
        /// Текстовая карта для тестов. Строка 0 — y = 0. Символы:
        /// '.' Default, '#' Empty, 'S' Start, 'E' End, '~' Water, 'R' Stone, 'F' Fire, 'V' Volcano, 'L' Lava, 'I' Ice,
        /// '*' звезда на Default, 'f' хрупкая Default, 'x' хрупкая со звездой, 'w' звезда на воде.
        /// </summary>
        public static LevelDef FromRows(params string[] rows)
        {
            int h = rows.Length;
            int w = rows[0].Length;
            var level = new LevelDef(w, h);
            for (int y = 0; y < h; y++)
            {
                if (rows[y].Length != w) throw new ArgumentException($"Row {y} has length {rows[y].Length}, expected {w}");
                for (int x = 0; x < w; x++)
                {
                    char c = rows[y][x];
                    int i = y * w + x;
                    TerrainType t = c switch
                    {
                        '#' => TerrainType.Empty,
                        'S' => TerrainType.Start,
                        'E' => TerrainType.End,
                        '~' or 'w' => TerrainType.Water,
                        'R' => TerrainType.Stone,
                        'F' => TerrainType.Fire,
                        'V' => TerrainType.Volcano,
                        'L' => TerrainType.Lava,
                        'I' => TerrainType.Ice,
                        'P' => TerrainType.LilyPad,
                        'D' => TerrainType.Sand,
                        'B' => TerrainType.BoxFloor,
                        'T' => TerrainType.Plate,
                        'H' => TerrainType.Gate,
                        'n' => TerrainType.ThinIce,
                        'k' => TerrainType.CrackedIce,
                        'q' => TerrainType.Quicksand,
                        'Z' => TerrainType.Bamboo,
                        _ => TerrainType.Default,
                    };
                    level.Terrain[i] = t;
                    level.Fragile[i] = c == 'f' || c == 'x';
                    if (c == 'b') level.Entities.Add(new EntityDef { Kind = EntityKind.Box, Position = new Pos(x, y) });
                    if (c == '*' || c == 'x' || c == 'w')
                        level.Stars.Add(new Pos(x, y));
                }
            }
            return level;
        }
    }
}
