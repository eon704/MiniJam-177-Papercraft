using System.Collections;
using System.Collections.Generic;
using PlayerStateMachine;
using UnityEngine;
using UnityEngine.Events;


public class Player : MonoBehaviour
{
    public BoardPiecePrefab BoardPiecePrefab { get; private set; }

    public GameObject changeStateEffect;
    
    public readonly UnityEvent OnPlayerWon = new();
    public readonly UnityEvent OnPlayerDied = new();
    public readonly UnityEvent OnTransformation = new();
    public readonly UnityEvent<StateType, int> OnMovesLeftChanged = new();

    private StateMachine _stateMachine;
    private SpriteRenderer _spriteRenderer;

    [Header("State sprites")] [SerializeField]
    private Sprite defaultStateSprite;

    [SerializeField] private Sprite craneStateSprite;
    [SerializeField] private Sprite planeStateSprite;
    [SerializeField] private Sprite boatStateSprite;
    [SerializeField] private Sprite frogStateSprite;

    private IState _defaultState;
    private IState _craneState;
    private IState _planeState;
    private IState _boatState;
    private IState _frogState;
    
    
    public enum StateType
    {
        Default,
        Crane,
        Plane,
        Boat,
        Frog
    }

    private Dictionary<StateType, int> movesPerForm;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell)
    {
        this.BoardPiecePrefab.Initialize(boardPiece, startCell);
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
        _spriteRenderer = GetComponent<SpriteRenderer>();
        this.BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        _stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateSprite, _spriteRenderer, this);
        _craneState = new CraneState(craneStateSprite, _spriteRenderer, this);
        _planeState = new PlaneState(planeStateSprite, _spriteRenderer, this);
        _boatState = new BoatState(boatStateSprite, _spriteRenderer, this);
        _frogState = new FrogState(frogStateSprite, _spriteRenderer, this);
    }

    private IEnumerator Start()
    {
        yield return null;
        SetDefaultState();
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
        IState currentState = (this._stateMachine.CurrentState as IState)!; 
        StateType type = currentState.StateType;
        bool forceFailMovement = this.movesPerForm[type] <= 0;
        bool success = this.BoardPiecePrefab.Move(targetCell, this.OnMove, forceFailMovement);
        
        if (success)
        {
            this.movesPerForm[type]--;
            this.OnMovesLeftChanged?.Invoke(type, this.movesPerForm[type]);
        }
    }

    private void Update() => _stateMachine.Tick();

    private void SetState(IState state)
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(state);
        this.BoardPiecePrefab.BoardPiece.SetState(state);
    }

    private void OnMove()
    {
        Cell targetCell = this.BoardPiecePrefab.CurrentCell.Cell;
        if (targetCell.Terrain == Cell.TerrainType.End)
        {
            this.OnPlayerWon?.Invoke();
        } else if (targetCell.Terrain == Cell.TerrainType.Fire)
        {
            this.OnPlayerDied?.Invoke();
        }
    }
}