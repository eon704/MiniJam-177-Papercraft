using System.Linq;
using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class DesertMechanicsTests
    {
        private static LevelDef Parse(string budget, string[] map, string[] flow = null)
        {
            int w = map[0].Length, h = map.Length;
            string text = $"name: t\nsize: {w} {h}\nbudget: {budget}\nmap:\n{string.Join("\n", map)}\nmarks:\n"
                + string.Join("\n", map.Select(r => new string('.', r.Length))) + "\n";
            if (flow != null) text += "flow:\n" + string.Join("\n", flow) + "\n";
            return LevelText.Parse(text);
        }

        [Fact]
        public void Wind_DeflectsPlane_OneCellPerWindCell()
        {
            var level = Parse("crane=0 plane=1 boat=0 frog=0", new[] { "+++++", "+++++", "x+++y" }, new[] { ".....", ".>...", "....." });
            var targets = Rules.GetOptions(level, GameState.Initial(level), Form.Plane).Select(o => o.Target).ToList();
            Assert.DoesNotContain(new Pos(1, 1), targets); // на клетке ветра не сесть
            Assert.Contains(new Pos(2, 1), targets);       // снесло на 1 вправо
            Assert.Contains(new Pos(3, 2), targets);       // и дальше по диагонали
        }

        [Fact]
        public void Butterfly_CarriedToEndOfFlow_CollectsStars()
        {
            string text = "name: t\nsize: 4 2\nbudget: crane=0 plane=0 boat=0 frog=0 butterfly=1\nmap:\n++++\nx++y\nmarks:\n..*.\n....\nflow:\n.>>.\n....\n";
            var level = LevelText.Parse(text);
            var o = Assert.Single(Rules.GetOptions(level, GameState.Initial(level), Form.Butterfly));
            Assert.Equal(new Pos(3, 1), o.Target);
            var r = Rules.Apply(level, GameState.Initial(level), Form.Butterfly, o.Target);
            Assert.True(r.Success);
            Assert.Equal(1, r.After.StarsCollected);
        }

        [Fact]
        public void Butterfly_IsWeightless()
        {
            // Лилия под бабочкой не тонет, плиту бабочка не жмёт.
            var level = Parse("crane=0 plane=0 boat=0 frog=0 butterfly=2", new[] { "WT+", "+P+", "xWy" });
            var s = Rules.Apply(level, GameState.Initial(level), Form.Butterfly, new Pos(1, 1)).After;
            s = Rules.Apply(level, s, Form.Butterfly, new Pos(2, 2)).After;
            Assert.Equal(GameStatus.Playing, s.Status);
            Assert.Equal(TerrainType.LilyPad, s.TerrainAt(level, new Pos(1, 1)));

            var plate = Parse("crane=0 plane=0 boat=0 frog=0 butterfly=1", new[] { "+TH", "x+y" });
            var p = Rules.Apply(plate, GameState.Initial(plate), Form.Butterfly, new Pos(1, 1)).After;
            Assert.False(Rules.PlatePressed(plate, p));
        }

        [Fact]
        public void Quicksand_NoFoldOnIt_FrogSinks_ButterflyImmune()
        {
            var level = Parse("crane=3 plane=1 boat=0 frog=1 butterfly=1", new[] { "+++++", "xQ++y" });
            var s = Rules.Apply(level, GameState.Initial(level), Form.Crane, new Pos(1, 0)).After;
            Assert.Equal(GameStatus.Playing, s.Status);
            Assert.DoesNotContain(Rules.LegalMoves(level, s), o => o.Form != Form.Crane);
            Assert.True(Rules.Apply(level, s, Form.Crane, new Pos(2, 0)).Success);
            Assert.False(Rules.Apply(level, s, Form.Plane, new Pos(2, 1)).Success);

            var frog = Parse("crane=0 plane=0 boat=0 frog=1", new[] { "x+Q+y" });
            var f = Rules.Apply(frog, GameState.Initial(frog), Form.Frog, new Pos(2, 0)).After;
            Assert.Equal(GameStatus.Dead, f.Status);
            Assert.Equal(DeathCause.Quicksand, f.Death);

            var bf = Parse("crane=1 plane=0 boat=0 frog=0 butterfly=1", new[] { "+Q++", "x+++" });
            var b = Rules.Apply(bf, GameState.Initial(bf), Form.Butterfly, new Pos(1, 1)).After;
            Assert.Contains(Rules.LegalMoves(bf, b), o => o.Form == Form.Crane); // бабочка на песке не вязнет
        }
    }
}
