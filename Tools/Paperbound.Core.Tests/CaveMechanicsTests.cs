using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class CaveMechanicsTests
    {
        private static MoveResult Move(LevelDef level, GameState s, Form f, int x, int y) => Rules.Apply(level, s, f, new Pos(x, y));

        [Fact]
        public void Box_CranePushes_OthersCannot()
        {
            var level = LevelDef.FromRows("Sb..E");
            level.SetBudget(Form.Crane, 3);
            level.SetBudget(Form.Frog, 1);
            var r = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.True(r.Success);
            Assert.Equal(new Pos(2, 0), r.After.Entities[0].Position);
            Assert.Equal(new Pos(1, 0), r.After.Player);
            // Лягушка не может приземлиться на короб.
            var frog = LevelDef.FromRows("S.b.E");
            frog.SetBudget(Form.Frog, 1);
            Assert.DoesNotContain(Rules.GetOptions(frog, GameState.Initial(frog), Form.Frog), o => o.Target == new Pos(2, 0));
        }

        [Fact]
        public void Box_BlockedByStoneAndEnd()
        {
            var level = LevelDef.FromRows("SbR.E");
            level.SetBudget(Form.Crane, 3);
            Assert.False(Move(level, GameState.Initial(level), Form.Crane, 1, 0).Success);
            var end = LevelDef.FromRows("SbE");
            end.SetBudget(Form.Crane, 3);
            Assert.False(Move(end, GameState.Initial(end), Form.Crane, 1, 0).Success);
        }

        [Fact]
        public void Box_FillsHole_BecomesRaftInWater_BurnsInLava()
        {
            var hole = LevelDef.FromRows("Sb#.E");
            hole.SetBudget(Form.Crane, 4);
            var a = Move(hole, GameState.Initial(hole), Form.Crane, 1, 0);
            Assert.Equal(TerrainType.BoxFloor, a.After.TerrainAt(hole, new Pos(2, 0)));
            Assert.True(Move(hole, a.After, Form.Crane, 2, 0).Success); // по полу из короба можно идти

            var water = LevelDef.FromRows("Sb~.E");
            water.SetBudget(Form.Crane, 4);
            var b = Move(water, GameState.Initial(water), Form.Crane, 1, 0);
            Assert.Equal(EntityKind.Raft, b.After.Entities[0].Kind);
            Assert.True(Move(water, b.After, Form.Crane, 2, 0).Success); // встали на плот-причал

            var lava = LevelDef.FromRows("SbL.E");
            lava.SetBudget(Form.Crane, 4);
            var c = Move(lava, GameState.Initial(lava), Form.Crane, 1, 0);
            Assert.Equal(EntityKind.None, c.After.Entities[0].Kind);
            Assert.Equal(TerrainType.Lava, c.After.TerrainAt(lava, new Pos(2, 0)));
        }

        [Fact]
        public void Box_StopsPlane()
        {
            var level = LevelDef.FromRows("S###", "#.##", "##b#", "###E");
            level.SetBudget(Form.Plane, 1);
            var options = Rules.GetOptions(level, GameState.Initial(level), Form.Plane);
            Assert.Contains(options, o => o.Target == new Pos(1, 1));
            Assert.DoesNotContain(options, o => o.Target == new Pos(3, 3));
        }

        [Fact]
        public void PlateOpensGate_WhileCraneOrBoxStandsOnIt()
        {
            // S T H E : журавль на плите → ворота открыты; ушёл — закрылись.
            var level = LevelDef.FromRows("STH.E", ".....");
            level.Terrain[level.Index(new Pos(1, 0))] = TerrainType.Plate;
            level.Terrain[level.Index(new Pos(2, 0))] = TerrainType.Gate;
            level.SetBudget(Form.Crane, 6);
            level.SetBudget(Form.Plane, 1);
            var s = GameState.Initial(level);
            var onPlate = Move(level, s, Form.Crane, 1, 0).After;
            Assert.True(Rules.GateOpen(level, onPlate, new Pos(2, 0)));
            var through = Move(level, onPlate, Form.Crane, 2, 0);
            Assert.True(through.Success); // в открытые ворота можно войти; занятые не закрываются
            var away = Move(level, onPlate, Form.Crane, 1, 1).After;
            Assert.False(Rules.GateOpen(level, away, new Pos(2, 0)));
        }

        [Fact]
        public void Plate_NotPressedByPlane()
        {
            var level = LevelDef.FromRows("S##", "#T#", "##H", "##E");
            level.Terrain[level.Index(new Pos(1, 1))] = TerrainType.Plate;
            level.Terrain[level.Index(new Pos(2, 2))] = TerrainType.Gate;
            level.SetBudget(Form.Plane, 2);
            var r = Move(level, GameState.Initial(level), Form.Plane, 1, 1);
            Assert.True(r.Success);
            Assert.False(Rules.PlatePressed(level, r.After));
        }

        [Fact]
        public void Validator_GateNeedsPlate_AndRaftsMustNotContest()
        {
            var gate = LevelDef.FromRows("S*H**E");
            gate.Terrain[gate.Index(new Pos(2, 0))] = TerrainType.Gate;
            gate.SetBudget(Form.Crane, 5);
            Assert.Contains(LevelValidator.Validate(gate), e => e.Contains("плиты"));

            var rafts = LevelDef.FromRows("S~~~*", "**..E");
            rafts.Flow[rafts.Index(new Pos(1, 0))] = Pos.Right;
            rafts.Flow[rafts.Index(new Pos(3, 0))] = Pos.Left;
            rafts.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = new Pos(1, 0) });
            rafts.Entities.Add(new EntityDef { Kind = EntityKind.Raft, Position = new Pos(3, 0) });
            rafts.SetBudget(Form.Crane, 5);
            Assert.Contains(LevelValidator.Validate(rafts), e => e.Contains("спорить"));
        }
    }
}
