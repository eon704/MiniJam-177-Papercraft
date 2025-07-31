using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public class LevelEditorWindow : EditorWindow
{
    private LevelData currentLevel;
    private WorkingLevelData workingLevel; // Working copy for editing
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
    private float leftPanelWidth = 350f; // Default width for the left panel
    private bool isDraggingSplitter = false;
    private float splitterWidth = 5f; // Width of the draggable splitter
    private bool? lastSolvabilityResult = null; // Cache the result
    private bool isCheckingSolvability = false; // Track if solvability check is in progress
    private List<TurnInfo> currentSolutionPath = null; // Store the current solution for visualization
    private bool showSolutionPath = false; // Toggle for showing/hiding the solution path

    // Colors for different player states
    private readonly Dictionary<Player.StateType, Color> stateColors = new()
    {
        { Player.StateType.Default, Color.white },
        { Player.StateType.Crane, Color.red },
        { Player.StateType.Plane, Color.yellow },
        { Player.StateType.Boat, Color.blue },
        { Player.StateType.Frog, Color.green }
    };

    // Helper method to create colored textures for button backgrounds
    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = color;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

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
        Event e = Event.current;
        // Handle keyboard shortcuts
        if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S)
        {
            SaveChanges();
            e.Use();
        }

        EditorGUILayout.BeginHorizontal();

        // Left Panel
        DrawLeftPanel();

        // Splitter
        Rect splitterRect = GUILayoutUtility.GetRect(splitterWidth, 0, GUILayout.ExpandHeight(true));
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

        // Handle splitter drag
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
            workingLevel = new WorkingLevelData(currentLevel);
            hasUnsavedChanges = false;
            
            // Clear solvability check state when level changes
            ResetSolvabilityCheck();
            
            // Load cached solution if available
            if (workingLevel?.CachedSolution != null && workingLevel.CachedSolution.Count > 0)
            {
                LoadCachedSolution();
            }
            
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

        if (GUILayout.Toggle(currentMode == EditorMode.Tiles, "Tiles", EditorStyles.toolbarButton, GUILayout.MinWidth(60)))
        {
            currentMode = EditorMode.Tiles;
        }
        if (GUILayout.Toggle(currentMode == EditorMode.Moves, "Moves", EditorStyles.toolbarButton, GUILayout.MinWidth(60)))
        {
            currentMode = EditorMode.Moves;
        }
        if (GUILayout.Toggle(currentMode == EditorMode.Analysis, "Analysis", EditorStyles.toolbarButton, GUILayout.MinWidth(70)))
        {
            currentMode = EditorMode.Analysis;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTilesTool()
    {
        EditorGUILayout.LabelField("Tile Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (workingLevel == null)
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
            return;
        }

        // Terrain Type Selection
        EditorGUILayout.LabelField("Terrain Type", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        foreach (var terrainType in toolTerrainTypes)
        {
            bool isSelected = selectedTerrainType == terrainType && selectedItemType == null;
            
            // Create a custom button style for better hover feedback
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 60,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 8, 8)
            };
            
            // Set colors for selected state
            if (isSelected)
            {
                buttonStyle.normal.background = MakeTexture(2, 2, new Color(0.24f, 0.48f, 0.90f, 0.25f));
                buttonStyle.hover.background = MakeTexture(2, 2, new Color(0.24f, 0.48f, 0.90f, 0.35f));
            }
            
            // Create button content
            GUIContent buttonContent = new GUIContent($"  {terrainType}");
            if (cellTextures.TryGetValue(terrainType, out Texture2D texture))
            {
                buttonContent.image = texture;
            }
            
            if (GUILayout.Button(buttonContent, buttonStyle, GUILayout.MaxWidth(280)))
            {
                selectedTerrainType = terrainType;
                selectedItemType = null; // Clear item selection when terrain is selected
                GUI.FocusControl(null);
            }
            
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
            
            // Create a custom button style for better hover feedback
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 48,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 8, 8)
            };
            
            // Set colors for selected state
            if (isSelected)
            {
                buttonStyle.normal.background = MakeTexture(2, 2, new Color(0.24f, 0.48f, 0.90f, 0.25f));
                buttonStyle.hover.background = MakeTexture(2, 2, new Color(0.24f, 0.48f, 0.90f, 0.35f));
            }
            
            // Create button content
            GUIContent buttonContent;
            if (itemType == CellItem.None)
            {
                buttonContent = new GUIContent("  None");
            }
            else
            {
                char? itemChar = itemTypes.FirstOrDefault(x => x.Value == itemType).Key;
                buttonContent = new GUIContent($"  {itemChar} - {itemType}");
                if (itemTextures.TryGetValue(itemType, out Texture2D texture))
                {
                    buttonContent.image = texture;
                }
            }
            
            if (GUILayout.Button(buttonContent, buttonStyle, GUILayout.MaxWidth(280)))
            {
                selectedItemType = itemType;
                selectedTerrainType = TerrainType.Empty; // Clear terrain type when item is selected
                GUI.FocusControl(null);
            }
            
            GUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawMovesTool()
    {
        EditorGUILayout.LabelField("Moves Editor", EditorStyles.boldLabel);
        if (workingLevel != null)
        {
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < workingLevel.StartMovesPerForm.Count; i++)
            {
                var moveEntry = workingLevel.StartMovesPerForm[i];
                if (moveEntry.State == Player.StateType.Default)
                    continue; // Hide Default moves from the editor
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(moveEntry.State.ToString(), GUILayout.Width(80));

                int newMoves = EditorGUILayout.IntField(moveEntry.Moves);
                if (newMoves != moveEntry.Moves)
                {
                    moveEntry.Moves = newMoves;
                    workingLevel.StartMovesPerForm[i] = moveEntry;
                    hasUnsavedChanges = true;
                    ResetSolvabilityCheck(); // Reset when level changes
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.EndChangeCheck();
        }
        else
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
        }
    }

    private void DrawAnalysisTool()
    {
        EditorGUILayout.LabelField("Level Analysis", EditorStyles.boldLabel);
        if (workingLevel != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Show cached solution status
            if (workingLevel.CachedSolution != null && workingLevel.CachedSolution.Count > 0)
            {
                EditorGUILayout.HelpBox($"✓ Level has a cached solution with {workingLevel.CachedSolution.Count} steps.", MessageType.Info);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Load Cached Solution"))
                {
                    LoadCachedSolution();
                }
                if (GUILayout.Button("Clear Cached Solution"))
                {
                    ClearCachedSolution();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
            }

            // Check Solvability Button
            EditorGUI.BeginDisabledGroup(isCheckingSolvability);
            if (GUILayout.Button(isCheckingSolvability ? "Checking..." : "Check if Level is Solvable"))
            {
                StartSolvabilityCheck();
            }
            EditorGUI.EndDisabledGroup();

            // Show loading message while checking
            if (isCheckingSolvability)
            {
                EditorGUILayout.HelpBox("Checking solvability... Please wait.", MessageType.Info);
            }
            // Show solvability result if available
            else if (lastSolvabilityResult.HasValue)
            {
                string message = lastSolvabilityResult.Value ? "✓ This level is solvable!" : "✗ This level is NOT solvable.";
                MessageType messageType = lastSolvabilityResult.Value ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(message, messageType);
            }

            EditorGUILayout.Space();

            // Solution Path Visualization Controls
            if (currentSolutionPath != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Solution Visualization", EditorStyles.boldLabel);
                
                bool newShowPath = EditorGUILayout.Toggle("Show Solution Path", showSolutionPath);
                if (newShowPath != showSolutionPath)
                {
                    showSolutionPath = newShowPath;
                    Repaint();
                }
                
                if (GUILayout.Button("Clear Solution"))
                {
                    currentSolutionPath = null;
                    showSolutionPath = false;
                    Repaint();
                }
                
                EditorGUILayout.Space();
                
                // Solution Steps Display - integrated directly into the analysis tab
                EditorGUILayout.LabelField("Solution Steps", EditorStyles.boldLabel);
                
                // Get final step info for display
                TurnInfo finalStep = currentSolutionPath[currentSolutionPath.Count - 1];
                EditorGUILayout.LabelField($"✓ Valid Solution: {currentSolutionPath.Count} steps to reach ({finalStep.Position.x}, {finalStep.Position.y}) with {finalStep.Stars} stars", EditorStyles.boldLabel);
                
                // Show collected star positions
                HashSet<Vector2Int> collectedStars = finalStep.CollectedStarPositions ?? new HashSet<Vector2Int>();
                if (collectedStars.Count > 0)
                {
                    string starPositions = string.Join(", ", collectedStars.Select(pos => $"({pos.x},{pos.y})"));
                    EditorGUILayout.LabelField($"★ Stars collected at: {starPositions}", EditorStyles.miniLabel);
                }
                
                EditorGUILayout.Space();
                
                // Header row
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUILayout.LabelField("Step", EditorStyles.toolbarButton, GUILayout.Width(40));
                EditorGUILayout.LabelField("Position", EditorStyles.toolbarButton, GUILayout.Width(70));
                EditorGUILayout.LabelField("Form", EditorStyles.toolbarButton, GUILayout.Width(70));
                EditorGUILayout.LabelField("Stars", EditorStyles.toolbarButton, GUILayout.Width(50));
                EditorGUILayout.LabelField("Action", EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                
                // Display solution steps directly without separate scroll view
                for (int i = 0; i < currentSolutionPath.Count; i++)
                {
                    TurnInfo turn = currentSolutionPath[i];
                    
                    // Get the color for the current state
                    Color stepColor = stateColors.TryGetValue(turn.State, out Color color) ? color : Color.white;
                    
                    // Create a colored background style for the step
                    GUIStyle stepStyle = new GUIStyle(GUI.skin.box);
                    stepStyle.normal.background = MakeTexture(2, 2, new Color(stepColor.r, stepColor.g, stepColor.b, 0.15f));
                    stepStyle.margin = new RectOffset(2, 2, 1, 1);
                    
                    EditorGUILayout.BeginHorizontal(stepStyle);
                    
                    // Step number
                    EditorGUILayout.LabelField($"{i + 1}", GUILayout.Width(40));
                    
                    // Position
                    EditorGUILayout.LabelField($"({turn.Position.x}, {turn.Position.y})", GUILayout.Width(70));
                    
                    // State with color indicator
                    GUIStyle stateStyle = new GUIStyle(EditorStyles.label);
                    stateStyle.normal.textColor = stepColor;
                    EditorGUILayout.LabelField($"{turn.State}", stateStyle, GUILayout.Width(70));
                    
                    // Stars collected
                    EditorGUILayout.LabelField($"★ {turn.Stars}", GUILayout.Width(50));
                    
                    // Action description
                    string actionDescription = "";
                    if (i == 0)
                    {
                        actionDescription = "Start position";
                    }
                    else
                    {
                        TurnInfo prevTurn = currentSolutionPath[i - 1];
                        if (turn.Stars > prevTurn.Stars)
                        {
                            actionDescription = "Collected star";
                        }
                        else if (turn.Position == finalStep.Position && i == currentSolutionPath.Count - 1)
                        {
                            actionDescription = "Reached end";
                        }
                        else
                        {
                            actionDescription = "Move";
                        }
                    }
                    
                    EditorGUILayout.LabelField(actionDescription, GUILayout.ExpandWidth(true));
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
        }
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
        if (workingLevel != null)
        {
            // Initialize map if it doesn't exist or has wrong size
            if (workingLevel.Map == null || workingLevel.Map.Length != workingLevel.MapSize.x * workingLevel.MapSize.y)
            {
                workingLevel.Map = new CellData[workingLevel.MapSize.x * workingLevel.MapSize.y];
                for (int i = 0; i < workingLevel.Map.Length; i++)
                {
                    workingLevel.Map[i] = new CellData(TerrainType.Default, CellItem.None);
                }
                hasUnsavedChanges = true;
                ResetSolvabilityCheck(); // Reset when level changes
            }

            // Calculate the total size of the grid with padding
            float totalWidth = workingLevel.MapSize.x * (tileSize + tilePadding);
            float totalHeight = workingLevel.MapSize.y * (tileSize + tilePadding);

            // Create a scroll view for the grid
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(totalWidth + 20), GUILayout.Height(totalHeight + 20));

            // Draw the grid
            for (int y = 0; y < workingLevel.MapSize.y; y++)
            {
                for (int x = 0; x < workingLevel.MapSize.x; x++)
                {
                    int index = y * workingLevel.MapSize.x + x;

                    // Ensure cell data exists
                    if (workingLevel.Map[index] == null)
                    {
                        workingLevel.Map[index] = new CellData(TerrainType.Default, CellItem.None);
                        hasUnsavedChanges = true;
                        ResetSolvabilityCheck(); // Reset when level changes
                    }

                    CellData cellData = workingLevel.Map[index];

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
                                    workingLevel.Map[index] = cellData;
                                    hasUnsavedChanges = true;
                                    ResetSolvabilityCheck(); // Reset when level changes
                                    e.Use();
                                }
                            }
                        }
                        else
                        {
                            if (cellData.Terrain != selectedTerrainType)
                            {
                                cellData.Terrain = selectedTerrainType;
                                workingLevel.Map[index] = cellData;
                                hasUnsavedChanges = true;
                                ResetSolvabilityCheck(); // Reset when level changes
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

            // Draw solution path visualization
            if (showSolutionPath && currentSolutionPath != null && currentSolutionPath.Count > 1)
            {
                DrawSolutionPath();
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
        workingLevel = new WorkingLevelData(currentLevel);
        hasUnsavedChanges = false;
        UpdateWindowTitle();
    }

    public override void SaveChanges()
    {
        if (currentLevel == null || workingLevel == null)
            return;

        // Validate the level before saving
        if (!workingLevel.IsValid())
        {
            EditorUtility.DisplayDialog("Level validation failed", "The current level is invalid and cannot be saved. Check the console for more details.", "OK");
            return;
        }

        // Copy changes from working level to the original asset
        CopyWorkingDataToLevel(workingLevel, currentLevel);
        
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
        
        // Recreate working copy from the fresh asset
        workingLevel = new WorkingLevelData(currentLevel);
        hasUnsavedChanges = false;
        
        // Reset solvability check state since we've reverted to saved state
        ResetSolvabilityCheck();
        
        UpdateWindowTitle();
        
        // Force UI repaint to show reverted state
        Repaint();
    }

    private bool IsLevelSolvable(LevelData level)
    {
        var solution = SolveLevel(level);
        // Cache the solution if one is found
        if (solution != null)
        {
            currentSolutionPath = solution;
        }
        else
        {
            currentSolutionPath = null;
        }
        return solution != null;
    }

    private List<TurnInfo> SolveLevel(LevelData level)
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
            }
        }

        // BFS to find solution
        Queue<TurnInfo> queue = new(); // Include depth to prevent infinite search
        HashSet<string> visited = new();
        Dictionary<string, TurnInfo> parent = new();

        // Initial state
        TurnInfo initialTurn = new TurnInfo
        {
            Position = startPos,
            Stars = 0,
            MovesPerForm = level.StartMovesPerForm.ToDictionary(m => m.State, m => m.Moves),
            State = Player.StateType.Default,
            CollectedStarPositions = new HashSet<Vector2Int>()
        };

        queue.Enqueue(initialTurn);
        string initialKey = GetStateKey(initialTurn);
        visited.Add(initialKey);
        int steps = 10000;

        while (queue.Count > 0 && steps > 0)
        {
            steps--;
            var current = queue.Dequeue();
            string currentKey = GetStateKey(current);

            if (current.Position == endPos && current.Stars == 3)
            {
                // Reconstruct path
                List<TurnInfo> solution = new();
                string key = currentKey;
                while (parent.ContainsKey(key))
                {
                    solution.Insert(0, parent[key]);
                    key = GetStateKey(parent[key]);
                }
                solution.Add(current);

                // Debug: Log the solution for verification
                Debug.Log($"Found potential solution with {solution.Count} steps, final state: Position={current.Position}, Stars={current.Stars}");

                // Final validation: ensure the solution path ends at the goal with all stars
                if (ValidateSolution(solution, endPos))
                {
                    return solution;
                }
                else
                {
                    Debug.LogError("Solution failed validation despite meeting BFS goal condition - this should not happen!");
                    return null; // This should not happen, but return null instead of continue
                }
            }

            // Try all possible states from current position
            foreach (Player.StateType stateType in System.Enum.GetValues(typeof(Player.StateType)))
            {
                if (stateType == Player.StateType.Default) continue;

                // For non-default states, check if we have moves left
                if (current.MovesPerForm[stateType] <= 0) continue;

                // Get the state model for movement options
                if (!StateModelInfo.StateModels.TryGetValue(stateType, out StateModel stateModel)) continue;

                // Try all possible moves for this state
                foreach (Vector2Int moveOffset in stateModel.MoveOptions)
                {
                    Vector2Int newPos = current.Position + moveOffset;

                    // Check bounds
                    if (newPos.x < 0 || newPos.x >= level.MapSize.x ||
                        newPos.y < 0 || newPos.y >= level.MapSize.y) continue;

                    int cellIndex = newPos.y * level.MapSize.x + newPos.x;
                    CellData targetCell = level.Map[cellIndex];

                    // Invalidate any moves that go on Fire terrain
                    if (targetCell.Terrain == TerrainType.Fire) continue;

                    // Check if this state can move to this terrain
                    if (!stateModel.MoveTerrain.Contains(targetCell.Terrain)) continue;

                    // Calculate new star count - only count if this star position hasn't been collected yet
                    int newStars = current.Stars;
                    HashSet<Vector2Int> newCollectedStars = new(current.CollectedStarPositions ?? new HashSet<Vector2Int>());

                    if (targetCell.Item == CellItem.Star && !newCollectedStars.Contains(newPos))
                    {
                        newStars++;
                        newCollectedStars.Add(newPos);
                    }

                    // Create new moves dictionary
                    Dictionary<Player.StateType, int> newMovesPerForm = new(current.MovesPerForm);
                    if (stateType != Player.StateType.Default)
                        newMovesPerForm[stateType]--;

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
                        queue.Enqueue(nextTurn);
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
        public HashSet<Vector2Int> CollectedStarPositions; // Track which star positions have been collected
    }

    private string GetStateKey(TurnInfo state)
    {
        // Create a more comprehensive key that includes move counts and collected star positions to avoid redundant states
        string movesKey = string.Join(",", state.MovesPerForm.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        string starsKey = string.Join(";", (state.CollectedStarPositions ?? new HashSet<Vector2Int>()).OrderBy(pos => pos.x).ThenBy(pos => pos.y).Select(pos => $"{pos.x},{pos.y}"));
        return $"{state.Position.x},{state.Position.y},{state.State},{state.Stars},{movesKey},{starsKey}";
    }

    private IEnumerable<CellData> GetNeighbors(LevelData level, Vector2Int position)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighborPos = position + dir;
            if (neighborPos.x >= 0 && neighborPos.x < level.MapSize.x &&
                neighborPos.y >= 0 && neighborPos.y < level.MapSize.y)
            {
                yield return level.Map[neighborPos.y * level.MapSize.x + neighborPos.x];
            }
        }
    }

    private bool CanMoveTo(CellData cell, Player.StateType state)
    {
        // Use the state models to determine valid terrain for each state
        if (!StateModelInfo.StateModels.TryGetValue(state, out StateModel stateModel))
            return false;

        return stateModel.MoveTerrain.Contains(cell.Terrain);
    }

    private void StartSolvabilityCheck()
    {
        if (isCheckingSolvability) return; // Prevent multiple simultaneous checks
        
        isCheckingSolvability = true;
        lastSolvabilityResult = null; // Clear previous result
        
        // Use EditorApplication.update to perform the check on the next frame
        // This allows the UI to refresh and show the loading message
        EditorApplication.update += PerformSolvabilityCheck;
        Repaint(); // Force UI refresh to show loading message
    }

    private void ResetSolvabilityCheck()
    {
        // Clean up any pending solvability check
        EditorApplication.update -= PerformSolvabilityCheck;
        isCheckingSolvability = false;
        lastSolvabilityResult = null;
        
        // Clear solution path visualization when level changes
        currentSolutionPath = null;
        showSolutionPath = false;
    }

    private void OnDestroy()
    {
        // Clean up callback when window is destroyed
        EditorApplication.update -= PerformSolvabilityCheck;
    }

    private void PerformSolvabilityCheck()
    {
        // Remove the update callback first
        EditorApplication.update -= PerformSolvabilityCheck;
        
        try
        {
            // Perform the actual solvability check and cache the solution
            if (workingLevel != null)
            {
                var solution = SolveLevel(workingLevel.ToLevelData());
                lastSolvabilityResult = solution != null;
                
                // Cache the solution for later use
                if (solution != null)
                {
                    currentSolutionPath = solution;
                    
                    // Store solution in the working level data
                    StoreSolutionInWorkingLevel(solution);
                    
                    Debug.Log($"Solvability check completed: Level is solvable with {solution.Count} steps. Solution cached.");
                }
                else
                {
                    currentSolutionPath = null;
                    
                    // Clear solution from working level data
                    if (workingLevel != null)
                    {
                        workingLevel.CachedSolution = new List<SolutionStep>();
                    }
                    
                    Debug.Log("Solvability check completed: Level is not solvable.");
                }
            }
            else
            {
                lastSolvabilityResult = false;
                currentSolutionPath = null;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during solvability check: {ex.Message}");
            lastSolvabilityResult = false;
            currentSolutionPath = null;
        }
        finally
        {
            isCheckingSolvability = false;
            Repaint(); // Force UI refresh to show result
        }
    }

    private void CopyLevelData(LevelData source, LevelData destination)
    {
        if (source == null || destination == null) return;
        
        // Copy basic properties
        destination.MapSize = source.MapSize;
        
        // Deep copy the map
        if (source.Map != null)
        {
            destination.Map = new CellData[source.Map.Length];
            for (int i = 0; i < source.Map.Length; i++)
            {
                destination.Map[i] = source.Map[i];
            }
        }
        
        // Deep copy StartMovesPerForm
        if (source.StartMovesPerForm != null)
        {
            destination.StartMovesPerForm = new List<MovePerFormEntry>();
            foreach (var move in source.StartMovesPerForm)
            {
                destination.StartMovesPerForm.Add(new MovePerFormEntry
                { 
                    State = move.State, 
                    Moves = move.Moves 
                });
            }
        }
    }

    private void CopyWorkingDataToLevel(WorkingLevelData source, LevelData destination)
    {
        if (source == null || destination == null) return;
        
        // Copy basic properties
        destination.MapSize = source.MapSize;
        
        // Deep copy the map
        if (source.Map != null)
        {
            destination.Map = new CellData[source.Map.Length];
            for (int i = 0; i < source.Map.Length; i++)
            {
                destination.Map[i] = new CellData(source.Map[i].Terrain, source.Map[i].Item);
            }
        }
        
        // Deep copy StartMovesPerForm
        if (source.StartMovesPerForm != null)
        {
            destination.StartMovesPerForm = new List<MovePerFormEntry>();
            foreach (var move in source.StartMovesPerForm)
            {
                destination.StartMovesPerForm.Add(new MovePerFormEntry
                { 
                    State = move.State, 
                    Moves = move.Moves 
                });
            }
        }
        
        // Copy cached solution
        if (source.CachedSolution != null)
        {
            destination.CachedSolution = new List<SolutionStep>(source.CachedSolution);
        }
    }

    // Plain data structure for editing - not a ScriptableObject
    [System.Serializable]
    public class WorkingLevelData
    {
        public Vector2Int MapSize;
        public CellData[] Map;
        public List<MovePerFormEntry> StartMovesPerForm;
        public List<SolutionStep> CachedSolution; // Store the cached solution
        
        public WorkingLevelData() { }
        
        public WorkingLevelData(LevelData original)
        {
            if (original == null) return;
            
            // Copy basic properties
            MapSize = original.MapSize;
            
            // Deep copy the map array
            if (original.Map != null)
            {
                Map = new CellData[original.Map.Length];
                for (int i = 0; i < original.Map.Length; i++)
                {
                    // CellData is a struct, so this creates a true copy
                    Map[i] = new CellData(original.Map[i].Terrain, original.Map[i].Item);
                }
            }
            
            // Deep copy StartMovesPerForm list
            if (original.StartMovesPerForm != null)
            {
                StartMovesPerForm = new List<MovePerFormEntry>();
                foreach (var move in original.StartMovesPerForm)
                {
                    // Create new instances to avoid reference sharing
                    StartMovesPerForm.Add(new MovePerFormEntry
                    { 
                        State = move.State, 
                        Moves = move.Moves 
                    });
                }
            }
            
            // Copy cached solution
            if (original.CachedSolution != null)
            {
                CachedSolution = new List<SolutionStep>(original.CachedSolution);
            }
            else
            {
                CachedSolution = new List<SolutionStep>();
            }
        }
        
        public bool IsValid()
        {
            if (Map == null || Map.Length == 0)
                return false;
                
            if (MapSize.x <= 0 || MapSize.y <= 0)
                return false;
                
            if (Map.Length != MapSize.x * MapSize.y)
                return false;
                
            // Check for exactly one start and one end
            int startCount = 0;
            int endCount = 0;
            
            foreach (var cell in Map)
            {
                if (cell.Terrain == TerrainType.Start) startCount++;
                if (cell.Terrain == TerrainType.End) endCount++;
            }
            
            return startCount == 1 && endCount == 1;
        }
        
        // Convert back to LevelData for solving/validation
        public LevelData ToLevelData()
        {
            LevelData levelData = ScriptableObject.CreateInstance<LevelData>();
            levelData.MapSize = MapSize;
            
            if (Map != null)
            {
                levelData.Map = new CellData[Map.Length];
                for (int i = 0; i < Map.Length; i++)
                {
                    levelData.Map[i] = new CellData(Map[i].Terrain, Map[i].Item);
                }
            }
            
            if (StartMovesPerForm != null)
            {
                levelData.StartMovesPerForm = new List<MovePerFormEntry>();
                foreach (var move in StartMovesPerForm)
                {
                    levelData.StartMovesPerForm.Add(new MovePerFormEntry
                    { 
                        State = move.State, 
                        Moves = move.Moves 
                    });
                }
            }
            
            // Copy cached solution
            if (CachedSolution != null)
            {
                levelData.CachedSolution = new List<SolutionStep>(CachedSolution);
            }
            
            return levelData;
        }
    }
    
    private void StoreSolutionInWorkingLevel(List<TurnInfo> solution)
    {
        if (workingLevel == null || solution == null) return;
        
        // Convert TurnInfo list to SolutionStep list
        workingLevel.CachedSolution = new List<SolutionStep>();
        
        foreach (var turn in solution)
        {
            workingLevel.CachedSolution.Add(new SolutionStep(
                turn.Position,
                turn.State,
                turn.Stars
            ));
        }
        
        // Mark as having unsaved changes since we've updated the solution
        hasUnsavedChanges = true;
    }
    
    private void LoadCachedSolution()
    {
        if (workingLevel?.CachedSolution == null || workingLevel.CachedSolution.Count == 0)
        {
            Debug.LogWarning("No cached solution available to load.");
            return;
        }
        
        // Convert SolutionStep list back to TurnInfo list for display
        currentSolutionPath = new List<TurnInfo>();
        
        foreach (var step in workingLevel.CachedSolution)
        {
            // Create a simplified TurnInfo for display purposes
            // Note: Some fields like MovesPerForm and CollectedStarPositions are not stored in the cached solution
            // so we'll create minimal TurnInfo objects just for visualization
            var turnInfo = new TurnInfo
            {
                Position = step.Position,
                State = step.State,
                Stars = step.StarsCollected,
                MovesPerForm = new Dictionary<Player.StateType, int>(), // Empty for cached solutions
                CollectedStarPositions = new HashSet<Vector2Int>() // Empty for cached solutions
            };
            
            currentSolutionPath.Add(turnInfo);
        }
        
        // Update UI state
        showSolutionPath = true;
        lastSolvabilityResult = true; // Indicate that the level is solvable
        
        Debug.Log($"Loaded cached solution with {currentSolutionPath.Count} steps.");
        Repaint();
    }
    
    private void ClearCachedSolution()
    {
        if (workingLevel != null)
        {
            workingLevel.CachedSolution = new List<SolutionStep>();
            hasUnsavedChanges = true;
        }
        
        // Also clear the current visualization
        currentSolutionPath = null;
        showSolutionPath = false;
        
        Debug.Log("Cached solution cleared.");
        Repaint();
    }
    
    private void DrawSolutionPath()
    {
        if (currentSolutionPath == null || currentSolutionPath.Count < 2) return;

        // Create arrow texture for path direction
        var arrowTexture = MakeArrowTexture();
        
        for (int i = 0; i < currentSolutionPath.Count - 1; i++)
        {
            TurnInfo currentStep = currentSolutionPath[i];
            TurnInfo nextStep = currentSolutionPath[i + 1];
            
            // Get positions in screen coordinates
            Vector2 currentPos = GetTileCenterPosition(currentStep.Position);
            Vector2 nextPos = GetTileCenterPosition(nextStep.Position);
            
            // Get color for current state
            Color pathColor = stateColors.TryGetValue(nextStep.State, out Color color) ? color : Color.white;
            
            // Draw line between positions
            DrawLine(currentPos, nextPos, pathColor, 4f);
            
            // Draw step number
            DrawStepNumber(nextPos, i + 1, pathColor);
            
            // Draw directional arrow
            DrawArrow(currentPos, nextPos, pathColor);
        }
        
        // Draw start and end markers
        if (currentSolutionPath.Count > 0)
        {
            Vector2 startPos = GetTileCenterPosition(currentSolutionPath[0].Position);
            Vector2 endPos = GetTileCenterPosition(currentSolutionPath[currentSolutionPath.Count - 1].Position);
            
            DrawMarker(startPos, "START", Color.green);
            DrawMarker(endPos, "END", Color.red);
        }
    }
    
    private Vector2 GetTileCenterPosition(Vector2Int tileCoord)
    {
        float posX = tileCoord.x * (tileSize + tilePadding) + tileSize * 0.5f;
        float posY = tileCoord.y * (tileSize + tilePadding) + tileSize * 0.5f;
        return new Vector2(posX, posY);
    }
    
    private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        // Calculate line direction and perpendicular
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        
        // Create line quad vertices
        Vector3[] lineVerts = new Vector3[4]
        {
            new Vector3(start.x - perpendicular.x, start.y - perpendicular.y, 0),
            new Vector3(start.x + perpendicular.x, start.y + perpendicular.y, 0),
            new Vector3(end.x + perpendicular.x, end.y + perpendicular.y, 0),
            new Vector3(end.x - perpendicular.x, end.y - perpendicular.y, 0)
        };
        
        // Draw the line using GUI
        GUI.color = color;
        for (int i = 0; i < 4; i++)
        {
            Vector2 pixelPos = lineVerts[i];
            GUI.DrawTexture(new Rect(pixelPos.x - 1, pixelPos.y - 1, 2, 2), EditorGUIUtility.whiteTexture);
        }
        
        // Draw main line
        float distance = Vector2.Distance(start, end);
        float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, distance, width), EditorGUIUtility.whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, start);
        
        GUI.color = Color.white;
    }
    
    private void DrawStepNumber(Vector2 position, int stepNumber, Color backgroundColor)
    {
        // Create background circle
        float circleSize = 20f;
        Rect circleRect = new Rect(position.x - circleSize * 0.5f, position.y - circleSize * 0.5f, circleSize, circleSize);
        
        GUI.color = backgroundColor;
        GUI.DrawTexture(circleRect, MakeCircleTexture());
        
        // Draw number text
        GUI.color = Color.white;
        GUIStyle numberStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
        GUI.Label(circleRect, stepNumber.ToString(), numberStyle);
        GUI.color = Color.white;
    }
    
    private void DrawArrow(Vector2 start, Vector2 end, Color color)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 arrowPos = Vector2.Lerp(start, end, 0.7f); // Position arrow 70% along the line
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float arrowSize = 12f;
        
        GUI.color = color;
        Rect arrowRect = new Rect(arrowPos.x - arrowSize * 0.5f, arrowPos.y - arrowSize * 0.5f, arrowSize, arrowSize);
        
        GUIUtility.RotateAroundPivot(angle, arrowPos);
        GUI.DrawTexture(arrowRect, MakeArrowTexture());
        GUIUtility.RotateAroundPivot(-angle, arrowPos);
        
        GUI.color = Color.white;
    }
    
    private void DrawMarker(Vector2 position, string text, Color color)
    {
        float markerSize = 30f;
        Rect markerRect = new Rect(position.x - markerSize * 0.5f, position.y - markerSize * 0.5f, markerSize, markerSize);
        
        GUI.color = color;
        GUI.DrawTexture(markerRect, MakeCircleTexture());
        
        GUI.color = Color.white;
        GUIStyle markerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 8
        };
        GUI.Label(markerRect, text, markerStyle);
        GUI.color = Color.white;
    }
    
    private Texture2D MakeCircleTexture()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.4f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    private Texture2D MakeArrowTexture()
    {
        int size = 16;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        // Create simple arrow shape
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Arrow pointing right
                bool isArrow = false;
                
                // Arrow head
                if (x >= size * 0.6f)
                {
                    float centerY = size * 0.5f;
                    float distFromCenter = Mathf.Abs(y - centerY);
                    float maxDist = (size - x) * 0.8f;
                    isArrow = distFromCenter <= maxDist;
                }
                // Arrow shaft
                else if (y >= size * 0.4f && y <= size * 0.6f)
                {
                    isArrow = true;
                }
                
                pixels[y * size + x] = isArrow ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private bool ValidateSolution(List<TurnInfo> solution, Vector2Int expectedEndPos)
    {
        if (solution == null || solution.Count == 0)
        {
            Debug.LogError("Solution validation failed: Solution is null or empty");
            return false;
        }
            
        // Check that the final step reaches the expected end position with exactly 3 stars
        TurnInfo finalStep = solution[solution.Count - 1];
        bool reachesEnd = finalStep.Position == expectedEndPos;
        bool hasExactly3Stars = finalStep.Stars == 3;
        
        Debug.Log($"Validating solution: Final step at {finalStep.Position} with {finalStep.Stars} stars, Expected end: {expectedEndPos}");
        
        // Validate that exactly 3 unique star positions were collected
        HashSet<Vector2Int> collectedStars = finalStep.CollectedStarPositions ?? new HashSet<Vector2Int>();
        Debug.Log($"Unique star positions collected: {collectedStars.Count}, Stars: {string.Join(", ", collectedStars)}");
        
        // Also validate the entire path for debugging
        int starsCollected = 0;
        for (int i = 0; i < solution.Count; i++)
        {
            TurnInfo step = solution[i];
            Debug.Log($"Step {i + 1}: Position {step.Position}, State {step.State}, Stars {step.Stars}");
            starsCollected = step.Stars; // Track final star count
        }
        
        if (!reachesEnd)
        {
            Debug.LogError($"Solution validation failed: Final position {finalStep.Position} does not match expected end position {expectedEndPos}");
        }
        
        if (!hasExactly3Stars)
        {
            Debug.LogError($"Solution validation failed: Final star count {finalStep.Stars} is not exactly 3");
        }
        
        return reachesEnd && hasExactly3Stars;
    }
}