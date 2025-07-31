using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class BoardPrefab : MonoBehaviour
{
  [Header("Setup")]
  [SerializeField] private Player player;
  [SerializeField] private Transform boardBorder;
  [SerializeField] private CellPrefab cellPrefab;

  private Grid worldGrid;
  public Vector2Int Size { get; private set; }
  public Vector3 WorldCenter { get; private set; }

  public Board Board { get; private set; }
  public LevelData LevelData { get; private set; }
  public bool IsSpawnAnimationComplete { get; private set; }

  private CellPrefab[,] cellPrefabs;

  private List<SpriteRenderer> revealedHints = new();

  private Sequence pulseSequence;

  public void Initialize(LevelData levelData)
  {
    LevelData = levelData;
    Size = levelData.MapSize;
    Board = new Board(Size, levelData.Map, levelData);
    cellPrefabs = new CellPrefab[Size.x, Size.y];

    InstantiateBoard();
    UpdateBorder();
    ComputeBoardCenterPosition();
  }

  public List<CellPrefab> GetCellPrefabs(List<Cell> cells)
  {
    return cells.Select(cell => GetCellPrefab(cell.Position)).ToList();
  }

  public List<CellPrefab> GetCellPrefabs(List<Vector2Int> coords)
  {
    return coords.Select(GetCellPrefab).ToList();
  }

  public CellPrefab GetCellPrefab(Cell cell)
  {
    return GetCellPrefab(cell.Position);
  }

  public CellPrefab GetCellPrefab(Vector2Int coord)
  {
    return cellPrefabs[coord.x, coord.y];
  }

  public CellPrefab GetStartCellPrefab()
  {
    return GetCellPrefab(Board.StartCell.Position);
  }

  public List<CellPrefab> GetStarCellPrefabs()
  {
    return GetCellPrefabs(Board.StarCells);
  }

  public (BoardPiece, CellPrefab) CreateNewPlayerPrefab()
  {
    BoardPiece playerPiece = Board.CreatePlayerPiece();
    CellPrefab startCell = GetCellPrefab(playerPiece.OccupiedCell.Value.Position);
    return (playerPiece, startCell);
  }

  private void Awake()
  {
    worldGrid = GetComponent<Grid>();
  }

    private void OnDestroy()
    {
      pulseSequence?.Kill();
      
      // Kill any remaining tweens on hint renderers
      foreach (SpriteRenderer hintRenderer in revealedHints)
      {
        if (hintRenderer != null)
          hintRenderer.DOKill();
      }
    }

    private void InstantiateBoard()
  {
    int centerX = Size.x / 2;
    int centerY = Size.y / 2;
    float longestDelay = 0f;

    for (int x = 0; x < Size.x; x++)
    {
      for (int y = 0; y < Size.y; y++)
      {
        Cell cell = Board.CellArray[x, y];
        Vector3 cellPosition = worldGrid.GetCellCenterWorld(new Vector3Int(x, y, 0));
        int distanceFromCenter = Mathf.Abs(centerX - x) + Mathf.Abs(centerY - y);
        float delay = distanceFromCenter * 0.1f + 0.5f;
        cellPrefabs[x, y] = Instantiate(cellPrefab, cellPosition, Quaternion.identity, transform);
        cellPrefabs[x, y].Initialize(cell, player, delay);

        if (cell.Terrain == TerrainType.Empty || delay < longestDelay)
          continue;

        longestDelay = delay;
      }
    }

    Invoke(nameof(SetAnimationComplete), longestDelay + 0.5f);
  }

  private void SetAnimationComplete()
  {
    IsSpawnAnimationComplete = true;
  }

  private void UpdateBorder()
  {
    boardBorder.gameObject.SetActive(true);
    boardBorder.localScale = new Vector3(Size.x + 0.1f, Size.y + 0.1f, 1);

    Vector3 borderPosition = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2, Size.y / 2, 0));

    if (Size.x % 2 == 0)
      borderPosition.x -= worldGrid.cellSize.x / 2;

    if (Size.y % 2 == 0)
      borderPosition.y -= worldGrid.cellSize.y / 2;

    boardBorder.position = borderPosition;
  }

  private void ComputeBoardCenterPosition()
  {
    bool isOddX = Size.x % 2 != 0;
    bool isOddY = Size.y % 2 != 0;

    float xPos;
    if (isOddX)
    {
      xPos = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2, 0, 0)).x;
    }
    else
    {
      float x1 = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2 - 1, 0, 0)).x;
      float x2 = worldGrid.GetCellCenterWorld(new Vector3Int(Size.x / 2, 0, 0)).x;
      xPos = (x1 + x2) / 2;
    }

    float yPos;
    if (isOddY)
    {
      yPos = worldGrid.GetCellCenterWorld(new Vector3Int(0, Size.y / 2, 0)).y;
    }
    else
    {
      float y1 = worldGrid.GetCellCenterWorld(new Vector3Int(0, Size.y / 2 - 1, 0)).y;
      float y2 = worldGrid.GetCellCenterWorld(new Vector3Int(0, Size.y / 2, 0)).y;
      yPos = (y1 + y2) / 2;
    }

    WorldCenter = new Vector3(xPos, yPos, -10);
  }

  // Hint System Methods

  /// <summary>
  /// Reveals the next cell in the solution as a hint.
  /// Returns true if a hint was revealed, false if no more hints available.
  /// </summary>
  public void RevealNextHint(out bool areAllHintsRevealed)
  {
    (Cell, int) revealedCellDepth = Board.RevealNextHint(out areAllHintsRevealed);
    Cell cell = revealedCellDepth.Item1;
    int depth = revealedCellDepth.Item2;

    CellPrefab cellPrefab = GetCellPrefab(cell);
    SpriteRenderer revealedHint = cellPrefab.HintRenderers[depth];
    revealedHint.gameObject.SetActive(true);
    revealedHint.color = revealedHint.color.ToTransparent();

    revealedHints.Add(revealedHint);
    PulseHintPath();
  }

  /// <summary>
  /// Gets the current hint step (0-based index).
  /// </summary>
  public int CurrentHintStep => Board.CurrentHintStep;

  /// <summary>
  /// Checks if there are more hints available to reveal.
  /// </summary>
  public bool HasMoreHints => Board.HasMoreHints;

  private void PulseHintPath()
  {
    pulseSequence?.Kill();
    
    // Kill any existing tweens on the hint renderers to prevent conflicts
    foreach (SpriteRenderer hintRenderer in revealedHints)
    {
      hintRenderer.DOKill();
      // Reset to transparent state
      hintRenderer.color = hintRenderer.color.ToTransparent();
    }
    
    pulseSequence = DOTween.Sequence();

    float staggerDelay = 0.2f;  // Time between each hint starting to pulse
    float fadeDuration = 0.4f;  // Duration for fade in/out
    float pauseDuration = 0.3f; // Pause between fade in and fade out
    
    for (int i = 0; i < revealedHints.Count; i++)
    {
      SpriteRenderer hintRenderer = revealedHints[i];
      float startTime = i * staggerDelay;
      
      // Fade in
      pulseSequence.Insert(startTime, hintRenderer.DOFade(1f, fadeDuration).SetEase(Ease.OutSine));
      // Fade out (after fade in + pause)
      pulseSequence.Insert(startTime + fadeDuration + pauseDuration, hintRenderer.DOFade(0f, fadeDuration).SetEase(Ease.InSine));
    }

    // Add a delay at the end before looping to create a clear cycle
    pulseSequence.AppendInterval(0.5f); // Brief pause before restarting
    
    pulseSequence.SetLoops(-1);
    pulseSequence.Play();
  }
}