using PlayerStateMachine;
using UnityEngine;


public class Player : MonoBehaviour
{
    public BoardPiecePrefab BoardPiecePrefab { get; private set; }

    public GameObject changeStateEffect;

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

    public void Initialize(BoardPiece boardPiece, CellPrefab startCell)
    {
        this.BoardPiecePrefab.Initialize(boardPiece, startCell);
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


        SetDefaultState();  
    }

    private void SetDefaultState()
    {
        _stateMachine.SetState(_defaultState);
    }

    public void SetCraneState()
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(_craneState);
        this.BoardPiecePrefab.BoardPiece.SetState(_craneState);
    }

    public void SerFrogState()
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(_frogState);
        this.BoardPiecePrefab.BoardPiece.SetState(_frogState);
    }

    public void SetPlaneState()
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(_planeState);
        this.BoardPiecePrefab.BoardPiece.SetState(_planeState);
    }

    public void SetBoatState()
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.ChangeState);
        _stateMachine.SetState(_boatState);
        this.BoardPiecePrefab.BoardPiece.SetState(_boatState);
    }

    private void Update() => _stateMachine.Tick();
}