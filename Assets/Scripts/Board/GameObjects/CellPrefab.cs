using UnityEngine;

public class CellPrefab : MonoBehaviour
{
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color highlightColor;

    public Cell Cell { get; private set; }
    private Player player;

    public void Initialize(Cell cellData, Player player)
    {
        this.Cell = cellData;
        this.player = player;
    }

    private void OnMouseUp()
    {
        this.player.BoardPiecePrefab.Move(this);
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