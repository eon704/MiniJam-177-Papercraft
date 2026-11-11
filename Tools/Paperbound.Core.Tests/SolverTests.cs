using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Paperbound.Core;
using Paperbound.Tools;
using Xunit;

namespace Paperbound.Core.Tests
{
    public class SolverTests
    {
        [Fact]
        public void FindsMinMoves_ForFinishAndThreeStars()
        {
            var level = LevelDef.FromRows(
                "S..E",
                "*.**");
            level.SetBudget(Form.Crane, 10);
            SolveReport r = Solver.Solve(level);
            Assert.True(r.CanFinish);
            Assert.Equal(3, r.MinMovesToFinish);
            Assert.True(r.CanThreeStar);
            Assert.Equal(5, r.MinMovesThreeStars);
            Assert.Equal(7, r.FinishSlack);
            Assert.Equal(GameStatus.Won, Solver.Replay(level, r.ThreeStarPath).Status);
            Assert.Equal(3, Solver.Replay(level, r.ThreeStarPath).StarsCollected);
        }

        [Fact]
        public void MinFolds_PrefersFewerFormChanges_EvenIfLonger()
        {
            // Лягушкой быстрее (2 прыжка), но звёзды по дороге собирает только журавль одной формой.
            var level = LevelDef.FromRows("S***E");
            level.SetBudget(Form.Crane, 4);
            level.SetBudget(Form.Frog, 2);
            SolveReport r = Solver.Solve(level);
            Assert.Equal(4, r.MinMovesThreeStars);
            Assert.Equal(1, r.MinFoldsThreeStars);
            Assert.All(r.MinFoldsPath, st => Assert.Equal(Form.Crane, st.Form));
        }

        [Fact]
        public void CountsSolutions()
        {
            // Две дороги вокруг ямы, обе собирают звёзды? Нет: звёзды только на верхней.
            var level = LevelDef.FromRows(
                "S..",
                ".#.",
                "..E");
            level.Stars.Add(new Pos(1, 0));
            level.Stars.Add(new Pos(2, 0));
            level.Stars.Add(new Pos(2, 1));
            level.SetBudget(Form.Crane, 4);
            SolveReport r = Solver.Solve(level);
            Assert.Equal(2, r.FinishSolutionCount);
            Assert.Equal(1, r.ThreeStarSolutionCount);
        }

        [Fact]
        public void ReportsUnsolvable()
        {
            var level = LevelDef.FromRows("S~E");
            level.SetBudget(Form.Crane, 3);
            SolveReport r = Solver.Solve(level);
            Assert.False(r.CanFinish);
            Assert.False(r.CanThreeStar);
        }

        [Fact]
        public void DeathIsNeverPartOfASolution()
        {
            var level = LevelDef.FromRows(
                "SF.",
                "..E");
            level.SetBudget(Form.Crane, 3);
            SolveReport r = Solver.Solve(level);
            Assert.Equal(3, r.MinMovesToFinish);
            Assert.DoesNotContain(r.FinishPath, st => st.Target == new Pos(1, 0));
        }
    }

    /// <summary>Регрессия по настоящим уровням из Assets/Data/Levels.</summary>
    public class ShippedLevelsTests
    {
        public static IEnumerable<object[]> LevelFiles()
        {
            string dir = Path.Combine(RepoRoot(), "Assets/Data/Levels");
            return Directory.GetFiles(dir, "*.asset").OrderBy(f => f).Select(f => new object[] { Path.GetFileName(f) });
        }

        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets")))
                dir = Path.GetDirectoryName(dir);
            return dir;
        }

        private static string LevelPath(string file) => Path.Combine(RepoRoot(), "Assets/Data/Levels", file);

        [Theory]
        [MemberData(nameof(LevelFiles))]
        public void Level_IsValid(string file)
        {
            LevelDef level = LevelAssetReader.Read(LevelPath(file));
            Assert.Empty(LevelValidator.Validate(level));
        }

        [Theory]
        [MemberData(nameof(LevelFiles))]
        public void Level_IsThreeStarSolvable_UnderNewAndLegacyRules(string file)
        {
            foreach (RuleSet rules in new[] { RuleSet.Default, RuleSet.Legacy })
            {
                LevelDef level = LevelAssetReader.Read(LevelPath(file), rules);
                SolveReport r = Solver.Solve(level);
                Assert.False(r.Truncated);
                Assert.True(r.CanThreeStar, $"{file}: no 3-star solution");
                GameState end = Solver.Replay(level, r.ThreeStarPath);
                Assert.Equal(GameStatus.Won, end.Status);
                Assert.Equal(3, end.StarsCollected);
            }
        }
    }
}
