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

            // Check if level is valid before attempting to solve
            if (!level.IsValid())
            {
                Debug.LogError($"Level {levelName} is invalid and cannot be solved. Skipping.");
                failCount++;
                continue;
            }

            // Compute solution
            var solution = LevelEditorWindow.SolveLevel(level);
            
            if (solution != null)
            {
                // Convert solution to SolutionStep format and cache it
                level.CachedSolution = ConvertToSolutionSteps(solution);
                
                // Mark asset as dirty and save
                EditorUtility.SetDirty(level);
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

    private static List<SolutionStep> ConvertToSolutionSteps(List<LevelEditorWindow.TurnInfo> solution)
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