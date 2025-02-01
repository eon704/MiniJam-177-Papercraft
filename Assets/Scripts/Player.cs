using PlayerStateMachine;
using UnityEngine;

public class Player : MonoBehaviour
{
    public BoardPiecePrefab BoardPiecePrefab { get; private set; }
    private StateMachine _stateMachine;
    private SpriteRenderer _spriteRenderer;

    [Header("State sprites")]
    [SerializeField] private Sprite defaultStateSprite;
    [SerializeField] private Sprite craneStateSprite;
    [SerializeField] private Sprite planeStateSprite;
    [SerializeField] private Sprite boatStateSprite;
    [SerializeField] private Sprite frogStateSprite;

    private IState _defaultState;
    private IState _craneState;
    private IState _planeState;
    private IState _boatState;
    private IState _frogState;

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell)
    {
        this.BoardPiecePrefab.Initialize(boardPiece, startCell);
    }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        this.BoardPiecePrefab = GetComponent<BoardPiecePrefab>();
        _stateMachine = new StateMachine();

        _defaultState = new DefaultState(defaultStateSprite, _spriteRenderer);
        _craneState = new CraneState(craneStateSprite, _spriteRenderer); 
        _planeState = new PlaneState(planeStateSprite, _spriteRenderer);
        _boatState = new BoatState(boatStateSprite, _spriteRenderer);
        _frogState = new FrogState(frogStateSprite, _spriteRenderer);
        

        _stateMachine.SetState(_defaultState); // Set the default state
    }

    public void SetDefaultState() { _stateMachine.SetState(_defaultState); }
    public void SetCraneState()  { _stateMachine.SetState(_craneState); }
    public void SerFrogState() { _stateMachine.SetState(_frogState); }
    public void SetPlaneState() { _stateMachine.SetState(_planeState); }
    public void SetBoatState() { _stateMachine.SetState(_boatState); }
    
    private void Update() => _stateMachine.Tick();
}