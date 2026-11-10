using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class SnowMechanicsTests
    {
        private static MoveResult Move(LevelDef level, GameState s, Form f, int x, int y) => Rules.Apply(level, s, f, new Pos(x, y));

        [Fact]
        public void ThinIce_HoldsCrane_FrogBreaksItAndSwims()
        {
            var level = LevelDef.FromRows("SnnE");
            level.SetBudget(Form.Crane, 3);
            var crane = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.True(crane.Success);
            Assert.Equal(TerrainType.ThinIce, crane.After.TerrainAt(level, new Pos(1, 0)));

            var frogLevel = LevelDef.FromRows("S.n.E");
            frogLevel.SetBudget(Form.Frog, 2);
            var frog = Move(frogLevel, GameState.Initial(frogLevel), Form.Frog, 2, 0);
            Assert.True(frog.Success);
            Assert.Equal(GameStatus.Playing, frog.After.Status);
            Assert.Equal(TerrainType.Water, frog.After.TerrainAt(frogLevel, new Pos(2, 0)));
            Assert.True(Move(frogLevel, frog.After, Form.Frog, 4, 0).Success); // выпрыгнула из полыньи
        }

        [Fact]
        public void ThinIce_BrokenByFrog_OpensWaterForBoat()
        {
            // Лягушка пробивает лёд, лодка плывёт по полынье.
            var level = LevelDef.FromRows("S.n.E");
            level.SetBudget(Form.Frog, 1);
            level.SetBudget(Form.Boat, 1);
            var s = Move(level, GameState.Initial(level), Form.Frog, 2, 0).After;
            // Лягушка в воде, сворачивается в лодку и причаливает к соседней суше.
            Assert.Contains(Rules.GetOptions(level, s, Form.Boat), o => o.Target == new Pos(3, 0));
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
