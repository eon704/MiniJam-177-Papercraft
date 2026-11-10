using System.Linq;
using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class OceanMechanicsTests
    {
        private static MoveResult Move(LevelDef level, GameState s, Form f, int x, int y) => Rules.Apply(level, s, f, new Pos(x, y));

        private static void SetFlow(LevelDef level, int x, int y, Pos dir) => level.Flow[level.Index(new Pos(x, y))] = dir;

        // ── Течение ───────────────────────────────────────────────────────────

        [Fact]
        public void Current_TurnsBoat()
        {
            // y=0: S ~ ~ #     течение в (2,0) вверх → лодка поворачивает и причаливает к (2,2)
            // y=1: # # ~ #
            // y=2: # # . E
            var level = LevelDef.FromRows("S~~#", "##~#", "##.E");
            SetFlow(level, 2, 0, Pos.Up);
            level.SetBudget(Form.Boat, 2);
            var r = Move(level, GameState.Initial(level), Form.Boat, 2, 2);
            Assert.True(r.Success);
            Assert.Equal(new[] { new Pos(1, 0), new Pos(2, 0), new Pos(2, 1), new Pos(2, 2) }, r.Path.ToArray());
        }

        [Fact]
        public void Current_CannotEnterAgainstFlow()
        {
            var level = LevelDef.FromRows("S~~.E");
            SetFlow(level, 1, 0, Pos.Left);
            level.SetBudget(Form.Boat, 1);
            Assert.DoesNotContain(Rules.GetOptions(level, GameState.Initial(level), Form.Boat), o => o.Target == new Pos(3, 0));
        }

        [Fact]
        public void Current_LoopWithoutExit_IsNoMove()
        {
            // Кольцо течения 2x2 посреди воды без выхода.
            var level = LevelDef.FromRows("S~~#", "#~~#", "####");
            SetFlow(level, 1, 0, Pos.Right); SetFlow(level, 2, 0, Pos.Up);
            SetFlow(level, 2, 1, Pos.Left); SetFlow(level, 1, 1, Pos.Down);
            level.SetBudget(Form.Boat, 1);
            Assert.Empty(Rules.GetOptions(level, GameState.Initial(level), Form.Boat));
        }

        // ── Прилив ────────────────────────────────────────────────────────────

        [Fact]
        public void Tide_TogglesWaterAndSand()
        {
            var level = LevelDef.FromRows("S.D~E");
            level.Schedules.Add(ScheduleDef.Tide(1, new[] { new Pos(2, 0), new Pos(3, 0) }));
            level.SetBudget(Form.Crane, 3);
            var r = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.Equal(TerrainType.Water, r.After.TerrainAt(level, new Pos(2, 0)));
            Assert.Equal(TerrainType.Sand, r.After.TerrainAt(level, new Pos(3, 0)));
        }

        [Fact]
        public void Tide_DrownsCraneButNotBoat()
        {
            var level = LevelDef.FromRows("SD..E");
            level.Schedules.Add(ScheduleDef.Tide(1, new[] { new Pos(1, 0) }));
            level.SetBudget(Form.Crane, 1);
            var crane = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.Equal(GameStatus.Dead, crane.After.Status);
            Assert.Equal(DeathCause.Drowned, crane.After.Death);

            var boatLevel = LevelDef.FromRows("S~D..E");
            boatLevel.Schedules.Add(ScheduleDef.Tide(1, new[] { new Pos(2, 0) }));
            boatLevel.SetBudget(Form.Boat, 2);
            var boat = Move(boatLevel, GameState.Initial(boatLevel), Form.Boat, 2, 0);
            Assert.Equal(GameStatus.Playing, boat.After.Status);
            // Лодка на залитой клетке причаливает к соседней суше одним шагом.
            Assert.Contains(Rules.GetOptions(boatLevel, boat.After, Form.Boat), o => o.Target == new Pos(3, 0));
        }

        // ── Плот ──────────────────────────────────────────────────────────────

        private static LevelDef RaftLevel()
        {
            // y=0: S ~ ~ ~ . E   течение вправо по воде, плот на (1,0)
            var level = LevelDef.FromRows("S~~w.E");
            for (int x = 1; x <= 3; x++) SetFlow(level, x, 0, Pos.Right);
            level.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = new Pos(1, 0) });
            return level;
        }

        [Fact]
        public void Raft_CarriesCraneAlongCurrent_AndCollectsStar()
        {
            var level = RaftLevel();
            level.SetBudget(Form.Crane, 5);
            var s1 = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.True(s1.Success);
            Assert.Equal(new Pos(2, 0), s1.After.Player); // плот сдвинулся и увёз журавля
            Assert.Contains(s1.Events, e => e.Type == MoveEventType.Carried);
            var s2 = Move(level, s1.After, Form.Crane, 1, 0); // шаг назад на воду невозможен
            Assert.False(s2.Success);
        }

        [Fact]
        public void Raft_DriftsWithoutRider_AndStopsAtShore()
        {
            // Журавль ходит по суше туда-обратно, плот без седока дрейфует по течению до берега.
            var l2 = LevelDef.FromRows("S~~~.E", ".#####");
            for (int x = 1; x <= 3; x++) SetFlow(l2, x, 0, Pos.Right);
            l2.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = new Pos(1, 0) });
            l2.SetBudget(Form.Crane, 5);
            var s = GameState.Initial(l2);
            for (int k = 0; k < 4; k++)
            {
                var r = Move(l2, s, Form.Crane, 0, s.Player == new Pos(0, 0) ? 1 : 0);
                Assert.True(r.Success);
                s = r.After;
            }
            Assert.Equal(new Pos(3, 0), s.Entities[0].Position); // дальше суша — плот стоит
        }

        [Fact]
        public void Raft_BoatDocks_PlaneCannotLand()
        {
            var level = LevelDef.FromRows("S~~~~E");
            level.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = new Pos(3, 0) });
            level.SetBudget(Form.Boat, 2);
            var r = Move(level, GameState.Initial(level), Form.Boat, 3, 0);
            Assert.True(r.Success); // лодка причалила к плоту
            Assert.Equal(new Pos(3, 0), r.After.Player);

            var plane = LevelDef.FromRows("S#", "#~", "##");
            plane.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = new Pos(1, 1) });
            plane.SetBudget(Form.Plane, 1);
            Assert.Empty(Rules.GetOptions(plane, GameState.Initial(plane), Form.Plane));
        }

        [Fact]
        public void Raft_UndoIsJustPreviousState()
        {
            var level = RaftLevel();
            level.SetBudget(Form.Crane, 5);
            var start = GameState.Initial(level);
            var r = Move(level, start, Form.Crane, 1, 0);
            Assert.Equal(new Pos(1, 0), start.Entities[0].Position); // старое состояние не изменилось
            Assert.Equal(new Pos(2, 0), r.After.Entities[0].Position);
        }

        [Fact]
        public void Pbl_RoundTripsFlowTideAndRaft()
        {
            string text = "name: t\nsize: 4 2\nbudget: crane=2 plane=0 boat=2 frog=0\nmap:\nx+Dy\nWWW+\nmarks:\n.*..\n*..*\nflow:\n....\n>>^.\ntide: period=2 -> 2,1\nentity: kind=raft at=0,0 dir=0,0 data=0\n";
            var level = LevelText.Parse(text);
            Assert.Equal(Pos.Right, level.FlowAt(new Pos(0, 0)));
            Assert.Equal(Pos.Up, level.FlowAt(new Pos(2, 0)));
            Assert.Equal("tide", level.Schedules[0].Kind);
            Assert.Equal(EntityKind.Raft, level.Entities[0].Kind);
            var again = LevelText.Parse(LevelText.Write(level));
            Assert.Equal(LevelText.Write(level), LevelText.Write(again));
        }
    }
}
