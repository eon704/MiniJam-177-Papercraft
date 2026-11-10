using System;
using System.Collections.Generic;
using DG.Tweening;
using Paperbound.Core;
using UnityEngine;

/// <summary>
/// Вид механик Океана по Tools/Levels/ui-requirements.md: стрелки течения на клетках, плоты со стрелкой следующего шага,
/// путь лодки со всеми поворотами (серый — если течение не пускает, с подсказкой почему), предпросмотр «плот повезёт»
/// и отдельная анимация сдвига плота после хода игрока (события EntityMoved и Carried).
///
/// НЕ СКОМПИЛИРОВАНО В UNITY: код собран и проверен вне редактора (сборка против UnityEngine.Modules), в игре не запускался.
/// Вместо арта — примитивы и процедурные меши; материалы создаются из встроенных шейдеров во время игры.
/// Правила не дублируются: всё, что показывается, спрашивается у ядра (Rules.GetOptions, Rules.RaftStep, Rules.Apply).
/// </summary>
public class OceanView : MonoBehaviour
{
    // Высоты над поверхностью клетки (в единицах мира).
    private const float ArrowHeight = 0.32f;
    private const float RaftHeight = 0.28f;
    private const float PathHeight = 0.55f;
    private const float DriftDuration = 0.35f;

    private static readonly Color FlowColor = new(0.85f, 0.95f, 1f, 0.75f);
    private static readonly Color RaftColor = new(0.62f, 0.44f, 0.26f, 1f);
    private static readonly Color RaftArrowColor = new(1f, 0.85f, 0.35f, 0.95f);
    private static readonly Color PathColor = new(0.95f, 0.9f, 0.3f, 0.95f);
    private static readonly Color BlockedColor = new(0.55f, 0.55f, 0.55f, 0.9f);
    private static readonly Color CarryColor = new(1f, 0.6f, 0.2f, 0.95f);

    private BoardPrefab _board;
    private LevelDef _def;
    private readonly Dictionary<int, GameObject> _rafts = new();      // индекс сущности → объект плота
    private readonly Dictionary<int, GameObject> _raftArrows = new(); // стрелка следующего шага плота
    private LineRenderer _pathLine;
    private LineRenderer _carryLine;
    private TextMesh _hint;
    private Material _flatMaterial;
    private Material _litMaterial;

    /// <summary>Создать вид, если в уровне есть течение или плоты. Иначе null.</summary>
    public static OceanView Create(BoardPrefab board)
    {
        LevelDef def = board.Board.Def;
        bool any = def.Entities.Exists(e => e.Kind == EntityKind.Raft);
        for (int i = 0; i < def.CellCount && !any; i++)
            any = def.Flow[i] != Pos.Zero && def.Terrain[i] == TerrainType.Water;
        if (!any) return null;

        var go = new GameObject("OceanView");
        go.transform.SetParent(board.transform, false);
        var view = go.AddComponent<OceanView>();
        view.Build(board);
        return view;
    }

    private void Build(BoardPrefab board)
    {
        _board = board;
        _def = board.Board.Def;
        _flatMaterial = MakeMaterial(false);
        _litMaterial = MakeMaterial(true);

        // Стрелки течения: только вода (ветер на суше — забота вида Пустыни).
        for (int i = 0; i < _def.CellCount; i++)
        {
            Pos p = _def.PosOf(i);
            Pos flow = _def.Flow[i];
            if (flow == Pos.Zero || _def.Terrain[i] != TerrainType.Water) continue;
            GameObject arrow = MakeArrow($"Flow {p}", 0.55f, FlowColor);
            arrow.transform.position = CellTop(p) + Vector3.up * ArrowHeight;
            arrow.transform.rotation = Quaternion.LookRotation(WorldDir(flow), Vector3.up);
        }

        for (int i = 0; i < _def.Entities.Count; i++)
        {
            if (_def.Entities[i].Kind != EntityKind.Raft) continue;
            GameObject raft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            raft.name = $"Raft {i}";
            Destroy(raft.GetComponent<Collider>()); // клики должны доходить до клетки под плотом
            raft.transform.SetParent(transform, false);
            raft.transform.localScale = new Vector3(0.8f, 0.12f, 0.8f);
            raft.GetComponent<Renderer>().sharedMaterial = _litMaterial;
            SetColor(raft, RaftColor);
            _rafts[i] = raft;

            GameObject next = MakeArrow($"Raft {i} next", 0.4f, RaftArrowColor);
            _raftArrows[i] = next;
        }

        _pathLine = MakeLine("Boat path", 0.09f);
        _carryLine = MakeLine("Raft carry", 0.07f);

        var hintGo = new GameObject("Ocean hint");
        hintGo.transform.SetParent(transform, false);
        _hint = hintGo.AddComponent<TextMesh>();
        _hint.characterSize = 0.08f;
        _hint.fontSize = 48;
        _hint.anchor = TextAnchor.LowerCenter;
        _hint.alignment = TextAlignment.Center;
        _hint.color = Color.white;
        hintGo.SetActive(false);

        Refresh(board.Board.State);
    }

    /// <summary>Поставить плоты и их стрелки по состоянию без анимации (старт, Undo).</summary>
    public void Refresh(GameState state)
    {
        foreach (KeyValuePair<int, GameObject> kv in _rafts)
        {
            kv.Value.transform.DOKill();
            kv.Value.transform.position = CellTop(state.Entities[kv.Key].Position) + Vector3.up * RaftHeight;
        }
        RefreshRaftArrows(state);
    }

    private void RefreshRaftArrows(GameState state)
    {
        foreach (KeyValuePair<int, GameObject> kv in _raftArrows)
        {
            // Стрелка — куда плот сдвинется после следующего хода, если фигурка останется на месте. Стоит — стрелки нет.
            Pos to = default;
            bool moves = state.Status == GameStatus.Playing && Rules.RaftStep(_def, state, kv.Key, out to);
            kv.Value.SetActive(moves);
            if (!moves) continue;
            Pos from = state.Entities[kv.Key].Position;
            kv.Value.transform.position = CellTop(from) + Vector3.up * (RaftHeight + 0.1f);
            kv.Value.transform.rotation = Quaternion.LookRotation(WorldDir(to - from), Vector3.up);
        }
    }

    // ── Предпросмотр хода ─────────────────────────────────────────────────

    /// <summary>
    /// Показать ход под курсором: путь (для лодки — со всеми поворотами), серый путь и подсказку, если течение не пускает,
    /// и куда фигурку довезёт плот, если она на него встанет.
    /// </summary>
    public void ShowPreview(MoveOption option, GameState state, Form form)
    {
        HidePreview();
        if (option == null || state.Status != GameStatus.Playing) return;

        if (option.Blocked != SailBlock.None)
        {
            DrawPath(_pathLine, state.Player, option.Path, BlockedColor);
            ShowHint(option.Target, option.Blocked == SailBlock.AgainstFlow
                ? "Против течения не проплыть"
                : "Течение по кругу: выхода нет");
            return;
        }
        if (!option.Legal) return;

        if (form == Form.Boat && option.Path.Count >= 2)
            DrawPath(_pathLine, state.Player, option.Path, PathColor);

        // Повезёт ли плот: спросить у правил, что будет после этого хода.
        MoveResult result = Rules.Apply(_def, state, form, option.Target);
        if (!result.Success) return;
        foreach (MoveEvent e in result.Events)
        {
            if (e.Type != MoveEventType.Carried) continue;
            DrawPath(_carryLine, e.Source, new List<Pos> { e.Pos }, CarryColor);
            ShowHint(e.Pos, "Плот повезёт");
            break;
        }
    }

    public void HidePreview()
    {
        if (_pathLine != null) _pathLine.positionCount = 0;
        if (_carryLine != null) _carryLine.positionCount = 0;
        if (_hint != null) _hint.gameObject.SetActive(false);
    }

    // ── Анимация после хода ───────────────────────────────────────────────

    /// <summary>
    /// После анимации хода игрока: сдвинуть плоты (EntityMoved) и вместе с плотом фигурку (Carried), затем onComplete.
    /// Если плоты не двигались — onComplete сразу.
    /// </summary>
    public void PlayDrift(MoveResult result, BoardPiecePrefab piece, Action onComplete)
    {
        HidePreview();
        Sequence seq = DOTween.Sequence();
        bool any = false;
        foreach (MoveEvent e in result.Events)
        {
            if (e.Type == MoveEventType.EntityMoved && _rafts.TryGetValue(e.EntityIndex, out GameObject raft))
            {
                seq.Join(raft.transform.DOMove(CellTop(e.Pos) + Vector3.up * RaftHeight, DriftDuration).SetEase(Ease.InOutSine));
                any = true;
            }
            else if (e.Type == MoveEventType.Carried && piece != null)
            {
                CellPrefab to = _board.GetCellPrefab(e.Pos.ToVector());
                seq.Join(piece.Drift(to, DriftDuration));
                seq.AppendCallback(to.TriggerStarPickupVisual); // звезда на клетке, куда довёз плот
                any = true;
            }
        }
        if (!any)
        {
            seq.Kill();
            RefreshRaftArrows(result.After);
            onComplete?.Invoke();
            return;
        }
        foreach (GameObject arrow in _raftArrows.Values) arrow.SetActive(false);
        seq.OnComplete(() =>
        {
            RefreshRaftArrows(result.After);
            onComplete?.Invoke();
        });
    }

    /// <summary>Есть ли в результате хода сдвиг плота, который надо проиграть отдельно.</summary>
    public static bool HasDrift(MoveResult result)
    {
        foreach (MoveEvent e in result.Events)
            if (e.Type == MoveEventType.EntityMoved || e.Type == MoveEventType.Carried) return true;
        return false;
    }

    // ── Построение примитивов ─────────────────────────────────────────────

    private Vector3 CellTop(Pos p) => _board.GetCellPrefab(p.ToVector()).FinalWorldPosition;

    private Vector3 WorldDir(Pos d)
    {
        Vector3 local = new(d.X, 0f, d.Y);
        Vector3 world = _board.transform.TransformDirection(local);
        return world.sqrMagnitude > 0.0001f ? world.normalized : Vector3.forward;
    }

    private void DrawPath(LineRenderer line, Pos from, List<Pos> path, Color color)
    {
        var points = new List<Vector3> { CellTop(from) + Vector3.up * PathHeight };
        foreach (Pos p in path) points.Add(CellTop(p) + Vector3.up * PathHeight);
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.startColor = line.endColor = color;
    }

    private void ShowHint(Pos at, string text)
    {
        _hint.text = text;
        _hint.transform.position = CellTop(at) + Vector3.up * (PathHeight + 0.35f);
        Camera cam = Camera.main;
        if (cam != null) _hint.transform.rotation = Quaternion.LookRotation(_hint.transform.position - cam.transform.position, cam.transform.up);
        _hint.gameObject.SetActive(true);
    }

    private LineRenderer MakeLine(string lineName, float width)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCornerVertices = 2; // повороты лодки читаются углами линии
        line.numCapVertices = 2;
        line.sharedMaterial = _flatMaterial;
        line.positionCount = 0;
        return line;
    }

    /// <summary>Плоская стрелка остриём вдоль +Z локальных осей, длиной length.</summary>
    private GameObject MakeArrow(string arrowName, float length, Color color)
    {
        var go = new GameObject(arrowName);
        go.transform.SetParent(transform, false);
        float half = length / 2f, shaft = length * 0.12f, head = length * 0.3f;
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-shaft, 0, -half), new Vector3(shaft, 0, -half),
                new Vector3(shaft, 0, half - head), new Vector3(-shaft, 0, half - head),
                new Vector3(-head, 0, half - head), new Vector3(head, 0, half - head), new Vector3(0, 0, half),
            },
            triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 6, 5 },
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _flatMaterial;
        SetColor(go, color);
        return go;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", color);
        block.SetColor("_BaseColor", color); // URP
        go.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    /// <summary>
    /// Материал из встроенного шейдера. В сборке шейдер должен быть в Always Included Shaders (см. Tools/Levels/UNITY_TODO.md),
    /// иначе Shader.Find вернёт null; тогда берётся любой доступный.
    /// </summary>
    private static Material MakeMaterial(bool lit)
    {
        string[] names = lit
            ? new[] { "Universal Render Pipeline/Lit", "Standard", "Sprites/Default" }
            : new[] { "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Color" };
        foreach (string n in names)
        {
            Shader shader = Shader.Find(n);
            if (shader != null) return new Material(shader);
        }
        Debug.LogWarning("[OceanView] Не найден ни один встроенный шейдер — стрелки и плоты будут розовыми.");
        return new Material(Shader.Find("Hidden/InternalErrorShader"));
    }

    private void OnDestroy()
    {
        foreach (GameObject raft in _rafts.Values)
            if (raft != null) raft.transform.DOKill();
    }
}
