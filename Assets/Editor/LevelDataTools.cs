using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class LevelDataTools
{
    [MenuItem("Tools/Save Level Maps to Json")]
    public static void SaveLevelMapsToJson()
    {
        Debug.Log("Saving level maps to Json");

        string[] guids = AssetDatabase.FindAssets("t:LevelData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string jsonDir = Path.Combine(Path.GetDirectoryName(path), "Json");
            string jsonPath = Path.Combine(jsonDir, Path.GetFileNameWithoutExtension(path) + ".json");
            if (!Directory.Exists(jsonDir))
            {
                Directory.CreateDirectory(jsonDir);
            }

            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            string json = JsonUtility.ToJson(level, true);
            File.WriteAllText(jsonPath, json);
        }

        Debug.Log("Level maps saved to Json");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Compute and Cache Solutions for Selected Levels")]
    public static void ComputeAndCacheSolutionsForSelectedLevels()
    {
        // Get selected objects in project window
        Object[] selectedObjects = Selection.objects;
        List<LevelData> selectedLevels = new List<LevelData>();

        // Filter selected objects to only include LevelData assets
        foreach (Object obj in selectedObjects)
        {
            if (obj is LevelData levelData)
            {
                selectedLevels.Add(levelData);
            }
        }

        if (selectedLevels.Count == 0)
        {
            EditorUtility.DisplayDialog("No Levels Selected", 
                "Please select one or more LevelData assets in the Project window.", "OK");
            return;
        }

        Debug.Log($"Computing solutions for {selectedLevels.Count} selected level(s)...");

        int successCount = 0;
        int failCount = 0;

        foreach (LevelData level in selectedLevels)
        {
            string levelName = level.name;
            Debug.Log($"Processing level: {levelName}");

            // Check if level is valid before attempting to solve
            if (!level.IsValid())
            {
                Debug.LogError($"Level {levelName} is invalid and cannot be solved. Skipping.");
                failCount++;
                continue;
            }

            // Compute solution
            var solution = SolveLevel(level);
            
            if (solution != null)
            {
                // Convert solution to SolutionStep format and cache it
                level.CachedSolution = ConvertToSolutionSteps(solution);
                
                // Mark asset as dirty and save
                EditorUtility.SetDirty(level);
                
                Debug.Log($"✓ Solution found and cached for level {levelName} ({solution.Count} steps)");
                successCount++;
            }
            else
            {
                // Clear any existing cached solution
                level.CachedSolution = new List<SolutionStep>();
                EditorUtility.SetDirty(level);
                
                Debug.LogWarning($"✗ No solution found for level {levelName}");
                failCount++;
            }
        }

        // Save all changes
        AssetDatabase.SaveAssets();

        // Show completion dialog
        string message = $"Solution computation complete!\n\n" +
                        $"Successfully solved: {successCount} level(s)\n" +
                        $"Failed to solve: {failCount} level(s)";
        
        EditorUtility.DisplayDialog("Solution Computation Complete", message, "OK");
        Debug.Log($"Solution computation finished. Success: {successCount}, Failed: {failCount}");
    }

    private static List<TurnInfo> SolveLevel(LevelData level)
    {
        if (level == null || level.Map == null || level.Map.Length == 0)
        {
            Debug.LogError("Invalid level data or its components are NULL or empty.");
            return null;
        }

        // Check if level is valid
        if (!level.IsValid())
        {
            Debug.LogError("LevelData is not valid.");
            return null;
        }

        // Find the start and end positions
        Vector2Int startPos = Vector2Int.zero;
        Vector2Int endPos = Vector2Int.zero;
        int totalStars = 0;

        for (int y = 0; y < level.MapSize.y; y++)
        {
            for (int x = 0; x < level.MapSize.x; x++)
            {
                int index = y * level.MapSize.x + x;
                CellData cell = level.Map[index];
                
                if (cell.Terrain == TerrainType.Start)
                    startPos = new Vector2Int(x, y);
                if (cell.Terrain == TerrainType.End)
                    endPos = new Vector2Int(x, y);
                if (cell.Item == CellItem.Star)
                    totalStars++;
            }
        }

        // BFS to find solution
        Queue<(TurnInfo, int)> queue = new Queue<(TurnInfo, int)>(); // Include depth to prevent infinite search
        HashSet<string> visited = new HashSet<string>();
        Dictionary<string, TurnInfo> parent = new Dictionary<string, TurnInfo>();
        const int maxDepth = 100; // Prevent infinite search

        // Initial state
        TurnInfo initialTurn = new TurnInfo
        {
            Position = startPos,
            Stars = 0,
            MovesPerForm = level.StartMovesPerForm.ToDictionary(m => m.State, m => m.Moves),
            State = Player.StateType.Default,
            CollectedStarPositions = new HashSet<Vector2Int>()
        };

        queue.Enqueue((initialTurn, 0));
        string initialKey = GetStateKey(initialTurn);
        visited.Add(initialKey);

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            string currentKey = GetStateKey(current);

            // Prevent too deep search
            if (depth > maxDepth) continue;

            // Check if we reached the goal (end position with exactly 3 stars)
            if (current.Position == endPos && current.Stars == 3)
            {
                // Reconstruct path
                List<TurnInfo> solution = new List<TurnInfo>();
                string key = currentKey;
                while (parent.ContainsKey(key))
                {
                    solution.Insert(0, parent[key]);
                    key = GetStateKey(parent[key]);
                }
                solution.Add(current);
                
                return solution;
            }

            // Try all possible states from current position
            foreach (Player.StateType stateType in System.Enum.GetValues(typeof(Player.StateType)))
            {
                // Default state can only be used on the initial turn (when depth == 0)
                if (stateType == Player.StateType.Default && depth > 0) continue;
                
                // For non-default states, check if we have moves left
                if (stateType != Player.StateType.Default && current.MovesPerForm[stateType] <= 0) continue;

                // Get the state model for movement options
                if (!StateModelInfo.StateModels.TryGetValue(stateType, out StateModel stateModel)) continue;

                // Skip default state if it has no movement options
                if (stateType == Player.StateType.Default && stateModel.MoveOptions.Count == 0) continue;

                foreach (Vector2Int moveOption in stateModel.MoveOptions)
                {
                    Vector2Int newPos = current.Position + moveOption;
                    
                    // Check bounds
                    if (newPos.x < 0 || newPos.x >= level.MapSize.x || newPos.y < 0 || newPos.y >= level.MapSize.y)
                        continue;
                        
                    int newIndex = newPos.y * level.MapSize.x + newPos.x;
                    CellData targetCell = level.Map[newIndex];
                    
                    // Check if this state can move to this terrain
                    if (!stateModel.MoveTerrain.Contains(targetCell.Terrain))
                        continue;

                    int newStars = current.Stars;
                    HashSet<Vector2Int> newCollectedStars = new HashSet<Vector2Int>(current.CollectedStarPositions);
                    
                    // Check if there's a star here and we haven't collected it yet
                    if (targetCell.Item == CellItem.Star && !current.CollectedStarPositions.Contains(newPos))
                    {
                        newStars++;
                        newCollectedStars.Add(newPos);
                    }

                    // Update moves
                    Dictionary<Player.StateType, int> newMovesPerForm = new Dictionary<Player.StateType, int>(current.MovesPerForm);
                    if (stateType != Player.StateType.Default && newMovesPerForm[stateType] > 0)
                    {
                        newMovesPerForm[stateType]--;
                    }

                    TurnInfo nextTurn = new TurnInfo
                    {
                        Position = newPos,
                        Stars = newStars,
                        MovesPerForm = newMovesPerForm,
                        State = stateType,
                        CollectedStarPositions = newCollectedStars
                    };

                    string nextKey = GetStateKey(nextTurn);
                    
                    // Check if we've visited this state before
                    if (!visited.Contains(nextKey))
                    {
                        visited.Add(nextKey);
                        parent[nextKey] = current;
                        queue.Enqueue((nextTurn, depth + 1));
                    }
                }
            }
        }

        return null; // No solution found
    }

    private struct TurnInfo
    {
        public Vector2Int Position;
        public int Stars;
        public Dictionary<Player.StateType, int> MovesPerForm;
        public Player.StateType State;
        public HashSet<Vector2Int> CollectedStarPositions;
    }

    private static string GetStateKey(TurnInfo state)
    {
        // Create a comprehensive key that includes move counts and collected star positions
        string movesKey = string.Join(",", state.MovesPerForm.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        string starsKey = string.Join(";", (state.CollectedStarPositions ?? new HashSet<Vector2Int>()).OrderBy(pos => pos.x).ThenBy(pos => pos.y).Select(pos => $"{pos.x},{pos.y}"));
        return $"{state.Position.x},{state.Position.y},{state.State},{state.Stars},{movesKey},{starsKey}";
    }

    private static List<SolutionStep> ConvertToSolutionSteps(List<TurnInfo> solution)
    {
        List<SolutionStep> solutionSteps = new List<SolutionStep>();
        
        foreach (var turn in solution)
        {
            solutionSteps.Add(new SolutionStep(
                turn.Position,
                turn.State,
                turn.Stars
            ));
        }
        
        return solutionSteps;
    }
}