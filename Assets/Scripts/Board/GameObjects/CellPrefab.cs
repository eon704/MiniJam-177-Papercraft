using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class CellPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
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
    
    private Sequence pulseSequence;

    public void Initialize(Cell cellData, Player newPlayer)
    {
        this.Cell = cellData;
        this.player = newPlayer;

        this.start.SetActive(this.Cell.Terrain == Cell.TerrainType.Start);
        this.end.SetActive(this.Cell.Terrain == Cell.TerrainType.End);
        this.fire.SetActive(this.Cell.Terrain == Cell.TerrainType.Fire);
        this.water.SetActive(this.Cell.Terrain == Cell.TerrainType.Water);
        this.stone.SetActive(this.Cell.Terrain == Cell.TerrainType.Stone);
    }

    public Sequence DoPulse(float duration)
    {
        this.fill.color = this.defaultColor;
        
        pulseSequence = DOTween.Sequence();
        pulseSequence.Append(this.fill.DOColor(this.reachableColor, duration / 2).SetEase(Ease.OutSine));
        pulseSequence.Append(this.fill.DOColor(this.defaultColor, duration / 2).SetEase(Ease.InSine));
        
        return pulseSequence;
    }
    
    public void ResetPulse()
    {
        pulseSequence?.Kill();
        this.fill.color = this.defaultColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        this.fill.color = this.highlightColor;
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.05f);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        this.fill.color = this.defaultColor;
    }
    public void OnPointerDown(PointerEventData eventData) {}
    
    public void OnPointerUp(PointerEventData eventData)
    {
        this.player.Move(this);
    }
}
