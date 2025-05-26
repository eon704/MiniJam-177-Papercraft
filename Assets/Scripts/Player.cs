using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PlayerStateMachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;


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
    private SpriteRenderer _spriteRenderer;

    [Header("States Objects")]
    [SerializeField] private Sprite defaultStateSprite;
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
    public bool isMovementLocked;
    
    private BoardPrefab _boardPrefab;
    
    public enum StateType
    {
        Default,
        Crane,
        Plane,
        Boat,
        Frog
    }

    private Dictionary<StateType, int> _movesPerForm;
    private Dictionary<StateType, bool> _unlockedForms;
    private List<CellPrefab> _moveOptionCells;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell, BoardPrefab boardPrefab)
    {
        _boardPrefab = boardPrefab;
        BoardPiecePrefab.Initialize(boardPiece, startCell, boardPrefab);
        BoardPiecePrefab.BoardPiece.OccupiedCell.OnChanged += OnPlayerMoved;
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
        _spriteRenderer = GetComponent<SpriteRenderer>();
        BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        _stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateSprite, _spriteRenderer);
        _craneState = new CraneState(craneStateGameObject, this);
        _planeState = new PlaneState(planeStateGameObject, this);
        _boatState = new BoatState(boatStateGameObject,this);
        _frogState = new FrogState(frogStateGameObject,this);
    }

    private IEnumerator Start()
    {
        yield return null;
        SetDefaultState();
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

    private void PulseReachableCells()
    {
        Cell startCell = BoardPiecePrefab.CurrentCell.Cell;
        Dictionary<int, List<CellPrefab>> reachableCellsByDistance = new();
        _moveOptionCells = BoardPiecePrefab.GetMoveOptionCellPrefabs();

        foreach (var cellPrefab in _moveOptionCells)
        {
            int distance = Cell.Distance(startCell, cellPrefab.Cell);
            if (!reachableCellsByDistance.ContainsKey(distance))
            {
                reachableCellsByDistance[distance] = new List<CellPrefab>();
            }

            reachableCellsByDistance[distance].Add(cellPrefab);
        }

        var duration = 1f;
        var delay = duration / 4;
        _pulseSequence?.Kill();
        _pulseSequence = DOTween.Sequence();
        
        _pulseSequence.Insert(0, BoardPiecePrefab.CurrentCell.DoPulse(duration));

        foreach (KeyValuePair<int, List<CellPrefab>> distanceToCellsPair in reachableCellsByDistance)
        {
            foreach (var cellPrefab in distanceToCellsPair.Value)
            {
                _pulseSequence.Insert(distanceToCellsPair.Key * delay, cellPrefab.DoPulse(duration));
            }
        }

        _pulseSequence.SetLoops(-1);
        _pulseSequence.Play();
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public void SetDefaultState()
    {
        OnTransformation?.Invoke(StateType.Default);
        SetState(_defaultState);
    }

    public void SetCraneState()
    {
        OnTransformation?.Invoke(StateType.Crane);
        SetState(_craneState);
    }

    public void SerFrogState()
    {
        OnTransformation?.Invoke(StateType.Frog);
        SetState(_frogState);
    }

    public void SetPlaneState()
    {
        OnTransformation?.Invoke(StateType.Plane);
        SetState(_planeState);
    }

    public void SetBoatState()
    {
        OnTransformation?.Invoke(StateType.Boat);
        SetState(_boatState);
    }

    public void Move(CellPrefab targetCell)
    {
        if (isMovementLocked)
            return;
        
        IState currentState = (_stateMachine.CurrentState as IState)!; 
        StateType type = currentState.StateType;
        bool forceFailMovement = _movesPerForm[type] <= 0;
        bool success = BoardPiecePrefab.Move(targetCell, OnMove, forceFailMovement);
        
        if (success)
        {
            isMovementLocked = targetCell.Cell.Terrain == Cell.TerrainType.Fire;
            targetCell.ShakeCell();
            _movesPerForm[type]--;
            AddHistoryRecord();
            OnMovesLeftChanged?.Invoke(type, _movesPerForm[type]);
            
            if (_movesPerForm[type] <= 0)
            {
                OnOutOfMoves();
            }
        }
    }
    
    public void UndoMove()
    {
        if (isMovementLocked)
            return;
        
        BoardRecord? lastRecord = _boardPrefab.Board.BoardHistory.Undo();
        if (!lastRecord.HasValue)
            return;

        Vector2Int playerPosition = lastRecord.Value.PlayerPosition;
        StateType playerState = lastRecord.Value.PlayerState;
        List<Vector2Int> starsRemaining = lastRecord.Value.StarsRemaining;

        BoardPiecePrefab.Teleport(_boardPrefab.GetCellPrefab(playerPosition), tweenMovement: true);
        SetState(GetState(playerState));
        
        foreach (var cell in starsRemaining)
        {
            _boardPrefab.GetCellPrefab(cell).Cell.ReassignStar();
        }
    }
    
    private void AddHistoryRecord()
    {
        Vector2Int playerPosition = BoardPiecePrefab.CurrentCell.Cell.Position;
        StateType playerState = (_stateMachine.CurrentState as IState)!.StateType;
        List<Vector2Int> starsRemaining = new();
        
        foreach (var cell in _boardPrefab.Board.StarCells)
        {
            if (cell.Item == Cell.CellItem.Star)
            {
                starsRemaining.Add(cell.Position);
            }
        }
        
        _boardPrefab.Board.BoardHistory.AddRecord(playerPosition, playerState, starsRemaining);
    }

    private void Update()
    {
        _stateMachine.Tick();
        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            UndoMove();
        }
    }

    private void OnDisable()
    {
        _pulseSequence?.Kill();
    }

    private void SetState(IState state)
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(state);
        BoardPiecePrefab.BoardPiece.SetState(state);
        ResetPulse();
        PulseReachableCells();
    }

    private void ResetPulse()
    {
        if (_moveOptionCells == null || _moveOptionCells.Count == 0)
            return;
        
        _moveOptionCells?.ForEach(cellPrefab => cellPrefab.ResetIsValidMoveOption());
        _pulseSequence?.Kill();
        foreach(var cellPrefab in _moveOptionCells!)
        {
            cellPrefab.ResetPulse();
        }
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

    private void OnMove()
    {
        Cell targetCell = BoardPiecePrefab.CurrentCell.Cell;

        if (targetCell.Item == Cell.CellItem.Star)
        {
            targetCell.CollectStar();
            GlobalSoundManager.PlayRandomSoundByType(SoundType.Ding,1f);
            StarAmount.Value += 1;
        }
        
        if (targetCell.Terrain == Cell.TerrainType.End)
        {
            OnPlayerWon?.Invoke(StarAmount);
        } else if (targetCell.Terrain == Cell.TerrainType.Fire)
        {
            OnPlayerDied?.Invoke();
        }
    }
}