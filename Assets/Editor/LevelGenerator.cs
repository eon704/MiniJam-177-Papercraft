using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Procedural level generator. Uses a solution-first approach:
/// 1. Build a guaranteed-winning move sequence.
/// 2. Trace that sequence onto a virtual grid.
/// 3. Grow decoy branches from solution cells so the player can get lost.
/// 4. Validate solvability and tightness (all moves consumed).
/// </summary>
public static class LevelGenerator
{
    // =========================================================================
    // Public configuration types
    // =========================================================================

    [Serializable]
    public class GenerationParams
    {
        public int GridMinX = 4, GridMinY = 4;
        public int GridMaxX = 9, GridMaxY = 9;

        public List<FormConfig> Forms = new()
        {
            new() { State = Player.StateType.Crane, Enabled = true,  MovesMin = 1, MovesMax = 3 },
            new() { State = Player.StateType.Frog,  Enabled = true,  MovesMin = 1, MovesMax = 3 },
            new() { State = Player.StateType.Plane, Enabled = false, MovesMin = 1, MovesMax = 2 },
            new() { State = Player.StateType.Boat,  Enabled = false, MovesMin = 1, MovesMax = 2 },
        };

        public PathPattern   Pattern  = PathPattern.Zigzag;
        public DecoyIntensity Decoys  = DecoyIntensity.Medium;

        public bool UseWater   = true;
        public bool UseStone   = true;
        public bool UseFire    = false;
        public bool UseLava    = false;
        public bool UseIce     = false;
        public bool UseFragile = false;

        public BiomeType Biome        = BiomeType.None;
        public int       VariantCount = 8;
        public int       MaxAttempts  = 300;
    }

    [Serializable]
    public class FormConfig
    {
        public Player.StateType State;
        public bool             Enabled;
        public int              MovesMin = 1, MovesMax = 3;
    }

    public enum PathPattern    { Linear, Zigzag, Grouped, Mixed }
    public enum DecoyIntensity { Low, Medium, High }

    public class GenerationResult
    {
        public LevelData               Level;
        public bool                    IsSolvable;
        public bool                    IsTight;
        public int                     SolutionSteps;
        public List<Player.StateType>  FormsUsed = new();
        public int                     Score;
        public string                  FailReason;
    }

    // =========================================================================
    // Internal types
    // =========================================================================

    private struct SolMove
    {
        public Player.StateType Form;
        public Vector2Int       Direction;
        public int              SlideLen;   // Plane: cells in slide (1-3)
        public int              WaterLen;   // Boat: water cells before landing (1-3)
    }

    private enum CellRole { Path, Water, Decoy, Obstacle }

    private class VCell
    {
        public TerrainType Terrain;
        public CellItem    Item;
        public bool        IsFragile;
        public CellRole    Role;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    public static List<GenerationResult> GenerateAll(GenerationParams p)
    {
        var results = new List<GenerationResult>(p.VariantCount);
        for (int i = 0; i < p.VariantCount; i++)
            results.Add(Generate(p, new System.Random()));
        return results;
    }

    public static GenerationResult Generate(GenerationParams p, System.Random rng)
    {
        for (int attempt = 0; attempt < p.MaxAttempts; attempt++)
        {
            // 1. Sequence of form-moves
            var sequence = BuildSequence(p, rng);
            if (sequence == null || sequence.Count == 0) continue;

            // 2. Trace solution path on a virtual infinite grid
            var cellMap      = new Dictionary<Vector2Int, VCell>();
            var solutionPath = new List<Vector2Int>();
            if (!TracePath(sequence, p, rng, cellMap, solutionPath)) continue;
            if (solutionPath.Count < 4) continue;

            // 3. Branch decoy paths from solution cells
            AddDecoys(cellMap, solutionPath, p, rng);

            // 3.5 Apply special cell challenges (Fire traps, Volcano/Lava, Ice barriers, Fragile)
            var volcanoConfigs = ApplySpecialCellChallenges(cellMap, solutionPath, sequence, p, rng);

            // 4. Place stars + mark start/end
            if (!PlaceSpecialCells(cellMap, solutionPath, rng)) continue;

            // 5. Convert virtual grid → LevelData
            var offset = ComputeOffset(cellMap);
            var level  = BuildGrid(cellMap, solutionPath, sequence, p, offset, volcanoConfigs);
            if (level == null || !level.IsValid()) continue;

            // 6. Solvability check
            var sol = LevelEditorWindow.SolveLevel(level);
            if (sol == null) continue;

            // 7. Try to make tight (all moves consumed)
            var solutionGridSet = BuildSolutionGridSet(solutionPath, offset, level);
            bool tight = TryMakeTight(level, solutionGridSet, rng);

            // 8. Score; reject low-quality candidates
            int score = ScoreLevel(level, solutionPath, sequence, tight);
            if (score < 25) continue;

            return new GenerationResult
            {
                Level         = level,
                IsSolvable    = true,
                IsTight       = tight,
                SolutionSteps = sol.Count,
                FormsUsed     = sequence.Select(m => m.Form).Distinct().ToList(),
                Score         = score,
            };
        }

        return new GenerationResult { FailReason = $"Failed after {p.MaxAttempts} attempts" };
    }

    // =========================================================================
    // Step 1 — Build move sequence
    // =========================================================================

    private static List<SolMove> BuildSequence(GenerationParams p, System.Random rng)
    {
        var enabled = p.Forms
            .Where(f => f.Enabled && f.State != Player.StateType.Default)
            .ToList();
        if (enabled.Count == 0) return null;

        // Roll move counts
        var pool = new List<Player.StateType>();
        foreach (var f in enabled)
        {
            int count = rng.Next(f.MovesMin, f.MovesMax + 1);
            for (int i = 0; i < count; i++) pool.Add(f.State);
        }
        if (pool.Count == 0) return null;

        pool = Arrange(pool, p.Pattern, rng);

        // Assign direction + mode-specific parameters
        var sequence = new List<SolMove>();
        var lastDir  = CardinalDir(rng);
        foreach (var form in pool)
        {
            var mode = StateModelInfo.StateModels[form].MoveMode;
            var dir  = ChooseDir(mode, lastDir, p.Pattern, rng);
            if (!IsDiagonal(dir)) lastDir = dir;

            var move = new SolMove { Form = form, Direction = dir };
            if (mode == MoveMode.PlaneSlide) move.SlideLen = rng.Next(1, 4);
            if (mode == MoveMode.BoatSlide)  move.WaterLen = rng.Next(1, 4);
            sequence.Add(move);
        }

        return sequence;
    }

    private static List<Player.StateType> Arrange(
        List<Player.StateType> pool, PathPattern pattern, System.Random rng)
    {
        switch (pattern)
        {
            case PathPattern.Grouped:
                return pool.GroupBy(f => f)
                           .OrderBy(_ => rng.Next())
                           .SelectMany(g => g.OrderBy(_ => rng.Next()))
                           .ToList();

            case PathPattern.Zigzag:
            {
                var result = new List<Player.StateType>();
                var queues = pool.GroupBy(f => f)
                                 .ToDictionary(g => g.Key, g => new Queue<Player.StateType>(g));
                var keys = queues.Keys.ToList();
                int ki = 0;
                while (result.Count < pool.Count)
                {
                    bool added = false;
                    for (int t = 0; t < keys.Count; t++)
                    {
                        var key = keys[(ki + t) % keys.Count];
                        if (queues[key].Count > 0)
                        {
                            result.Add(queues[key].Dequeue());
                            ki++;
                            added = true;
                            break;
                        }
                    }
                    if (!added) break;
                }
                return result;
            }

            default: // Linear, Mixed
                return pool.OrderBy(_ => rng.Next()).ToList();
        }
    }

    private static Vector2Int ChooseDir(
        MoveMode mode, Vector2Int last, PathPattern pattern, System.Random rng)
    {
        if (mode == MoveMode.PlaneSlide) return DiagonalDir(rng);

        var all = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        switch (pattern)
        {
            case PathPattern.Linear:
                return rng.NextDouble() < 0.55 && last != Vector2Int.zero
                    ? last
                    : Perp(last, rng);

            case PathPattern.Zigzag:
                return Perp(last, rng);

            default:
                var cands = all.Where(d => d != -last).ToArray();
                return cands[rng.Next(cands.Length)];
        }
    }

    private static Vector2Int CardinalDir(System.Random rng)
    {
        var d = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        return d[rng.Next(4)];
    }

    private static Vector2Int DiagonalDir(System.Random rng)
    {
        var d = new[]
        {
            new Vector2Int( 1, 1), new Vector2Int( 1,-1),
            new Vector2Int(-1, 1), new Vector2Int(-1,-1)
        };
        return d[rng.Next(4)];
    }

    private static Vector2Int Perp(Vector2Int dir, System.Random rng)
    {
        if (dir.x != 0) return rng.Next(2) == 0 ? Vector2Int.up    : Vector2Int.down;
        if (dir.y != 0) return rng.Next(2) == 0 ? Vector2Int.left   : Vector2Int.right;
        return CardinalDir(rng);
    }

    private static bool IsDiagonal(Vector2Int d) => d.x != 0 && d.y != 0;

    // =========================================================================
    // Step 2 — Trace solution path on virtual grid
    // =========================================================================

    private static bool TracePath(
        List<SolMove> sequence, GenerationParams p, System.Random rng,
        Dictionary<Vector2Int, VCell> cellMap, List<Vector2Int> solutionPath)
    {
        // Soft bounding box: path should stay within roughly the max grid size
        int halfX = (p.GridMaxX - 2) / 2;
        int halfY = (p.GridMaxY - 2) / 2;

        var pos = Vector2Int.zero;
        solutionPath.Add(pos);
        cellMap[pos] = new VCell { Terrain = TerrainType.Default, Role = CellRole.Path };

        foreach (var move in sequence)
        {
            var mode = StateModelInfo.StateModels[move.Form].MoveMode;
            var dirCandidates = DirectionCandidates(mode, move.Direction, rng);

            bool placed = false;
            foreach (var dir in dirCandidates)
            {
                if (TryPlace(mode, pos, dir, halfX, halfY, move, cellMap, solutionPath, p, rng, out var landing))
                {
                    pos    = landing;
                    placed = true;
                    break;
                }
            }
            if (!placed) return false;
        }

        return true;
    }

    private static Vector2Int[] DirectionCandidates(MoveMode mode, Vector2Int preferred, System.Random rng)
    {
        if (mode == MoveMode.PlaneSlide)
        {
            var diags = new[]
            {
                new Vector2Int( 1, 1), new Vector2Int( 1,-1),
                new Vector2Int(-1, 1), new Vector2Int(-1,-1)
            };
            return new[] { preferred }
                .Concat(diags.Where(d => d != preferred).OrderBy(_ => rng.Next()))
                .ToArray();
        }
        else
        {
            var cards = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            return new[] { preferred }
                .Concat(cards.Where(d => d != preferred).OrderBy(_ => rng.Next()))
                .ToArray();
        }
    }

    private static bool TryPlace(
        MoveMode mode, Vector2Int from, Vector2Int dir,
        int halfX, int halfY, SolMove move,
        Dictionary<Vector2Int, VCell> cellMap, List<Vector2Int> solutionPath,
        GenerationParams p, System.Random rng,
        out Vector2Int landing)
    {
        landing = from;
        bool InBound(Vector2Int v) => Mathf.Abs(v.x) <= halfX && Mathf.Abs(v.y) <= halfY;

        switch (mode)
        {
            case MoveMode.Normal:
            {
                var t = from + dir;
                if (!InBound(t)) return false;
                landing = t;
                SetPath(cellMap, t);
                solutionPath.Add(t);
                return true;
            }

            case MoveMode.FrogJump:
            {
                var t = from + dir * 2;
                if (!InBound(t)) return false;
                // Intermediate: place as walkable if empty (frog jumps over it)
                var mid = from + dir;
                if (!cellMap.ContainsKey(mid))
                    cellMap[mid] = new VCell { Terrain = TerrainType.Default, Role = CellRole.Decoy };
                landing = t;
                SetPath(cellMap, t);
                solutionPath.Add(t);
                return true;
            }

            case MoveMode.PlaneSlide:
            {
                int len = move.SlideLen > 0 ? move.SlideLen : rng.Next(1, 4);
                for (int s = 1; s <= len; s++)
                    if (!InBound(from + dir * s)) return false;
                // All cells along slide must be passable — mark as path
                for (int s = 1; s < len; s++)
                    SetPath(cellMap, from + dir * s);
                landing = from + dir * len;
                SetPath(cellMap, landing);
                solutionPath.Add(landing);
                return true;
            }

            case MoveMode.BoatSlide:
            {
                if (!p.UseWater) return false;
                int waterLen = move.WaterLen > 0 ? move.WaterLen : rng.Next(1, 4);
                landing = from + dir * (waterLen + 1);
                if (!InBound(landing)) return false;
                for (int w = 1; w <= waterLen; w++)
                {
                    var wPos = from + dir * w;
                    if (!InBound(wPos)) return false;
                    // Don't overwrite existing path cells with water
                    if (!cellMap.TryGetValue(wPos, out var ex) || ex.Role != CellRole.Path)
                        cellMap[wPos] = new VCell { Terrain = TerrainType.Water, Role = CellRole.Water };
                }
                SetPath(cellMap, landing);
                solutionPath.Add(landing);
                return true;
            }
        }

        return false;
    }

    // Only set a cell as Path if it's not already a Path cell
    private static void SetPath(Dictionary<Vector2Int, VCell> cellMap, Vector2Int pos)
    {
        if (!cellMap.TryGetValue(pos, out var c) || c.Role != CellRole.Path)
            cellMap[pos] = new VCell { Terrain = TerrainType.Default, Role = CellRole.Path };
    }

    // =========================================================================
    // Step 3 — Add decoy paths (misleading walkable branches)
    // =========================================================================

    private static void AddDecoys(
        Dictionary<Vector2Int, VCell> cellMap, List<Vector2Int> solutionPath,
        GenerationParams p, System.Random rng)
    {
        float branchProb = p.Decoys switch
        {
            DecoyIntensity.Low  => 0.25f,
            DecoyIntensity.High => 0.70f,
            _                   => 0.45f,
        };
        int maxLen = p.Decoys switch
        {
            DecoyIntensity.Low  => 2,
            DecoyIntensity.High => 6,
            _                   => 4,
        };

        var cards = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        bool boatInUse = p.Forms.Any(f => f.Enabled && f.State == Player.StateType.Boat);

        // Skip first and last path cells (start and end)
        foreach (var pathPos in solutionPath.Skip(1).SkipLast(1).Distinct())
        {
            if (rng.NextDouble() > branchProb) continue;

            // Pick direction not already used by solution at this cell
            var usedDirs = PathDirectionsAt(pathPos, solutionPath);
            var free     = cards.Where(d => !usedDirs.Contains(d)).ToArray();
            if (free.Length == 0) free = cards;

            var dir = free[rng.Next(free.Length)];
            int len = rng.Next(1, maxLen + 1);
            var cur = pathPos;

            for (int i = 0; i < len; i++)
            {
                cur += dir;
                if (cellMap.ContainsKey(cur)) break; // don't overwrite solution/water cells

                TerrainType t = TerrainType.Default;
                if (p.UseWater && boatInUse && rng.NextDouble() < 0.18) t = TerrainType.Water;

                var cell = new VCell { Terrain = t, Role = CellRole.Decoy };
                // Fragile more likely at branch entry (i=0) → creates one-way explorations
                float fragileProb = (i == 0) ? 0.45f : 0.15f;
                if (p.UseFragile && t == TerrainType.Default && rng.NextDouble() < fragileProb)
                    cell.IsFragile = true;

                cellMap[cur] = cell;

                // Occasionally veer mid-branch
                if (rng.NextDouble() < 0.30) dir = cards[rng.Next(4)];
            }

            // High intensity: second branch from the same junction
            if (p.Decoys == DecoyIntensity.High && rng.NextDouble() < 0.40)
            {
                var alt = free.Where(d => d != dir).ToArray();
                if (alt.Length > 0)
                {
                    var dir2 = alt[rng.Next(alt.Length)];
                    var cur2 = pathPos;
                    int len2 = rng.Next(1, 4);
                    for (int i = 0; i < len2; i++)
                    {
                        cur2 += dir2;
                        if (cellMap.ContainsKey(cur2)) break;
                        cellMap[cur2] = new VCell { Terrain = TerrainType.Default, Role = CellRole.Decoy };
                    }
                }
            }
        }

        // Scatter Stone cells at edges of decoy areas to shape the map
        // (Fire/Lava are now handled in ApplySpecialCellChallenges for smarter placement)
        if (p.UseStone)
            ScatterObstacles(cellMap, p, rng);
    }

    private static HashSet<Vector2Int> PathDirectionsAt(Vector2Int pos, List<Vector2Int> path)
    {
        var dirs = new HashSet<Vector2Int>();
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] != pos) continue;
            if (i > 0)
            {
                var d = path[i] - path[i - 1];
                dirs.Add(new Vector2Int(Math.Sign(d.x), Math.Sign(d.y)));
            }
            if (i < path.Count - 1)
            {
                var d = path[i + 1] - path[i];
                dirs.Add(new Vector2Int(Math.Sign(d.x), Math.Sign(d.y)));
            }
        }
        return dirs;
    }

    private static void ScatterObstacles(
        Dictionary<Vector2Int, VCell> cellMap, GenerationParams p, System.Random rng)
    {
        var cards  = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var decoys = cellMap.Where(kv => kv.Value.Role == CellRole.Decoy)
                            .Select(kv => kv.Key).ToList();

        foreach (var dPos in decoys)
        foreach (var d in cards)
        {
            var nb = dPos + d;
            if (cellMap.ContainsKey(nb)) continue;
            if (rng.NextDouble() > 0.12) continue;

            if (rng.NextDouble() < 0.70) // 70% of qualified neighbors get Stone
                cellMap[nb] = new VCell { Terrain = TerrainType.Stone, Role = CellRole.Obstacle };
        }
    }

    // =========================================================================
    // Step 3.5 — Apply special cell challenges
    // =========================================================================

    /// <summary>
    /// Orchestrates placement of Fire/Lava hazards, Ice terrain, and Fragile path cells
    /// after decoy branches have been built, so all three systems can see the full map.
    /// </summary>
    // Returns VolcanoConfigs in VIRTUAL coordinates — offset is applied later in BuildGrid.
    private static List<VolcanoConfig> ApplySpecialCellChallenges(
        Dictionary<Vector2Int, VCell> cellMap,
        List<Vector2Int> solutionPath,
        List<SolMove> sequence,
        GenerationParams p,
        System.Random rng)
    {
        var volcanoConfigs = new List<VolcanoConfig>();

        if (p.UseFire)
            AddFireHazards(cellMap, solutionPath, p, rng);

        if (p.UseLava)
        {
            var cfg = GenerateVolcano(cellMap, solutionPath, p, rng);
            if (cfg != null) volcanoConfigs.Add(cfg);
        }

        if (p.UseIce)
            AddIceCells(cellMap, solutionPath, sequence, p, rng);

        if (p.UseFragile)
            AddFragilePathCells(cellMap, solutionPath, rng);

        return volcanoConfigs;
    }

    // ─── Fire ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Places static Fire cells as traps and danger framing:
    ///   1. Converts decoy branch endpoints into instant-death dead-ends.
    ///   2. Flanks straight path corridors on perpendicular sides.
    ///   3. Adds sparse fire borders adjacent to path for atmosphere.
    /// Fire is NEVER placed on solution path cells.
    /// </summary>
    private static void AddFireHazards(
        Dictionary<Vector2Int, VCell> cellMap,
        List<Vector2Int> solutionPath,
        GenerationParams p,
        System.Random rng)
    {
        var cards = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // Bounding box — avoid expanding the grid excessively
        int minX = cellMap.Keys.Min(v => v.x) - 1;
        int maxX = cellMap.Keys.Max(v => v.x) + 1;
        int minY = cellMap.Keys.Min(v => v.y) - 1;
        int maxY = cellMap.Keys.Max(v => v.y) + 1;
        bool InBounds(Vector2Int v) => v.x >= minX && v.x <= maxX && v.y >= minY && v.y <= maxY;

        // --- 1. Turn decoy endpoints into fire death traps ---
        float trapProb = p.Decoys switch
        {
            DecoyIntensity.Low  => 0.35f,
            DecoyIntensity.High => 0.65f,
            _                   => 0.50f,
        };

        foreach (var kv in cellMap.Where(kv => kv.Value.Role == CellRole.Decoy).ToList())
        {
            var pos = kv.Key;
            int connections = cards.Count(d =>
                cellMap.TryGetValue(pos + d, out var nb) &&
                (nb.Role == CellRole.Path || nb.Role == CellRole.Decoy));

            if (connections <= 1 && rng.NextDouble() < trapProb)
                cellMap[pos] = new VCell { Terrain = TerrainType.Fire, Role = CellRole.Obstacle };
        }

        // --- 2. Fire corridor flanks on straight 2+ step path segments ---
        for (int i = 1; i < solutionPath.Count - 2; i++)
        {
            var a   = solutionPath[i];
            var b   = solutionPath[i + 1];
            var dir = new Vector2Int(Math.Sign(b.x - a.x), Math.Sign(b.y - a.y));
            if (dir.x != 0 && dir.y != 0) continue; // skip Plane diagonals

            var prev    = solutionPath[i - 1];
            var prevDir = new Vector2Int(Math.Sign(a.x - prev.x), Math.Sign(a.y - prev.y));
            if (prevDir != dir) continue; // must be same direction for ≥2 steps

            if (rng.NextDouble() > 0.40) continue;

            var perps = new[] { new Vector2Int(dir.y, -dir.x), new Vector2Int(-dir.y, dir.x) };
            foreach (var perp in perps)
            {
                if (rng.NextDouble() > 0.60) continue;
                var fp = a + perp;
                if (!InBounds(fp) || cellMap.ContainsKey(fp)) continue;
                cellMap[fp] = new VCell { Terrain = TerrainType.Fire, Role = CellRole.Obstacle };
            }
        }

        // --- 3. Sparse fire adjacent to path cells for atmosphere ---
        int maxBorders = p.Decoys == DecoyIntensity.High ? 4 : 2;
        int borderCount = 0;
        foreach (var pathPos in solutionPath.Skip(1).SkipLast(2).OrderBy(_ => rng.Next()))
        {
            if (borderCount >= maxBorders) break;
            foreach (var dir in cards.OrderBy(_ => rng.Next()))
            {
                var nb = pathPos + dir;
                if (!InBounds(nb) || cellMap.ContainsKey(nb)) continue;
                if (rng.NextDouble() > 0.18) continue;
                cellMap[nb] = new VCell { Terrain = TerrainType.Fire, Role = CellRole.Obstacle };
                borderCount++;
                break;
            }
        }
    }

    // ─── Volcano / Lava ──────────────────────────────────────────────────────

    /// <summary>
    /// Places one Volcano cell and builds a VolcanoConfig whose LavaSequence
    /// covers decoy cells in BFS order (spreading outward from the volcano).
    /// Solution path cells are NEVER in the sequence, so the level stays solvable.
    /// The volcano creates time pressure: exploring wrong branches fills with lava.
    /// Returns null if no suitable decoy cell exists.
    /// Positions are in VIRTUAL grid coordinates; BuildGrid applies the offset.
    /// </summary>
    private static VolcanoConfig GenerateVolcano(
        Dictionary<Vector2Int, VCell> cellMap,
        List<Vector2Int> solutionPath,
        GenerationParams p,
        System.Random rng)
    {
        var pathSet = new HashSet<Vector2Int>(solutionPath);
        var cards   = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // --- Pick a volcano position: a decoy endpoint far from the path ---
        var endpointDecoys = cellMap
            .Where(kv => kv.Value.Role == CellRole.Decoy)
            .Where(kv =>
            {
                int decoyNeighbors = cards.Count(d =>
                    cellMap.TryGetValue(kv.Key + d, out var n) && n.Role == CellRole.Decoy);
                return decoyNeighbors <= 1; // edge of the decoy area
            })
            .Select(kv => kv.Key)
            .OrderByDescending(pos =>   // prefer positions far from the path centroid
            {
                var cx = solutionPath.Average(v => (double)v.x);
                var cy = solutionPath.Average(v => (double)v.y);
                return Math.Abs(pos.x - cx) + Math.Abs(pos.y - cy);
            })
            .ToList();

        if (endpointDecoys.Count == 0) return null;

        // Pick from the top-3 farthest candidates randomly for variety
        var volcanoPos = endpointDecoys[rng.Next(Math.Min(3, endpointDecoys.Count))];

        // Convert that decoy cell to Volcano terrain
        cellMap[volcanoPos] = new VCell { Terrain = TerrainType.Volcano, Role = CellRole.Obstacle };

        // --- Build LavaSequence by BFS through adjacent decoy cells ---
        // Lava spreads outward from the volcano, covering decoys (never path cells).
        var lavaSeq  = new List<Vector2Int>();
        var visited  = new HashSet<Vector2Int> { volcanoPos };
        var queue    = new Queue<Vector2Int>();
        queue.Enqueue(volcanoPos);

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            // Shuffle directions for visual variety in spread order
            foreach (var dir in cards.OrderBy(_ => rng.Next()))
            {
                var next = curr + dir;
                if (visited.Contains(next)) continue;
                visited.Add(next);

                if (!cellMap.TryGetValue(next, out var cell)) continue;
                if (cell.Role != CellRole.Decoy) continue;     // skip path / obstacle cells
                if (pathSet.Contains(next)) continue;           // safety: never lava on path

                lavaSeq.Add(next);
                queue.Enqueue(next);
            }
        }

        if (lavaSeq.Count == 0) return null;

        // Period: fast (2) for short paths, slower (3) for longer ones
        int totalMoves = solutionPath.Count - 1;
        int period = totalMoves <= 4 ? 3 : 2;

        return new VolcanoConfig
        {
            Position     = volcanoPos,
            Period       = period,
            LavaSequence = lavaSeq,
        };
    }

    // ─── Ice ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces Default terrain with Ice on eligible cells.
    ///   • Path landing cells reached by non-Boat forms → Ice forces form awareness.
    ///   • Decoy areas → Ice sections inaccessible to Boat, rewarding the right form.
    /// Ice is never placed on Boat landing cells (Boat.MoveTerrain excludes Ice).
    /// </summary>
    private static void AddIceCells(
        Dictionary<Vector2Int, VCell> cellMap,
        List<Vector2Int> solutionPath,
        List<SolMove> sequence,
        GenerationParams p,
        System.Random rng)
    {
        bool boatEnabled = p.Forms.Any(f => f.Enabled && f.State == Player.StateType.Boat);
        // Higher probability when Boat is enabled so Ice creates meaningful routing constraints
        float pathIceProb  = boatEnabled ? 0.35f : 0.20f;
        float decoyIceProb = boatEnabled ? 0.30f : 0.15f;

        // --- Solution path landing cells (solutionPath[i+1] corresponds to sequence[i]) ---
        for (int i = 0; i < sequence.Count && i + 1 < solutionPath.Count - 1; i++)
        {
            if (sequence[i].Form == Player.StateType.Boat) continue; // Boat can't land on Ice

            var pos = solutionPath[i + 1];
            if (cellMap.TryGetValue(pos, out var cell)
                && cell.Terrain == TerrainType.Default
                && rng.NextDouble() < pathIceProb)
            {
                cell.Terrain = TerrainType.Ice;
            }
        }

        // --- Intermediate Plane cells (Path role but not solutionPath landing cells) ---
        var solutionSet = new HashSet<Vector2Int>(solutionPath);
        foreach (var kv in cellMap
            .Where(kv => kv.Value.Role == CellRole.Path
                      && kv.Value.Terrain == TerrainType.Default
                      && !solutionSet.Contains(kv.Key))
            .ToList())
        {
            if (rng.NextDouble() < pathIceProb * 0.5f)
                kv.Value.Terrain = TerrainType.Ice;
        }

        // --- Decoy cells ---
        foreach (var kv in cellMap
            .Where(kv => kv.Value.Role == CellRole.Decoy
                      && kv.Value.Terrain == TerrainType.Default)
            .ToList())
        {
            if (rng.NextDouble() < decoyIceProb)
                kv.Value.Terrain = TerrainType.Ice;
        }
    }

    // ─── Fragile path cells ───────────────────────────────────────────────────

    /// <summary>
    /// Marks 1–2 inner solution path cells as Fragile at roughly 1/3 and 2/3 of the path.
    /// Fragile path cells collapse after being stepped on, creating one-way commitments
    /// and tension when placed near junctions.
    /// </summary>
    private static void AddFragilePathCells(
        Dictionary<Vector2Int, VCell> cellMap,
        List<Vector2Int> solutionPath,
        System.Random rng)
    {
        // Skip start (index 0) and end (last) cells
        var inner = solutionPath.Skip(1).SkipLast(1).ToList();
        if (inner.Count < 3) return;

        int n = inner.Count;
        foreach (int idx in new[] { n / 3, 2 * n / 3 })
        {
            if (rng.NextDouble() > 0.65) continue; // 65% chance each
            var pos = inner[idx];
            if (!cellMap.TryGetValue(pos, out var cell)) continue;
            if (cell.Terrain == TerrainType.Water || cell.Terrain == TerrainType.Empty) continue;
            cell.IsFragile = true;
        }
    }

    // =========================================================================
    // Step 4 — Place stars / mark start and end
    // =========================================================================

    private static bool PlaceSpecialCells(
        Dictionary<Vector2Int, VCell> cellMap, List<Vector2Int> solutionPath, System.Random rng)
    {
        // Unique inner path cells (skip start and end)
        var inner = solutionPath.Skip(1).SkipLast(1).Distinct().ToList();
        if (inner.Count < 3) return false;

        // Spread three stars at ~25%, ~50%, ~75% of inner path
        var pickedPositions = new HashSet<Vector2Int>();
        int n = inner.Count;

        foreach (int frac in new[] { 1, 2, 3 })
        {
            int target = n * frac / 4;
            for (int delta = 0; delta < n; delta++)
            {
                // Alternately try +delta and -delta from target
                int idx = (target + (delta % 2 == 0 ? delta / 2 : -(delta / 2 + 1)) + n) % n;
                var candidate = inner[idx];
                if (!pickedPositions.Contains(candidate))
                {
                    pickedPositions.Add(candidate);
                    break;
                }
            }
        }

        if (pickedPositions.Count < 3) return false;

        foreach (var pos in pickedPositions)
            cellMap[pos].Item = CellItem.Star;

        return true;
    }

    // =========================================================================
    // Step 5 — Build LevelData from virtual cell map
    // =========================================================================

    private static Vector2Int ComputeOffset(Dictionary<Vector2Int, VCell> cellMap)
    {
        int minX = cellMap.Keys.Min(v => v.x);
        int minY = cellMap.Keys.Min(v => v.y);
        return new Vector2Int(-minX + 1, -minY + 1); // 1-cell empty border
    }

    private static LevelData BuildGrid(
        Dictionary<Vector2Int, VCell> cellMap,
        List<Vector2Int> solutionPath,
        List<SolMove> sequence,
        GenerationParams p,
        Vector2Int offset,
        List<VolcanoConfig> volcanoConfigs)
    {
        // Grid dimensions: content + 1-cell padding on all sides
        int contentW = cellMap.Keys.Max(v => v.x) - cellMap.Keys.Min(v => v.x) + 1;
        int contentH = cellMap.Keys.Max(v => v.y) - cellMap.Keys.Min(v => v.y) + 1;
        int w = Mathf.Max(contentW + 2, p.GridMinX);
        int h = Mathf.Max(contentH + 2, p.GridMinY);

        if (w > p.GridMaxX || h > p.GridMaxY) return null;

        var level = ScriptableObject.CreateInstance<LevelData>();
        level.MapSize = new Vector2Int(w, h);
        level.Map = Enumerable.Repeat(0, w * h)
                              .Select(_ => new CellData(TerrainType.Empty, CellItem.None))
                              .ToArray();

        // Fill from virtual map
        foreach (var kv in cellMap)
        {
            var gp = kv.Key + offset;
            if (!InGrid(gp, w, h)) continue;
            level.Map[gp.y * w + gp.x] = new CellData(kv.Value.Terrain, kv.Value.Item, kv.Value.IsFragile);
        }

        // Overwrite start and end (these override any existing terrain/item)
        var startGP = solutionPath[0]  + offset;
        var endGP   = solutionPath[^1] + offset;
        if (!InGrid(startGP, w, h) || !InGrid(endGP, w, h)) return null;
        if (startGP == endGP) return null;

        level.Map[startGP.y * w + startGP.x] = new CellData(TerrainType.Start, CellItem.None);
        level.Map[endGP.y   * w + endGP.x]   = new CellData(TerrainType.End,   CellItem.None);

        // Move counts: count actual moves per form in the sequence
        var counts = new Dictionary<Player.StateType, int>();
        foreach (var m in sequence)
        {
            counts.TryGetValue(m.Form, out int c);
            counts[m.Form] = c + 1;
        }

        level.StartMovesPerForm = new List<MovePerFormEntry>
            { new() { State = Player.StateType.Default, Moves = 0 } };
        foreach (Player.StateType st in Enum.GetValues(typeof(Player.StateType)))
        {
            if (st == Player.StateType.Default) continue;
            counts.TryGetValue(st, out int cnt);
            level.StartMovesPerForm.Add(new MovePerFormEntry { State = st, Moves = cnt });
        }

        level.Biome            = p.Biome;
        level.IceSourceConfigs = new List<IceSourceConfig>();

        // Convert volcano configs from virtual to grid coordinates
        level.VolcanoConfigs = (volcanoConfigs ?? new List<VolcanoConfig>())
            .Select(cfg => new VolcanoConfig
            {
                Position     = cfg.Position + offset,
                Period       = cfg.Period,
                LavaSequence = cfg.LavaSequence
                    .Select(vp => vp + offset)
                    .Where(gp => InGrid(gp, w, h))
                    .ToList(),
            })
            .Where(cfg => InGrid(cfg.Position, w, h))
            .ToList();

        // Final sanity: exactly 3 stars required
        if (level.Map.Count(c => c.Item == CellItem.Star) != 3) return null;

        return level;
    }

    private static bool InGrid(Vector2Int p, int w, int h) =>
        p.x >= 0 && p.x < w && p.y >= 0 && p.y < h;

    private static HashSet<Vector2Int> BuildSolutionGridSet(
        List<Vector2Int> solutionPath, Vector2Int offset, LevelData level)
    {
        var set = new HashSet<Vector2Int>();
        foreach (var p in solutionPath)
        {
            var gp = p + offset;
            if (InGrid(gp, level.MapSize.x, level.MapSize.y)) set.Add(gp);
        }
        return set;
    }

    // =========================================================================
    // Step 6 — Try to make tight (greedy obstacle addition)
    // =========================================================================

    private static bool TryMakeTight(
        LevelData level, HashSet<Vector2Int> solutionGridSet, System.Random rng)
    {
        int w = level.MapSize.x, h = level.MapSize.y;

        for (int iteration = 0; iteration < 8; iteration++)
        {
            // Already tight?
            if (LevelEditorWindow.SolveLevel(level, requireLooseSolution: true) == null)
                return true;

            // Find non-solution walkable cells as blocking candidates
            var candidates = new List<Vector2Int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var gp = new Vector2Int(x, y);
                if (solutionGridSet.Contains(gp)) continue;
                var t = level.Map[y * w + x].Terrain;
                if (t == TerrainType.Default || t == TerrainType.Water)
                    candidates.Add(gp);
            }

            if (candidates.Count == 0) break;

            // Shuffle, try blocking a few cells each iteration
            candidates = candidates.OrderBy(_ => rng.Next()).ToList();
            bool progress = false;

            foreach (var gp in candidates.Take(4))
            {
                int idx  = gp.y * w + gp.x;
                var orig = level.Map[idx];
                level.Map[idx] = new CellData(TerrainType.Stone, CellItem.None);

                // Keep the block only if the level is still solvable
                if (LevelEditorWindow.SolveLevel(level) != null)
                    progress = true;
                else
                    level.Map[idx] = orig;
            }

            if (!progress) break;
        }

        return LevelEditorWindow.SolveLevel(level, requireLooseSolution: true) == null;
    }

    // =========================================================================
    // Step 7 — Score
    // =========================================================================

    private static int ScoreLevel(
        LevelData level, List<Vector2Int> path, List<SolMove> sequence, bool tight)
    {
        int score = 0;

        // Form variety (0–60)
        score += sequence.Select(m => m.Form).Distinct().Count() * 15;

        // Tight bonus (25)
        if (tight) score += 25;

        // Path non-linearity: direction changes (0–20)
        int turns = 0;
        for (int i = 2; i < path.Count; i++)
        {
            var d1 = Norm(path[i - 1] - path[i - 2]);
            var d2 = Norm(path[i]     - path[i - 1]);
            if (d1 != d2) turns++;
        }
        score += Mathf.Min(turns * 4, 20);

        // Map fill ratio: decoys make the level look fuller (0–15)
        int filled = level.Map.Count(c => c.Terrain != TerrainType.Empty);
        score += (int)(Mathf.Clamp01((float)filled / (level.MapSize.x * level.MapSize.y)) * 15);

        // Path length (0–15)
        score += Mathf.Min(path.Count * 2, 15);

        // Special cell bonuses — reward levels that successfully use advanced mechanics
        if (level.Map.Any(c => c.Terrain == TerrainType.Fire))
            score += 8;
        if (level.VolcanoConfigs?.Count > 0)
            score += 12; // volcano is complex and validated; strong reward
        if (level.Map.Any(c => c.Terrain == TerrainType.Ice))
            score += 8;
        if (level.Map.Any(c => c.IsFragile))
            score += 10;

        return score;
    }

    private static Vector2Int Norm(Vector2Int v) =>
        new(Math.Sign(v.x), Math.Sign(v.y));
}
