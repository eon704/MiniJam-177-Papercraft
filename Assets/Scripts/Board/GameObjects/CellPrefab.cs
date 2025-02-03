using UnityEngine;

public class CellPrefab : MonoBehaviour
{
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private GameObject fire;
    [SerializeField] private GameObject water;
    [SerializeField] private GameObject stone;
    [SerializeField] private GameObject start;
    [SerializeField] private GameObject end;
    
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color highlightColor;
    [SerializeField] private Color reachableColor;
    
    private bool isReachable;
    
    public Cell Cell { get; private set; }
    private Player player;

    public void Initialize(Cell cellData, Player player)
    {
        this.Cell = cellData;
        this.player = player;

        this.start.SetActive(this.Cell.Terrain == Cell.TerrainType.Start);
        this.end.SetActive(this.Cell.Terrain == Cell.TerrainType.End);
        this.fire.SetActive(this.Cell.Terrain == Cell.TerrainType.Fire);
        this.water.SetActive(this.Cell.Terrain == Cell.TerrainType.Water);
        this.stone.SetActive(this.Cell.Terrain == Cell.TerrainType.Stone);
    }

    private void OnMouseUp()
    {
        this.player.Move(this);
    }

    private void OnMouseEnter()
    {
        this.fill.color = this.highlightColor;
    }

    private void OnMouseExit()
    {
        this.fill.color = this.defaultColor;
    }
}