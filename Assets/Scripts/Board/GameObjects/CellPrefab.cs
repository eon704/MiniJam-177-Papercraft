using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class CellPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler
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

    [FormerlySerializedAs("reachableColor")] [SerializeField]
    private Color pulseColor;

    [SerializeField] private Color validMoveColor;
    [SerializeField] private Color invalidMoveColor;

    private bool isValid;
    private bool isPointerOver;

    public Cell Cell { get; private set; }
    private Player player;

    private Sequence pulseSequence;
    private Sequence rippleSequence;
    
    private Tween starGrowTween;
    private Tween starShrinkTween;

    public void Initialize(Cell cellData, Player newPlayer, float delay)
    {
        Cell = cellData;
        gameObject.SetActive(Cell.Terrain != TerrainType.None);
        if (Cell.Terrain == TerrainType.None)
            return;
        
        Cell.Item.OnChanged += OnCellItemChange;

        player = newPlayer;
        
        start.SetActive(Cell.Terrain == TerrainType.Start);
        end.SetActive(Cell.Terrain == TerrainType.End);
        fire.SetActive(Cell.Terrain == TerrainType.Fire);
        water.SetActive(Cell.Terrain == TerrainType.Water);
        stone.SetActive(Cell.Terrain == TerrainType.Stone);

        star.SetActive(Cell.Item == CellItem.Star);
        starDefaultScale = star.transform.localScale.x;
        
        transform.localScale = Vector3.zero;
        
        rippleSequence = DOTween.Sequence();
        rippleSequence.AppendInterval(delay);
        rippleSequence.Append(transform.DOScale(1f, 0.5f).SetEase(Ease.OutQuad));
        rippleSequence.Play();
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
        return DoPulse(0.5f, invalidMoveColor);
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
            {
                bool isPlayerOnCell = !Cell.IsFree;
                fill.color = isPlayerOnCell ? Color.white : isValid ? validMoveColor : invalidMoveColor;
            }
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

        bool isPlayerOnCell = !Cell.IsFree;
        fill.color = isPlayerOnCell ? Color.white : isValid ? validMoveColor : invalidMoveColor;
        
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.1f);
        transform.DOScale(1.05f, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
        fill.color = defaultColor;
        isPointerOver = false;

        if (pulseSequence == null)
            return;

        pulseSequence.Goto(Time.time);
        pulseSequence.Play();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        player.Move(this);
    }

    private void OnCellItemChange(Observable<CellItem> item, CellItem oldValue, CellItem newValue)
    {
        if (oldValue == CellItem.Star && newValue == CellItem.None)
        {
            starGrowTween?.Kill();
            starShrinkTween = star.transform.DOScale(Vector3.zero, 0.5f)
                .OnComplete(() => star.SetActive(false));
        }
        else if (oldValue == CellItem.None && newValue == CellItem.Star)
        {
            star.SetActive(true);
            starShrinkTween?.Kill();
            starGrowTween = star.transform.DOScale(Vector3.one * starDefaultScale, 0.5f);
        }
    }

    public void ShakeCell()
    {
        var cell = gameObject;
        var shakeSequence = DOTween.Sequence();
        shakeSequence.AppendInterval(0.5f); // Add a delay of 0.5 seconds
        shakeSequence.Append(cell.transform.DOShakePosition(0.3f, strength: new Vector3(0.05f, 0.05f, 0), vibrato: 20, randomness: 90, snapping: false, fadeOut: true));
    }

    private void OnDestroy()
    {
        rippleSequence?.Kill();
        pulseSequence?.Kill();
        DOTween.Kill(this);
    }
}