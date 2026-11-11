using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keyboard and gamepad control for the PC build. Creates itself at startup, no scene setup needed.
///
/// On the board (Game scene, level in progress, settings closed):
///   move cursor  — WASD / arrows / D-pad / left stick (direction follows the camera)
///   move there   — Enter / Space / gamepad A
///   form N       — keys 1-9: N-th form card of the level, in on-screen order left to right;
///                  Q / E and LB / RB cycle through the same list
///   undo         — Z / Backspace / gamepad X
///   restart      — R / gamepad Y
/// In menus the regular uGUI navigation is used; this component only gives focus to the first
/// button when a keyboard or gamepad user starts navigating with nothing selected.
///
/// It talks to the game only through public API (Player, CellPrefab pointer handlers, card buttons)
/// so the board model can change underneath it.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class PcInputController : MonoBehaviour
{
    public static PcInputController Instance { get; private set; }

    private const float RepeatDelay = 0.35f;
    private const float RepeatInterval = 0.14f;
    private const float StickDeadZone = 0.5f;
    private const float MouseMoveThreshold = 4f;

    private InputAction _navigate;
    private InputAction _confirm;
    private InputAction _undo;
    private InputAction _restart;
    private InputAction _prevForm;
    private InputAction _nextForm;
    private const int MaxFormKeys = 9;
    private readonly InputAction[] _formKeys = new InputAction[MaxFormKeys];

    // Bound game objects (Game scene only).
    private GameController _game;
    private Player _player;
    private readonly List<StateCardUI> _cards = new();
    private Player.StateType _currentForm = Player.StateType.Default;
    private bool _levelOver;

    // Board cursor.
    private bool _cursorVisible;
    private Vector2Int _cursor;
    private CellPrefab _hoveredCell;
    private bool _wasMovementLocked;

    // Navigation repeat.
    private Vector2Int _heldDirection;
    private float _nextRepeatTime;

    private bool _usingPointer = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[PcInput]");
        DontDestroyOnLoad(go);
        go.AddComponent<PcInputController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CreateActions();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnableActions(true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EnableActions(false);
    }

    private void Start() => TryBindGame();

    private void OnDestroy()
    {
        Unbind();
        foreach (var action in AllActions())
            action?.Dispose();
        if (Instance == this)
            Instance = null;
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private void CreateActions()
    {
        _navigate = new InputAction("Navigate", InputActionType.PassThrough);
        _navigate.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        _navigate.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        _navigate.AddBinding("<Gamepad>/dpad");
        _navigate.AddBinding("<Gamepad>/leftStick");

        _confirm = new InputAction("Confirm", InputActionType.Button);
        _confirm.AddBinding("<Keyboard>/enter");
        _confirm.AddBinding("<Keyboard>/numpadEnter");
        _confirm.AddBinding("<Keyboard>/space");
        _confirm.AddBinding("<Gamepad>/buttonSouth");

        _undo = new InputAction("Undo", InputActionType.Button);
        _undo.AddBinding("<Keyboard>/z");
        _undo.AddBinding("<Keyboard>/backspace");
        _undo.AddBinding("<Gamepad>/buttonWest");

        _restart = new InputAction("Restart", InputActionType.Button);
        _restart.AddBinding("<Keyboard>/r");
        _restart.AddBinding("<Gamepad>/buttonNorth");

        _prevForm = new InputAction("PrevForm", InputActionType.Button);
        _prevForm.AddBinding("<Keyboard>/q");
        _prevForm.AddBinding("<Gamepad>/leftShoulder");

        _nextForm = new InputAction("NextForm", InputActionType.Button);
        _nextForm.AddBinding("<Keyboard>/e");
        _nextForm.AddBinding("<Gamepad>/rightShoulder");

        for (int i = 0; i < _formKeys.Length; i++)
        {
            string digit = (i + 1).ToString();
            _formKeys[i] = new InputAction("Form" + digit, InputActionType.Button);
            _formKeys[i].AddBinding("<Keyboard>/" + digit);
            _formKeys[i].AddBinding("<Keyboard>/numpad" + digit);
        }
    }

    private IEnumerable<InputAction> AllActions()
    {
        yield return _navigate;
        yield return _confirm;
        yield return _undo;
        yield return _restart;
        yield return _prevForm;
        yield return _nextForm;
        foreach (var a in _formKeys) yield return a;
    }

    private void EnableActions(bool enable)
    {
        foreach (var action in AllActions())
        {
            if (action == null) continue;
            if (enable) action.Enable();
            else action.Disable();
        }
    }

    /// <summary>True when the last button press this frame came from a keyboard or gamepad.</summary>
    public static bool ConfirmPressedThisFrame => Instance != null && Instance._confirm.WasPressedThisFrame();

    // ── Binding to the Game scene ─────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_game == null)
            TryBindGame();
    }

    private void TryBindGame()
    {
        Unbind();

        _game = FindFirstObjectByType<GameController>();
        if (_game == null) return;

        _player = _game.PlayerPrefab;
        _levelOver = false;
        _currentForm = Player.StateType.Default;
        _cursorVisible = false;

        if (_player != null)
        {
            _player.OnTransformation.AddListener(OnTransformation);
            _player.OnPlayerWon.AddListener(OnPlayerWon);
        }
        _game.OnMapReset.AddListener(OnMapReset);
    }

    private void Unbind()
    {
        ClearHover();
        if (_player != null)
        {
            _player.OnTransformation.RemoveListener(OnTransformation);
            _player.OnPlayerWon.RemoveListener(OnPlayerWon);
        }
        if (_game != null)
            _game.OnMapReset.RemoveListener(OnMapReset);

        _game = null;
        _player = null;
        _cards.Clear();
    }

    private void OnTransformation(Player.StateType form) => _currentForm = form;
    private void OnPlayerWon(int _) { _levelOver = true; HideCursor(); }
    private void OnMapReset() => _levelOver = false;

    // ── Update ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_game == null && _player != null)
            Unbind(); // scene unloaded

        DetectPointerUse();

        Vector2Int dir = ReadNavigationStep();
        bool confirm = _confirm.WasPressedThisFrame();

        if (IsBoardActive())
        {
            // Keep uGUI from also reacting to Enter/Space/arrows on a button the mouse clicked earlier.
            if (!_usingPointer && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            UpdateBoard(dir, confirm);
        }
        else
        {
            HideCursor();
            if (dir != Vector2Int.zero || confirm)
                EnsureMenuSelection();
        }
    }

    private void DetectPointerUse()
    {
        var mouse = Mouse.current;
        bool pointerMoved = mouse != null &&
                            (mouse.delta.ReadValue().sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold ||
                             mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        if (pointerMoved)
        {
            _usingPointer = true;
            HideCursor();
            return;
        }

        foreach (var action in AllActions())
        {
            if (action.WasPressedThisFrame() || (action == _navigate && action.ReadValue<Vector2>().sqrMagnitude > StickDeadZone * StickDeadZone))
            {
                _usingPointer = false;
                return;
            }
        }
    }

    private bool IsBoardActive()
    {
        if (_game == null || _player == null || _levelOver) return false;
        if (_game.BoardPrefab == null || _game.BoardPrefab.Board == null || !_game.BoardPrefab.IsSpawnAnimationComplete) return false;

        // Settings panel open on top of the board.
        var settings = FindFirstObjectByType<SettingsMenuUI>();
        if (settings != null && settings.isActiveAndEnabled) return false;

        return true;
    }

    private void UpdateBoard(Vector2Int dir, bool confirm)
    {
        // Re-apply the hover highlight when the player finishes a move (cells ignore hover while locked).
        bool locked = _player.isMovementLocked;
        if (_wasMovementLocked && !locked && _cursorVisible)
            Hover(CellAt(_cursor));
        _wasMovementLocked = locked;

        if (dir != Vector2Int.zero)
        {
            if (!_cursorVisible)
                ShowCursorAtPlayer();
            else
                MoveCursor(dir);
        }

        if (confirm)
        {
            if (!_cursorVisible)
                ShowCursorAtPlayer();
            else if (!locked)
                CellAt(_cursor)?.OnPointerUp(null);
        }

        // Undo and restart are not gated here: after a death the player is locked, and undoing the
        // fatal move must still work. Player/GameController decide whether the request is valid.
        if (_undo.WasPressedThisFrame())
            _player.UndoMove();

        if (_restart.WasPressedThisFrame())
            _game.ResetMap();

        for (int i = 0; i < _formKeys.Length; i++)
            if (_formKeys[i].WasPressedThisFrame())
                PressCard(i);

        if (_prevForm.WasPressedThisFrame()) CycleForm(-1);
        if (_nextForm.WasPressedThisFrame()) CycleForm(+1);
    }

    // ── Navigation input with key repeat ───────────────────────────────────

    private Vector2Int ReadNavigationStep()
    {
        Vector2 v = _navigate.ReadValue<Vector2>();
        Vector2Int dir = new Vector2Int(
            Mathf.Abs(v.x) >= StickDeadZone ? (int)Mathf.Sign(v.x) : 0,
            Mathf.Abs(v.y) >= StickDeadZone ? (int)Mathf.Sign(v.y) : 0);

        if (dir == Vector2Int.zero)
        {
            _heldDirection = Vector2Int.zero;
            return Vector2Int.zero;
        }

        float now = Time.unscaledTime;
        if (dir != _heldDirection)
        {
            _heldDirection = dir;
            _nextRepeatTime = now + RepeatDelay;
            return dir;
        }

        if (now >= _nextRepeatTime)
        {
            _nextRepeatTime = now + RepeatInterval;
            return dir;
        }

        return Vector2Int.zero;
    }

    // ── Board cursor ───────────────────────────────────────────────────────

    private CellPrefab CellAt(Vector2Int p)
    {
        var board = _game.BoardPrefab;
        if (p.x < 0 || p.y < 0 || p.x >= board.Size.x || p.y >= board.Size.y) return null;
        return board.GetCellPrefab(p);
    }

    private static bool IsSelectable(CellPrefab cell) =>
        cell != null && cell.gameObject.activeInHierarchy && cell.Cell != null && cell.Cell.Terrain != TerrainType.Empty;

    private void ShowCursorAtPlayer()
    {
        var current = _player.BoardPiecePrefab != null ? _player.BoardPiecePrefab.CurrentCell : null;
        if (current == null || current.Cell == null) return;

        _cursor = current.Cell.Position;
        _cursorVisible = true;
        Hover(current);
    }

    private void HideCursor()
    {
        if (!_cursorVisible) return;
        _cursorVisible = false;
        ClearHover();
    }

    private void MoveCursor(Vector2Int screenDir)
    {
        Vector2Int step = ScreenToGridStep(screenDir);
        if (step == Vector2Int.zero) return;

        // Skip over empty cells in that direction.
        Vector2Int p = _cursor + step;
        while (true)
        {
            CellPrefab cell = CellAt(p);
            if (cell == null) return; // left the board — stay put
            if (IsSelectable(cell))
            {
                _cursor = p;
                Hover(cell);
                return;
            }
            p += step;
        }
    }

    /// <summary>
    /// Converts a screen-space direction (up = away from the player) into a grid step, using how the
    /// grid axes look through the current camera. Works for any camera yaw, including isometric views
    /// where "up" maps to a diagonal.
    /// </summary>
    private Vector2Int ScreenToGridStep(Vector2Int screenDir)
    {
        Camera cam = Camera.main;
        CellPrefab origin = CellAt(_cursor);
        if (cam == null || origin == null) return screenDir;

        Vector3 o = origin.transform.position;
        // BoardPrefab places cells in world space: grid x along world X, grid y along world Z.
        Vector2 sx = (Vector2)(cam.WorldToScreenPoint(o + Vector3.right) - cam.WorldToScreenPoint(o));
        Vector2 sy = (Vector2)(cam.WorldToScreenPoint(o + Vector3.forward) - cam.WorldToScreenPoint(o));
        if (sx.sqrMagnitude < 1e-4f || sy.sqrMagnitude < 1e-4f) return screenDir;

        Vector2 v = ((Vector2)screenDir).normalized;
        float a = Vector2.Dot(v, sx.normalized);
        float b = Vector2.Dot(v, sy.normalized);
        const float threshold = 0.38f; // ~cos(67.5°): splits the circle into 8 sectors
        return new Vector2Int(
            Mathf.Abs(a) >= threshold ? (int)Mathf.Sign(a) : 0,
            Mathf.Abs(b) >= threshold ? (int)Mathf.Sign(b) : 0);
    }

    private void Hover(CellPrefab cell)
    {
        if (cell == _hoveredCell)
        {
            // Refresh (e.g. after a move unlocked input).
            cell?.OnPointerExit(null);
            cell?.OnPointerEnter(null);
            return;
        }

        ClearHover();
        _hoveredCell = cell;
        _hoveredCell?.OnPointerEnter(null);
    }

    private void ClearHover()
    {
        if (_hoveredCell != null)
            _hoveredCell.OnPointerExit(null);
        _hoveredCell = null;
    }

    // ── Forms ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Form cards shown for this level, in on-screen order (left to right). Rebuilt on every use so
    /// it follows levels that show a different set or number of forms.
    /// </summary>
    private List<StateCardUI> Cards()
    {
        _cards.Clear();
        foreach (var card in FindObjectsByType<StateCardUI>(FindObjectsSortMode.None))
            if (card.isActiveAndEnabled)
                _cards.Add(card);
        _cards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return _cards;
    }

    private static bool TryPress(StateCardUI card)
    {
        if (card == null) return false;
        var button = card.GetComponent<Button>();
        if (button == null || !button.IsInteractable()) return false;
        button.onClick.Invoke();
        return true;
    }

    private void PressCard(int index)
    {
        var cards = Cards();
        if (index >= 0 && index < cards.Count)
            TryPress(cards[index]);
    }

    private void CycleForm(int delta)
    {
        var cards = Cards();
        if (cards.Count == 0) return;

        int current = cards.FindIndex(c => c.StateType == _currentForm);
        int start = current >= 0 ? current : (delta > 0 ? -1 : 0);

        for (int i = 1; i <= cards.Count; i++)
        {
            int idx = ((start + delta * i) % cards.Count + cards.Count) % cards.Count;
            if (idx == current) continue;
            if (TryPress(cards[idx])) return;
        }
    }

    // ── Menus ──────────────────────────────────────────────────────────────

    private void EnsureMenuSelection()
    {
        var es = EventSystem.current;
        if (es == null) return;

        GameObject selected = es.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
        {
            var sel = selected.GetComponent<Selectable>();
            if (sel == null || sel.IsInteractable()) return;
        }

        Selectable best = null;
        int bestOrder = int.MinValue;
        Vector2 bestPos = default;

        foreach (Selectable s in Selectable.allSelectablesArray)
        {
            if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) continue;
            if (!IsVisible(s)) continue;

            var canvas = s.GetComponentInParent<Canvas>();
            int order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;
            Vector2 pos = s.transform.position;

            // Topmost canvas wins; within it, prefer buttons higher on screen, then further left.
            bool better = best == null || order > bestOrder ||
                          (order == bestOrder && (pos.y > bestPos.y + 1f ||
                                                  (Mathf.Abs(pos.y - bestPos.y) <= 1f && pos.x < bestPos.x)));
            if (better)
            {
                best = s;
                bestOrder = order;
                bestPos = pos;
            }
        }

        if (best != null)
            es.SetSelectedGameObject(best.gameObject);
    }

    private static bool IsVisible(Selectable s)
    {
        // Skip controls inside hidden or non-interactable CanvasGroups (faded-out panels).
        var groups = s.GetComponentsInParent<CanvasGroup>();
        foreach (var g in groups)
        {
            if (!g.interactable || g.alpha <= 0.01f) return false;
            if (g.ignoreParentGroups) break;
        }
        return true;
    }
}
