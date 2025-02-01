using UnityEngine;

public class CellPrefab : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Cell cell;

    public void Initialize(Cell cellData)
    {
        this.cell = cellData;
    }
}