using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class SnowMechanicsTests
    {
        private static MoveResult Move(LevelDef level, GameState s, Form f, int x, int y) => Rules.Apply(level, s, f, new Pos(x, y));

        [Fact]
        public void ThinIce_HoldsCrane_CracksUnderFrog_BecomesWaterWhenLeft()
        {
            var level = LevelDef.FromRows("SnnE");
            level.SetBudget(Form.Crane, 3);
            var crane = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.True(crane.Success);
            Assert.Equal(TerrainType.ThinIce, crane.After.TerrainAt(level, new Pos(1, 0)));
            var crane2 = Move(level, crane.After, Form.Crane, 2, 0);
            Assert.Equal(TerrainType.ThinIce, crane2.After.TerrainAt(level, new Pos(1, 0))); // журавль лёд не ломает

            var frogLevel = LevelDef.FromRows("S.n.E");
            frogLevel.SetBudget(Form.Frog, 2);
            var frog = Move(frogLevel, GameState.Initial(frogLevel), Form.Frog, 2, 0);
            Assert.True(frog.Success);
            Assert.Equal(GameStatus.Playing, frog.After.Status);
            Assert.Equal(TerrainType.CrackedIce, frog.After.TerrainAt(frogLevel, new Pos(2, 0)));
            var away = Move(frogLevel, frog.After, Form.Frog, 4, 0);
            Assert.True(away.Success);
            Assert.Equal(TerrainType.Water, away.After.TerrainAt(frogLevel, new Pos(2, 0)));
        }

        [Fact]
        public void ThinIce_BrokenByFrog_OpensWaterForBoat()
        {
            // Лягушка трескает лёд и уходит — полынья становится протокой для лодки.
            var level = LevelDef.FromRows("S~n~E");
            level.SetBudget(Form.Frog, 2);
            level.SetBudget(Form.Boat, 1);
            var s = Move(level, GameState.Initial(level), Form.Frog, 2, 0).After;
            s = Move(level, s, Form.Frog, 0, 0).After; // назад на старт, лёд за спиной стал водой
            Assert.Equal(TerrainType.Water, s.TerrainAt(level, new Pos(2, 0)));
            Assert.Contains(Rules.GetOptions(level, s, Form.Boat), o => o.Target == new Pos(4, 0));
        }

        [Fact]
        public void CrackedIce_ShurikenStopsBeforeHole()
        {
            var level = LevelDef.FromRows("S.n..E");
            level.SetBudget(Form.Frog, 2);
            level.SetBudget(Form.Shuriken, 1);
            var s = Move(level, GameState.Initial(level), Form.Frog, 2, 0).After;
            s = Move(level, s, Form.Frog, 0, 0).After;
            var o = Assert.Single(Rules.GetOptions(level, s, Form.Shuriken));
            Assert.Equal(new Pos(1, 0), o.Target); // упор перед полыньёй
        }

        [Fact]
        public void ThinIce_FrogSwimsHypothesis_BreaksAtOnce()
        {
            var level = LevelDef.FromRows("S.n.E");
            level.Rules.FrogSwims = true;
            level.SetBudget(Form.Frog, 2);
            var s = Move(level, GameState.Initial(level), Form.Frog, 2, 0).After;
            Assert.Equal(GameStatus.Playing, s.Status);
            Assert.Equal(TerrainType.Water, s.TerrainAt(level, new Pos(2, 0)));
        }

        [Fact]
        public void Tide_DrownsFrog_UnlessFrogSwims()
        {
            string text = "name: t\nsize: 4 1\nbudget: crane=0 plane=0 boat=0 frog=1\nmap:\nx+Dy\nmarks:\n....\ntide: period=1 -> 2,0\n";
            var level = LevelText.Parse(text);
            var r = Rules.Apply(level, GameState.Initial(level), Form.Frog, new Pos(2, 0));
            Assert.Equal(GameStatus.Dead, r.After.Status);

            var swim = LevelText.Parse(text);
            swim.Rules.FrogSwims = true;
            Assert.Equal(GameStatus.Playing, Rules.Apply(swim, GameState.Initial(swim), Form.Frog, new Pos(2, 0)).After.Status);
            Assert.Contains("frog_swims=on", LevelText.Write(swim));
        }

        [Fact]
        public void Shuriken_SlidesToObstacle_CutsBamboo()
        {
            var level = LevelDef.FromRows("S.Z.R.E");
            level.SetBudget(Form.Shuriken, 2);
            var options = Rules.GetOptions(level, GameState.Initial(level), Form.Shuriken);
            var o = Assert.Single(options);
            Assert.Equal(new Pos(3, 0), o.Target); // остановился перед камнем
            var r = Move(level, GameState.Initial(level), Form.Shuriken, 3, 0);
            Assert.True(r.Success);
            Assert.Equal(TerrainType.Default, r.After.TerrainAt(level, new Pos(2, 0)));
        }

        [Fact]
        public void Shuriken_StopsBeforeWater_PassesFire_CollectsStars()
        {
            var level = LevelDef.FromRows("SF*~E");
            level.SetBudget(Form.Shuriken, 1);
            var r = Move(level, GameState.Initial(level), Form.Shuriken, 2, 0);
            Assert.True(r.Success);
            Assert.Equal(GameStatus.Playing, r.After.Status);
            Assert.Equal(1, r.After.StarsCollected);
        }

        [Fact]
        public void Bamboo_BlocksOtherForms()
        {
            var level = LevelDef.FromRows("SZ.E");
            level.SetBudget(Form.Crane, 3);
            level.SetBudget(Form.Frog, 1);
            Assert.False(Move(level, GameState.Initial(level), Form.Crane, 1, 0).Success);
            Assert.True(Move(level, GameState.Initial(level), Form.Frog, 2, 0).Success); // перепрыгнуть можно
        }

        [Fact]
        public void Pbl_WritesShurikenBudget()
        {
            var level = LevelDef.FromRows("SZ.E");
            level.SetBudget(Form.Shuriken, 2);
            string text = LevelText.Write(level);
            Assert.Contains("shuriken=2", text);
            Assert.Equal(2, LevelText.Parse(text).Budget[(int)Form.Shuriken]);
        }
    }
}
