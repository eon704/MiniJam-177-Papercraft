using System;

namespace Paperbound.Core
{
    /// <summary>Клетка поля. Чистая замена Vector2Int, чтобы ядро не зависело от UnityEngine.</summary>
    [Serializable]
    public readonly struct Pos : IEquatable<Pos>
    {
        public readonly int X;
        public readonly int Y;

        public Pos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly Pos Zero = new(0, 0);
        public static readonly Pos Up = new(0, 1);
        public static readonly Pos Down = new(0, -1);
        public static readonly Pos Left = new(-1, 0);
        public static readonly Pos Right = new(1, 0);

        public static Pos operator +(Pos a, Pos b) => new(a.X + b.X, a.Y + b.Y);
        public static Pos operator -(Pos a, Pos b) => new(a.X - b.X, a.Y - b.Y);
        public static Pos operator *(Pos a, int k) => new(a.X * k, a.Y * k);
        public static bool operator ==(Pos a, Pos b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Pos a, Pos b) => !(a == b);

        public bool Equals(Pos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Pos p && Equals(p);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static int Manhattan(Pos a, Pos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
