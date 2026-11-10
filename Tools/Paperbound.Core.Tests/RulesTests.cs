using System.Linq;
using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class RulesTests
    {
        private static LevelDef Level(params string[] rows) => LevelDef.FromRows(rows);

        private static MoveResult Move(LevelDef level, GameState s, Form f, int x, int y) => Rules.Apply(level, s, f, new Pos(x, y));

        private static LevelDef WithBudget(LevelDef level, Form f, int moves)
        {
            level.SetBudget(f, moves);
            return level;
        }

        // ── Журавль ───────────────────────────────────────────────────────────

        [Fact]
        public void Crane_StepsOrthogonally_AndCannotEnterWater()
        {
            var level = WithBudget(Level("S.~"), Form.Crane, 5);
            var s = GameState.Initial(level);
            var options = Rules.GetOptions(level, s, Form.Crane);
            Assert.Single(options);
            Assert.Equal(new Pos(1, 0), options[0].Target);

            s = Move(level, s, Form.Crane, 1, 0).After;
            var fromMiddle = Rules.GetOptions(level, s, Form.Crane);
            Assert.Contains(fromMiddle, o => o.Target == new Pos(2, 0) && !o.Legal);
            Assert.False(Move(level, s, Form.Crane, 2, 0).Success);
        }

        [Fact]
        public void Crane_OnlyFormThatWalksOnStone()
        {
            var level = Level("SR.E");
            foreach (Form f in Forms.Movable) level.SetBudget(f, 3);
            var s = GameState.Initial(level);
            Assert.True(Move(level, s, Form.Crane, 1, 0).Success);
            Assert.False(Move(level, s, Form.Boat, 1, 0).Success);
            Assert.DoesNotContain(Rules.GetOptions(level, s, Form.Plane), o => o.Target == new Pos(1, 0));
        }

        [Fact]
        public void Crane_SlidesOnIce_UntilFirstNonIceCell()
        {
            var level = WithBudget(Level("SIII.E"), Form.Crane, 5);
            var r = Move(level, GameState.Initial(level), Form.Crane, 4, 0);
            Assert.True(r.Success);
            Assert.Equal(new Pos(4, 0), r.After.Player);
            Assert.Equal(4, r.Path.Count);
            Assert.Equal(4, r.After.Budget[(int)Form.Crane]);
        }

        [Fact]
        public void Crane_ClickOnFirstIceCell_ResolvesToSlideEnd()
        {
            var level = WithBudget(Level("SII.E"), Form.Crane, 5);
            var option = Rules.ResolveClick(level, GameState.Initial(level), Form.Crane, new Pos(1, 0));
            Assert.NotNull(option);
            Assert.Equal(new Pos(3, 0), option.Target);
        }

        [Fact]
        public void Crane_StopsOnLastIce_WhenNextCellBlocked()
        {
            var level = WithBudget(Level("SII~E"), Form.Crane, 5);
            var r = Move(level, GameState.Initial(level), Form.Crane, 2, 0);
            Assert.True(r.Success);
            Assert.Equal(new Pos(2, 0), r.After.Player);
        }

        [Fact]
        public void Crane_SlideCollectsStarsOnIce()
        {
            var level = WithBudget(Level("SI.E"), Form.Crane, 5);
            level.Stars.Add(new Pos(1, 0));
            var r = Move(level, GameState.Initial(level), Form.Crane, 2, 0);
            Assert.Equal(1, r.After.StarsCollected);
        }

        [Fact]
        public void Ice_IsNotSlippery_UnderLegacyRules()
        {
            var level = WithBudget(Level("SII.E"), Form.Crane, 5);
            level.Rules = RuleSet.Legacy;
            var r = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.True(r.Success);
            Assert.Equal(new Pos(1, 0), r.After.Player);
        }

        [Fact]
        public void Frog_LandsOnIce_WithoutSliding()
        {
            var level = WithBudget(Level("S.II.E"), Form.Frog, 5);
            var r = Move(level, GameState.Initial(level), Form.Frog, 2, 0);
            Assert.True(r.Success);
            Assert.Equal(new Pos(2, 0), r.After.Player);
        }

        // ── Лягушка ───────────────────────────────────────────────────────────

        [Fact]
        public void Frog_JumpsExactlyTwo_OverAnything()
        {
            var level = WithBudget(Level("S#.~.FE"), Form.Frog, 5);
            var s = GameState.Initial(level);
            var options = Rules.GetOptions(level, s, Form.Frog);
            Assert.Single(options);
            Assert.Equal(new Pos(2, 0), options[0].Target);
            s = Move(level, s, Form.Frog, 2, 0).After;
            s = Move(level, s, Form.Frog, 4, 0).After;
            Assert.Equal(new Pos(4, 0), s.Player);
            Assert.Equal(GameStatus.Won, Move(level, s, Form.Frog, 6, 0).After.Status);
        }

        [Fact]
        public void Frog_CannotLandOnStoneOrWater()
        {
            var level = WithBudget(Level("S.R.~"), Form.Frog, 5);
            var s = GameState.Initial(level);
            Assert.False(Move(level, s, Form.Frog, 2, 0).Success);
        }

        // ── Самолёт ───────────────────────────────────────────────────────────

        [Fact]
        public void Plane_SlidesDiagonally_AnyDistance_CollectsStarsAndCollapsesFragile()
        {
            var level = WithBudget(Level(
                "S...",
                ".x..",
                "..*.",
                "...E"), Form.Plane, 1);
            var r = Move(level, GameState.Initial(level), Form.Plane, 3, 3);
            Assert.True(r.Success);
            Assert.Equal(GameStatus.Won, r.After.Status);
            Assert.Equal(2, r.After.StarsCollected);
            Assert.True(r.After.IsCollapsed(level, new Pos(1, 1)));
        }

        [Fact]
        public void Plane_BlockedByWaterStoneAndEmpty()
        {
            var level = WithBudget(Level(
                "S..",
                ".~.",
                "..E"), Form.Plane, 1);
            var s = GameState.Initial(level);
            Assert.Empty(Rules.GetOptions(level, s, Form.Plane));
        }

        [Fact]
        public void Plane_FliesOverWaterAndEmpty_WhenHypothesisEnabled()
        {
            var level = WithBudget(Level(
                "S...",
                ".~..",
                "..#.",
                "...E"), Form.Plane, 1);
            level.Rules = new RuleSet { PlaneFliesOverWaterAndEmpty = true };
            var s = GameState.Initial(level);
            var options = Rules.GetOptions(level, s, Form.Plane);
            Assert.Single(options);
            Assert.Equal(new Pos(3, 3), options[0].Target);
            Assert.Equal(GameStatus.Won, Move(level, s, Form.Plane, 3, 3).After.Status);
        }

        [Fact]
        public void Plane_FlyingOverFire_Dies_AtTheFireCell()
        {
            var level = WithBudget(Level(
                "S..",
                ".F.",
                "..*"), Form.Plane, 1);
            level.Terrain[level.Index(new Pos(2, 2))] = TerrainType.Default;
            level.Terrain[level.Index(new Pos(2, 0))] = TerrainType.End;
            var r = Move(level, GameState.Initial(level), Form.Plane, 2, 2);
            Assert.True(r.Success);
            Assert.Equal(GameStatus.Dead, r.After.Status);
            Assert.Equal(DeathCause.FlewOverFire, r.After.Death);
            Assert.Equal(new Pos(1, 1), r.After.DeathPos);
            Assert.Equal(0, r.After.StarsCollected);
        }

        // ── Лодка ─────────────────────────────────────────────────────────────

        [Fact]
        public void Boat_SailsThroughWater_ToFirstLand()
        {
            var level = WithBudget(Level("S~~.E"), Form.Boat, 2);
            var s = GameState.Initial(level);
            var options = Rules.GetOptions(level, s, Form.Boat);
            Assert.Single(options);
            Assert.Equal(new Pos(3, 0), options[0].Target);
            Assert.Equal(new Pos(3, 0), Rules.ResolveClick(level, s, Form.Boat, new Pos(1, 0)).Target);
        }

        [Fact]
        public void Boat_NeedsAdjacentWater()
        {
            var level = WithBudget(Level("S.~.E"), Form.Boat, 2);
            Assert.Empty(Rules.GetOptions(level, GameState.Initial(level), Form.Boat));
        }

        [Fact]
        public void Boat_NoLanding_WhenWaterEndsAtEdgeOrStone()
        {
            var level = WithBudget(Level("S~~R", "E..."), Form.Boat, 2);
            Assert.Empty(Rules.GetOptions(level, GameState.Initial(level), Form.Boat));
        }

        [Fact]
        public void Boat_CollectsStarsOnWater_UnderNewRules()
        {
            var level = WithBudget(Level("Sw~.E"), Form.Boat, 1);
            Assert.Equal(1, Move(level, GameState.Initial(level), Form.Boat, 3, 0).After.StarsCollected);

            level.Rules = RuleSet.Legacy;
            Assert.Equal(0, Move(level, GameState.Initial(level), Form.Boat, 3, 0).After.StarsCollected);
        }

        // ── Общие правила ─────────────────────────────────────────────────────

        [Fact]
        public void Move_SpendsBudgetOfThatForm_AndFailsWhenEmpty()
        {
            var level = WithBudget(Level("S..E"), Form.Crane, 1);
            var s = GameState.Initial(level);
            var r = Move(level, s, Form.Crane, 1, 0);
            Assert.Equal(0, r.After.Budget[(int)Form.Crane]);
            Assert.Equal(1, s.Budget[(int)Form.Crane]);
            var r2 = Move(level, r.After, Form.Crane, 2, 0);
            Assert.False(r2.Success);
            Assert.Equal(MoveFailure.NoBudget, r2.Failure);
        }

        [Fact]
        public void Apply_DoesNotMutateInputState()
        {
            var level = WithBudget(Level("Sf*E"), Form.Crane, 3);
            var s = GameState.Initial(level);
            var a = Move(level, s, Form.Crane, 1, 0).After;
            var b = Move(level, a, Form.Crane, 2, 0).After;
            Assert.Equal(new Pos(0, 0), s.Player);
            Assert.False(a.IsCollapsed(level, new Pos(1, 0)));
            Assert.True(b.IsCollapsed(level, new Pos(1, 0)));
            Assert.Equal(0, a.StarsCollected);
            Assert.Equal(1, b.StarsCollected);
        }

        [Fact]
        public void FragileCell_CollapsesWhenLeft_AndBecomesHole()
        {
            var level = WithBudget(Level("Sf.E"), Form.Crane, 5);
            var s = GameState.Initial(level);
            s = Move(level, s, Form.Crane, 1, 0).After;
            s = Move(level, s, Form.Crane, 2, 0).After;
            Assert.True(s.IsCollapsed(level, new Pos(1, 0)));
            Assert.DoesNotContain(Rules.GetOptions(level, s, Form.Crane), o => o.Target == new Pos(1, 0));
        }

        [Fact]
        public void EndCell_WinsWithAnyStars()
        {
            var level = WithBudget(Level("SE*"), Form.Crane, 5);
            var r = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.Equal(GameStatus.Won, r.After.Status);
            Assert.False(Move(level, r.After, Form.Crane, 2, 0).Success);
        }

        [Fact]
        public void LandingOnFire_Dies()
        {
            var level = WithBudget(Level("SFE"), Form.Crane, 5);
            var r = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.Equal(GameStatus.Dead, r.After.Status);
            Assert.Equal(DeathCause.Terrain, r.After.Death);
        }

        [Fact]
        public void Folds_CountFormChanges()
        {
            var level = Level("S...E");
            level.SetBudget(Form.Crane, 5);
            level.SetBudget(Form.Frog, 5);
            var s = GameState.Initial(level);
            s = Move(level, s, Form.Crane, 1, 0).After;
            Assert.Equal(1, s.Folds);
            s = Move(level, s, Form.Crane, 2, 0).After;
            Assert.Equal(1, s.Folds);
            s = Rules.Fold(s, Form.Frog);
            Assert.Equal(2, s.Folds);
            s = Rules.Fold(s, Form.Frog);
            Assert.Equal(2, s.Folds);
            s = Move(level, s, Form.Frog, 4, 0).After;
            Assert.Equal(2, s.Folds);
            Assert.Equal(GameStatus.Won, s.Status);
        }

        // ── Расписания ────────────────────────────────────────────────────────

        [Fact]
        public void Volcano_EruptsEveryPeriod_AndKillsPlayerOnTarget()
        {
            var level = WithBudget(Level("S...E", "V...."), Form.Crane, 10);
            level.Schedules.Add(ScheduleDef.Volcano(new Pos(0, 1), 2, new[] { new Pos(2, 0), new Pos(3, 0) }));
            var s = GameState.Initial(level);
            s = Move(level, s, Form.Crane, 1, 0).After;
            Assert.Equal(TerrainType.Default, s.TerrainAt(level, new Pos(2, 0)));
            var r = Move(level, s, Form.Crane, 2, 0);
            Assert.Equal(GameStatus.Dead, r.After.Status);
            Assert.Equal(DeathCause.Schedule, r.After.Death);
            Assert.Contains(r.Events, e => e.Type == MoveEventType.TerrainChanged && e.Pos == new Pos(2, 0));
        }

        [Fact]
        public void Volcano_LavaBecomesDeadlyTerrain()
        {
            var level = WithBudget(Level("S...E", "V...."), Form.Crane, 10);
            level.Schedules.Add(ScheduleDef.Volcano(new Pos(0, 1), 1, new[] { new Pos(2, 0) }));
            var s = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.Equal(TerrainType.Lava, s.After.TerrainAt(level, new Pos(2, 0)));
            Assert.Equal(TerrainType.Default, s.Before.TerrainAt(level, new Pos(2, 0)));
            var death = Move(level, s.After, Form.Crane, 2, 0);
            Assert.Equal(DeathCause.Terrain, death.After.Death);
        }

        [Fact]
        public void IceSource_FreezesQueueInOrder()
        {
            var level = WithBudget(Level("S....E"), Form.Crane, 10);
            level.Schedules.Add(ScheduleDef.IceSource(new Pos(5, 0), 1, new[] { new Pos(3, 0), new Pos(4, 0) }));
            var s = Move(level, GameState.Initial(level), Form.Crane, 1, 0).After;
            Assert.Equal(TerrainType.Ice, s.TerrainAt(level, new Pos(3, 0)));
            Assert.Equal(TerrainType.Default, s.TerrainAt(level, new Pos(4, 0)));
            var upcoming = Rules.UpcomingEffects(level, s).Single();
            Assert.Equal(new Pos(4, 0), upcoming.target);
        }

        [Fact]
        public void UndoHistory_IsAStackOfStates_IncludingSchedulesAndEntities()
        {
            var level = WithBudget(Level("S....E"), Form.Crane, 10);
            level.Schedules.Add(ScheduleDef.IceSource(new Pos(5, 0), 1, new[] { new Pos(3, 0) }));
            level.Entities.Add(new EntityDef { Kind = 1, Position = new Pos(4, 0) });
            var history = new System.Collections.Generic.Stack<GameState>();
            var s = GameState.Initial(level);
            history.Push(s);
            s = Move(level, s, Form.Crane, 1, 0).After;
            history.Push(s);
            s.Entities[0].Position = new Pos(9, 9); // будущая механика двигает сущность в своём состоянии
            history.Pop();
            GameState back = history.Peek();
            Assert.Equal(new Pos(0, 0), back.Player);
            Assert.Equal(TerrainType.Default, back.TerrainAt(level, new Pos(3, 0)));
            Assert.Equal(new Pos(4, 0), back.Entities[0].Position);
            Assert.Equal(level.Schedules[0].Period, back.ScheduleCountdown[0]);
        }

        // ── Валидатор ─────────────────────────────────────────────────────────

        [Fact]
        public void Validator_RejectsNegativeBudget()
        {
            var level = Level("S***E");
            level.SetBudget(Form.Crane, -1);
            Assert.Contains(LevelValidator.Validate(level), e => e.Contains("-1"));
            level.SetBudget(Form.Crane, 4);
            Assert.Empty(LevelValidator.Validate(level));
        }

        [Fact]
        public void Validator_RequiresOneStartOneEndThreeStars()
        {
            var level = Level("SS*E");
            level.SetBudget(Form.Crane, 4);
            var errors = LevelValidator.Validate(level);
            Assert.Contains(errors, e => e.Contains("Start"));
            Assert.Contains(errors, e => e.Contains("Звёзд"));
        }
    }
}
