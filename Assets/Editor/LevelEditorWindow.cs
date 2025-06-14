using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class LevelEditorWindow : EditorWindow
{
    private LevelData currentLevel;
    private Vector2 scrollPosition;
    private bool hasUnsavedChanges;
    private float tileSize = 64f; // Size of each tile in pixels
    private float tilePadding = 4f; // Padding between tiles
    private Dictionary<Cell.TerrainType, Texture2D> cellTextures;
    private Dictionary<Cell.CellItem, Texture2D> itemTextures;

    private Dictionary<Cell.TerrainType, string> tileTexturePaths = new() {
        { Cell.TerrainType.None, "Assets/Sprites/Cell/NewCell/Border.png" },
        { Cell.TerrainType.Default, "Assets/Sprites/Cell/NewCell/Default Layer 2.png" },
        { Cell.TerrainType.Start, "Assets/Sprites/Cell/NewCell/Start.png" },
        { Cell.TerrainType.End, "Assets/Sprites/Cell/NewCell/Finish cell/StaticEnd.png" },
        { Cell.TerrainType.Water, "Assets/Obstacles/water/0.gif" },
        { Cell.TerrainType.Stone, "Assets/Sprites/Cell/NewCell/Group 869.png" },
        { Cell.TerrainType.Fire, "Assets/Obstacles/fire/1.jpeg" }
    };

    private Dictionary<Cell.CellItem, string> itemTexturePaths = new() {
        { Cell.CellItem.Star, "Assets/Sprites/Stars/Group 988.png" },
    };

    private Dictionary<char, Cell.TerrainType> cellTypes = new() {
        { '0', Cell.TerrainType.None },

        { '+', Cell.TerrainType.Default },
        { 'G', Cell.TerrainType.Default },
        { '1', Cell.TerrainType.Default },

        { 'W', Cell.TerrainType.Water },
        { '2', Cell.TerrainType.Water },

        { 'S', Cell.TerrainType.Stone },
        { '3', Cell.TerrainType.Stone },

        { 'F', Cell.TerrainType.Fire },

        { 'x', Cell.TerrainType.Start },
        { 'y', Cell.TerrainType.End }
    };

    private Dictionary<char, Cell.CellItem> itemTypes = new() {
        { 'G', Cell.CellItem.Star },
        { '1', Cell.CellItem.Star },
        { '2', Cell.CellItem.Star },
        { '3', Cell.CellItem.Star }
    };

    [MenuItem("Tools/Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditorWindow>("Level Editor");
    }

    private void OnEnable()
    {
        LoadTileTextures();
    }

    private void LoadTileTextures()
    {
        cellTextures = new();
        itemTextures = new();
        
        foreach (var tileType in tileTexturePaths)
        {
            cellTextures[tileType.Key] = AssetDatabase.LoadAssetAtPath<Texture2D>(tileType.Value);
        }

        foreach (var itemType in itemTexturePaths)
        {
            itemTextures[itemType.Key] = AssetDatabase.LoadAssetAtPath<Texture2D>(itemType.Value);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        
        // Left Panel
        DrawLeftPanel();
        
        // Right Panel (Preview)
        DrawPreviewPanel();
        
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        
        EditorGUILayout.LabelField("Level Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Level Selection
        EditorGUILayout.LabelField("Current Level", EditorStyles.boldLabel);
        currentLevel = (LevelData)EditorGUILayout.ObjectField(currentLevel, typeof(LevelData), false);
        
        EditorGUILayout.Space();

        // Buttons
        if (GUILayout.Button("New Level"))
        {
            CreateNewLevel();
        }

        if (GUILayout.Button("Save Changes") && currentLevel != null)
        {
            SaveChanges();
        }

        if (GUILayout.Button("Discard Changes") && currentLevel != null)
        {
            DiscardChanges();
        }

        EditorGUILayout.EndVertical();
    }

    private string SanitizeMapString(string map)
    {
        // Remove all newline characters and carriage returns
        return Regex.Replace(map, @"[\r\n]+", "");
    }

    private void DrawPreviewPanel()
    {
        EditorGUILayout.BeginVertical();
        
        if (currentLevel != null)
        {
            EditorGUILayout.LabelField("Level Preview", EditorStyles.boldLabel);
            
            // Sanitize the map string
            string sanitizedMap = SanitizeMapString(currentLevel.Map);
            
            // Calculate the total size of the grid with padding
            float totalWidth = currentLevel.MapSize.x * (tileSize + tilePadding);
            float totalHeight = currentLevel.MapSize.y * (tileSize + tilePadding);
            
            // Create a scroll view for the grid
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(totalWidth + 20), GUILayout.Height(totalHeight + 20));
            
            // Draw the grid
            for (int y = 0; y < currentLevel.MapSize.y; y++)
            {
                for (int x = 0; x < currentLevel.MapSize.x; x++)
                {
                    int index = y * currentLevel.MapSize.x + x;
                    if (index < sanitizedMap.Length)
                    {
                        char tileChar = sanitizedMap[index];
                        
                        // Calculate position with padding
                        float posX = x * (tileSize + tilePadding);
                        float posY = y * (tileSize + tilePadding);
                        Rect tileRect = new Rect(posX, posY, tileSize, tileSize);

                        // Draw cell texture
                        Cell.TerrainType cellType = cellTypes[tileChar];
                        Texture2D cellTexture = cellTextures[cellType];
                        GUI.DrawTexture(tileRect, cellTexture);

                        // Draw item texture if present
                        if (itemTypes.TryGetValue(tileChar, out Cell.CellItem itemType))
                        {
                            if (itemTextures.TryGetValue(itemType, out Texture2D itemTexture))
                            {
                                // Calculate star rect to be half the size and centered
                                float starSize = tileSize * 0.5f;
                                float starX = posX + (tileSize - starSize) * 0.5f;
                                float starY = posY + (tileSize - starSize) * 0.5f;
                                Rect starRect = new Rect(starX, starY, starSize, starSize);
                                GUI.DrawTexture(starRect, itemTexture);
                            }
                        }
                    }
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("No level selected. Create a new level or select an existing one.", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
    }

    private void CreateNewLevel()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create New Level",
            "NewLevel",
            "asset",
            "Please enter a name for the new level"
        );

        if (string.IsNullOrEmpty(path))
            return;

        LevelData newLevel = LevelData.DefaultLevel;
        AssetDatabase.CreateAsset(newLevel, path);
        AssetDatabase.SaveAssets();
        
        currentLevel = newLevel;
        hasUnsavedChanges = false;
    }

    private void SaveChanges()
    {
        if (currentLevel == null)
            return;

        EditorUtility.SetDirty(currentLevel);
        AssetDatabase.SaveAssets();
        hasUnsavedChanges = false;
    }

    private void DiscardChanges()
    {
        if (currentLevel == null)
            return;

        // Reload the asset from disk
        string path = AssetDatabase.GetAssetPath(currentLevel);
        currentLevel = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        hasUnsavedChanges = false;
    }
} 