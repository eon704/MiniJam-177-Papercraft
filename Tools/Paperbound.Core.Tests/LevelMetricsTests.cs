using System.Linq;
using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class LevelMetricsTests
    {
        [Fact]
        public void Validator_LilyPadMustTouchWater()
        {
            var ok = LevelDef.FromRows("S**P~*E");
            ok.SetBudget(Form.Crane, 6);
            Assert.DoesNotContain(LevelValidator.Validate(ok), e => e.Contains("Кувшинка"));

            var dry = LevelDef.FromRows("S**P.*E");
            dry.SetBudget(Form.Crane, 6);
            Assert.Contains(LevelValidator.Validate(dry), e => e.Contains("Кувшинка (3,0)"));
        }

        [Fact]
        public void DeadCells_AreUnreachableNonEmptyCells()
        {
            var level = LevelDef.FromRows("S*.*.*E", "#######", "..#####");
            level.SetBudget(Form.Crane, 6);
            var dead = LevelMetrics.DeadCells(level);
            Assert.Equal(new[] { new Pos(0, 2), new Pos(1, 2) }, dead.OrderBy(p => p.X).ToArray());
        }

        [Fact]
        public void DeadCells_IgnoreWaterThatHoldsLilyPad()
        {
            var level = LevelDef.FromRows("S**P*E", "###~##");
            level.SetBudget(Form.Crane, 5);
            Assert.Empty(LevelMetrics.DeadCells(level));
        }

        [Fact]
        public void Forgiveness_CountsTwoMoveSequencesThatKeepThreeStars()
        {
            // Коридор: первый ход вынужден, второй — вперёд (3★ ещё возможны) или назад (бюджет 4 уже не хватит).
            var level = LevelDef.FromRows("S***E");
            level.SetBudget(Form.Crane, 4);
            double f = LevelMetrics.Forgiveness(level, out int good, out int total);
            Assert.Equal(1, good);
            Assert.Equal(2, total);
            Assert.Equal(0.5, f, 3);
        }
    }
}
