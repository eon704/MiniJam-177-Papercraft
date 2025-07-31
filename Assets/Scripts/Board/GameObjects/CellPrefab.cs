using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class CellPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private SpriteRenderer hint1;
    [SerializeField] private SpriteRenderer hint2;
    [SerializeField] private SpriteRenderer hint3;
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
    private Sequence hintPulseSequence;
    
    private Tween starGrowTween;
    private Tween starShrinkTween;

    public void Initialize(Cell cellData, Player newPlayer, float delay)
    {
        fill.color = new Color(0, 0, 0, 0);
        hint1.gameObject.SetActive(false);
        hint2.gameObject.SetActive(false);
        hint3.gameObject.SetActive(false);

        Cell = cellData;
        gameObject.SetActive(Cell.Terrain != TerrainType.Empty);
        if (Cell.Terrain == TerrainType.Empty)
            return;
        
        Cell.Item.OnChanged += OnCellItemChange;
        Cell.IsHintRevealed.OnChanged += OnHintRevealedChange;

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

    private void OnHintRevealedChange(Observable<int> observable, int oldValue, int newValue)
    {
        if (newValue <= 0)
        {
            HideHints();
        }
        else
        {
            ShowHint(newValue);
        }
    }

    private void ShowHint(int order)
    {
        switch (order)
        {
            case 1:
                hint1.gameObject.SetActive(true);
                break;
            case 2:
                hint2.gameObject.SetActive(true);
                break;
            case 3:
                hint3.gameObject.SetActive(true);
                break;
        }
    }

    private void HideHints()
    {
        hint1.gameObject.SetActive(false);
        hint2.gameObject.SetActive(false);
        hint3.gameObject.SetActive(false);
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
        isPointerOver = false;

        fill.color = defaultColor;

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
        hintPulseSequence?.Kill();
        DOTween.Kill(this);
    }
}