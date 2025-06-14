using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public class LevelEditorWindow : EditorWindow
{
    private LevelData currentLevel;
    private Vector2 scrollPosition;
    private new bool hasUnsavedChanges;
    private float tileSize = 64f; // Size of each tile in pixels
    private float tilePadding = 4f; // Padding between tiles
    private Dictionary<Cell.TerrainType, Texture2D> cellTextures;
    private Dictionary<Cell.CellItem, Texture2D> itemTextures;

    private enum EditorMode
    {
        Tiles,
        Moves,
        Analysis
    }

    private EditorMode currentMode = EditorMode.Tiles;

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

    private List<Cell.TerrainType> toolTerrainTypes = new() {
        Cell.TerrainType.None,
        Cell.TerrainType.Default,
        Cell.TerrainType.Start,
        Cell.TerrainType.End,
        Cell.TerrainType.Water,
    };

    private List<Cell.CellItem> toolItemTypes = new() {
        Cell.CellItem.Star,
    };

    private Dictionary<char, Cell.CellItem> itemTypes = new() {
        { 'G', Cell.CellItem.Star },
        { '1', Cell.CellItem.Star },
        { '2', Cell.CellItem.Star },
        { '3', Cell.CellItem.Star }
    };

    private Cell.TerrainType selectedTerrainType = Cell.TerrainType.Default;
    private Cell.CellItem? selectedItemType = null;

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

    private void UpdateWindowTitle()
    {
        string title = "Level Editor";
        if (currentLevel != null)
        {
            title += $" - {currentLevel.name}";
            if (hasUnsavedChanges)
            {
                title += " *";
            }
        }
        this.titleContent = new GUIContent(title);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        
        // Left Panel
        DrawLeftPanel();
        
        // Right Panel (Preview)
        DrawPreviewPanel();
        
        EditorGUILayout.EndHorizontal();

        // Update window title to reflect current state
        UpdateWindowTitle();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        
        EditorGUILayout.LabelField("Level Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Level Selection
        EditorGUILayout.LabelField("Current Level", EditorStyles.boldLabel);
        var newLevel = (LevelData)EditorGUILayout.ObjectField(currentLevel, typeof(LevelData), false);
        if (newLevel != currentLevel)
        {
            currentLevel = newLevel;
            hasUnsavedChanges = false;
            UpdateWindowTitle();
        }
        
        EditorGUILayout.Space();

        // Tool Selection
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        
        // Create a button group for the tools
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        currentMode = (EditorMode)GUILayout.SelectionGrid((int)currentMode, 
            new[] { "Tiles", "Moves", "Analysis" }, 
            1, 
            EditorStyles.miniButton);
        EditorGUILayout.EndVertical();

        if (EditorGUI.EndChangeCheck())
        {
            // Reset any tool-specific state when switching modes
            hasUnsavedChanges = false;
        }

        EditorGUILayout.Space();

        // Mode-specific content
        switch (currentMode)
        {
            case EditorMode.Tiles:
                DrawTilesTool();
                break;
            case EditorMode.Moves:
                DrawMovesTool();
                break;
            case EditorMode.Analysis:
                DrawAnalysisTool();
                break;
        }

        EditorGUILayout.Space();

        // Common buttons
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

    private void DrawTilesTool()
    {
        EditorGUILayout.LabelField("Tile Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Terrain Type Selection
        EditorGUILayout.LabelField("Terrain Type", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        foreach (var terrainType in toolTerrainTypes)
        {
            bool isSelected = selectedTerrainType == terrainType;
            Rect rowRect = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true), GUILayout.Height(48));

            // Detect hover
            bool isHover = rowRect.Contains(Event.current.mousePosition);

            // Draw background highlight
            if (Event.current.type == EventType.Repaint)
            {
                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.25f)); // Subtle blue
                else if (isHover)
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.08f)); // Subtle white
            }

            EditorGUI.BeginChangeCheck();
            GUI.BeginGroup(rowRect);
            EditorGUILayout.BeginHorizontal();

            // Make the entire row clickable
            if (GUI.Button(new Rect(0, 0, rowRect.width, rowRect.height), GUIContent.none, GUIStyle.none))
            {
                selectedTerrainType = terrainType;
                GUI.FocusControl(null);
            }

            // Show the texture
            if (cellTextures.TryGetValue(terrainType, out Texture2D texture))
            {
                GUI.Label(new Rect(8, 8, 48, 48), texture);
            }

            // Find the character that represents this terrain type
            char? terrainChar = cellTypes.FirstOrDefault(x => x.Value == terrainType).Key;
            GUI.Label(new Rect(64, 16, rowRect.width - 64, 24), $"{terrainChar} - {terrainType}");

            EditorGUILayout.EndHorizontal();
            GUI.EndGroup();
            GUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Item Type Selection
        EditorGUILayout.LabelField("Item Type", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // None option
        bool isNoneSelected = selectedItemType == null;
        Rect noneRowRect = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true), GUILayout.Height(48));
        bool isNoneHover = noneRowRect.Contains(Event.current.mousePosition);
        if (Event.current.type == EventType.Repaint)
        {
            if (isNoneSelected)
                EditorGUI.DrawRect(noneRowRect, new Color(0.24f, 0.48f, 0.90f, 0.25f));
            else if (isNoneHover)
                EditorGUI.DrawRect(noneRowRect, new Color(1f, 1f, 1f, 0.08f));
        }
        GUI.BeginGroup(noneRowRect);
        if (GUI.Button(new Rect(0, 0, noneRowRect.width, noneRowRect.height), GUIContent.none, GUIStyle.none))
        {
            selectedItemType = null;
            GUI.FocusControl(null);
        }
        GUI.Label(new Rect(8, 16, noneRowRect.width - 8, 24), "None");
        GUI.EndGroup();
        GUILayout.Space(2);

        // Item options
        foreach (var itemType in toolItemTypes)
        {
            bool isSelected = selectedItemType == itemType;
            Rect itemRowRect = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true), GUILayout.Height(48));
            bool isItemHover = itemRowRect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (isSelected)
                    EditorGUI.DrawRect(itemRowRect, new Color(0.24f, 0.48f, 0.90f, 0.25f));
                else if (isItemHover)
                    EditorGUI.DrawRect(itemRowRect, new Color(1f, 1f, 1f, 0.08f));
            }
            GUI.BeginGroup(itemRowRect);
            if (GUI.Button(new Rect(0, 0, itemRowRect.width, itemRowRect.height), GUIContent.none, GUIStyle.none))
            {
                selectedItemType = itemType;
                GUI.FocusControl(null);
            }
            if (itemTextures.TryGetValue(itemType, out Texture2D texture))
            {
                GUI.Label(new Rect(8, 8, 48, 48), texture);
            }
            char? itemChar = itemTypes.FirstOrDefault(x => x.Value == itemType).Key;
            GUI.Label(new Rect(64, 16, itemRowRect.width - 64, 24), $"{itemChar} - {itemType}");
            GUI.EndGroup();
            GUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Click on tiles in the preview to edit them.", MessageType.Info);
    }

    private void DrawMovesTool()
    {
        EditorGUILayout.LabelField("Moves Editor", EditorStyles.boldLabel);
        if (currentLevel != null)
        {
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < currentLevel.StartMovesPerForm.Count; i++)
            {
                var moveEntry = currentLevel.StartMovesPerForm[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(moveEntry.State.ToString(), GUILayout.Width(80));
                moveEntry.Moves = EditorGUILayout.IntField(moveEntry.Moves);
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                hasUnsavedChanges = true;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
        }
    }

    private void DrawAnalysisTool()
    {
        EditorGUILayout.LabelField("Level Analysis", EditorStyles.boldLabel);
        if (currentLevel != null)
        {
            // TODO: Add analysis tools
            EditorGUILayout.HelpBox("Analysis tools coming soon.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
        }
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
        UpdateWindowTitle();
    }

    public override void SaveChanges()
    {
        if (currentLevel == null)
            return;

        EditorUtility.SetDirty(currentLevel);
        AssetDatabase.SaveAssets();
        hasUnsavedChanges = false;
        UpdateWindowTitle();
    }

    public override void DiscardChanges()
    {
        if (currentLevel == null)
            return;

        // Reload the asset from disk
        string path = AssetDatabase.GetAssetPath(currentLevel);
        currentLevel = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        hasUnsavedChanges = false;
        UpdateWindowTitle();
    }
} 