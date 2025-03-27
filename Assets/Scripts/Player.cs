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
        this.BoardPiecePrefab.Initialize(boardPiece, startCell, boardPrefab);
        this.BoardPiecePrefab.BoardPiece.OccupiedCell.OnChanged += this.OnPlayerMoved;
    }

    public void SetTransformationLimits(Dictionary<StateType, int> startingMoves)
    {
        this._movesPerForm = new Dictionary<StateType, int>(startingMoves);
        
        this.OnMovesLeftChanged?.Invoke(StateType.Boat, this._movesPerForm[StateType.Boat]);
        this.OnMovesLeftChanged?.Invoke(StateType.Crane, this._movesPerForm[StateType.Crane]);
        this.OnMovesLeftChanged?.Invoke(StateType.Frog, this._movesPerForm[StateType.Frog]);
        this.OnMovesLeftChanged?.Invoke(StateType.Plane, this._movesPerForm[StateType.Plane]);
    } 
    

    private void Awake()
    {
        this._spriteRenderer = GetComponent<SpriteRenderer>();
        this.BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        this._stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateSprite, this._spriteRenderer);
        _craneState = new CraneState(craneStateGameObject, this);
        _planeState = new PlaneState(planeStateGameObject, this);
        _boatState = new BoatState(boatStateGameObject,this);
        _frogState = new FrogState(frogStateGameObject,this);
    }

    private IEnumerator Start()
    {
        yield return null;
        SetDefaultState();
    }

    private void OnPlayerMoved(Observable<Cell> cell, Cell oldCell, Cell newCell)
    {
        ResetPulse();
        _moveOptionCells = BoardPiecePrefab.GetMoveOptionCellPrefabs();
        PulseReachableCells();
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
                this._pulseSequence.Insert(distanceToCellsPair.Key * delay, cellPrefab.DoPulse(duration));
            }
        }

        this._pulseSequence.SetLoops(-1);
        this._pulseSequence.Play();
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public void SetDefaultState()
    {
        this.OnTransformation?.Invoke(StateType.Default);
        SetState(_defaultState);
    }

    public void SetCraneState()
    {
        this.OnTransformation?.Invoke(StateType.Crane);
        SetState(_craneState);
    }

    public void SerFrogState()
    {
        this.OnTransformation?.Invoke(StateType.Frog);
        SetState(_frogState);
    }

    public void SetPlaneState()
    {
        this.OnTransformation?.Invoke(StateType.Plane);
        SetState(_planeState);
    }

    public void SetBoatState()
    {
        this.OnTransformation?.Invoke(StateType.Boat);
        SetState(_boatState);
    }

    public void Move(CellPrefab targetCell)
    {
        if (isMovementLocked)
            return;
        
        IState currentState = (this._stateMachine.CurrentState as IState)!; 
        StateType type = currentState.StateType;
        bool forceFailMovement = this._movesPerForm[type] <= 0;
        isMovementLocked = targetCell.Cell.Terrain == Cell.TerrainType.Fire;
        bool success = this.BoardPiecePrefab.Move(targetCell, this.OnMove, forceFailMovement);
        
        if (success)
        {
            targetCell.ShakeCell();
            _movesPerForm[type]--;
            OnMovesLeftChanged?.Invoke(type, this._movesPerForm[type]);
            
            if (_movesPerForm[type] <= 0)
            {
                OnOutOfMoves();
            }
        }
    }

    private void Update() => this._stateMachine.Tick();

    private void OnDisable()
    {
        this._pulseSequence?.Kill();
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
        Cell targetCell = this.BoardPiecePrefab.CurrentCell.Cell;

        if (targetCell.Item == Cell.CellItem.Star)
        {
            targetCell.CollectStar();
            GlobalSoundManager.PlayRandomSoundByType(SoundType.Ding,1f);
            this.StarAmount.Value += 1;
        }
        
        if (targetCell.Terrain == Cell.TerrainType.End)
        {
            this.OnPlayerWon?.Invoke(this.StarAmount);
        } else if (targetCell.Terrain == Cell.TerrainType.Fire)
        {
            this.OnPlayerDied?.Invoke();
        }
    }
}