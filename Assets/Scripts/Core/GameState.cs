using System;

namespace Paperbound.Core
{
    public enum GameStatus
    {
        Playing,
        Won,
        Dead,
    }

    public enum DeathCause
    {
        None,
        /// <summary>Остановка на огне или лаве.</summary>
        Terrain,
        /// <summary>Самолёт пролетел над огнём или лавой.</summary>
        FlewOverFire,
        /// <summary>Прилив залил клетку под журавлём или лягушкой.</summary>
        Drowned,
        /// <summary>Расписание с Lethal (снаряд вулкана) сработало на клетке игрока.</summary>
        Schedule,
        /// <summary>Лягушка приземлилась на зыбучий песок.</summary>
        Quicksand,
        /// <summary>Встреча со стражем (Храм).</summary>
        Guard,
    }

    /// <summary>Состояние сущности на поле. Входит в снимок для Undo и в ключ решателя.</summary>
    public struct EntityState : IEquatable<EntityState>
    {
        public int Kind;
        public Pos Position;
        public Pos Direction;
        public int Data;

        public bool Equals(EntityState o) => Kind == o.Kind && Position == o.Position && Direction == o.Direction && Data == o.Data;
        public override bool Equals(object obj) => obj is EntityState e && Equals(e);
        public override int GetHashCode() => HashCode.Combine(Kind, Position, Direction, Data);
    }

    /// <summary>
    /// Полное состояние партии. Значение: Rules.Apply возвращает новый объект, старый не меняется,
    /// поэтому история Undo — это просто стек состояний, и в неё автоматически попадает всё, что
    /// есть в состоянии (расписания, сущности, обрушения, бюджет, складки).
    /// </summary>
    public sealed class GameState
    {
        public Pos Player;
        public Form Form;
        public int[] Budget;
        public TerrainType[] Terrain;
        public bool[] Collapsed;
        public int StarMask;
        /// <summary>Подобранные предметы (биты по индексу LevelDef.Items).</summary>
        public int ItemMask;
        public int[] ScheduleCountdown;
        public int[] ScheduleIndex;
        public EntityState[] Entities;
        /// <summary>Число смен формы (складок). Не входит в равенство состояний.</summary>
        public int Folds;
        public int MovesMade;
        public GameStatus Status;
        public DeathCause Death;
        public Pos DeathPos;

        public int StarsCollected
        {
            get
            {
                int n = 0, m = StarMask;
                while (m != 0) { n += m & 1; m >>= 1; }
                return n;
            }
        }

        public bool HasStar(int starIndex) => (StarMask & (1 << starIndex)) != 0;

        public bool HasItem(int itemIndex) => (ItemMask & (1 << itemIndex)) != 0;

        public int BudgetOf(Form f) => Budget[(int)f];

        public TerrainType TerrainAt(LevelDef level, Pos p) => Terrain[level.Index(p)];

        public bool IsCollapsed(LevelDef level, Pos p) => Collapsed[level.Index(p)];

        /// <summary>Звезда ещё лежит на клетке.</summary>
        public bool StarRemainsAt(LevelDef level, Pos p)
        {
            int i = level.StarIndex(p);
            return i >= 0 && !HasStar(i);
        }

        /// <summary>Индекс сущности вида kind на клетке (-1 — нет). kind &lt; 0 — любого вида.</summary>
        public int EntityAt(Pos p, int kind = -1)
        {
            for (int i = 0; i < Entities.Length; i++)
                if (Entities[i].Kind != EntityKind.None && Entities[i].Position == p && (kind < 0 || Entities[i].Kind == kind)) return i;
            return -1;
        }

        public GameState Clone()
        {
            var s = (GameState)MemberwiseClone();
            s.Budget = (int[])Budget.Clone();
            s.Terrain = (TerrainType[])Terrain.Clone();
            s.Collapsed = (bool[])Collapsed.Clone();
            s.ScheduleCountdown = (int[])ScheduleCountdown.Clone();
            s.ScheduleIndex = (int[])ScheduleIndex.Clone();
            s.Entities = (EntityState[])Entities.Clone();
            return s;
        }

        public static GameState Initial(LevelDef level)
        {
            var s = new GameState
            {
                Player = level.Start,
                Form = Form.Default,
                Budget = (int[])level.Budget.Clone(),
                Terrain = (TerrainType[])level.Terrain.Clone(),
                Collapsed = new bool[level.CellCount],
                StarMask = 0,
                ScheduleCountdown = new int[level.Schedules.Count],
                ScheduleIndex = new int[level.Schedules.Count],
                Entities = new EntityState[level.Entities.Count],
                Status = GameStatus.Playing,
            };
            for (int i = 0; i < level.Schedules.Count; i++)
                s.ScheduleCountdown[i] = level.Schedules[i].Period;
            for (int i = 0; i < level.Entities.Count; i++)
            {
                EntityDef e = level.Entities[i];
                s.Entities[i] = new EntityState { Kind = e.Kind, Position = e.Position, Direction = e.Direction, Data = e.Data };
            }
            return s;
        }

        // ── Равенство для решателя ────────────────────────────────────────────

        /// <summary>Равенство без учёта складок и счётчика ходов. includeForm — учитывать текущую форму.</summary>
        public bool SameAs(GameState o, bool includeForm)
        {
            if (Player != o.Player || StarMask != o.StarMask || ItemMask != o.ItemMask || Status != o.Status) return false;
            if (includeForm && Form != o.Form) return false;
            return SeqEq(Budget, o.Budget) && SeqEq(Terrain, o.Terrain) && SeqEq(Collapsed, o.Collapsed)
                && SeqEq(ScheduleCountdown, o.ScheduleCountdown) && SeqEq(ScheduleIndex, o.ScheduleIndex)
                && SeqEq(Entities, o.Entities);
        }

        public int Hash(bool includeForm)
        {
            var h = new HashCode();
            h.Add(Player);
            h.Add(StarMask);
            h.Add(ItemMask);
            h.Add(Status);
            if (includeForm) h.Add(Form);
            foreach (int b in Budget) h.Add(b);
            foreach (TerrainType t in Terrain) h.Add((int)t);
            int bits = 0, k = 0;
            foreach (bool c in Collapsed)
            {
                if (c) bits |= 1 << (k & 31);
                if (++k % 32 == 0) { h.Add(bits); bits = 0; }
            }
            h.Add(bits);
            foreach (int c in ScheduleCountdown) h.Add(c);
            foreach (int c in ScheduleIndex) h.Add(c);
            foreach (EntityState e in Entities) h.Add(e);
            return h.ToHashCode();
        }

        private static bool SeqEq<T>(T[] a, T[] b) where T : IEquatable<T>
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        private static bool SeqEq(TerrainType[] a, TerrainType[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    public sealed class StateComparer : System.Collections.Generic.IEqualityComparer<GameState>
    {
        public static readonly StateComparer IgnoreForm = new(false);
        public static readonly StateComparer WithForm = new(true);

        private readonly bool includeForm;
        private StateComparer(bool includeForm) => this.includeForm = includeForm;

        public bool Equals(GameState a, GameState b) => ReferenceEquals(a, b) || (a != null && b != null && a.SameAs(b, includeForm));
        public int GetHashCode(GameState s) => s.Hash(includeForm);
    }
}
