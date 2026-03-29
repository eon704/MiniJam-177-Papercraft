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
    [SerializeField] private GameObject volcano;
    [SerializeField] private GameObject lava;
    [SerializeField] private GameObject ice;

    private float starDefaultScale;
    private Vector3 _originalLocalPosition;

    public ReadOnlyCollection<GameObject> HintObjects => hintObjects.AsReadOnly();

    public Cell Cell { get; private set; }
    private Player player;

    private Sequence rippleSequence;
    private Sequence collapseSequence;
    private Tween starGrowTween;
    private Tween starShrinkTween;

    public void Initialize(Cell cellData, Player newPlayer, float delay)
    {
        hintObjects.ForEach(hint => hint.SetActive(false));

        Cell = cellData;
        gameObject.SetActive(Cell.Terrain != TerrainType.Empty);
        if (Cell.Terrain == TerrainType.Empty)
            return;

        _originalLocalPosition = transform.localPosition;

        Cell.Item.OnChanged += OnCellItemChange;
        player = newPlayer;

        if (Cell.IsFragile)
        {
            Cell.OnCollapsed += OnCellCollapsed;
            Cell.OnCollapsedInstant += OnCellCollapsedInstant;
            Cell.OnCollapseReset += OnCellCollapseReset;
        }

        Cell.OnTerrainChanged += HandleTerrainChanged;

        ApplyTerrainVisuals(Cell.Terrain);

        star.SetActive(Cell.Item == CellItem.Star);
        starDefaultScale = star.transform.localScale.x;

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
        if (!FMODEvents.Instance.click.IsNull) FMODUnity.RuntimeManager.PlayOneShot(FMODEvents.Instance.click);
    }

    public void OnPointerExit(PointerEventData eventData) { }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
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

    private void ApplyTerrainVisuals(TerrainType terrain)
    {
        if (start != null) start.SetActive(terrain == TerrainType.Start);
        if (end != null) end.SetActive(terrain == TerrainType.End);
        // If a dedicated lava object is assigned use it; otherwise fall back to fire visuals
        bool isLava = terrain == TerrainType.Lava;
        if (lava != null)
        {
            lava.SetActive(isLava);
            if (fire != null) fire.SetActive(terrain == TerrainType.Fire);
        }
        else
        {
            if (fire != null) fire.SetActive(terrain == TerrainType.Fire || isLava);
        }
        if (water != null) water.SetActive(terrain == TerrainType.Water);
        if (stone != null) stone.SetActive(terrain == TerrainType.Stone);
        if (volcano != null) volcano.SetActive(terrain == TerrainType.Volcano);
        if (ice != null) ice.SetActive(terrain == TerrainType.Ice);
    }

    private void HandleTerrainChanged(TerrainType oldTerrain, TerrainType newTerrain)
    {
        ApplyTerrainVisuals(newTerrain);
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

    private void OnCellCollapsed()
    {
        collapseSequence?.Kill();
        collapseSequence = DOTween.Sequence();
        // Небольшое дрожание — клетка "трещит" перед падением
        collapseSequence.Append(transform.DOShakePosition(0.25f,
            strength: new Vector3(0.08f, 0.04f, 0.08f),
            vibrato: 18, randomness: 60, snapping: false, fadeOut: true));
        // Пауза на краю
        collapseSequence.AppendInterval(0.1f);
        // Падение вниз + уменьшение + лёгкий завал
        collapseSequence.Append(transform.DOLocalMoveY(_originalLocalPosition.y - 3.5f, 0.7f).SetEase(Ease.InCubic));
        collapseSequence.Join(transform.DOScale(Vector3.zero, 0.65f).SetEase(Ease.InQuad));
        collapseSequence.Join(transform.DOLocalRotate(
            new Vector3(Random.Range(-25f, 25f), Random.Range(-20f, 20f), Random.Range(-25f, 25f)),
            0.65f, RotateMode.LocalAxisAdd).SetEase(Ease.InCubic));
    }

    private void OnCellCollapsedInstant()
    {
        collapseSequence?.Kill();
        collapseSequence = null;
        transform.localScale = Vector3.zero;
    }

    private void OnCellCollapseReset()
    {
        collapseSequence?.Kill();
        collapseSequence = null;
        DOTween.Kill(transform);
        transform.localRotation = Quaternion.identity;
        transform.localPosition = _originalLocalPosition;
        transform.localScale = Vector3.zero;
        collapseSequence = DOTween.Sequence();
        collapseSequence.Append(transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
    }

    private void OnDestroy()
    {
        if (Cell != null)
        {
            Cell.OnTerrainChanged -= HandleTerrainChanged;
            if (Cell.IsFragile)
            {
                Cell.OnCollapsed -= OnCellCollapsed;
                Cell.OnCollapsedInstant -= OnCellCollapsedInstant;
                Cell.OnCollapseReset -= OnCellCollapseReset;
            }
        }
        rippleSequence?.Kill();
        collapseSequence?.Kill();
        DOTween.Kill(this);
    }
}
