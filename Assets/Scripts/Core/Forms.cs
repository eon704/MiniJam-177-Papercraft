using System.Collections.Generic;

namespace Paperbound.Core
{
    /// <summary>
    /// Форма фигурки. Порядок значений совпадает с Player.StateType, преобразование — простым приведением.
    /// Новые формы (сюрикен, бабочка) добавляются в конец перечисления и в таблицу <see cref="Forms"/>.
    /// </summary>
    public enum Form
    {
        Default = 0,
        Crane = 1,
        Plane = 2,
        Boat = 3,
        Frog = 4,
    }

    /// <summary>Как форма перемещается.</summary>
    public enum MoveKind
    {
        None,
        /// <summary>Шаг на соседнюю клетку (журавль). По льду скользит, если включено правило.</summary>
        Step,
        /// <summary>Прыжок на фиксированное смещение, промежуточная клетка не проверяется (лягушка).</summary>
        Jump,
        /// <summary>Полёт по направлению на любое расстояние, каждая клетка пути — возможная цель (самолёт).</summary>
        Slide,
        /// <summary>Плавание через воду до первой суши (лодка).</summary>
        Sail,
    }

    public sealed class FormDef
    {
        public readonly Form Form;
        public readonly MoveKind Kind;
        public readonly Pos[] Directions;
        private readonly bool[] walkable;
        public readonly TerrainType[] WalkableTerrain;

        public FormDef(Form form, MoveKind kind, Pos[] directions, params TerrainType[] walkableTerrain)
        {
            Form = form;
            Kind = kind;
            Directions = directions;
            WalkableTerrain = walkableTerrain;
            walkable = new bool[TerrainInfo.Count];
            foreach (TerrainType t in walkableTerrain)
                walkable[(int)t] = true;
        }

        /// <summary>Может ли форма остановиться (и для самолёта — пролететь) над этой клеткой.</summary>
        public bool CanStandOn(TerrainType terrain) => walkable[(int)terrain];
    }

    public static class Forms
    {
        public const int Count = 5;

        private static readonly Pos[] Orthogonal = { Pos.Up, Pos.Down, Pos.Right, Pos.Left };
        private static readonly Pos[] OrthogonalTwo = { new(0, 2), new(0, -2), new(2, 0), new(-2, 0) };
        private static readonly Pos[] Diagonal = { new(-1, 1), new(-1, -1), new(1, 1), new(1, -1) };

        private static readonly FormDef[] Table =
        {
            new(Form.Default, MoveKind.None, new Pos[0]),
            new(Form.Crane, MoveKind.Step, Orthogonal,
                TerrainType.Default, TerrainType.Stone, TerrainType.Fire, TerrainType.Lava,
                TerrainType.Ice, TerrainType.Start, TerrainType.End),
            new(Form.Plane, MoveKind.Slide, Diagonal,
                TerrainType.Default, TerrainType.Fire, TerrainType.Lava, TerrainType.Start, TerrainType.End),
            new(Form.Boat, MoveKind.Sail, Orthogonal,
                TerrainType.Default, TerrainType.Start, TerrainType.End, TerrainType.Fire, TerrainType.Lava),
            new(Form.Frog, MoveKind.Jump, OrthogonalTwo,
                TerrainType.Default, TerrainType.Fire, TerrainType.Lava, TerrainType.Ice,
                TerrainType.Start, TerrainType.End),
        };

        public static FormDef Get(Form form) => Table[(int)form];

        /// <summary>Формы, которыми можно ходить (без листа).</summary>
        public static IEnumerable<Form> Movable
        {
            get
            {
                for (int i = 1; i < Count; i++)
                    yield return (Form)i;
            }
        }
    }

    public static class TerrainInfo
    {
        public const int Count = 10;

        public static bool IsDeadly(TerrainType t) => t == TerrainType.Fire || t == TerrainType.Lava;
    }
}
