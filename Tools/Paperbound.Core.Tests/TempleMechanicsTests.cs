using System.Linq;
using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class TempleMechanicsTests
    {
        private static LevelDef Parse(string budget, string[] map, params string[] extra)
        {
            int w = map[0].Length, h = map.Length;
            string text = $"name: t\nsize: {w} {h}\nbudget: {budget}\nmap:\n{string.Join("\n", map)}\nmarks:\n"
                + string.Join("\n", map.Select(r => new string('.', r.Length))) + "\n" + string.Join("\n", extra) + "\n";
            return LevelText.Parse(text);
        }

        [Fact]
        public void Guard_StepsAlongClosedRoute_AfterPlayerMove()
        {
            var level = Parse("crane=2 plane=0 boat=0 frog=0", new[] { "+++++", "+++++", "x+++y" },
                "route: 3,1 4,1 4,2 3,2", "entity: kind=guard at=3,1 dir=1,0 data=0");
            Assert.DoesNotContain(LevelValidator.Validate(level), e => !e.Contains("Звёзд"));
            var s = Rules.Apply(level, GameState.Initial(level), Form.Crane, new Pos(1, 0)).After;
            Assert.Equal(new Pos(4, 1), s.Entities[0].Position);
            s = Rules.Apply(level, s, Form.Crane, new Pos(2, 0)).After;
            Assert.Equal(new Pos(4, 2), s.Entities[0].Position);
        }

        [Fact]
        public void Guard_MeetingKills_BothWays()
        {
            // Игрок шагает на стража.
            var level = Parse("crane=2 plane=0 boat=0 frog=0", new[] { "x+++y" }, "route: 1,0 2,0", "entity: kind=guard at=1,0 dir=1,0 data=0");
            var r = Rules.Apply(level, GameState.Initial(level), Form.Crane, new Pos(1, 0));
            Assert.Equal(GameStatus.Dead, r.After.Status);
            Assert.Equal(DeathCause.Guard, r.After.Death);

            // Страж шагает на игрока.
            var l2 = Parse("crane=2 plane=0 boat=0 frog=0", new[] { "x++++y" }, "route: 3,0 2,0", "entity: kind=guard at=3,0 dir=1,0 data=0");
            var r2 = Rules.Apply(l2, GameState.Initial(l2), Form.Crane, new Pos(1, 0)).After; // страж 3,0 -> 2,0
            Assert.Equal(GameStatus.Playing, r2.Status);
            Assert.Equal(new Pos(2, 0), r2.Entities[0].Position);
            var r3 = Rules.Apply(l2, r2, Form.Crane, new Pos(0, 0)).After; // уходим, страж назад на 3,0
            Assert.Equal(GameStatus.Playing, r3.Status);
        }

        [Fact]
        public void Guard_ReversedByBox_PressesPlate()
        {
            var level = Parse("crane=3 plane=0 boat=0 frog=0", new[] { "+T+H+", "x+++y" },
                "route: 0,1 1,1 2,1", "entity: kind=guard at=1,1 dir=1,0 data=0", "entity: kind=box at=2,1 dir=0,0 data=0");
            var s = Rules.Apply(level, GameState.Initial(level), Form.Crane, new Pos(1, 0)).After;
            Assert.Equal(new Pos(0, 1), s.Entities[0].Position); // короб впереди — развернулся и шагнул назад
            Assert.Equal(-1, s.Entities[0].Direction.X);
            s = Rules.Apply(level, s, Form.Crane, new Pos(2, 0)).After;
            Assert.Equal(new Pos(1, 1), s.Entities[0].Position); // открытый маршрут: от края обратно, на плиту
            Assert.True(Rules.PlatePressed(level, s));
        }

        [Fact]
        public void Torii_StepAndFlight_KeepDirection()
        {
            // Журавль шагает в тории (1,0) и выходит из пары (3,2).
            var level = Parse("crane=1 plane=1 boat=0 frog=0 shuriken=1", new[] { "0+0O+", "00000", "xO0+y" }, "torii: 1,0 3,2");
            var step = Rules.GetOptions(level, GameState.Initial(level), Form.Crane).Single(o => o.Target == new Pos(1, 0));
            Assert.Equal(new Pos(3, 2), step.Path.Last());
            var r = Rules.Apply(level, GameState.Initial(level), Form.Crane, new Pos(1, 0));
            Assert.Equal(new Pos(3, 2), r.After.Player);

            // Сюрикен влетает в тории вправо и вылетает из пары дальше вправо.
            var rush = Assert.Single(Rules.GetOptions(level, GameState.Initial(level), Form.Shuriken));
            Assert.Equal(new Pos(4, 2), rush.Target);
        }
    }
}
