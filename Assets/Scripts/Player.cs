using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PlayerStateMachine;
using UnityEngine;
using UnityEngine.Events;


public class Player : MonoBehaviour
{
    public BoardPiecePrefab BoardPiecePrefab { get; private set; }

    public GameObject ChangeStateEffect;

    public readonly Observable<int> StarAmount = new(0);
    
    public readonly UnityEvent<int> OnPlayerWon = new();
    public readonly UnityEvent OnPlayerDied = new();
    public readonly UnityEvent OnTransformation = new();
    public readonly UnityEvent<StateType, int> OnMovesLeftChanged = new();

    private StateMachine stateMachine;
    private SpriteRenderer spriteRenderer;

    [Header("State sprites")]
    [SerializeField] private Sprite defaultStateSprite;
    [SerializeField] private Sprite craneStateSprite;
    [SerializeField] private Sprite planeStateSprite;
    [SerializeField] private Sprite boatStateSprite;
    [SerializeField] private Sprite frogStateSprite;
    
    [Header("State sprites Preview")] 
    public GameObject defaultStateSpritePreview;
    public GameObject craneStateSpritePreview;
    public GameObject planeStateSpritePreview;
    public GameObject boatStateSpritePreview;
    public GameObject frogStateSpritePreview;

    private IState _defaultState;
    private IState _craneState;
    private IState _planeState;
    private IState _boatState;
    private IState _frogState;
    
    private Sequence pulseSequence;
    
    public enum StateType
    {
        Default,
        Crane,
        Plane,
        Boat,
        Frog
    }

    private Dictionary<StateType, int> movesPerForm;
    private List<CellPrefab> reachableCells;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell, BoardPrefab boardPrefab)
    {
        this.BoardPiecePrefab.Initialize(boardPiece, startCell, boardPrefab);
        this.BoardPiecePrefab.BoardPiece.OccupiedCell.OnChanged += this.OnPlayerMoved;
    }

    public void SetTransformationLimits(Dictionary<StateType, int> startingMoves)
    {
        this.movesPerForm = new Dictionary<StateType, int>(startingMoves);
        this.OnMovesLeftChanged?.Invoke(StateType.Boat, this.movesPerForm[StateType.Boat]);
        this.OnMovesLeftChanged?.Invoke(StateType.Crane, this.movesPerForm[StateType.Crane]);
        this.OnMovesLeftChanged?.Invoke(StateType.Frog, this.movesPerForm[StateType.Frog]);
        this.OnMovesLeftChanged?.Invoke(StateType.Plane, this.movesPerForm[StateType.Plane]);
    } 
    

    private void Awake()
    {
        this.spriteRenderer = GetComponent<SpriteRenderer>();
        this.BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        this.stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateSprite, this.spriteRenderer, this);
        _craneState = new CraneState(craneStateSprite, this.spriteRenderer, this);
        _planeState = new PlaneState(planeStateSprite, this.spriteRenderer, this);
        _boatState = new BoatState(boatStateSprite, this.spriteRenderer, this);
        _frogState = new FrogState(frogStateSprite, this.spriteRenderer, this);
    }

    private IEnumerator Start()
    {
        yield return null;
        SetDefaultState();
    }

    private void OnPlayerMoved(Observable<Cell> cell, Cell oldCell, Cell newCell)
    {
        this.PulseReachableCells();
    }

    private void PulseReachableCells()
    {
        Cell startCell = this.BoardPiecePrefab.CurrentCell.Cell;
        this.reachableCells?.ForEach(cellPrefab => cellPrefab.ResetPulse());
        
        Dictionary<int, List<CellPrefab>> reachableCellsByDistance = new();
        this.reachableCells = this.BoardPiecePrefab.GetReachableCellPrefabs();

        foreach (CellPrefab cellPrefab in this.reachableCells)
        {
            int distance = Cell.Distance(startCell, cellPrefab.Cell);
            if (!reachableCellsByDistance.ContainsKey(distance))
            {
                reachableCellsByDistance[distance] = new List<CellPrefab>();
            }

            reachableCellsByDistance[distance].Add(cellPrefab);
        }

        float duration = 1f;
        float delay = duration / 4;
        this.pulseSequence?.Kill(true);
        this.pulseSequence = DOTween.Sequence();
        this.pulseSequence.Insert(0, this.BoardPiecePrefab.CurrentCell.DoPulse(duration));
        
        foreach (KeyValuePair<int, List<CellPrefab>> distanceToCellsPair in reachableCellsByDistance)
        {
            foreach (CellPrefab cellPrefab in distanceToCellsPair.Value)
            {
                this.pulseSequence.Insert(distanceToCellsPair.Key * delay, cellPrefab.DoPulse(duration));
            }
        }

        this.pulseSequence.SetLoops(-1);
        this.pulseSequence.Play();
    }

    public void SetDefaultState()
    {
        SetState(_defaultState);
    }

    public void SetCraneState()
    {
        this.OnTransformation?.Invoke();
        SetState(_craneState);
    }

    public void SerFrogState()
    {
        this.OnTransformation?.Invoke();
        SetState(_frogState);
    }

    public void SetPlaneState()
    {
        this.OnTransformation?.Invoke();
        SetState(_planeState);
    }

    public void SetBoatState()
    {
        this.OnTransformation?.Invoke();
        SetState(_boatState);
    }

    public void Move(CellPrefab targetCell)
    {
        IState currentState = (this.stateMachine.CurrentState as IState)!; 
        StateType type = currentState.StateType;
        bool forceFailMovement = this.movesPerForm[type] <= 0;
        bool success = this.BoardPiecePrefab.Move(targetCell, this.OnMove, forceFailMovement);
        
        if (success)
        {
            this.movesPerForm[type]--;
            this.OnMovesLeftChanged?.Invoke(type, this.movesPerForm[type]);
        }
    }

    private void Update() => this.stateMachine.Tick();

    private void OnDisable()
    {
        this.pulseSequence?.Kill();
    }

    private void SetState(IState state)
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        this.stateMachine.SetState(state);
        this.BoardPiecePrefab.BoardPiece.SetState(state);
        this.PulseReachableCells();
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