using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

    private Dictionary<StateType, int> _movesPerForm;
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
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        SetState(_craneState);
    }

    public void SetFrogState()
    {
        if (isStateChangeLocked) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        SetState(_frogState);
    }

    public void SetPlaneState()
    {
        if (isStateChangeLocked) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
        SetState(_planeState);
    }

    public void SetBoatState()
    {
        if (isStateChangeLocked) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxChangeState);
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

        // Для самолёта строим путь по диагонали для пошаговой анимации
        List<CellPrefab> planePath = null;
        if (type == StateType.Plane && !forceFailMovement)
        {
            var pathCells = BoardPiecePrefab.BoardPiece.GetPlanePathCells(targetCell.Cell);
            planePath = new List<CellPrefab>(pathCells.Count);
            foreach (var c in pathCells)
                planePath.Add(_boardPrefab.GetCellPrefab(c));
        }

        bool success = BoardPiecePrefab.Move(targetCell, OnMove, forceFailMovement, boatPath, planePath: planePath,
            onStarCollected: () => StarAmount.Value += 1,
            onFireCell: cell => { cell.ActivateFireSplash(); OnPlayerDied?.Invoke(); },
            onLastHopStart: () => isStateChangeLocked = false);

        if (success)
        {
            isMovementLocked = true;
            isStateChangeLocked = true;
            if (boatPath == null && planePath == null)
                FMODAudioManager.Instance.PlayStepSound(targetCell.Cell.Terrain, targetCell.Cell.IsFragile);
            const float hopDuration = 0.44f;
            float shakeDelay = boatPath != null ? boatPath.Count * hopDuration
                             : planePath != null ? planePath.Count * hopDuration
                             : 0.5f;
            targetCell.ShakeCell(shakeDelay);
            _movesPerForm[type]--;
            OnMovesLeftChanged?.Invoke(type, _movesPerForm[type]);

            if (_movesPerForm[type] <= 0)
                OnOutOfMoves();

            bool allExhausted = true;
            foreach (var kvp in _movesPerForm)
                if (kvp.Value > 0) { allExhausted = false; break; }
            if (allExhausted)
                OnAllMovesExhausted?.Invoke();

        }
    }

    public void UndoMove()
    {
        if (isMovementLocked)
            return;

        BoardRecord? lastRecord = _boardPrefab.Board.BoardHistory.Undo();
        if (!lastRecord.HasValue)
            return;

        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxUndo);
        OnUndoHistoryChange?.Invoke(_boardPrefab.Board.BoardHistory.Count);
        BoardPiecePrefab.CancelMove();
        BoardPiecePrefab.BoardPiece.OccupiedCell.Value.FreePiece();

        Vector2Int playerPosition = lastRecord.Value.PlayerPosition;
        StateType playerState = lastRecord.Value.PlayerState;
        List<Vector2Int> starsRemaining = lastRecord.Value.StarsRemaining;

        _movesPerForm = new Dictionary<StateType, int>(lastRecord.Value.MovesPerForm);
        foreach (var kvp in _movesPerForm)
            OnMovesLeftChanged?.Invoke(kvp.Key, kvp.Value);

        // Restore fragile cells BEFORE teleporting so the target cell exists.
        // Only reset cells NOT in the target collapsed list — cells that stay collapsed
        // must not be animated (reset → instant-collapse would trigger two animations).
        var targetCollapsed = new System.Collections.Generic.HashSet<Vector2Int>(lastRecord.Value.CollapsedCells);
        foreach (var cell in _boardPrefab.Board.FragileCells)
            if (!targetCollapsed.Contains(cell.Position))
                cell.ResetCollapse();
        foreach (var pos in lastRecord.Value.CollapsedCells)
            _boardPrefab.Board.GetCell(pos)?.CollapseInstant();

        _biomeCellSystem?.RestoreSnapshot(lastRecord.Value.BiomeSnapshot);

        isMovementLocked = true;
        BoardPiecePrefab.Teleport(_boardPrefab.GetCellPrefab(playerPosition), tweenMovement: true,
            onComplete: () => { isMovementLocked = false; isStateChangeLocked = false; });
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

    public void ShowHoverPath(CellPrefab hoveredCell)
    {
        HideHoverPath();

        IState currentState = (_stateMachine.CurrentState as IState)!;
        StateType type = currentState.StateType;

        List<CellPrefab> pathCells = new();

        if (type == StateType.Boat && hoveredCell.Cell.Terrain == TerrainType.Water)
        {
            Cell landing = BoardPiecePrefab.BoardPiece.FindBoatLandingCell(hoveredCell.Cell);
            if (landing != null)
            {
                var boatPath = BoardPiecePrefab.BoardPiece.GetBoatPathCells(landing);
                foreach (var c in boatPath)
                    pathCells.Add(_boardPrefab.GetCellPrefab(c));
            }
        }
        else if (type == StateType.Plane)
        {
            var planePath = BoardPiecePrefab.BoardPiece.GetPlanePathCells(hoveredCell.Cell);
            foreach (var c in planePath)
                pathCells.Add(_boardPrefab.GetCellPrefab(c));
        }

        foreach (var cell in pathCells)
        {
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

    private void OnMove()
    {
        // Tick biome (volcano/ice) only after the player's movement animation completes
        _biomeCellSystem?.OnPlayerMoved();
        AddHistoryRecord();

        Cell targetCell = BoardPiecePrefab.CurrentCell.Cell;
        if (targetCell.Terrain == TerrainType.End)
        {
            OnPlayerWon?.Invoke(StarAmount);
            // keep locked — game is over
        }
        else if (targetCell.Terrain == TerrainType.Fire || targetCell.Terrain == TerrainType.Lava)
        {
            BoardPiecePrefab.CurrentCell.ActivateFireSplash();
            OnPlayerDied?.Invoke();
            // keep locked — player died
        }
        else if (_pendingEruptions > 0)
        {
            _unlockAfterEruption = true;
            // stay locked until eruption visual finishes
        }
        else
        {
            isMovementLocked = false;
            isStateChangeLocked = false;
        }
    }
}
