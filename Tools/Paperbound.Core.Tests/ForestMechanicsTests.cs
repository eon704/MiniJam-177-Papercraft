using System.IO;
using System.Linq;
using Paperbound.Core;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class ForestMechanicsTests
    {
        private static MoveResult Move(LevelDef level, GameState s, Form f, int x, int y) => Rules.Apply(level, s, f, new Pos(x, y));

        // ── Кувшинка ──────────────────────────────────────────────────────────

        [Fact]
        public void LilyPad_CraneAndFrogStand_PlaneCannot()
        {
            var level = LevelDef.FromRows("SP.E", "....");
            foreach (Form f in Forms.Movable) level.SetBudget(f, 3);
            var s = GameState.Initial(level);
            Assert.True(Move(level, s, Form.Crane, 1, 0).Success);
            var plane = LevelDef.FromRows("S..", ".P.", "..E");
            plane.SetBudget(Form.Plane, 1);
            Assert.Empty(Rules.GetOptions(plane, GameState.Initial(plane), Form.Plane));
        }

        [Fact]
        public void LilyPad_SinksWhenLeft_AndBecomesWater()
        {
            var level = LevelDef.FromRows("SP.E");
            level.SetBudget(Form.Crane, 5);
            var s = Move(level, GameState.Initial(level), Form.Crane, 1, 0).After;
            Assert.Equal(TerrainType.LilyPad, s.TerrainAt(level, new Pos(1, 0)));
            var r = Move(level, s, Form.Crane, 2, 0);
            Assert.Equal(TerrainType.Water, r.After.TerrainAt(level, new Pos(1, 0)));
            Assert.Contains(r.Events, e => e.Type == MoveEventType.Sank && e.Pos == new Pos(1, 0));
            Assert.False(Move(level, r.After, Form.Crane, 1, 0).Success);
        }

        [Fact]
        public void LilyPad_BoatDocksLikeLand_AndSailsOverItAfterItSinks()
        {
            // S ~ P ~ . E : лодка до кувшинки, журавль-лягушка уходит, кувшинка тонет и становится водой.
            var level = LevelDef.FromRows("S~P~.E");
            level.SetBudget(Form.Boat, 3);
            var s = GameState.Initial(level);
            var r = Move(level, s, Form.Boat, 2, 0);
            Assert.True(r.Success);
            Assert.Equal(new Pos(2, 0), r.After.Player);
            r = Move(level, r.After, Form.Boat, 4, 0);
            Assert.True(r.Success);
            Assert.Equal(TerrainType.Water, r.After.TerrainAt(level, new Pos(2, 0)));

            // Вернуться лодкой теперь можно только через воду до старта.
            var back = Rules.GetOptions(level, r.After, Form.Boat);
            Assert.Contains(back, o => o.Target == new Pos(0, 0));
        }

        [Fact]
        public void LilyPad_FrogJumpingOff_SinksIt()
        {
            var level = LevelDef.FromRows("SP~.E");
            level.SetBudget(Form.Crane, 1);
            level.SetBudget(Form.Frog, 1);
            var s = Move(level, GameState.Initial(level), Form.Crane, 1, 0).After;
            var r = Move(level, s, Form.Frog, 3, 0);
            Assert.True(r.Success);
            Assert.Equal(TerrainType.Water, r.After.TerrainAt(level, new Pos(1, 0)));
        }

        // ── Листок ────────────────────────────────────────────────────────────

        [Fact]
        public void Leaf_GivesOneMoveToItsForm_WhenLandedOn()
        {
            var level = LevelDef.FromRows("S..E");
            level.SetBudget(Form.Crane, 1);
            level.SetBudget(Form.Frog, 0);
            level.Items.Add(new ItemDef { Position = new Pos(1, 0), Form = Form.Frog, Amount = 1 });
            var r = Move(level, GameState.Initial(level), Form.Crane, 1, 0);
            Assert.Equal(0, r.After.BudgetOf(Form.Crane));
            Assert.Equal(1, r.After.BudgetOf(Form.Frog));
            Assert.Contains(r.Events, e => e.Type == MoveEventType.ItemCollected);
            Assert.Equal(GameStatus.Won, Move(level, r.After, Form.Frog, 3, 0).After.Status);
        }

        [Fact]
        public void Leaf_IsPickedOnce()
        {
            var level = LevelDef.FromRows("S.E");
            level.SetBudget(Form.Crane, 3);
            level.Items.Add(new ItemDef { Position = new Pos(1, 0), Form = Form.Crane });
            var s = Move(level, GameState.Initial(level), Form.Crane, 1, 0).After;
            Assert.Equal(3, s.BudgetOf(Form.Crane));
            s = Move(level, s, Form.Crane, 0, 0).After;
            s = Move(level, s, Form.Crane, 1, 0).After;
            Assert.Equal(1, s.BudgetOf(Form.Crane));
        }

        [Fact]
        public void Leaf_PlaneAndBoatPickItOnTheWay()
        {
            var plane = LevelDef.FromRows("S..", "...", "..E");
            plane.SetBudget(Form.Plane, 1);
            plane.Items.Add(new ItemDef { Position = new Pos(1, 1), Form = Form.Crane });
            Assert.Equal(1, Move(plane, GameState.Initial(plane), Form.Plane, 2, 2).After.BudgetOf(Form.Crane));

            var boat = LevelDef.FromRows("S~~.E");
            boat.SetBudget(Form.Boat, 1);
            boat.Items.Add(new ItemDef { Position = new Pos(1, 0), Form = Form.Crane });
            Assert.Equal(1, Move(boat, GameState.Initial(boat), Form.Boat, 3, 0).After.BudgetOf(Form.Crane));
        }

        [Fact]
        public void Solver_UsesLeaves()
        {
            // Без листка журавлю не хватает хода.
            var level = LevelDef.FromRows("S***E");
            level.SetBudget(Form.Crane, 3);
            Assert.False(Solver.Solve(level).CanFinish);
            level.Items.Add(new ItemDef { Position = new Pos(2, 0), Form = Form.Crane });
            SolveReport r = Solver.Solve(level);
            Assert.True(r.CanThreeStar);
            Assert.Equal(4, r.MinMovesThreeStars);
        }
    }

    public class LevelTextTests
    {
        private const string Sample = @"# пример
name: sample
size: 5 3
budget: crane=3 plane=1 boat=2 frog=1
rules: slippery_ice=on boat_stars=off plane_over_water=on
map:
x+W+y
+0P0+
++I++
marks:
.*...
..f..
.x..*
leaf: 1,0 frog +2
volcano: 0,2 period=2 -> 1,1 2,1
ice: 4,1 period=1 loop=on -> 3,1
entity: kind=7 at=3,0 dir=1,0 data=4
";

        [Fact]
        public void Parse_ReadsEverything()
        {
            LevelDef l = LevelText.Parse(Sample);
            Assert.Equal("sample", l.Name);
            Assert.Equal(5, l.Width);
            Assert.Equal(TerrainType.Start, l.Terrain[l.Index(new Pos(0, 2))]);
            Assert.Equal(TerrainType.End, l.Terrain[l.Index(new Pos(4, 2))]);
            Assert.Equal(TerrainType.LilyPad, l.Terrain[l.Index(new Pos(2, 1))]);
            Assert.Equal(TerrainType.Ice, l.Terrain[l.Index(new Pos(2, 0))]);
            Assert.True(l.Fragile[l.Index(new Pos(2, 1))]);
            Assert.True(l.Fragile[l.Index(new Pos(1, 0))]);
            Assert.Equal(3, l.Stars.Count);
            Assert.Contains(new Pos(1, 2), l.Stars);
            Assert.Equal(2, l.Budget[(int)Form.Boat]);
            Assert.False(l.Rules.BoatCollectsStars);
            Assert.True(l.Rules.PlaneFliesOverWaterAndEmpty);
            Assert.Equal(Form.Frog, l.Items.Single().Form);
            Assert.Equal(2, l.Items.Single().Amount);
            Assert.Equal(2, l.Schedules.Count);
            Assert.Equal("volcano", l.Schedules[0].Kind);
            Assert.True(l.Schedules[1].Loop);
            Assert.Equal(7, l.Entities.Single().Kind);
        }

        [Fact]
        public void WriteThenParse_RoundTrips()
        {
            LevelDef a = LevelText.Parse(Sample);
            string text = LevelText.Write(a);
            LevelDef b = LevelText.Parse(text);
            Assert.Equal(text, LevelText.Write(b));
            Assert.Equal(a.Terrain, b.Terrain);
            Assert.Equal(a.Fragile, b.Fragile);
            Assert.Equal(a.Stars, b.Stars);
            Assert.Equal(a.Budget, b.Budget);
        }

        [Fact]
        public void Parse_AcceptsLegacyStarCharacters()
        {
            LevelDef l = LevelText.Parse("size: 4 1\nbudget: crane=3\nmap:\nxG2y\n");
            Assert.Equal(2, l.Stars.Count);
            Assert.Equal(TerrainType.Water, l.Terrain[2]);
        }

        [Fact]
        public void LegacyJsonMap_IndexesFromBottomRow()
        {
            LevelDef l = LevelText.FromLegacyMap(4, 3, "00302FGyx000", new[] { 0, 1, 1, 1, 1 });
            Assert.Equal(TerrainType.Start, l.Terrain[l.Index(new Pos(0, 2))]);
            Assert.Equal(TerrainType.End, l.Terrain[l.Index(new Pos(3, 1))]);
            Assert.Equal(3, l.Stars.Count);
        }

        [Fact]
        public void Parse_ReportsLineOfError()
        {
            var e = Assert.Throws<System.FormatException>(() => LevelText.Parse("size: 2 1\nmap:\nxQ\n"));
            Assert.Contains("Q", e.Message);
        }

        [Fact]
        public void ShippedLevels_RoundTripThroughText()
        {
            string dir = Path.Combine(Root(), "Assets/Data/Levels");
            foreach (string f in Directory.GetFiles(dir, "*.asset"))
            {
                LevelDef a = Paperbound.Tools.LevelAssetReader.Read(f);
                LevelDef b = LevelText.Parse(LevelText.Write(a));
                Assert.Equal(a.Terrain, b.Terrain);
                Assert.Equal(a.Stars, b.Stars);
                Assert.Equal(Solver.Solve(a).MinMovesThreeStars, Solver.Solve(b).MinMovesThreeStars);
            }
        }

        private static string Root()
        {
            string dir = System.AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }
    }

    public class LevelQualityTests
    {
        [Fact]
        public void FlagsCheapFinish_AndNoSlack()
        {
            var cheap = LevelDef.FromRows("SE.", "***");
            cheap.SetBudget(Form.Crane, 9);
            Assert.Contains(LevelQuality.FinishTooCheap, LevelQuality.Flags(Solver.Solve(cheap)));

            var tight = LevelDef.FromRows("S***E");
            tight.SetBudget(Form.Crane, 4);
            var flags = LevelQuality.Flags(Solver.Solve(tight));
            Assert.Contains(LevelQuality.NoSlack, flags);
            Assert.DoesNotContain(LevelQuality.FinishTooCheap, flags);
        }
    }
}
