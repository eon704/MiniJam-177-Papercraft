using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class CellPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private GameObject fire;
    [SerializeField] private GameObject water;
    [SerializeField] private GameObject stone;
    [SerializeField] private GameObject start;
    [SerializeField] private GameObject end;
    
    [SerializeField] private GameObject star;
    private float starDefaultScale;
    
    [SerializeField] private Color defaultColor;
    [FormerlySerializedAs("reachableColor")] [SerializeField] private Color pulseColor;
    [SerializeField] private Color validMoveColor;
    [SerializeField] private Color invalidMoveColor;
    
    private bool isValid;
    private bool isPointerOver;
    
    public Cell Cell { get; private set; }
    private Player player;
    
    private Sequence pulseSequence;

    public void Initialize(Cell cellData, Player newPlayer)
    {
        this.Cell = cellData;
        this.Cell.Item.OnChanged += this.OnCellItemChange;
        
        this.player = newPlayer;

        this.gameObject.SetActive(this.Cell.Terrain != Cell.TerrainType.None);
        
        this.start.SetActive(this.Cell.Terrain == Cell.TerrainType.Start);
        this.end.SetActive(this.Cell.Terrain == Cell.TerrainType.End);
        this.fire.SetActive(this.Cell.Terrain == Cell.TerrainType.Fire);
        this.water.SetActive(this.Cell.Terrain == Cell.TerrainType.Water);
        this.stone.SetActive(this.Cell.Terrain == Cell.TerrainType.Stone);
        
        this.star.SetActive(this.Cell.Item == Cell.CellItem.Star);
        this.starDefaultScale = this.star.transform.localScale.x;
    }
    
    public void SetIsValidMoveOption(bool newIsValid)
    {
        isValid = newIsValid;
    }

    public void ResetIsValidMoveOption()
    {
        isValid = false;
    }

    public Sequence DoOutOfMovesPulse()
    {
        return this.DoPulse(0.5f, invalidMoveColor);
    }

    public Sequence DoPulse(float duration)
    {
        return DoPulse(duration, pulseColor);
    }

    public Sequence DoPulse(float duration, Color color)
    {
        fill.color = defaultColor;
            
        pulseSequence = DOTween.Sequence();
        pulseSequence.Append(fill.DOColor(color, duration / 2).SetEase(Ease.OutSine));
        pulseSequence.Append(fill.DOColor(defaultColor, duration / 2).SetEase(Ease.InSine));
        pulseSequence.OnUpdate(() =>
        {
            if (isPointerOver)
                fill.color = isValid ? validMoveColor : invalidMoveColor;
        });
        
        return pulseSequence;
    }
    
    public void ResetPulse()
    {
        pulseSequence?.Kill(true);
        fill.color = defaultColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pulseSequence?.Pause();
        isPointerOver = true;
        fill.color = isValid ? validMoveColor : invalidMoveColor;
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.05f);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        fill.color = defaultColor;
        isPointerOver = false;
        
        if (pulseSequence == null)
            return;
        
        pulseSequence.Goto(Time.time);
        pulseSequence.Play();
    }
    public void OnPointerDown(PointerEventData eventData) {}
    
    public void OnPointerUp(PointerEventData eventData)
    {
        player.Move(this);
    }

    private void OnCellItemChange(Observable<Cell.CellItem> item, Cell.CellItem oldValue, Cell.CellItem newValue)
    {
        if (oldValue == Cell.CellItem.Star && newValue == Cell.CellItem.None)
        {
            star.transform
                .DOScale(Vector3.zero, 0.5f)
                .OnComplete(() => this.star.SetActive(false));
        } else if (oldValue == Cell.CellItem.None && newValue == Cell.CellItem.Star)
        {
            star.SetActive(true);
            star.transform.DOScale(Vector3.one * this.starDefaultScale, 0.5f);
        }
    }

    private void OnDestroy()
    {
        pulseSequence.Kill();
    }
}
