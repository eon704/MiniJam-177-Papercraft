using System.Collections.Generic;
using System.Collections.ObjectModel;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class CellPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] public List<GameObject> hintObjects;
    [SerializeField] private GameObject fire;
    [SerializeField] private GameObject water;
    [SerializeField] private GameObject stone;
    [SerializeField] private GameObject start;
    [SerializeField] private GameObject end;
    [SerializeField] private GameObject star;

    private float starDefaultScale;

    public ReadOnlyCollection<GameObject> HintObjects => hintObjects.AsReadOnly();

    public Cell Cell { get; private set; }
    private Player player;

    private Sequence rippleSequence;
    private Tween starGrowTween;
    private Tween starShrinkTween;

    public void Initialize(Cell cellData, Player newPlayer, float delay)
    {
        if (fire == null)  Debug.LogWarning($"[Cell {name}] fire is NULL");
        if (water == null) Debug.LogWarning($"[Cell {name}] water is NULL");
        if (stone == null) Debug.LogWarning($"[Cell {name}] stone is NULL");
        if (start == null) Debug.LogWarning($"[Cell {name}] start is NULL");
        if (end == null)   Debug.LogWarning($"[Cell {name}] end is NULL");
        if (star == null)  Debug.LogWarning($"[Cell {name}] star is NULL");

        hintObjects.ForEach(hint => hint.SetActive(false));

        Cell = cellData;
        gameObject.SetActive(Cell.Terrain != TerrainType.Empty);
        if (Cell.Terrain == TerrainType.Empty)
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

        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogError($"[Cell {name}] No Collider — clicks won't register! Add BoxCollider.");

        transform.localScale = Vector3.zero;
        rippleSequence = DOTween.Sequence();
        rippleSequence.AppendInterval(delay);
        rippleSequence.Append(transform.DOScale(1f, 0.5f).SetEase(Ease.OutQuad));
        rippleSequence.Play();
    }

    public void SetIsValidMoveOption(bool newIsValid) { }
    public void ResetIsValidMoveOption() { }

    public Sequence DoOutOfMovesPulse() => DOTween.Sequence();
    public Sequence DoPulse(float duration) => DOTween.Sequence();
    public Sequence DoPulse(float duration, Color color) => DOTween.Sequence();
    public void ResetPulse() { }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"[Cell {name}] OnPointerEnter  terrain={Cell?.Terrain}  pos={transform.position}");
        transform.DOScale(1.05f, 0.2f).SetEase(Ease.OutQuad);
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[Cell {name}] OnPointerExit");
        transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[Cell {name}] OnPointerDown");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"[Cell {name}] OnPointerUp → calling Player.Move");
        player.Move(this);
    }

    public void ShakeCell()
    {
        var shakeSequence = DOTween.Sequence();
        shakeSequence.AppendInterval(0.5f);
        shakeSequence.Append(transform.DOShakePosition(0.3f,
            strength: new Vector3(0.05f, 0f, 0.05f),
            vibrato: 20, randomness: 90, snapping: false, fadeOut: true));
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

    private void OnDestroy()
    {
        rippleSequence?.Kill();
        DOTween.Kill(this);
    }
}
