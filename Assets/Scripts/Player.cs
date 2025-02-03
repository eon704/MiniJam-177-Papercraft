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
    
    public UnityEvent OnPlayerWon;
    public UnityEvent OnPlayerDied;

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

    private Dictionary<IState, int> transformationLeft;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell)
    {
        this.BoardPiecePrefab.Initialize(boardPiece, startCell);
    }

    private void Awake()
    {
        OnPlayerWon = new UnityEvent();
        OnPlayerDied = new UnityEvent();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        this.BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        _stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateSprite, _spriteRenderer, this);
        _craneState = new CraneState(craneStateSprite, _spriteRenderer, this);
        _planeState = new PlaneState(planeStateSprite, _spriteRenderer, this);
        _boatState = new BoatState(boatStateSprite, _spriteRenderer, this);
        _frogState = new FrogState(frogStateSprite, _spriteRenderer, this);

        this.transformationLeft = new Dictionary<IState, int>
        {
            { _defaultState, 1 },
            { _craneState, 2 },
            { _planeState, 1 },
            { _boatState, 2 },
            { _frogState, 2 },
        };
    }

    private IEnumerator Start()
    {
        yield return null;
        SetDefaultState();
    }

    private void SetDefaultState()
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
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(state);
        this.BoardPiecePrefab.BoardPiece.SetState(state);
        // this.BoardPiecePrefab.CurrentCell.Neighbors.
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