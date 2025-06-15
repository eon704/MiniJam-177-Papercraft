using UnityEngine;
using UnityEditor;
using System.IO;

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

    [MenuItem("Tools/Sanitize Level Maps")]
    public static void SanitizeLevelMaps()
    {
        Debug.Log("Sanitizing level maps");

        string[] guids = AssetDatabase.FindAssets("t:LevelData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level != null)
            {
                SanitizeLevel(level);
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
            }
        }

        Debug.Log("Level maps sanitized");
    }

    private static void SanitizeLevel(LevelData level)
    {
        if (level == null && level.Map == null)
        {
            return;
        }
        
        // Remove all whitespaces from the level map
        // Normalize the \r\n to \n
        for (int i = level.Map.Length - 1; i >= 0 ; i--)
        {
            char c = level.Map[i];
            if (char.IsWhiteSpace(c) || c == '\r')
            {
                level.Map = level.Map.Remove(i, 1);
            }
        }
    }
}