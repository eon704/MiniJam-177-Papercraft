using System.Collections.Generic;
using System.Collections.ObjectModel;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class CellPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] public List<GameObject> hintObjects;
    [SerializeField] private GameObject mainModel;
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
    [SerializeField] private GameObject splash;
    [SerializeField] private GameObject fireSplash;
    [SerializeField] private ParticleSystem fragileStepDust;
    [SerializeField] private SpriteRenderer volcanoBorderSprite;
    [SerializeField] private CellFragmentGroup fragmentGroup;

    public Transform VolcanoTop => volcanoTop;

    // World position where projectile should land (top surface of cell)
    public Vector3 ExplosionWorldPosition => lava != null
        ? lava.transform.position
        : transform.position;
    [SerializeField] private SpriteRenderer highlightSprite;

    private static readonly Color ColorBlue    = new Color(0.2f, 0.6f, 1.0f, 0.7f);
    private static readonly Color ColorGreen   = new Color(0.1f, 0.9f, 0.3f, 0.85f);
    private static readonly Color ColorRed     = new Color(1.0f, 0.2f, 0.2f, 0.85f);
    private static readonly Color ColorOrange  = new Color(1.0f, 0.6f, 0.0f, 0.8f);
    private static readonly Color ColorWarning = new Color(1.0f, 0.25f, 0.0f, 1.0f);
    private static readonly Color ColorPath    = new Color(0.9f, 0.85f, 0.2f, 0.65f);

    // Matches VolcanoArcPreview CoreNormal/CoreUrgent colours and PulsePeriod
    private static readonly Color BorderNormal = new Color(1.00f, 0.60f, 0.00f, 1.00f);
    private static readonly Color BorderUrgent = new Color(1.00f, 0.15f, 0.00f, 1.00f);
    private const float BorderHalfPeriod = 0.325f; // half of arc PulsePeriod (0.65f)

    private bool _isReachable;
    private bool _isHovered;
    private bool _isPathPreview;
    private bool _isCollapsed;
    private int  _volcanoCountdown = 1;
    private bool _starPendingVisual;
    private System.Action _onStarCollected;
    private bool _hasVolcanoWarning;
    private Tween _pulseTween;
    private Tween _borderPulseTween;

    private float starDefaultScale;
    private Vector3 _originalLocalPosition;

    public ReadOnlyCollection<GameObject> HintObjects => hintObjects.AsReadOnly();

    public Cell Cell { get; private set; }
    private Player player;

    /// <summary>World position the cell will occupy when fully spawned (ignores spawn animation offset).</summary>
    public Vector3 FinalWorldPosition => transform.parent != null
        ? transform.parent.TransformPoint(_originalLocalPosition)
        : _originalLocalPosition;

    private Sequence rippleSequence;
    private Sequence collapseSequence;
    private bool _collapseVisualDeferred;
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
        if (volcanoBorderSprite != null) volcanoBorderSprite.enabled = false;

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

        if (Cell.IsFragile && fragmentGroup != null)
        {
            mainModel.SetActive(false);
            fragmentGroup.gameObject.SetActive(true);
        }

        star.SetActive(Cell.Item == CellItem.Star);
        starDefaultScale = star.transform.localScale.x;

        transform.localPosition = _originalLocalPosition + Vector3.down * 3f;
        rippleSequence = DOTween.Sequence();
        rippleSequence.AppendInterval(delay);
        rippleSequence.Append(transform.DOLocalMoveY(_originalLocalPosition.y, 1.6f).SetEase(Ease.OutBack));
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

    public void SetVolcanoWarning(bool active, int countdown = 1)
    {
        _hasVolcanoWarning = active;
        _volcanoCountdown = countdown;
        if (active)
        {
            if (!_isHovered) StartWarningPulse();
        }
        else
        {
            DisableVolcanoBorder();
            if (!_isHovered && !_isReachable && !_isPathPreview)
            {
                _pulseTween?.Kill();
                _pulseTween = null;
                if (highlightSprite != null) highlightSprite.enabled = false;
            }
        }
    }

    /// <summary>Call after the arc line has been drawn to this cell.</summary>
    public void EnableVolcanoBorder(int countdown)
    {
        if (volcanoBorderSprite == null) return;
        bool urgent    = countdown <= 1;
        Color bright   = urgent ? BorderUrgent : BorderNormal;
        float minAlpha = urgent ? 0.10f : 0.15f;
        _borderPulseTween?.Kill();
        volcanoBorderSprite.enabled = true;
        volcanoBorderSprite.color = bright;
        _borderPulseTween = volcanoBorderSprite
            .DOFade(minAlpha, BorderHalfPeriod)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void DisableVolcanoBorder()
    {
        if (volcanoBorderSprite == null) return;
        _borderPulseTween?.Kill();
        _borderPulseTween = null;
        volcanoBorderSprite.enabled = false;
    }

    public void SetIsPathPreview(bool isPreview)
    {
        _isPathPreview = isPreview;
        if (highlightSprite == null || _isHovered || _isReachable) return;

        if (isPreview)
        {
            _pulseTween?.Kill();
            highlightSprite.enabled = true;
            highlightSprite.color = ColorPath;
            _pulseTween = highlightSprite
                .DOFade(0.1f, 0.3f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            _pulseTween?.Kill();
            _pulseTween = null;
            if (_hasVolcanoWarning)
                StartWarningPulse();
            else
                highlightSprite.enabled = false;
        }
    }

    public void ResetPulse()
    {
        _isHovered = false;
        _isPathPreview = false;
        _pulseTween?.Kill();
        _pulseTween = null;
        if (_hasVolcanoWarning)
        {
            if (highlightSprite != null) StartWarningPulse();
            // border is managed externally — don't touch it here
        }
        else
        {
            if (highlightSprite != null) highlightSprite.enabled = false;
        }
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

    private void StartWarningPulse()
    {
        if (_isHovered) return;
        // Faster pulse = fewer moves until eruption; smoother fade range
        float speed   = _volcanoCountdown <= 1 ? 0.38f : _volcanoCountdown == 2 ? 0.58f : 0.80f;
        float minAlpha = _volcanoCountdown <= 1 ? 0.30f : 0.42f;
        _pulseTween?.Kill();
        highlightSprite.enabled = true;
        highlightSprite.color = ColorWarning;
        _pulseTween = highlightSprite
            .DOFade(minAlpha, speed)
            .SetEase(Ease.InOutQuad)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isCollapsed || highlightSprite == null || player == null || player.isMovementLocked) return;

        _isHovered = true;
        _pulseTween?.Kill();
        _pulseTween = null;
        highlightSprite.color = _isReachable ? ColorGreen : ColorRed;
        highlightSprite.enabled = true;

        player.ShowHoverPath(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightSprite == null) return;

        _isHovered = false;
        player?.HideHoverPath();

        if (_isReachable)
        {
            highlightSprite.color = ColorBlue;
            highlightSprite.enabled = true;
        }
        else if (_isPathPreview)
        {
            SetIsPathPreview(true);
        }
        else if (_hasVolcanoWarning)
        {
            StartWarningPulse();
        }
        else
        {
            highlightSprite.enabled = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isCollapsed) return;
        player.Move(this);
    }

    public void ShakeCell(float initialDelay = 0.5f)
    {
        var shakeSequence = DOTween.Sequence();
        shakeSequence.AppendInterval(initialDelay);
        if (Cell.IsFragile)
        {
            shakeSequence.AppendCallback(() =>
            {
                if (fragileStepDust != null)
                {
                    fragileStepDust.gameObject.SetActive(true);
                    fragileStepDust.Play();
                    DOVirtual.DelayedCall(fragileStepDust.main.duration, () =>
                    {
                        if (fragileStepDust != null) fragileStepDust.gameObject.SetActive(false);
                    });
                }
            });
            shakeSequence.Append(transform.DOShakePosition(0.6f,
                strength: new Vector3(0.09f, 0f, 0.09f),
                vibrato: 20, randomness: 90, snapping: false, fadeOut: true));
        }
        else
            shakeSequence.Append(transform.DOShakePosition(0.3f,
                strength: new Vector3(0.05f, 0f, 0.05f),
                vibrato: 20, randomness: 90, snapping: false, fadeOut: true));
    }

    private void ApplyTerrainVisuals(TerrainType terrain)
    {
        bool isWater = terrain == TerrainType.Water;
        if (mainModel != null) mainModel.SetActive(!isWater);
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
        // Если клетка обрушена и использует фрагменты — terrain вернётся в OnCellCollapseReset,
        // иначе получим спрайт поверх рассыпавшейся колонны
        if (fragmentGroup != null && Cell.IsCollapsed) return;
        ApplyTerrainVisuals(newTerrain);
    }

    public void SetStarVisualDeferred(System.Action onCollected = null)
    {
        _starPendingVisual = true;
        _onStarCollected = onCollected;
    }

    public void ResetStarVisualDeferred()
    {
        _starPendingVisual = false;
        _onStarCollected = null;
    }

    public void TriggerStarPickupVisual()
    {
        if (!_starPendingVisual) return;
        _starPendingVisual = false;
        _onStarCollected?.Invoke();
        _onStarCollected = null;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxStarPickUp);
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxPop);
        starGrowTween?.Kill();
        starShrinkTween = star.transform.DOScale(Vector3.zero, 0.5f)
            .OnComplete(() => star.SetActive(false));
    }

    private void OnCellItemChange(Observable<CellItem> item, CellItem oldValue, CellItem newValue)
    {
        if (oldValue == CellItem.Star && newValue == CellItem.None)
        {
            if (_starPendingVisual) return;
            FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxPop);
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

    public void PlayStepDust()
    {
        if (fragileStepDust == null) return;
        if (Cell.Terrain != TerrainType.Default && Cell.Terrain != TerrainType.Stone && Cell.Terrain != TerrainType.Start) return;
        fragileStepDust.gameObject.SetActive(true);
        fragileStepDust.Play();
        DOVirtual.DelayedCall(fragileStepDust.main.duration, () =>
        {
            if (fragileStepDust != null) fragileStepDust.gameObject.SetActive(false);
        });
    }

    public void DeferCollapseVisual() => _collapseVisualDeferred = true;

    public void TriggerDeferredCollapseVisual()
    {
        if (!_collapseVisualDeferred) return;
        _collapseVisualDeferred = false;
        OnCellCollapsed();
    }

    private void OnCellCollapsed()
    {
        if (_collapseVisualDeferred) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxCellCollapse);
        collapseSequence?.Kill();
        collapseSequence = DOTween.Sequence();
        // Небольшое дрожание — клетка "трещит" перед падением
        collapseSequence.Append(transform.DOShakePosition(0.25f,
            strength: new Vector3(0.08f, 0.04f, 0.08f),
            vibrato: 18, randomness: 60, snapping: false, fadeOut: true));
        // Пауза на краю
        collapseSequence.AppendInterval(0.1f);
        if (fragmentGroup != null)
        {
            // Скрыть все оверлеи — transform не скалируется, поэтому прячем вручную
            collapseSequence.AppendCallback(() =>
            {
                _isCollapsed = true;
                if (_isHovered) { _isHovered = false; player?.HideHoverPath(); }
                _pulseTween?.Kill();
                _pulseTween = null;
                _borderPulseTween?.Kill();
                _borderPulseTween = null;
                if (highlightSprite != null)     highlightSprite.enabled     = false;
                if (volcanoBorderSprite != null)  volcanoBorderSprite.enabled  = false;
                if (star != null)    star.SetActive(false);
                if (fire != null)    fire.SetActive(false);
                if (water != null)   water.SetActive(false);
                if (stone != null)   stone.SetActive(false);
                if (lava != null)    lava.SetActive(false);
                if (ice != null)     ice.SetActive(false);
                if (volcano != null) volcano.SetActive(false);
                if (start != null)   start.SetActive(false);
                if (end != null)     end.SetActive(false);
                fragmentGroup.PlayCollapse();
            });
        }
        else
        {
            // Падение вниз + уменьшение + лёгкий завал
            collapseSequence.Append(transform.DOLocalMoveY(_originalLocalPosition.y - 3.5f, 0.7f).SetEase(Ease.InCubic));
            collapseSequence.Join(transform.DOScale(Vector3.zero, 0.65f).SetEase(Ease.InQuad));
            collapseSequence.Join(transform.DOLocalRotate(
                new Vector3(Random.Range(-25f, 25f), Random.Range(-20f, 20f), Random.Range(-25f, 25f)),
                0.65f, RotateMode.LocalAxisAdd).SetEase(Ease.InCubic));
        }
    }

    private void OnCellCollapsedInstant()
    {
        _isCollapsed = true;
        if (_isHovered) { _isHovered = false; player?.HideHoverPath(); }
        collapseSequence?.Kill();
        collapseSequence = null;
        if (fragmentGroup != null)
            fragmentGroup.PlayCollapse();
        else
            transform.localScale = Vector3.zero;
        // _collapseDelays при instant тоже сохранятся внутри PlayCollapse — undo отработает корректно
    }

    private void OnCellCollapseReset()
    {
        _isCollapsed = false;
        collapseSequence?.Kill();
        collapseSequence = null;
        DOTween.Kill(transform);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        transform.localPosition = _originalLocalPosition + Vector3.down * 3f;
        if (fragmentGroup != null)
        {
            transform.localPosition = _originalLocalPosition;
            // terrain показываем только после того как фрагменты долетели на место
            fragmentGroup.ResetFragments(animated: true, onComplete: () =>
            {
                ApplyTerrainVisuals(Cell.Terrain);
                mainModel.SetActive(false);
            });
        }
        else
        {
            collapseSequence = DOTween.Sequence();
            collapseSequence.Append(transform.DOLocalMoveY(_originalLocalPosition.y, 0.3f).SetEase(Ease.OutBack));
        }
    }

    [SerializeField] private float explosionDuration = 1f;
    [SerializeField] private float fireSplashDuration = 1.5f;

    
    public void ActivateSplash()
    {
        if (splash != null)
            splash.SetActive(true);
    }

    public void ActivateFireSplash()
    {
        if (fireSplash == null) return;
        fireSplash.SetActive(true);
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxFireSplash);
        DOVirtual.DelayedCall(fireSplashDuration, () =>
        {
            if (fireSplash != null)
                fireSplash.SetActive(false);
        });
    }

    // Called by GameController when the projectile lands on this cell
    public void ActivateExplosion(System.Action onComplete)
    {
        if (explosionEffect != null)
        {
            explosionEffect.SetActive(true);
            FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxExplosion);
        }

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
        _borderPulseTween?.Kill();
        rippleSequence?.Kill();
        collapseSequence?.Kill();
        DOTween.Kill(this);
    }
}
