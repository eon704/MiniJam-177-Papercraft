using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
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

    private int MovesLeftForCurrentState => _movesPerForm[(_stateMachine.CurrentState as IState)!.StateType];

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

    private const int TotalStars = 3;

    private Sequence _pulseSequence;
    private Tween _spinTween;
    public bool isMovementLocked;

    private BoardPrefab _boardPrefab;
    private BiomeCellSystem _biomeCellSystem;

    public Action<int> OnUndoHistoryChange;

    public enum StateType
    {
        Default,
        Crane,
        Plane,
        Boat,
        Frog
    }

    private Dictionary<StateType, int> _movesPerForm;
    private List<CellPrefab> _moveOptionCells;
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

    public void SetTransformationLimits(Dictionary<StateType, int> startingMoves)
    {
        _movesPerForm = new Dictionary<StateType, int>(startingMoves);

        OnMovesLeftChanged?.Invoke(StateType.Boat, _movesPerForm[StateType.Boat]);
        OnMovesLeftChanged?.Invoke(StateType.Crane, _movesPerForm[StateType.Crane]);
        OnMovesLeftChanged?.Invoke(StateType.Frog, _movesPerForm[StateType.Frog]);
        OnMovesLeftChanged?.Invoke(StateType.Plane, _movesPerForm[StateType.Plane]);
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
        AddHistoryRecord();
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
        SetState(_craneState);
    }

    public void SetFrogState()
    {
        SetState(_frogState);
    }

    public void SetPlaneState()
    {
        SetState(_planeState);
    }

    public void SetBoatState()
    {
        SetState(_boatState);
    }

    public void Move(CellPrefab targetCell)
    {
        if (isMovementLocked)
            return;

        IState currentState = (_stateMachine.CurrentState as IState)!;
        StateType type = currentState.StateType;

        // Лодка: клик по воде — находим клетку приземления на другом берегу
        if (type == StateType.Boat && targetCell.Cell.Terrain == TerrainType.Water)
        {
            Cell landing = BoardPiecePrefab.BoardPiece.FindBoatLandingCell(targetCell.Cell);
            if (landing == null) return;
            targetCell = _boardPrefab.GetCellPrefab(landing);
        }

        bool forceFailMovement = _movesPerForm[type] <= 0;

        // Для лодки строим путь через воду для пошаговой анимации
        List<CellPrefab> boatPath = null;
        if (type == StateType.Boat && !forceFailMovement)
        {
            var pathCells = BoardPiecePrefab.BoardPiece.GetBoatPathCells(targetCell.Cell);
            boatPath = new List<CellPrefab>(pathCells.Count);
            foreach (var c in pathCells)
                boatPath.Add(_boardPrefab.GetCellPrefab(c));
        }

        float arcHeight = 1.5f;
        if (type == StateType.Plane && !forceFailMovement)
        {
            Vector3 from = BoardPiecePrefab.transform.position;
            Vector3 to = targetCell.transform.position;
            float dist = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z));
            arcHeight = Mathf.Max(1.25f, dist * 0.3f);
        }

        bool success = BoardPiecePrefab.Move(targetCell, OnMove, forceFailMovement, boatPath, arcHeight);

        if (success)
        {
            isMovementLocked = true;
            targetCell.ShakeCell();
            _movesPerForm[type]--;
            OnMovesLeftChanged?.Invoke(type, _movesPerForm[type]);

            if (_movesPerForm[type] <= 0)
                OnOutOfMoves();

            _biomeCellSystem?.OnPlayerMoved();
            AddHistoryRecord();
        }
    }

    public void UndoMove()
    {
        if (isMovementLocked)
            return;

        BoardRecord? lastRecord = _boardPrefab.Board.BoardHistory.Undo();
        if (!lastRecord.HasValue)
            return;

        OnUndoHistoryChange?.Invoke(_boardPrefab.Board.BoardHistory.Count);
        BoardPiecePrefab.CancelMove();
        BoardPiecePrefab.BoardPiece.OccupiedCell.Value.FreePiece();

        Vector2Int playerPosition = lastRecord.Value.PlayerPosition;
        StateType playerState = lastRecord.Value.PlayerState;
        List<Vector2Int> starsRemaining = lastRecord.Value.StarsRemaining;

        _movesPerForm = new Dictionary<StateType, int>(lastRecord.Value.MovesPerForm);
        foreach (var kvp in _movesPerForm)
            OnMovesLeftChanged?.Invoke(kvp.Key, kvp.Value);

        // Restore fragile cells BEFORE teleporting so the target cell exists
        _boardPrefab.Board.ResetFragileCells();
        foreach (var pos in lastRecord.Value.CollapsedCells)
            _boardPrefab.Board.GetCell(pos)?.CollapseInstant();

        _biomeCellSystem?.RestoreSnapshot(lastRecord.Value.BiomeSnapshot);

        isMovementLocked = true;
        BoardPiecePrefab.Teleport(_boardPrefab.GetCellPrefab(playerPosition), tweenMovement: true,
            onComplete: () => isMovementLocked = false);
        SetState(GetState(playerState));

        foreach (var cell in starsRemaining)
            _boardPrefab.GetCellPrefab(cell).Cell.ReassignStar();

        StarAmount.Value = TotalStars - starsRemaining.Count;
    }

    private void AddHistoryRecord()
    {
        Vector2Int playerPosition = BoardPiecePrefab.CurrentCell.Cell.Position;
        StateType playerState = (_stateMachine.CurrentState as IState)!.StateType;
        List<Vector2Int> starsRemaining = new();

        foreach (var cell in _boardPrefab.Board.StarCells)
        {
            if (cell.Item == CellItem.Star)
                starsRemaining.Add(cell.Position);
        }

        List<Vector2Int> collapsedCells = new();
        foreach (var cell in _boardPrefab.Board.FragileCells)
        {
            if (cell.IsCollapsed)
                collapsedCells.Add(cell.Position);
        }

        BiomeCellSnapshot biomeSnapshot = _biomeCellSystem?.TakeSnapshot() ?? default;
        _boardPrefab.Board.BoardHistory.AddRecord(
            playerPosition,
            playerState,
            starsRemaining,
            new Dictionary<StateType, int>(_movesPerForm),
            collapsedCells,
            biomeSnapshot
        );

        OnUndoHistoryChange?.Invoke(_boardPrefab.Board.BoardHistory.Count);
    }

    private void Update()
    {
        if (!_isInitialized) return;
        _stateMachine.Tick();
    }

    private void OnDisable()
    {
        _pulseSequence?.Kill();
    }

    private void SetState(IState state)
    {
        if (!FMODEvents.Instance.changeState.IsNull) RuntimeManager.PlayOneShot(FMODEvents.Instance.changeState);
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

    private void OnCollectStar()
    {
        StarAmount.Value += 1;
    }

    private void OnMove()
    {
        Cell targetCell = BoardPiecePrefab.CurrentCell.Cell;
        if (targetCell.Terrain == TerrainType.End)
        {
            OnPlayerWon?.Invoke(StarAmount);
            // keep locked — game is over
        }
        else if (targetCell.Terrain == TerrainType.Fire || targetCell.Terrain == TerrainType.Lava)
        {
            OnPlayerDied?.Invoke();
            // keep locked — player died
        }
        else
        {
            isMovementLocked = false;
        }
    }
}
