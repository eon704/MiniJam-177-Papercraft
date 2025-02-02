using System;
using UnityEngine;

public class CellPrefab : MonoBehaviour
{
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private GameObject fire;
    [SerializeField] private GameObject water;
    [SerializeField] private GameObject stone;
    
    // [Header("Default Colors")]
    // [SerializeField] private Color defaultColor;

    public Cell Cell { get; private set; }
    private Player player;

    public void Initialize(Cell cellData, Player player)
    {
        this.Cell = cellData;
        this.player = player;
        
        this.fire.SetActive(this.Cell.Terrain == Cell.TerrainType.Fire);
        this.water.SetActive(this.Cell.Terrain == Cell.TerrainType.Water);
        this.stone.SetActive(this.Cell.Terrain == Cell.TerrainType.Stone);
    }

    private void OnMouseUp()
    {
        this.player.BoardPiecePrefab.Move(this);
    }

    private void OnMouseEnter()
    {
        // this.fill.color = this.defaultColor + new Color(0.2f, 0.2f, 0.2f);
    }

    private void OnMouseExit()
    {
        // this.fill.color = this.defaultColor;
    }
}