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
}