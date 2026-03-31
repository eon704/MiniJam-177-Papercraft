using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window UI for the procedural level generator.
/// Open via Tools → Level Generator.
/// </summary>
public class LevelGeneratorWindow : EditorWindow
{
    // =========================================================================
    // State
    // =========================================================================

    private LevelGenerator.GenerationParams genParams = new();
    private List<GenerationCard> cards = new();
    private int selectedCard = -1;

    private Vector2 leftScroll;
    private Vector2 galleryScroll;

    private bool isGenerating;
    private float leftPanelWidth = 310f;
    private bool isDraggingSplitter;
    private const float SplitterWidth = 5f;

    // Preview thumbnail size
    private const int ThumbW = 120;
    private const int ThumbH = 120;

    private class GenerationCard
    {
        public LevelGenerator.GenerationResult Result;
        public Texture2D                        Thumbnail;
        public string                           Label;
    }

    // Terrain colours used in mini-previews
    private static readonly Dictionary<TerrainType, Color> TerrainColors = new()
    {
        { TerrainType.Empty,   new Color(0.10f, 0.10f, 0.10f) },
        { TerrainType.Default, new Color(0.55f, 0.75f, 0.45f) },
        { TerrainType.Start,   new Color(0.30f, 0.75f, 0.95f) },
        { TerrainType.End,     new Color(1.00f, 0.85f, 0.20f) },
        { TerrainType.Water,   new Color(0.20f, 0.45f, 0.90f) },
        { TerrainType.Stone,   new Color(0.55f, 0.55f, 0.55f) },
        { TerrainType.Fire,    new Color(0.95f, 0.35f, 0.10f) },
        { TerrainType.Volcano, new Color(0.40f, 0.10f, 0.10f) },
        { TerrainType.Ice,     new Color(0.70f, 0.90f, 1.00f) },
        { TerrainType.Lava,    new Color(1.00f, 0.35f, 0.00f) },
    };

    private static readonly Dictionary<Player.StateType, Color> FormColors = new()
    {
        { Player.StateType.Crane, new Color(1f, 0.3f, 0.3f) },
        { Player.StateType.Frog,  new Color(0.3f, 0.9f, 0.3f) },
        { Player.StateType.Plane, new Color(1f,   0.9f, 0.2f) },
        { Player.StateType.Boat,  new Color(0.3f, 0.5f, 1.0f) },
    };

    // =========================================================================
    // Lifecycle
    // =========================================================================

    [MenuItem("Tools/Level Generator")]
    public static void ShowWindow() => GetWindow<LevelGeneratorWindow>("Level Generator");

    private void OnGUI()
    {
        DrawToolbar();

        float splitterX = leftPanelWidth;
        var area = new Rect(0, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight);

        // Left panel
        var leftRect = new Rect(area.x, area.y, splitterX, area.height);
        GUILayout.BeginArea(leftRect);
        leftScroll = GUILayout.BeginScrollView(leftScroll);
        DrawParams();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // Splitter
        var splitRect = new Rect(splitterX, area.y, SplitterWidth, area.height);
        EditorGUI.DrawRect(splitRect, new Color(0.15f, 0.15f, 0.15f));
        EditorGUIUtility.AddCursorRect(splitRect, MouseCursor.ResizeHorizontal);
        HandleSplitterDrag(splitRect);

        // Right panel — gallery
        var rightRect = new Rect(splitterX + SplitterWidth, area.y, area.width - splitterX - SplitterWidth, area.height);
        GUILayout.BeginArea(rightRect);
        DrawGallery(rightRect);
        GUILayout.EndArea();
    }

    // =========================================================================
    // Toolbar
    // =========================================================================

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.enabled = !isGenerating;
        if (GUILayout.Button("Generate", EditorStyles.toolbarButton, GUILayout.Width(80)))
            RunGeneration();
        GUI.enabled = true;

        if (isGenerating)
        {
            GUILayout.Label("Generating…", EditorStyles.miniLabel);
        }
        else if (cards.Count > 0)
        {
            GUILayout.Label($"{cards.Count} variants", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();

        GUI.enabled = selectedCard >= 0 && selectedCard < cards.Count;

        if (GUILayout.Button("Open in Editor", EditorStyles.toolbarButton, GUILayout.Width(110)))
            OpenSelectedInEditor();

        if (GUILayout.Button("Save Asset…", EditorStyles.toolbarButton, GUILayout.Width(90)))
            SaveSelectedAsAsset();

        GUI.enabled = true;

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            ClearCards();
            selectedCard = -1;
            Repaint();
        }

        GUILayout.EndHorizontal();
    }

    // =========================================================================
    // Left panel — parameters
    // =========================================================================

    private void DrawParams()
    {
        GUILayout.Space(4);

        // ── Grid Size ──────────────────────────────────────────────────────────
        GUILayout.Label("Размер поля", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Мин. ширина (колонки)",  GUILayout.Width(160));
        genParams.GridMinX = Mathf.Clamp(EditorGUILayout.IntField(genParams.GridMinX, GUILayout.Width(40)), 3, genParams.GridMaxX);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Мин. высота (строки)", GUILayout.Width(140));
        genParams.GridMinY = Mathf.Clamp(EditorGUILayout.IntField(genParams.GridMinY, GUILayout.Width(40)), 3, genParams.GridMaxY);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Макс. ширина (колонки)", GUILayout.Width(160));
        genParams.GridMaxX = Mathf.Clamp(EditorGUILayout.IntField(genParams.GridMaxX, GUILayout.Width(40)), genParams.GridMinX, 20);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Макс. высота (строки)",  GUILayout.Width(140));
        genParams.GridMaxY = Mathf.Clamp(EditorGUILayout.IntField(genParams.GridMaxY, GUILayout.Width(40)), genParams.GridMinY, 20);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        // ── Forms ──────────────────────────────────────────────────────────────
        GUILayout.Space(6);
        GUILayout.Label("Формы", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        foreach (var fc in genParams.Forms)
        {
            if (fc.State == Player.StateType.Default) continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header row: toggle + name
            EditorGUILayout.BeginHorizontal();
            fc.Enabled = EditorGUILayout.Toggle(fc.Enabled, GUILayout.Width(16));
            if (FormColors.TryGetValue(fc.State, out var col))
            {
                var old = GUI.color; GUI.color = col;
                GUILayout.Label(fc.State.ToString(), EditorStyles.boldLabel);
                GUI.color = old;
            }
            else
            {
                GUILayout.Label(fc.State.ToString(), EditorStyles.boldLabel);
            }
            EditorGUILayout.EndHorizontal();

            // Moves row (always visible but dimmed when disabled)
            GUI.enabled = fc.Enabled;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Мин. ходов", GUILayout.Width(90));
            fc.MovesMin = Mathf.Clamp(EditorGUILayout.IntField(fc.MovesMin, GUILayout.Width(40)), 1, fc.MovesMax);
            GUILayout.Space(12);
            GUILayout.Label("Макс. ходов", GUILayout.Width(90));
            fc.MovesMax = Mathf.Clamp(EditorGUILayout.IntField(fc.MovesMax, GUILayout.Width(40)), fc.MovesMin, 12);
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();

        // ── Path / Decoys ──────────────────────────────────────────────────────
        GUILayout.Space(6);
        GUILayout.Label("Паттерн пути", EditorStyles.boldLabel);
        genParams.Pattern = (LevelGenerator.PathPattern)EditorGUILayout.EnumPopup(genParams.Pattern);

        GUILayout.Space(4);
        GUILayout.Label("Обманные пути", EditorStyles.boldLabel);
        genParams.Decoys = (LevelGenerator.DecoyIntensity)EditorGUILayout.EnumPopup(genParams.Decoys);

        // ── Terrain ────────────────────────────────────────────────────────────
        GUILayout.Space(6);
        GUILayout.Label("Тайлы", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        genParams.UseWater   = GUILayout.Toggle(genParams.UseWater,   "Вода (нужна для Лодки)");
        genParams.UseStone   = GUILayout.Toggle(genParams.UseStone,   "Камень (препятствие)");
        EditorGUILayout.Space(2);
        genParams.UseFire    = GUILayout.Toggle(genParams.UseFire,    "Огонь 🔥 (ловушки в тупиках)");
        genParams.UseLava    = GUILayout.Toggle(genParams.UseLava,    "Вулкан 🌋 (лава расширяется со временем)");
        EditorGUILayout.Space(2);
        genParams.UseIce     = GUILayout.Toggle(genParams.UseIce,     "Лёд ❄️ (блокирует Лодку)");
        EditorGUILayout.Space(2);
        genParams.UseFragile = GUILayout.Toggle(genParams.UseFragile, "Хрупкие клетки (исчезают после)");
        EditorGUILayout.EndVertical();

        // ── Biome ──────────────────────────────────────────────────────────────
        GUILayout.Space(6);
        GUILayout.Label("Биом", EditorStyles.boldLabel);
        genParams.Biome = (BiomeType)EditorGUILayout.EnumPopup(genParams.Biome);

        // ── Batch ──────────────────────────────────────────────────────────────
        GUILayout.Space(6);
        GUILayout.Label("Генерация", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        genParams.VariantCount = EditorGUILayout.IntSlider("Вариантов",    genParams.VariantCount, 1, 20);
        genParams.MaxAttempts  = EditorGUILayout.IntSlider("Макс. попыток", genParams.MaxAttempts,  50, 1000);
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        GUI.enabled = !isGenerating;
        if (GUILayout.Button("Generate", GUILayout.Height(32)))
            RunGeneration();
        GUI.enabled = true;

        GUILayout.Space(4);
    }

    // =========================================================================
    // Right panel — gallery
    // =========================================================================

    private void DrawGallery(Rect area)
    {
        if (cards.Count == 0)
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(isGenerating ? "Generating…" : "No variants yet.\nClick Generate.", EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            return;
        }

        const float CardW  = 144f;
        const float CardH  = 190f;
        const float Margin = 8f;

        float innerW = area.width - 16f;
        int cols = Mathf.Max(1, (int)((innerW + Margin) / (CardW + Margin)));

        galleryScroll = GUILayout.BeginScrollView(galleryScroll);

        int row = 0, col = 0;
        GUILayout.BeginHorizontal();
        GUILayout.Space(Margin);

        foreach (var (card, idx) in cards.Select((c, i) => (c, i)))
        {
            if (col >= cols)
            {
                GUILayout.EndHorizontal();
                GUILayout.Space(Margin);
                GUILayout.BeginHorizontal();
                GUILayout.Space(Margin);
                col = 0;
                row++;
            }

            DrawCard(card, idx, CardW, CardH);
            GUILayout.Space(Margin);
            col++;
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(Margin);
        GUILayout.EndScrollView();

        // Detail strip at bottom if a card is selected
        if (selectedCard >= 0 && selectedCard < cards.Count)
            DrawDetailStrip(cards[selectedCard]);
    }

    private void DrawCard(GenerationCard card, int idx, float w, float h)
    {
        bool selected = idx == selectedCard;
        var cardStyle = new GUIStyle(GUI.skin.box);

        var cardRect = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));

        Color bg = selected
            ? new Color(0.25f, 0.45f, 0.70f)
            : new Color(0.22f, 0.22f, 0.22f);
        EditorGUI.DrawRect(cardRect, bg);

        if (card.Result.IsSolvable)
        {
            // Thumbnail
            var thumbRect = new Rect(cardRect.x + 4, cardRect.y + 4, w - 8, h - 48);
            if (card.Thumbnail != null)
                GUI.DrawTexture(thumbRect, card.Thumbnail, ScaleMode.ScaleToFit);

            // Labels
            float labelY = cardRect.y + h - 46;
            float lw = w - 8;

            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            string tight = card.Result.IsTight ? "TIGHT" : "";
            GUI.Label(new Rect(cardRect.x + 4, labelY, lw, 14), $"Score {card.Result.Score}  {tight}", labelStyle);
            GUI.Label(new Rect(cardRect.x + 4, labelY + 14, lw, 14), card.Label, labelStyle);
            GUI.Label(new Rect(cardRect.x + 4, labelY + 28, lw, 14), $"{card.Result.SolutionSteps} steps", labelStyle);
        }
        else
        {
            var errStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };
            GUI.Label(cardRect, card.Result.FailReason ?? "Failed", errStyle);
        }

        // Click detection
        if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
        {
            selectedCard = idx;
            GUI.changed  = true;
            Repaint();
        }
    }

    private void DrawDetailStrip(GenerationCard card)
    {
        if (!card.Result.IsSolvable) return;

        EditorGUI.DrawRect(new Rect(0, position.height - 40, position.width - leftPanelWidth - SplitterWidth, 40),
            new Color(0.18f, 0.18f, 0.18f));

        GUILayout.BeginHorizontal(GUILayout.Height(38));
        GUILayout.Space(8);

        string forms = string.Join(" + ", card.Result.FormsUsed.Select(f => f.ToString()));
        GUILayout.Label($"Forms: {forms}", GUILayout.Width(240));
        GUILayout.Label($"Map: {card.Result.Level?.MapSize.x}×{card.Result.Level?.MapSize.y}", GUILayout.Width(80));
        GUILayout.Label($"Tight: {(card.Result.IsTight ? "Yes" : "No")}", GUILayout.Width(70));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Open in Editor", GUILayout.Width(110), GUILayout.Height(24)))
            OpenSelectedInEditor();
        if (GUILayout.Button("Save Asset…", GUILayout.Width(90), GUILayout.Height(24)))
            SaveSelectedAsAsset();
        if (GUILayout.Button("Retry this", GUILayout.Width(80), GUILayout.Height(24)))
            RetrySelected();

        GUILayout.Space(8);
        GUILayout.EndHorizontal();
    }

    // =========================================================================
    // Actions
    // =========================================================================

    private void RunGeneration()
    {
        isGenerating = true;
        Repaint();

        try
        {
            ClearCards();
            var rng = new System.Random();
            var results = LevelGenerator.GenerateAll(genParams);
            foreach (var (r, i) in results.Select((r, i) => (r, i)))
            {
                var card = new GenerationCard
                {
                    Result    = r,
                    Thumbnail = r.IsSolvable ? BuildThumbnail(r.Level) : null,
                    Label     = r.IsSolvable
                        ? string.Join("+", r.FormsUsed.Select(f => f.ToString().Substring(0, 2)))
                        : "Failed",
                };
                cards.Add(card);
            }

            selectedCard = cards.FindIndex(c => c.Result.IsSolvable);
        }
        finally
        {
            isGenerating = false;
            Repaint();
        }
    }

    private void RetrySelected()
    {
        if (selectedCard < 0 || selectedCard >= cards.Count) return;

        isGenerating = true;
        Repaint();

        try
        {
            var rng = new System.Random();
            var r   = LevelGenerator.Generate(genParams, rng);
            cards[selectedCard] = new GenerationCard
            {
                Result    = r,
                Thumbnail = r.IsSolvable ? BuildThumbnail(r.Level) : null,
                Label     = r.IsSolvable
                    ? string.Join("+", r.FormsUsed.Select(f => f.ToString().Substring(0, 2)))
                    : "Failed",
            };
        }
        finally
        {
            isGenerating = false;
            Repaint();
        }
    }

    private void OpenSelectedInEditor()
    {
        if (selectedCard < 0 || selectedCard >= cards.Count) return;
        var card = cards[selectedCard];
        if (!card.Result.IsSolvable || card.Result.Level == null) return;

        var editorWindow = GetWindow<LevelEditorWindow>("Level Editor");
        editorWindow.LoadGeneratedLevel(card.Result.Level);
        editorWindow.Focus();
    }

    private void SaveSelectedAsAsset()
    {
        if (selectedCard < 0 || selectedCard >= cards.Count) return;
        var card = cards[selectedCard];
        if (!card.Result.IsSolvable || card.Result.Level == null) return;

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Level",
            $"GeneratedLevel_{DateTime.Now:yyyyMMdd_HHmmss}",
            "asset",
            "Choose where to save the generated level.",
            "Assets/Data/Levels");

        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(card.Result.Level, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = card.Result.Level;
    }

    private void ClearCards()
    {
        foreach (var c in cards)
        {
            if (c.Thumbnail != null)
                DestroyImmediate(c.Thumbnail);
        }
        cards.Clear();
    }

    // =========================================================================
    // Thumbnail builder
    // =========================================================================

    private static Texture2D BuildThumbnail(LevelData level)
    {
        if (level == null) return null;

        int w = level.MapSize.x;
        int h = level.MapSize.y;
        if (w <= 0 || h <= 0) return null;

        // Compute cell pixel size to fill ThumbW×ThumbH
        int cellPx = Mathf.Max(1, Mathf.Min(ThumbW / w, ThumbH / h));
        int texW   = w * cellPx;
        int texH   = h * cellPx;

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        var pixels = new Color[texW * texH];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var cell  = level.Map[y * w + x];
            Color col = TerrainColors.TryGetValue(cell.Terrain, out var c) ? c : Color.magenta;

            // Star dot: brighter tint
            if (cell.Item == CellItem.Star)
                col = Color.Lerp(col, Color.white, 0.55f);

            // Fill the cellPx×cellPx block (y is flipped: Unity tex origin is bottom-left)
            for (int py = 0; py < cellPx; py++)
            for (int px = 0; px < cellPx; px++)
            {
                int texX = x * cellPx + px;
                int texY = (h - 1 - y) * cellPx + py;
                pixels[texY * texW + texX] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false);
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    // =========================================================================
    // Splitter drag
    // =========================================================================

    private void HandleSplitterDrag(Rect splitRect)
    {
        var e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown when splitRect.Contains(e.mousePosition):
                isDraggingSplitter = true;
                e.Use();
                break;
            case EventType.MouseDrag when isDraggingSplitter:
                leftPanelWidth = Mathf.Clamp(e.mousePosition.x, 200f, position.width - 300f);
                Repaint();
                e.Use();
                break;
            case EventType.MouseUp:
                isDraggingSplitter = false;
                break;
        }
    }

    private void OnDestroy() => ClearCards();
}
