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
    private Dictionary<TerrainType, Texture2D> cellTextures;
    private Dictionary<CellItem, Texture2D> itemTextures;

    private enum EditorMode
    {
        Tiles,
        Moves,
        Analysis
    }

    private EditorMode currentMode = EditorMode.Tiles;

    private Dictionary<TerrainType, string> tileTexturePaths = new() {
        { TerrainType.Empty, "Assets/Sprites/Cell/NewCell/Border.png" },
        { TerrainType.Default, "Assets/Sprites/Cell/NewCell/Default Layer 2.png" },
        { TerrainType.Start, "Assets/Sprites/Cell/NewCell/Start.png" },
        { TerrainType.End, "Assets/Sprites/Cell/NewCell/Finish cell/StaticEnd.png" },
        { TerrainType.Water, "Assets/Obstacles/water/0.gif" },
        { TerrainType.Stone, "Assets/Sprites/Cell/NewCell/Group 869.png" },
        { TerrainType.Fire, "Assets/Obstacles/fire/1.jpeg" }
    };

    private Dictionary<CellItem, string> itemTexturePaths = new() {
        { CellItem.Star, "Assets/Sprites/Stars/Group 988.png" },
    };

    private Dictionary<char, TerrainType> cellTypes = new() {
        { '0', TerrainType.Empty },

        { '+', TerrainType.Default },
        { 'G', TerrainType.Default },
        { '1', TerrainType.Default },

        { 'W', TerrainType.Water },
        { '2', TerrainType.Water },

        { 'S', TerrainType.Stone },
        { '3', TerrainType.Stone },

        { 'F', TerrainType.Fire },

        { 'x', TerrainType.Start },
        { 'y', TerrainType.End }
    };

    private List<TerrainType> toolTerrainTypes = new() {
        TerrainType.Empty,
        TerrainType.Default,
        TerrainType.Start,
        TerrainType.End,
        TerrainType.Water,
        TerrainType.Stone,
        TerrainType.Fire
    };

    private List<CellItem> toolItemTypes = new() {
        CellItem.None,
        CellItem.Star,
    };

    private Dictionary<char, CellItem> itemTypes = new() {
        { 'G', CellItem.Star },
        { '1', CellItem.Star },
        { '2', CellItem.Star },
        { '3', CellItem.Star }
    };

    private TerrainType selectedTerrainType = TerrainType.Default;
    private CellItem? selectedItemType = null;

    private Vector2 leftPanelScroll;
    private float leftPanelWidth = 300f; // Default width for the left panel
    private bool isDraggingSplitter = false;
    private float splitterWidth = 5f; // Width of the draggable splitter

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

        // Splitter
        Rect splitterRect = GUILayoutUtility.GetRect(splitterWidth, 0, GUILayout.ExpandHeight(true));
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

        // Handle splitter drag
        Event e = Event.current;
        if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition))
        {
            isDraggingSplitter = true;
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            isDraggingSplitter = false;
        }
        else if (e.type == EventType.MouseDrag && isDraggingSplitter)
        {
            leftPanelWidth += e.delta.x;
            leftPanelWidth = Mathf.Clamp(leftPanelWidth, 200f, 500f); // Min and max width
            e.Use();
            Repaint();
        }

        // Preview Panel
        DrawPreviewPanel();

        EditorGUILayout.EndHorizontal();

        // Update window title to reflect current state
        UpdateWindowTitle();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
        leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll);
        
        // Add padding container
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
        
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

        EditorGUILayout.Space();

        // Tool Selection
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
        DrawToolSelection();

        EditorGUILayout.Space();

        // Draw the appropriate tool panel
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

        EditorGUILayout.EndVertical(); // End padding container
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawToolSelection()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Toggle(currentMode == EditorMode.Tiles, "Tiles", EditorStyles.toolbarButton))
        {
            currentMode = EditorMode.Tiles;
        }
        if (GUILayout.Toggle(currentMode == EditorMode.Moves, "Moves", EditorStyles.toolbarButton))
        {
            currentMode = EditorMode.Moves;
        }
        if (GUILayout.Toggle(currentMode == EditorMode.Analysis, "Analysis", EditorStyles.toolbarButton))
        {
            currentMode = EditorMode.Analysis;
        }
        
        EditorGUILayout.EndHorizontal();
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
            bool isSelected = selectedTerrainType == terrainType && selectedItemType == null;
            Rect rowRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true), GUILayout.Height(60));

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
                selectedItemType = null; // Clear item selection when terrain is selected
                GUI.FocusControl(null);
            }

            // Show the texture
            if (cellTextures.TryGetValue(terrainType, out Texture2D texture))
            {
                GUI.Label(new Rect(8, 8, 48, 48), texture);
            }

            // Find the character that represents this terrain type
            char? terrainChar = cellTypes.FirstOrDefault(x => x.Value == terrainType).Key;
            GUI.Label(new Rect(64, 16, rowRect.width - 64, 24), $"{terrainType}");

            EditorGUILayout.EndHorizontal();
            GUI.EndGroup();
            GUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Item Type Selection
        EditorGUILayout.LabelField("Item Type", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

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
                selectedTerrainType = TerrainType.Empty; // Clear terrain type when item is selected
                GUI.FocusControl(null);
            }
            if (itemType == CellItem.None)
            {
                GUI.Label(new Rect(8, 16, itemRowRect.width - 8, 24), "None");
            }
            else
            {
                if (itemTextures.TryGetValue(itemType, out Texture2D texture))
                {
                    GUI.Label(new Rect(8, 8, 48, 48), texture);
                }
                char? itemChar = itemTypes.FirstOrDefault(x => x.Value == itemType).Key;
                GUI.Label(new Rect(64, 16, itemRowRect.width - 64, 24), $"{itemChar} - {itemType}");
            }
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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Count different terrain types
            Dictionary<TerrainType, int> terrainCounts = new();
            Dictionary<CellItem, int> itemCounts = new();

            for (int i = 0; i < currentLevel.Map.Length; i++)
            {
                CellData cell = currentLevel.Map[i];
                terrainCounts[cell.Terrain] = terrainCounts.GetValueOrDefault(cell.Terrain) + 1;
                itemCounts[cell.Item] = itemCounts.GetValueOrDefault(cell.Item) + 1;
            }

            EditorGUILayout.LabelField("Terrain Distribution", EditorStyles.boldLabel);
            foreach (var count in terrainCounts)
            {
                EditorGUILayout.LabelField($"{count.Key}: {count.Value}");
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Item Distribution", EditorStyles.boldLabel);
            foreach (var count in itemCounts)
            {
                EditorGUILayout.LabelField($"{count.Key}: {count.Value}");
            }

            EditorGUILayout.Space();

            // Debug Preview
            EditorGUILayout.LabelField("Debug Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Map Size: {currentLevel.MapSize}");
            EditorGUILayout.LabelField($"Total Cells: {currentLevel.Map.Length}");

            if (GUILayout.Button("Print Map Data"))
            {
                string mapData = "";
                for (int y = 0; y < currentLevel.MapSize.y; y++)
                {
                    for (int x = 0; x < currentLevel.MapSize.x; x++)
                    {
                        int index = y * currentLevel.MapSize.x + x;
                        CellData cell = currentLevel.Map[index];
                        mapData += $"({cell.Terrain}, {cell.Item}) ";
                    }
                    mapData += "\n";
                }
                Debug.Log($"Map Data:\n{mapData}");
            }

            EditorGUILayout.EndVertical();
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
        
        // Add padding container
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
        
        // Preview Header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Level Preview", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Preview Area
        if (currentLevel != null)
        {
            // Initialize map if it doesn't exist or has wrong size
            if (currentLevel.Map == null || currentLevel.Map.Length != currentLevel.MapSize.x * currentLevel.MapSize.y)
            {
                currentLevel.Map = new CellData[currentLevel.MapSize.x * currentLevel.MapSize.y];
                for (int i = 0; i < currentLevel.Map.Length; i++)
                {
                    currentLevel.Map[i] = new CellData(TerrainType.Default, CellItem.None);
                }
                hasUnsavedChanges = true;
            }

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
                    
                    // Ensure cell data exists
                    if (currentLevel.Map[index] == null)
                    {
                        currentLevel.Map[index] = new CellData(TerrainType.Default, CellItem.None);
                        hasUnsavedChanges = true;
                    }

                    CellData cellData = currentLevel.Map[index];

                    // Calculate position with padding
                    float posX = x * (tileSize + tilePadding);
                    float posY = y * (tileSize + tilePadding);
                    Rect tileRect = new Rect(posX, posY, tileSize, tileSize);

                    // Handle click on tile
                    Event e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 0 && tileRect.Contains(e.mousePosition))
                    {
                        if (selectedItemType.HasValue)
                        {
                            // Get the current terrain type for this cell
                            TerrainType currentTerrain = cellData.Terrain;
                            
                            // Check if the current terrain type is valid for items
                            bool isValidTerrain = currentTerrain == TerrainType.Default || 
                                                currentTerrain == TerrainType.Stone || 
                                                currentTerrain == TerrainType.Water;

                            if (isValidTerrain)
                            {
                                if (cellData.Item != selectedItemType.Value)
                                {
                                    cellData.Item = selectedItemType.Value;
                                    currentLevel.Map[index] = cellData;
                                    hasUnsavedChanges = true;
                                    e.Use();
                                }
                            }
                        }
                        else
                        {
                            if (cellData.Terrain != selectedTerrainType)
                            {
                                cellData.Terrain = selectedTerrainType;
                                currentLevel.Map[index] = cellData;
                                hasUnsavedChanges = true;
                                e.Use();
                            }
                        }
                    }

                    // Draw cell texture
                    Texture2D cellTexture = cellTextures[cellData.Terrain];
                    GUI.DrawTexture(tileRect, cellTexture);

                    // Draw item texture if present
                    if (cellData.Item != CellItem.None)
                    {
                        if (itemTextures.TryGetValue(cellData.Item, out Texture2D itemTexture))
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
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("No level selected. Create a new level or select an existing one.", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical(); // End padding container
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