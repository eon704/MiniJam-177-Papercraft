using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public class LevelEditorWindow : EditorWindow
{
    [SerializeField] private LevelData currentLevel;
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
        Analysis,
        VolcanoSequence,
        IceSequence
    }

    private EditorMode currentMode = EditorMode.Tiles;

    // Sequence editing
    private Vector2Int? selectedVolcanoPos = null;
    private Vector2Int? selectedIceSourcePos = null;

    private Dictionary<TerrainType, string> tileTexturePaths = new() {
        { TerrainType.Empty, "Assets/Art/Sprites/Cell/NewCell/Border.png" },
        { TerrainType.Default, "Assets/Art/Sprites/Cell/NewCell/Default Layer 2.png" },
        { TerrainType.Start, "Assets/Art/Sprites/Cell/NewCell/Start.png" },
        { TerrainType.End, "Assets/Art/Sprites/Cell/NewCell/Finish cell/StaticEnd.png" },
        { TerrainType.Water, "Assets/Art/Sprites/Obstacles/water/0.gif" },
        { TerrainType.Stone, "Assets/Art/Sprites/Cell/NewCell/Rock.png" },
        { TerrainType.Fire, "Assets/Art/Sprites/Obstacles/fire/1.jpeg" }
        // Volcano, Lava, Ice: drawn with solid color fallback
    };

    private Dictionary<CellItem, string> itemTexturePaths = new() {
        { CellItem.Star, "Assets/Art/Sprites/Stars/Group 988.png" },
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
        { 'y', TerrainType.End },

        { 'V', TerrainType.Volcano },
        { 'I', TerrainType.Ice }
    };

    private List<TerrainType> toolTerrainTypes = new() {
        TerrainType.Empty,
        TerrainType.Default,
        TerrainType.Start,
        TerrainType.End,
        TerrainType.Water,
        TerrainType.Stone,
        TerrainType.Fire,
        TerrainType.Volcano,
        TerrainType.Ice
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
    private bool? lastSolvabilityResult = null;
    private bool? lastTightSolveResult = null; // All finite form moves fully exhausted?
    private List<(Player.StateType state, int remaining)> wastedMoves = null; // Forms with leftover moves in the regular solution
    private bool isCheckingSolvability = false;
    private List<TurnInfo> currentSolutionPath = null;
    private bool showSolutionPath = false;

    // Fallback colors for terrain types that have no texture
    private readonly Dictionary<TerrainType, Color> terrainFallbackColors = new()
    {
        { TerrainType.Volcano, new Color(0.4f, 0.1f, 0.1f) },
        { TerrainType.Lava,    new Color(1f,   0.3f, 0f) },
        { TerrainType.Ice,     new Color(0.6f, 0.85f, 1f) }
    };

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

        // Restore working copy after recompile (currentLevel survives via [SerializeField], workingLevel doesn't)
        if (currentLevel != null && workingLevel == null)
        {
            workingLevel = new WorkingLevelData(currentLevel);
            if (workingLevel.CachedSolution != null && workingLevel.CachedSolution.Count > 0)
                LoadCachedSolution();
        }

        // AssetDatabase may not be ready during domain reload — reload textures after it settles
        EditorApplication.delayCall += () => { LoadTileTextures(); Repaint(); };
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

        // Map Size
        if (workingLevel != null)
            DrawMapSizeEditor();

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
                DrawSpecialCellsConfig();
                break;
            case EditorMode.Moves:
                DrawMovesTool();
                break;
            case EditorMode.Analysis:
                DrawAnalysisTool();
                break;
            case EditorMode.VolcanoSequence:
                DrawVolcanoSequenceTool();
                break;
            case EditorMode.IceSequence:
                DrawIceSequenceTool();
                break;
        }

        EditorGUILayout.EndVertical(); // End padding container
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawMapSizeEditor()
    {
        EditorGUILayout.LabelField("Map Size", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        int newW = Mathf.Max(1, EditorGUILayout.IntField("Width",  workingLevel.MapSize.x));
        int newH = Mathf.Max(1, EditorGUILayout.IntField("Height", workingLevel.MapSize.y));

        bool sizeChanged = newW != workingLevel.MapSize.x || newH != workingLevel.MapSize.y;

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = sizeChanged;
        if (GUILayout.Button("Apply", GUILayout.Height(22)))
            ResizeMap(newW, newH);
        GUI.enabled = true;

        if (GUILayout.Button("+ Col",  GUILayout.Height(22))) ResizeMap(workingLevel.MapSize.x + 1, workingLevel.MapSize.y);
        if (GUILayout.Button("+ Row",  GUILayout.Height(22))) ResizeMap(workingLevel.MapSize.x,     workingLevel.MapSize.y + 1);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = workingLevel.MapSize.x > 1;
        if (GUILayout.Button("- Col",  GUILayout.Height(22))) ResizeMap(workingLevel.MapSize.x - 1, workingLevel.MapSize.y);
        GUI.enabled = workingLevel.MapSize.y > 1;
        if (GUILayout.Button("- Row",  GUILayout.Height(22))) ResizeMap(workingLevel.MapSize.x,     workingLevel.MapSize.y - 1);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void ResizeMap(int newW, int newH)
    {
        if (workingLevel == null) return;
        newW = Mathf.Max(1, newW);
        newH = Mathf.Max(1, newH);

        int oldW = workingLevel.MapSize.x;
        int oldH = workingLevel.MapSize.y;
        var oldMap = workingLevel.Map ?? new CellData[0];

        var newMap = new CellData[newW * newH];
        for (int i = 0; i < newMap.Length; i++)
            newMap[i] = new CellData(TerrainType.Empty, CellItem.None);

        // Copy overlapping region
        for (int y = 0; y < Mathf.Min(oldH, newH); y++)
        for (int x = 0; x < Mathf.Min(oldW, newW); x++)
        {
            int oldIdx = y * oldW + x;
            int newIdx = y * newW + x;
            if (oldIdx < oldMap.Length)
                newMap[newIdx] = new CellData(oldMap[oldIdx].Terrain, oldMap[oldIdx].Item, oldMap[oldIdx].IsFragile);
        }

        workingLevel.MapSize = new Vector2Int(newW, newH);
        workingLevel.Map     = newMap;

        // Clamp volcano/ice source positions that fell outside
        workingLevel.VolcanoConfigs?.RemoveAll(v =>
            v.Position.x >= newW || v.Position.y >= newH);
        workingLevel.IceSourceConfigs?.RemoveAll(v =>
            v.Position.x >= newW || v.Position.y >= newH);

        hasUnsavedChanges = true;
        ResetSolvabilityCheck();
        Repaint();
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
        if (GUILayout.Toggle(currentMode == EditorMode.VolcanoSequence, "Volcano", EditorStyles.toolbarButton, GUILayout.MinWidth(70)))
        {
            currentMode = EditorMode.VolcanoSequence;
        }
        if (GUILayout.Toggle(currentMode == EditorMode.IceSequence, "Ice", EditorStyles.toolbarButton, GUILayout.MinWidth(50)))
        {
            currentMode = EditorMode.IceSequence;
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

        EditorGUILayout.Space();

        // Board Operations
        EditorGUILayout.LabelField("Board Operations", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (GUILayout.Button("Flip Y Axis", GUILayout.MaxWidth(280)))
        {
            FlipBoardYAxis();
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

                if (lastSolvabilityResult.Value && lastTightSolveResult.HasValue)
                {
                    if (lastTightSolveResult.Value)
                    {
                        EditorGUILayout.HelpBox("✓ Tight: a solution exists that uses ALL move counts to zero. Level is optimally balanced.", MessageType.Info);
                    }
                    else
                    {
                        // Build a description of which forms still had moves left
                        string wastedDesc = wastedMoves != null && wastedMoves.Count > 0
                            ? string.Join(", ", wastedMoves.Select(w => $"{w.state} (+{w.remaining})"))
                            : "unknown";
                        EditorGUILayout.HelpBox(
                            $"⚠ Not tight: no solution uses ALL moves. Forms with leftover moves in the shortest solution: {wastedDesc}\n" +
                            "Consider reducing those move counts so the player must use every last move.",
                            MessageType.Warning);
                    }
                }
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

    private void DrawSpecialCellsConfig()
    {
        if (workingLevel == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Special Biome Cells", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Volcano configs
        EditorGUILayout.LabelField("Volcanoes", EditorStyles.boldLabel);
        if (workingLevel.VolcanoConfigs.Count == 0)
        {
            EditorGUILayout.HelpBox("No volcano cells on the map.", MessageType.Info);
        }
        else
        {
            foreach (var cfg in workingLevel.VolcanoConfigs)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Volcano ({cfg.Position.x}, {cfg.Position.y})", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Period:", GUILayout.Width(50));
                int newPeriod = EditorGUILayout.IntField(cfg.Period, GUILayout.Width(50));
                if (newPeriod != cfg.Period) { cfg.Period = Mathf.Max(1, newPeriod); hasUnsavedChanges = true; }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Lava sequence: {cfg.LavaSequence?.Count ?? 0} cells", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Edit", GUILayout.MaxWidth(80))) { currentMode = EditorMode.VolcanoSequence; selectedVolcanoPos = cfg.Position; }
                if (GUILayout.Button("Clear", GUILayout.MaxWidth(80))) { cfg.LavaSequence?.Clear(); hasUnsavedChanges = true; Repaint(); }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.Space();

        // Ice source configs
        EditorGUILayout.LabelField("Ice Sources", EditorStyles.boldLabel);
        if (workingLevel.IceSourceConfigs.Count == 0)
        {
            EditorGUILayout.HelpBox("No ice source cells on the map. Paint an Ice tile to configure it.", MessageType.Info);
        }
        else
        {
            foreach (var cfg in workingLevel.IceSourceConfigs)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Ice Source ({cfg.Position.x}, {cfg.Position.y})", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Period:", GUILayout.Width(50));
                int newPeriod = EditorGUILayout.IntField(cfg.Period, GUILayout.Width(50));
                if (newPeriod != cfg.Period) { cfg.Period = Mathf.Max(1, newPeriod); hasUnsavedChanges = true; }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Freeze sequence: {cfg.FreezeSequence?.Count ?? 0} cells", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Edit", GUILayout.MaxWidth(80))) { currentMode = EditorMode.IceSequence; selectedIceSourcePos = cfg.Position; }
                if (GUILayout.Button("Clear", GUILayout.MaxWidth(80))) { cfg.FreezeSequence?.Clear(); hasUnsavedChanges = true; Repaint(); }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawVolcanoSequenceTool()
    {
        EditorGUILayout.LabelField("Volcano Sequence Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (workingLevel == null)
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
            return;
        }

        if (workingLevel.VolcanoConfigs.Count == 0)
        {
            EditorGUILayout.HelpBox("No volcano cells on the map. Switch to Tiles mode and paint a Volcano cell.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "1. Click a Volcano cell in the preview to select it.\n" +
            "2. Then click other cells to add them to the lava sequence.\n" +
            "Right-click a sequence cell to remove the last entry.",
            MessageType.Info);

        EditorGUILayout.Space();

        if (selectedVolcanoPos.HasValue)
        {
            var cfg = workingLevel.VolcanoConfigs.Find(c => c.Position == selectedVolcanoPos.Value);
            if (cfg != null)
            {
                EditorGUILayout.LabelField($"Selected: Volcano at ({cfg.Position.x}, {cfg.Position.y})", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Period (moves):", GUILayout.Width(110));
                int newPeriod = EditorGUILayout.IntField(cfg.Period, GUILayout.Width(50));
                if (newPeriod != cfg.Period)
                {
                    cfg.Period = Mathf.Max(1, newPeriod);
                    hasUnsavedChanges = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Lava Sequence:", EditorStyles.boldLabel);

                if (cfg.LavaSequence == null || cfg.LavaSequence.Count == 0)
                {
                    EditorGUILayout.LabelField("  (empty — click cells in the preview to add)", EditorStyles.miniLabel);
                }
                else
                {
                    for (int i = 0; i < cfg.LavaSequence.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  {i + 1}. ({cfg.LavaSequence[i].x}, {cfg.LavaSequence[i].y})", GUILayout.Width(130));
                        if (GUILayout.Button("✕", GUILayout.Width(24)))
                        {
                            cfg.LavaSequence.RemoveAt(i);
                            hasUnsavedChanges = true;
                            Repaint();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("Clear Sequence", GUILayout.MaxWidth(150)))
                {
                    cfg.LavaSequence?.Clear();
                    hasUnsavedChanges = true;
                    Repaint();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Selected volcano no longer exists. Click a Volcano cell to re-select.", MessageType.Warning);
                selectedVolcanoPos = null;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Click a Volcano cell in the preview to select it.", MessageType.Info);
        }
    }

    private void DrawIceSequenceTool()
    {
        EditorGUILayout.LabelField("Ice Source Sequence Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (workingLevel == null)
        {
            EditorGUILayout.HelpBox("No level selected.", MessageType.Info);
            return;
        }

        if (workingLevel.IceSourceConfigs.Count == 0)
        {
            EditorGUILayout.HelpBox("No ice source cells on the map. Switch to Tiles mode and paint an Ice cell.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "1. Click an Ice source cell in the preview to select it.\n" +
            "2. Then click other cells to define the freeze sequence.",
            MessageType.Info);

        EditorGUILayout.Space();

        if (selectedIceSourcePos.HasValue)
        {
            var cfg = workingLevel.IceSourceConfigs.Find(c => c.Position == selectedIceSourcePos.Value);
            if (cfg != null)
            {
                EditorGUILayout.LabelField($"Selected: Ice Source at ({cfg.Position.x}, {cfg.Position.y})", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Period (moves):", GUILayout.Width(110));
                int newPeriod = EditorGUILayout.IntField(cfg.Period, GUILayout.Width(50));
                if (newPeriod != cfg.Period)
                {
                    cfg.Period = Mathf.Max(1, newPeriod);
                    hasUnsavedChanges = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Freeze Sequence:", EditorStyles.boldLabel);

                if (cfg.FreezeSequence == null || cfg.FreezeSequence.Count == 0)
                {
                    EditorGUILayout.LabelField("  (empty — click cells in the preview to add)", EditorStyles.miniLabel);
                }
                else
                {
                    for (int i = 0; i < cfg.FreezeSequence.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  {i + 1}. ({cfg.FreezeSequence[i].x}, {cfg.FreezeSequence[i].y})", GUILayout.Width(130));
                        if (GUILayout.Button("✕", GUILayout.Width(24)))
                        {
                            cfg.FreezeSequence.RemoveAt(i);
                            hasUnsavedChanges = true;
                            Repaint();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("Clear Sequence", GUILayout.MaxWidth(150)))
                {
                    cfg.FreezeSequence?.Clear();
                    hasUnsavedChanges = true;
                    Repaint();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Selected ice source no longer exists.", MessageType.Warning);
                selectedIceSourcePos = null;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Click an Ice source cell in the preview to select it.", MessageType.Info);
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
                // Remove special cell configs that are now out of bounds
                workingLevel.VolcanoConfigs.RemoveAll(cfg =>
                    cfg.Position.x < 0 || cfg.Position.x >= workingLevel.MapSize.x ||
                    cfg.Position.y < 0 || cfg.Position.y >= workingLevel.MapSize.y);
                workingLevel.IceSourceConfigs.RemoveAll(cfg =>
                    cfg.Position.x < 0 || cfg.Position.x >= workingLevel.MapSize.x ||
                    cfg.Position.y < 0 || cfg.Position.y >= workingLevel.MapSize.y);
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
                    // Flip Y coordinate to match Unity's coordinate system (Y=0 at bottom)
                    float posX = x * (tileSize + tilePadding);
                    float posY = (workingLevel.MapSize.y - 1 - y) * (tileSize + tilePadding);
                    Rect tileRect = new Rect(posX, posY, tileSize, tileSize);

                    // Handle click on tile
                    Event e = Event.current;

                    // Right-click: toggle fragile (only in Tiles mode)
                    if (currentMode == EditorMode.Tiles && e.type == EventType.MouseDown && e.button == 1 && tileRect.Contains(e.mousePosition))
                    {
                        if (cellData.Terrain != TerrainType.Empty)
                        {
                            cellData.IsFragile = !cellData.IsFragile;
                            workingLevel.Map[index] = cellData;
                            hasUnsavedChanges = true;
                            ResetSolvabilityCheck();
                            e.Use();
                        }
                    }

                    if (currentMode == EditorMode.VolcanoSequence && e.type == EventType.MouseDown && e.button == 0 && tileRect.Contains(e.mousePosition))
                    {
                        var clickedPos = new Vector2Int(x, y);
                        if (cellData.Terrain == TerrainType.Volcano)
                        {
                            selectedVolcanoPos = clickedPos;
                            if (workingLevel.VolcanoConfigs.Find(c => c.Position == clickedPos) == null)
                                workingLevel.VolcanoConfigs.Add(new VolcanoConfig { Position = clickedPos, Period = 2 });
                            e.Use();
                            Repaint();
                        }
                        else if (selectedVolcanoPos.HasValue && cellData.Terrain != TerrainType.Empty)
                        {
                            var cfg = workingLevel.VolcanoConfigs.Find(c => c.Position == selectedVolcanoPos.Value);
                            if (cfg != null)
                            {
                                if (cfg.LavaSequence == null) cfg.LavaSequence = new System.Collections.Generic.List<Vector2Int>();
                                if (!cfg.LavaSequence.Contains(clickedPos))
                                {
                                    cfg.LavaSequence.Add(clickedPos);
                                    hasUnsavedChanges = true;
                                    e.Use();
                                    Repaint();
                                }
                            }
                        }
                    }

                    if (currentMode == EditorMode.IceSequence && e.type == EventType.MouseDown && e.button == 0 && tileRect.Contains(e.mousePosition))
                    {
                        var clickedPos = new Vector2Int(x, y);
                        if (cellData.Terrain == TerrainType.Ice)
                        {
                            selectedIceSourcePos = clickedPos;
                            if (workingLevel.IceSourceConfigs.Find(c => c.Position == clickedPos) == null)
                                workingLevel.IceSourceConfigs.Add(new IceSourceConfig { Position = clickedPos, Period = 3 });
                            e.Use();
                            Repaint();
                        }
                        else if (selectedIceSourcePos.HasValue && cellData.Terrain != TerrainType.Empty)
                        {
                            var cfg = workingLevel.IceSourceConfigs.Find(c => c.Position == selectedIceSourcePos.Value);
                            if (cfg != null)
                            {
                                if (cfg.FreezeSequence == null) cfg.FreezeSequence = new System.Collections.Generic.List<Vector2Int>();
                                if (!cfg.FreezeSequence.Contains(clickedPos))
                                {
                                    cfg.FreezeSequence.Add(clickedPos);
                                    hasUnsavedChanges = true;
                                    e.Use();
                                    Repaint();
                                }
                            }
                        }
                    }

                    if (currentMode == EditorMode.Tiles && e.type == EventType.MouseDown && e.button == 0 && tileRect.Contains(e.mousePosition))
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
                            var newPos = new Vector2Int(x, y);
                            if (cellData.Terrain != selectedTerrainType)
                            {
                                // When painting Volcano, auto-create a config entry
                                if (selectedTerrainType == TerrainType.Volcano &&
                                    workingLevel.VolcanoConfigs.Find(c => c.Position == newPos) == null)
                                {
                                    workingLevel.VolcanoConfigs.Add(new VolcanoConfig { Position = newPos, Period = 2 });
                                }
                                // When erasing a Volcano, remove its config
                                if (cellData.Terrain == TerrainType.Volcano)
                                {
                                    workingLevel.VolcanoConfigs.RemoveAll(c => c.Position == newPos);
                                }
                                // When painting Ice, auto-create an ice source config
                                if (selectedTerrainType == TerrainType.Ice &&
                                    workingLevel.IceSourceConfigs.Find(c => c.Position == newPos) == null)
                                {
                                    workingLevel.IceSourceConfigs.Add(new IceSourceConfig { Position = newPos, Period = 3 });
                                }
                                // When erasing an Ice source, remove its config
                                if (cellData.Terrain == TerrainType.Ice)
                                {
                                    workingLevel.IceSourceConfigs.RemoveAll(c => c.Position == newPos);
                                }
                                cellData.Terrain = selectedTerrainType;
                                workingLevel.Map[index] = cellData;
                                hasUnsavedChanges = true;
                                ResetSolvabilityCheck(); // Reset when level changes
                                e.Use();
                            }
                        }
                    }

                    // Draw cell texture (or solid color fallback)
                    if (!cellTextures.TryGetValue(cellData.Terrain, out Texture2D cellTexture) || cellTexture == null)
                    {
                        if (terrainFallbackColors.TryGetValue(cellData.Terrain, out Color fallback))
                        {
                            Color prev = GUI.color;
                            GUI.color = fallback;
                            GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
                            GUI.color = prev;
                            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = Color.white },
                                fontStyle = FontStyle.Bold
                            };
                            GUI.Label(tileRect, cellData.Terrain.ToString(), labelStyle);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        GUI.DrawTexture(tileRect, cellTexture);
                    }

                    // Fragile overlay: orange tint + "~" label
                    if (cellData.IsFragile)
                    {
                        Color prevColor = GUI.color;
                        GUI.color = new Color(1f, 0.55f, 0f, 0.45f);
                        GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
                        GUI.color = prevColor;
                        GUIStyle fragileLabel = new GUIStyle(EditorStyles.boldLabel)
                        {
                            fontSize = 18,
                            alignment = TextAnchor.UpperRight,
                            normal = { textColor = Color.white }
                        };
                        GUI.Label(tileRect, "~", fragileLabel);
                    }

                    // Draw item texture if present
                    if (cellData.Item != CellItem.None)
                    {
                        if (itemTextures.TryGetValue(cellData.Item, out Texture2D itemTexture) && itemTexture != null)
                        {
                            // Calculate star rect to be half the size and centered
                            float starSize = tileSize * 0.5f;
                            float starX = posX + (tileSize - starSize) * 0.5f;
                            float starY = posY + (tileSize - starSize) * 0.5f;
                            Rect starRect = new Rect(starX, starY, starSize, starSize);
                            GUI.DrawTexture(starRect, itemTexture);
                        }
                    }

                    // VolcanoSequence mode overlays
                    if (currentMode == EditorMode.VolcanoSequence)
                    {
                        var cellXY = new Vector2Int(x, y);

                        if (selectedVolcanoPos.HasValue && cellXY == selectedVolcanoPos.Value)
                        {
                            Color prev = GUI.color;
                            GUI.color = new Color(1f, 1f, 0f, 0.6f);
                            GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
                            GUI.color = prev;
                        }

                        if (selectedVolcanoPos.HasValue)
                        {
                            var cfg = workingLevel.VolcanoConfigs.Find(c => c.Position == selectedVolcanoPos.Value);
                            if (cfg?.LavaSequence != null)
                            {
                                int seqIdx = cfg.LavaSequence.IndexOf(cellXY);
                                if (seqIdx >= 0)
                                {
                                    Color prev = GUI.color;
                                    GUI.color = new Color(1f, 0.3f, 0f, 0.45f);
                                    GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
                                    GUI.color = prev;
                                    GUIStyle numStyle = new GUIStyle(EditorStyles.boldLabel)
                                    {
                                        fontSize = 20,
                                        alignment = TextAnchor.MiddleCenter,
                                        normal = { textColor = Color.white }
                                    };
                                    GUI.Label(tileRect, (seqIdx + 1).ToString(), numStyle);
                                }
                            }
                        }
                    }

                    // IceSequence mode overlays
                    if (currentMode == EditorMode.IceSequence)
                    {
                        var cellXY = new Vector2Int(x, y);

                        if (selectedIceSourcePos.HasValue && cellXY == selectedIceSourcePos.Value)
                        {
                            Color prev = GUI.color;
                            GUI.color = new Color(0.5f, 0.9f, 1f, 0.6f);
                            GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
                            GUI.color = prev;
                        }

                        if (selectedIceSourcePos.HasValue)
                        {
                            var cfg = workingLevel.IceSourceConfigs.Find(c => c.Position == selectedIceSourcePos.Value);
                            if (cfg?.FreezeSequence != null)
                            {
                                int seqIdx = cfg.FreezeSequence.IndexOf(cellXY);
                                if (seqIdx >= 0)
                                {
                                    Color prev = GUI.color;
                                    GUI.color = new Color(0.3f, 0.7f, 1f, 0.45f);
                                    GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
                                    GUI.color = prev;
                                    GUIStyle numStyle = new GUIStyle(EditorStyles.boldLabel)
                                    {
                                        fontSize = 20,
                                        alignment = TextAnchor.MiddleCenter,
                                        normal = { textColor = Color.white }
                                    };
                                    GUI.Label(tileRect, (seqIdx + 1).ToString(), numStyle);
                                }
                            }
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

    private void FlipBoardYAxis()
    {
        if (workingLevel == null || workingLevel.Map == null)
        {
            Debug.LogWarning("Cannot flip board: No level data available.");
            return;
        }

        int width = workingLevel.MapSize.x;
        int height = workingLevel.MapSize.y;
        CellData[] flippedMap = new CellData[workingLevel.Map.Length];

        // Flip the map along the Y axis
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int originalIndex = y * width + x;
                int flippedIndex = (height - 1 - y) * width + x;
                flippedMap[flippedIndex] = workingLevel.Map[originalIndex];
            }
        }

        // Apply the flipped map
        workingLevel.Map = flippedMap;
        hasUnsavedChanges = true;
        ResetSolvabilityCheck(); // Reset when level changes
        
        Debug.Log("Board flipped along Y axis.");
        Repaint();
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

    public static List<TurnInfo> SolveLevel(LevelData level, bool requireAllMovesExhausted = false, bool requireLooseSolution = false)
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
            CollectedStarPositions = new HashSet<Vector2Int>(),
            CollapsedCells = new HashSet<Vector2Int>()
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

            bool _exhausted = AllMovesExhausted(current.MovesPerForm, level.StartMovesPerForm);
            bool _goalMet = requireAllMovesExhausted ? _exhausted
                          : requireLooseSolution     ? !_exhausted
                          : true;

            if (current.Position == endPos && current.Stars == 3 && _goalMet)
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

                Debug.Log($"Found solution with {solution.Count} steps, Position={current.Position}, Stars={current.Stars}");

                if (ValidateSolution(solution, endPos))
                    return solution;

                Debug.LogError("Solution failed validation despite meeting BFS goal condition - this should not happen!");
                return null;
            }

            // Try all possible states from current position
            foreach (Player.StateType stateType in System.Enum.GetValues(typeof(Player.StateType)))
            {
                if (stateType == Player.StateType.Default) continue;

                // For non-default states, check if we have moves left
                if (current.MovesPerForm[stateType] <= 0) continue;

                // Get the state model for movement options
                if (!StateModelInfo.StateModels.TryGetValue(stateType, out StateModel stateModel)) continue;

                // Determine which cells are collapsed when leaving the current cell
                HashSet<Vector2Int> collapsedAfterMove = new(current.CollapsedCells ?? new HashSet<Vector2Int>());
                int curIdx = current.Position.y * level.MapSize.x + current.Position.x;
                if (level.Map[curIdx].IsFragile)
                    collapsedAfterMove.Add(current.Position);

                // Generate target positions based on MoveMode (Plane slides, Boat crosses water, etc.)
                foreach (Vector2Int newPos in GetMoveTargets(current.Position, stateModel, level, collapsedAfterMove))
                {
                    int cellIndex = newPos.y * level.MapSize.x + newPos.x;
                    CellData targetCell = level.Map[cellIndex];

                    // Fire is always fatal — never a valid destination
                    if (targetCell.Terrain == TerrainType.Fire) continue;

                    // Stars
                    int newStars = current.Stars;
                    HashSet<Vector2Int> newCollectedStars = new(current.CollectedStarPositions ?? new HashSet<Vector2Int>());
                    if (targetCell.Item == CellItem.Star && !newCollectedStars.Contains(newPos))
                    {
                        newStars++;
                        newCollectedStars.Add(newPos);
                    }

                    // Move count
                    Dictionary<Player.StateType, int> newMovesPerForm = new(current.MovesPerForm);
                    newMovesPerForm[stateType]--;

                    TurnInfo nextTurn = new TurnInfo
                    {
                        Position = newPos,
                        Stars = newStars,
                        MovesPerForm = newMovesPerForm,
                        State = stateType,
                        CollectedStarPositions = newCollectedStars,
                        CollapsedCells = collapsedAfterMove
                    };

                    string nextKey = GetStateKey(nextTurn);
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

    public struct TurnInfo
    {
        public Vector2Int Position;
        public int Stars;
        public Dictionary<Player.StateType, int> MovesPerForm;
        public Player.StateType State;
        public HashSet<Vector2Int> CollectedStarPositions;
        public HashSet<Vector2Int> CollapsedCells; // Fragile cells that have been stepped on and left
    }

    public static string GetStateKey(TurnInfo state)
    {
        string movesKey = string.Join(",", state.MovesPerForm.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        string starsKey = string.Join(";", (state.CollectedStarPositions ?? new HashSet<Vector2Int>()).OrderBy(p => p.x).ThenBy(p => p.y).Select(p => $"{p.x},{p.y}"));
        string collapsedKey = string.Join(";", (state.CollapsedCells ?? new HashSet<Vector2Int>()).OrderBy(p => p.x).ThenBy(p => p.y).Select(p => $"{p.x},{p.y}"));
        return $"{state.Position.x},{state.Position.y},{state.State},{state.Stars},{movesKey},{starsKey}|{collapsedKey}";
    }

    // Returns true if every form that started with finite (>0) moves has used them all.
    private static bool AllMovesExhausted(Dictionary<Player.StateType, int> movesPerForm, List<MovePerFormEntry> startMoves)
    {
        foreach (var entry in startMoves)
        {
            if (entry.State == Player.StateType.Default) continue;
            if (entry.Moves <= 0) continue; // 0 = never usable, -1 = unlimited
            if (movesPerForm.TryGetValue(entry.State, out int remaining) && remaining > 0)
                return false;
        }
        return true;
    }

    // Returns all valid landing positions for a given state from 'pos', respecting MoveMode mechanics.
    private static IEnumerable<Vector2Int> GetMoveTargets(
        Vector2Int pos, StateModel stateModel, LevelData level, HashSet<Vector2Int> collapsedCells)
    {
        bool InBounds(Vector2Int p) =>
            p.x >= 0 && p.x < level.MapSize.x && p.y >= 0 && p.y < level.MapSize.y;
        CellData Cell(Vector2Int p) => level.Map[p.y * level.MapSize.x + p.x];
        bool Collapsed(Vector2Int p) => collapsedCells != null && collapsedCells.Contains(p);

        switch (stateModel.MoveMode)
        {
            case MoveMode.Normal:
            case MoveMode.FrogJump:
                // Single step (Normal) or fixed 2-cell jump (FrogJump) — offsets already encode distance
                foreach (var offset in stateModel.MoveOptions)
                {
                    var target = pos + offset;
                    if (!InBounds(target)) continue;
                    var cell = Cell(target);
                    if (cell.Terrain == TerrainType.Empty) continue;
                    if (Collapsed(target)) continue;
                    if (!stateModel.MoveTerrain.Contains(cell.Terrain)) continue;
                    yield return target;
                }
                break;

            case MoveMode.PlaneSlide:
                // Slide diagonally until terrain blocks or out of bounds.
                // Fire blocks the slide (you can't fly past fire without dying).
                foreach (var dir in stateModel.MoveOptions)
                {
                    var cursor = pos + dir;
                    while (InBounds(cursor))
                    {
                        var cell = Cell(cursor);
                        if (cell.Terrain == TerrainType.Empty) break;
                        if (cell.Terrain == TerrainType.Fire) break;   // fire blocks slide
                        if (Collapsed(cursor)) break;
                        if (!stateModel.MoveTerrain.Contains(cell.Terrain)) break;
                        yield return cursor;
                        cursor += dir;
                    }
                }
                break;

            case MoveMode.BoatSlide:
                // Must start with a Water cell, slide through all water, land on the first non-water
                // cell that is in MoveTerrain (mirrors BoardPiece.GetBoatMoveOptions).
                foreach (var dir in stateModel.MoveOptions)
                {
                    var first = pos + dir;
                    if (!InBounds(first)) continue;
                    if (Cell(first).Terrain != TerrainType.Water) continue;

                    var cursor = first + dir;
                    while (InBounds(cursor))
                    {
                        var cell = Cell(cursor);
                        if (cell.Terrain == TerrainType.Water) { cursor += dir; continue; }
                        // First non-water cell is the landing spot
                        if (cell.Terrain != TerrainType.Empty && !Collapsed(cursor) &&
                            stateModel.MoveTerrain.Contains(cell.Terrain))
                            yield return cursor;
                        break;
                    }
                }
                break;
        }
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
        EditorApplication.update -= PerformSolvabilityCheck;
        isCheckingSolvability = false;
        lastSolvabilityResult = null;
        lastTightSolveResult = null;
        wastedMoves = null;
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
                LevelData levelData = workingLevel.ToLevelData();

                // 1. Regular solve — just find any valid solution
                var solution = SolveLevel(levelData);
                lastSolvabilityResult = solution != null;

                if (solution != null)
                {
                    currentSolutionPath = solution;
                    StoreSolutionInWorkingLevel(solution);
                    Debug.Log($"Solvability check: solvable in {solution.Count} steps. Solution cached.");

                    // 2. Loose solve — look for ANY winning path that leaves moves unused.
                    //    Tight = no such path exists. This correctly catches cases where
                    //    the player can win via a different, easier route.
                    var looseSolution = SolveLevel(levelData, requireLooseSolution: true);
                    lastTightSolveResult = looseSolution == null; // tight iff zero loose solutions

                    if (looseSolution != null)
                    {
                        // Show which forms still had moves left in the loose winning path
                        TurnInfo looseEnd = looseSolution[looseSolution.Count - 1];
                        wastedMoves = new List<(Player.StateType, int)>();
                        foreach (var entry in levelData.StartMovesPerForm)
                        {
                            if (entry.State == Player.StateType.Default) continue;
                            if (entry.Moves <= 0) continue;
                            if (looseEnd.MovesPerForm.TryGetValue(entry.State, out int rem) && rem > 0)
                                wastedMoves.Add((entry.State, rem));
                        }
                        Debug.LogWarning($"Not tight: player can win with leftover moves in {looseSolution.Count} steps. Wasted: " +
                            string.Join(", ", wastedMoves.Select(w => $"{w.state}(+{w.remaining})")));
                    }
                    else
                    {
                        wastedMoves = null;
                        Debug.Log("Tight: every winning path exhausts all finite move counts.");
                    }
                }
                else
                {
                    currentSolutionPath = null;
                    lastTightSolveResult = null;
                    wastedMoves = null;
                    workingLevel.CachedSolution = new List<SolutionStep>();
                    Debug.Log("Solvability check: level is NOT solvable.");
                }
            }
            else
            {
                lastSolvabilityResult = false;
                lastTightSolveResult = null;
                wastedMoves = null;
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
                destination.Map[i] = new CellData(source.Map[i].Terrain, source.Map[i].Item, source.Map[i].IsFragile);
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

        // Copy volcano configs
        destination.VolcanoConfigs = new List<VolcanoConfig>();
        if (source.VolcanoConfigs != null)
        {
            foreach (var cfg in source.VolcanoConfigs)
            {
                destination.VolcanoConfigs.Add(new VolcanoConfig
                {
                    Position = cfg.Position,
                    Period = cfg.Period,
                    LavaSequence = cfg.LavaSequence != null ? new List<Vector2Int>(cfg.LavaSequence) : new List<Vector2Int>()
                });
            }
        }

        destination.IceSourceConfigs = new List<IceSourceConfig>();
        if (source.IceSourceConfigs != null)
        {
            foreach (var cfg in source.IceSourceConfigs)
            {
                destination.IceSourceConfigs.Add(new IceSourceConfig
                {
                    Position = cfg.Position,
                    Period = cfg.Period,
                    FreezeSequence = cfg.FreezeSequence != null ? new List<Vector2Int>(cfg.FreezeSequence) : new List<Vector2Int>()
                });
            }
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
        public List<VolcanoConfig> VolcanoConfigs = new();
        public List<IceSourceConfig> IceSourceConfigs = new();

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
                    Map[i] = new CellData(original.Map[i].Terrain, original.Map[i].Item, original.Map[i].IsFragile);
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

            // Copy volcano configs
            VolcanoConfigs = new List<VolcanoConfig>();
            if (original.VolcanoConfigs != null)
            {
                foreach (var cfg in original.VolcanoConfigs)
                {
                    VolcanoConfigs.Add(new VolcanoConfig
                    {
                        Position = cfg.Position,
                        Period = cfg.Period,
                        LavaSequence = cfg.LavaSequence != null ? new List<Vector2Int>(cfg.LavaSequence) : new List<Vector2Int>()
                    });
                }
            }

            // Copy ice source configs
            IceSourceConfigs = new List<IceSourceConfig>();
            if (original.IceSourceConfigs != null)
            {
                foreach (var cfg in original.IceSourceConfigs)
                {
                    IceSourceConfigs.Add(new IceSourceConfig
                    {
                        Position = cfg.Position,
                        Period = cfg.Period,
                        FreezeSequence = cfg.FreezeSequence != null ? new List<Vector2Int>(cfg.FreezeSequence) : new List<Vector2Int>()
                    });
                }
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
                    levelData.Map[i] = new CellData(Map[i].Terrain, Map[i].Item, Map[i].IsFragile);
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

            // Copy volcano configs
            levelData.VolcanoConfigs = new List<VolcanoConfig>();
            if (VolcanoConfigs != null)
            {
                foreach (var cfg in VolcanoConfigs)
                {
                    levelData.VolcanoConfigs.Add(new VolcanoConfig
                    {
                        Position = cfg.Position,
                        Period = cfg.Period,
                        LavaSequence = cfg.LavaSequence != null ? new List<Vector2Int>(cfg.LavaSequence) : new List<Vector2Int>()
                    });
                }
            }

            levelData.IceSourceConfigs = new List<IceSourceConfig>();
            if (IceSourceConfigs != null)
            {
                foreach (var cfg in IceSourceConfigs)
                {
                    levelData.IceSourceConfigs.Add(new IceSourceConfig
                    {
                        Position = cfg.Position,
                        Period = cfg.Period,
                        FreezeSequence = cfg.FreezeSequence != null ? new List<Vector2Int>(cfg.FreezeSequence) : new List<Vector2Int>()
                    });
                }
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
        // Flip Y coordinate to match Unity's coordinate system (Y=0 at bottom)
        float posY = (workingLevel.MapSize.y - 1 - tileCoord.y) * (tileSize + tilePadding) + tileSize * 0.5f;
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

    public static bool ValidateSolution(List<TurnInfo> solution, Vector2Int expectedEndPos)
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

    // =========================================================================
    // Generator integration
    // =========================================================================

    /// <summary>
    /// Load a generated LevelData into the editor as an unsaved working copy.
    /// Called from LevelGeneratorWindow when the user clicks "Open in Editor".
    /// </summary>
    public void LoadGeneratedLevel(LevelData level)
    {
        if (level == null) return;
        currentLevel  = level;
        workingLevel  = new WorkingLevelData(level);
        hasUnsavedChanges = true;
        currentSolutionPath = null;
        showSolutionPath    = false;
        lastSolvabilityResult = null;
        lastTightSolveResult  = null;
        wastedMoves           = null;
        UpdateWindowTitle();
        Repaint();
    }
}