using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Paperbound.Core;
using PlayerStateMachine;
using UnityEngine;
using UnityEngine.Events;

public class Player : MonoBehaviour
{
    public BoardPiecePrefab BoardPiecePrefab { get; private set; }
    public GameObject changeStateEffect;

    public readonly Observable<int> StarAmount = new(0);

    public readonly UnityEvent<int> OnPlayerWon = new();
    public readonly UnityEvent OnPlayerDied = new();
    public readonly UnityEvent<StateType> OnTransformation = new();
    public readonly UnityEvent<StateType, int> OnMovesLeftChanged = new();
    public readonly UnityEvent OnAllMovesExhausted = new();
    public readonly UnityEvent OnNoMovesAvailable = new();

    private Board Board => _boardPrefab.Board;

    /// <summary>Фигурка погибла; ход отменяется кнопкой Undo.</summary>
    public bool IsDead => _boardPrefab != null && Board.State.Status == GameStatus.Dead;

    private StateMachine _stateMachine;

    [Header("States Objects")]
    [SerializeField] private GameObject defaultStateObject;
    [SerializeField] private GameObject craneStateGameObject;
    [SerializeField] private GameObject planeStateGameObject;
    [SerializeField] private GameObject boatStateGameObject;
    [SerializeField] private GameObject frogStateGameObject;

    private IState _defaultState;
    private IState _craneState;
    private IState _planeState;
    private IState _boatState;
    private IState _frogState;


    private Sequence _pulseSequence;
    private Tween _spinTween;
    public bool isMovementLocked;
    public bool isStateChangeLocked;

    private BoardPrefab _boardPrefab;
    private BiomeCellSystem _biomeCellSystem;

    public Action<int> OnUndoHistoryChange;

    private int _pendingEruptions;
    private bool _unlockAfterEruption;

    public void StartEruption() => _pendingEruptions++;

    public void EndEruption()
    {
        _pendingEruptions = Mathf.Max(0, _pendingEruptions - 1);
        if (_pendingEruptions == 0 && _unlockAfterEruption)
        {
            _unlockAfterEruption = false;
            if (Board.State.Status != GameStatus.Playing) return;
            isMovementLocked = false;
            isStateChangeLocked = false;
        }
    }

    public void CancelPendingEruptions()
    {
        _pendingEruptions = 0;
        _unlockAfterEruption = false;
    }

    public enum StateType
    {
        Default,
        Crane,
        Plane,
        Boat,
        Frog
    }

    private List<CellPrefab> _moveOptionCells;
    private List<CellPrefab> _hoverPathCells = new();
    private bool _isInitialized;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell, BoardPrefab boardPrefab)
    {
        _boardPrefab = boardPrefab;
        BoardPiecePrefab.Initialize(boardPiece, startCell, boardPrefab);
        BoardPiecePrefab.BoardPiece.OccupiedCell.OnChanged += OnPlayerMoved;
        BoardPiecePrefab.BoardPiece.OnCollectedStar += OnCollectStar;
        _isInitialized = true;
    }

    public void SetBiomeCellSystem(BiomeCellSystem biomeCellSystem)
    {
        _biomeCellSystem = biomeCellSystem;
    }

    /// <summary>Разослать UI текущий бюджет каждой формы из состояния партии.</summary>
    public void RefreshMovesLeft()
    {
        GameState state = Board.State;
        OnMovesLeftChanged?.Invoke(StateType.Boat, state.BudgetOf(Form.Boat));
        OnMovesLeftChanged?.Invoke(StateType.Crane, state.BudgetOf(Form.Crane));
        OnMovesLeftChanged?.Invoke(StateType.Frog, state.BudgetOf(Form.Frog));
        OnMovesLeftChanged?.Invoke(StateType.Plane, state.BudgetOf(Form.Plane));
    }

    private void Awake()
    {
        BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        _stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateObject);
        _craneState = new CraneState(craneStateGameObject, this);
        _planeState = new PlaneState(planeStateGameObject, this);
        _boatState = new BoatState(boatStateGameObject, this);
        _frogState = new FrogState(frogStateGameObject, this);
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => _isInitialized);
        SetDefaultState();
        yield return null;
        OnUndoHistoryChange?.Invoke(Board.BoardHistory.Count);
    }

    private void OnPlayerMoved(Observable<Cell> cell, Cell oldCell, Cell newCell)
    {
        ResetPulse();
        _moveOptionCells = BoardPiecePrefab.GetMoveOptionCellPrefabs();
        PulseReachableCells();
    }

    private IState GetState(StateType stateType)
    {
        return stateType switch
        {
            StateType.Default => _defaultState,
            StateType.Crane => _craneState,
            StateType.Plane => _planeState,
            StateType.Boat => _boatState,
            StateType.Frog => _frogState,
            _ => throw new ArgumentOutOfRangeException(nameof(stateType), stateType, null)
        };
    }

    private StateType GetStateType(IState state)
    {
        switch (state)
        {
            case DefaultState:
                return StateType.Default;
            case CraneState:
                return StateType.Crane;
            case PlaneState:
                return StateType.Plane;
            case BoatState:
                return StateType.Boat;
            case FrogState:
                return StateType.Frog;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private void PulseReachableCells()
    {
        Cell startCell = BoardPiecePrefab.CurrentCell.Cell;
        Dictionary<int, List<CellPrefab>> reachableCellsByDistance = new();
        _moveOptionCells = BoardPiecePrefab.GetMoveOptionCellPrefabs();

        foreach (var cellPrefab in _moveOptionCells)
        {
            int distance = Cell.Distance(startCell, cellPrefab.Cell);
            if (!reachableCellsByDistance.ContainsKey(distance))
                reachableCellsByDistance[distance] = new List<CellPrefab>();

            reachableCellsByDistance[distance].Add(cellPrefab);
        }

        if (_moveOptionCells.Count == 0)
        {
            OnNoMovesAvailable?.Invoke();
            return;
        }

        var duration = 1f;
        var delay = duration / 4;
        _pulseSequence?.Kill();
        _pulseSequence = DOTween.Sequence();

        foreach (KeyValuePair<int, List<CellPrefab>> distanceToCellsPair in reachableCellsByDistance)
        {
            foreach (var cellPrefab in distanceToCellsPair.Value)
                _pulseSequence.Insert(distanceToCellsPair.Key * delay, cellPrefab.DoPulse(duration));
        }

        _pulseSequence.SetLoops(-1);
        _pulseSequence.Play();
    }

    public void SetDefaultState()
    {
        SetState(_defaultState);
    }

    public void SetCraneState()
    {
        if (isStateChangeLocked) return;
        if (Board.State.Status != GameStatus.Playing) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        Board.Fold(Form.Crane);
        SetState(_craneState);
    }

    public void SetFrogState()
    {
        if (isStateChangeLocked) return;
        if (Board.State.Status != GameStatus.Playing) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        Board.Fold(Form.Frog);
        SetState(_frogState);
    }

    public void SetPlaneState()
    {
        if (isStateChangeLocked) return;
        if (Board.State.Status != GameStatus.Playing) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        Board.Fold(Form.Plane);
        SetState(_planeState);
    }

    public void SetBoatState()
    {
        if (isStateChangeLocked) return;
        if (Board.State.Status != GameStatus.Playing) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        Board.Fold(Form.Boat);
        SetState(_boatState);
    }

    public void Move(CellPrefab targetCell)
    {
        if (isMovementLocked)
            return;
        if (Board.State.Status != GameStatus.Playing)
            return;

        IState currentState = (_stateMachine.CurrentState as IState)!;
        StateType type = currentState.StateType;
        Form form = type.ToForm();
        GameState before = Board.State;

        // Клик по воде (лодка) или по первой клетке льда (журавль) означает ход до конца пути.
        MoveOption option = BoardPiecePrefab.BoardPiece.ResolveClick(targetCell.Cell);
        MoveResult result = option != null ? Rules.Apply(Board.Def, before, form, option.Target) : null;

        if (result == null || !result.Success)
        {
            BoardPiecePrefab.PlayFailShake();
            return;
        }

        // Путь для анимации обрезается на клетке гибели (самолёт над огнём).
        int deathIndex = -1;
        foreach (MoveEvent e in result.Events)
            if (e.Type == MoveEventType.Died && e.PathIndex < result.Path.Count) { deathIndex = e.PathIndex; break; }
        int animatedCount = deathIndex >= 0 ? deathIndex + 1 : result.Path.Count;

        var path = new List<CellPrefab>(animatedCount);
        for (int i = 0; i < animatedCount; i++)
            path.Add(_boardPrefab.GetCellPrefab(result.Path[i].ToVector()));

        CellPrefab startCell = BoardPiecePrefab.CurrentCell;
        CellPrefab endCell = path[path.Count - 1];
        bool multiHop = path.Count > 1;

        // Визуалы звёзд и обрушений откладываются до момента, когда фигурка долетит до клетки.
        foreach (MoveEvent e in result.Events)
        {
            CellPrefab cell = _boardPrefab.GetCellPrefab(e.Pos.ToVector());
            if (e.Type == MoveEventType.StarCollected)
                cell.SetStarVisualDeferred(() => StarAmount.Value += 1);
            else if (e.Type == MoveEventType.Collapsed && (multiHop || e.PathIndex >= 0))
                cell.DeferCollapseVisual();
        }

        // Модель: новое состояние, клетки (без террейна расписаний — его покажет BiomeCellSystem), фигурка.
        Board.Commit(result.After);
        Board.SyncCells(result.After, syncTerrain: false, animateCollapse: true);
        foreach (MoveEvent e in result.Events)
            if (e.Type == MoveEventType.Sank)
                Board.GetCell(e.Pos.ToVector())?.SetTerrain(e.NewTerrain); // кувшинка утонула
        BoardPiecePrefab.BoardPiece.TeleportTo(endCell.Cell);
        OnUndoHistoryChange?.Invoke(Board.BoardHistory.Count);

        isMovementLocked = true;
        isStateChangeLocked = true;
        if (!multiHop)
            FMODAudioManager.Instance.PlayStepSound(endCell.Cell.Terrain, endCell.Cell.IsFragile);
        float shakeDelay = multiHop ? path.Count * BoardPiecePrefab.HopDuration : 0.5f;
        endCell.ShakeCell(shakeDelay);

        BoardPiecePrefab.PlayMove(startCell, path, () => OnMoveAnimated(result),
            onLastHopStart: () => isStateChangeLocked = false);

        int left = result.After.BudgetOf(form);
        OnMovesLeftChanged?.Invoke(type, left);
        if (left <= 0)
            OnOutOfMoves();

        bool allExhausted = true;
        foreach (Form f in Forms.Movable)
            if (result.After.BudgetOf(f) > 0) { allExhausted = false; break; }
        if (allExhausted && result.After.Status == GameStatus.Playing)
            OnAllMovesExhausted?.Invoke();
    }

    public void UndoMove()
    {
        // После гибели фигурка заблокирована, но Undo доступен — им смерть и отменяется.
        bool deadAndSettled = IsDead && _pendingEruptions == 0;
        if (isMovementLocked && !deadAndSettled)
            return;

        GameState previous = Board.Undo();
        if (previous == null)
            return;

        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxUndo);
        OnUndoHistoryChange?.Invoke(Board.BoardHistory.Count);
        BoardPiecePrefab.CancelMove();
        BoardPiecePrefab.BoardPiece.OccupiedCell.Value.FreePiece();

        // Клетки — до телепорта, чтобы целевая клетка существовала.
        Board.SyncCells(previous, syncTerrain: true, animateCollapse: false);
        _biomeCellSystem?.Refresh();
        RefreshMovesLeft();

        isMovementLocked = true;
        isStateChangeLocked = true;
        BoardPiecePrefab.Teleport(_boardPrefab.GetCellPrefab(previous.Player.ToVector()), tweenMovement: true,
            onComplete: () => { isMovementLocked = false; isStateChangeLocked = false; });
        SetState(GetState(previous.Form.ToStateType()));

        StarAmount.Value = previous.StarsCollected;
    }

    private void Update()
    {
        if (!_isInitialized) return;
        _stateMachine.Tick();
    }

    public void ShowHoverPath(CellPrefab hoveredCell)
    {
        HideHoverPath();

        MoveOption option = BoardPiecePrefab.BoardPiece.ResolveClick(hoveredCell.Cell);
        if (option == null || !option.Legal || option.Path.Count < 2)
            return;

        foreach (Cell c in BoardPiecePrefab.BoardPiece.GetPathCells(option))
        {
            CellPrefab cell = _boardPrefab.GetCellPrefab(c);
            if (cell == hoveredCell) continue;
            cell.SetIsPathPreview(true);
            _hoverPathCells.Add(cell);
        }
    }

    public void HideHoverPath()
    {
        foreach (var cell in _hoverPathCells)
            cell.SetIsPathPreview(false);
        _hoverPathCells.Clear();
    }

    private void OnDisable()
    {
        _pulseSequence?.Kill();
        HideHoverPath();
    }

    private void SetState(IState state)
    {
        _stateMachine.SetState(state);
        BoardPiecePrefab.BoardPiece.SetState(state);
        OnTransformation?.Invoke(GetStateType(state));

        // Быстрый спин по Y — скрывает переключение модели
        _spinTween?.Kill();
        Vector3 euler = transform.localEulerAngles;
        euler.y = 0f;
        transform.localEulerAngles = euler;
        _spinTween = transform.DORotate(new Vector3(0f, 360f, 0f), 0.3f, RotateMode.LocalAxisAdd)
            .SetEase(Ease.InOutCubic);

        ResetPulse();
        PulseReachableCells();
    }

    private void ResetPulse()
    {
        if (_moveOptionCells == null || _moveOptionCells.Count == 0)
            return;

        _moveOptionCells?.ForEach(cellPrefab => cellPrefab.ResetIsValidMoveOption());
        _pulseSequence?.Kill();
        foreach (var cellPrefab in _moveOptionCells!)
            cellPrefab.ResetPulse();
    }

    private void OnOutOfMoves()
    {
        ResetPulse();
        _pulseSequence?.Kill(true);
        _pulseSequence = DOTween.Sequence();
        _pulseSequence.Append(BoardPiecePrefab.CurrentCell.DoOutOfMovesPulse());
        _pulseSequence.SetLoops(-1);
        _pulseSequence.Play();
    }

    private void OnCollectStar() { }

    private void OnMoveAnimated(MoveResult result)
    {
        // Расписания (вулкан, лёд) показываются после анимации хода.
        _biomeCellSystem?.PlayEvents(result);

        GameState state = result.After;
        StarAmount.Value = state.StarsCollected;

        if (state.Status == GameStatus.Won)
        {
            OnPlayerWon?.Invoke(state.StarsCollected);
            // keep locked — game is over
        }
        else if (state.Status == GameStatus.Dead && state.Death != DeathCause.Schedule)
        {
            BoardPiecePrefab.CurrentCell.ActivateFireSplash();
            OnPlayerDied?.Invoke();
            // keep locked — Undo отменяет смерть
        }
        else if (_pendingEruptions > 0)
        {
            // Гибель от снаряда вулкана объявит GameController, когда снаряд долетит.
            _unlockAfterEruption = true;
        }
        else
        {
            isMovementLocked = false;
            isStateChangeLocked = false;
        }
    }
}
