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
    [SerializeField] private Transform volcanoTop;
    [SerializeField] private GameObject explosionEffect;

    public Transform VolcanoTop => volcanoTop;

    // World position where projectile should land (top surface of cell)
    public Vector3 ExplosionWorldPosition => lava != null
        ? lava.transform.position
        : transform.position;
    [SerializeField] private SpriteRenderer highlightSprite;

    private static readonly Color ColorBlue   = new Color(0.2f, 0.6f, 1.0f, 0.7f);
    private static readonly Color ColorGreen  = new Color(0.1f, 0.9f, 0.3f, 0.85f);
    private static readonly Color ColorRed    = new Color(1.0f, 0.2f, 0.2f, 0.85f);
    private static readonly Color ColorOrange = new Color(1.0f, 0.6f, 0.0f, 0.8f);

    private bool _isReachable;
    private bool _isHovered;
    private Tween _pulseTween;

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

        if (highlightSprite != null) highlightSprite.enabled = false;

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

    public void SetIsValidMoveOption(bool isValid)
    {
        _isReachable = isValid;
    }

    public void ResetIsValidMoveOption()
    {
        _isReachable = false;
    }

    public Sequence DoOutOfMovesPulse()
    {
        if (highlightSprite == null) return DOTween.Sequence();
        Sequence trigger = DOTween.Sequence();
        trigger.AppendCallback(() => StartLocalPulse(ColorOrange, 0.5f));
        trigger.AppendInterval(0.5f);
        return trigger;
    }

    public Sequence DoPulse(float duration) => DoPulse(duration, ColorBlue);

    public Sequence DoPulse(float duration, Color color)
    {
        if (highlightSprite == null) return DOTween.Sequence();
        Sequence trigger = DOTween.Sequence();
        trigger.AppendCallback(() => StartLocalPulse(color, duration * 0.7f));
        trigger.AppendInterval(duration * 0.7f);
        return trigger;
    }

    public void ResetPulse()
    {
        if (highlightSprite == null) return;
        _isHovered = false;
        _pulseTween?.Kill();
        _pulseTween = null;
        highlightSprite.enabled = false;
    }

    private void StartLocalPulse(Color color, float duration)
    {
        if (_isHovered) return;
        _pulseTween?.Kill();
        highlightSprite.enabled = true;
        highlightSprite.color = new Color(color.r, color.g, color.b, color.a);
        _pulseTween = highlightSprite
            .DOFade(color.a * 0.1f, duration * 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!FMODEvents.Instance.click.IsNull) FMODUnity.RuntimeManager.PlayOneShot(FMODEvents.Instance.click);

        if (highlightSprite == null || player == null || player.isMovementLocked) return;

        _isHovered = true;
        _pulseTween?.Kill();
        _pulseTween = null;
        highlightSprite.color = _isReachable ? ColorGreen : ColorRed;
        highlightSprite.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightSprite == null) return;

        _isHovered = false;

        if (_isReachable)
        {
            highlightSprite.color = ColorBlue;
            highlightSprite.enabled = true;
        }
        else
        {
            highlightSprite.enabled = false;
        }
    }

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

    [SerializeField] private float explosionDuration = 1f;

    // Called by GameController when the projectile lands on this cell
    public void ActivateExplosion(System.Action onComplete)
    {
        if (explosionEffect != null)
            explosionEffect.SetActive(true);

        DOVirtual.DelayedCall(explosionDuration, () =>
        {
            if (explosionEffect != null)
                explosionEffect.SetActive(false);
            onComplete?.Invoke();
        });
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
