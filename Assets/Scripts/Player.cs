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
    public readonly UnityEvent<StateType, int> OnTransformation = new();

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

    private Dictionary<StateType, int> transformationsLeft;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell, Dictionary<StateType, int> startTransformations)
    {
        this.BoardPiecePrefab.Initialize(boardPiece, startCell);
        this.transformationsLeft = startTransformations;
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
        
        // Send updated counts to UI
        this.OnTransformation?.Invoke(StateType.Boat, transformationsLeft[StateType.Boat]);
        this.OnTransformation?.Invoke(StateType.Crane, transformationsLeft[StateType.Crane]);
        this.OnTransformation?.Invoke(StateType.Frog, transformationsLeft[StateType.Frog]);
        this.OnTransformation?.Invoke(StateType.Plane, transformationsLeft[StateType.Plane]);
    }

    public void SetDefaultState()
    {
        SetState(_defaultState);
    }

    public void SetCraneState()
    {
        SetState(_craneState);
    }

    public void SerFrogState()
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
        this.BoardPiecePrefab.Move(targetCell, this.OnMove);
    }

    private void Update() => _stateMachine.Tick();

    private void SetState(IState state)
    {
        StateType type = state.StateType;
        if (type != StateType.Default && this.transformationsLeft[type] <= 0)
        {
            return;
        }

        this.transformationsLeft[type]--;
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(state);
        this.BoardPiecePrefab.BoardPiece.SetState(state);
        OnTransformation?.Invoke(state.StateType, transformationsLeft[type]);
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