using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StateCardUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Player.StateType stateType;
    [SerializeField] private Player player;

    [Header("Moves")]
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Image movesImage;
    [SerializeField] private Sprite movesActiveSprite;
    [SerializeField] private Sprite movesExhaustedSprite;

    [Header("Idle Rotation")]
    [SerializeField] private float idleAngle = 7f;
    [SerializeField] private float idleHalfPeriod = 1.4f;

    [Header("Rubber Band Hover")]
    [SerializeField] private float stretchDown = 14f;
    [SerializeField] private float stretchScale = 1.08f;
    [SerializeField] private float stretchDuration = 0.22f;
    [SerializeField] private float snapDuration = 0.55f;

    private Button _button;
    private RectTransform _rectTransform;
    private Vector2 _restPos;
    private Tween _scaleTween;
    private Tween _moveTween;
    private Tween _idleTween;
    private bool _stretchLocked; // true after click — no re-stretch until pointer exits

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rectTransform = GetComponent<RectTransform>();
        // Store BEFORE FormsUIAnimator (order 10) offsets cards
        _restPos = _rectTransform.anchoredPosition;
    }

    private void Start()
    {
        player.OnMovesLeftChanged.AddListener(OnMove);
        player.OnTransformation.AddListener(OnTransformation);
        StartIdleRotation();
    }

    // ── Moves ──────────────────────────────────────────────────────────────

    private void OnMove(Player.StateType moveStateType, int transformationsLeft)
    {
        if (moveStateType != stateType) return;
        if (movesText != null) movesText.text = transformationsLeft.ToString();
        if (_button != null) _button.interactable = transformationsLeft > 0;
        UpdateMovesSprite(transformationsLeft);
    }

    private void UpdateMovesSprite(int movesLeft)
    {
        if (movesImage == null) return;
        bool exhausted = movesLeft <= 0;
        if (exhausted && movesExhaustedSprite != null) movesImage.sprite = movesExhaustedSprite;
        else if (!exhausted && movesActiveSprite != null) movesImage.sprite = movesActiveSprite;
    }

    // ── Transformation ─────────────────────────────────────────────────────

    private void OnTransformation(Player.StateType activeStateType)
    {
        if (activeStateType != stateType) return;
        _scaleTween?.Kill();
        transform.localScale = Vector3.one;
        _scaleTween = DOTween.Sequence()
            .Append(transform.DOScale(1.18f, 0.12f).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(1f, 0.35f).SetEase(Ease.OutElastic));
    }

    // ── Pointer events ─────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_button.interactable || _stretchLocked) return;
        StretchDown();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _stretchLocked = false;
        SnapBack();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_button.interactable)
        {
            PlayErrorAnim();
            return;
        }
        _stretchLocked = true;
        SnapBackQuiet();
    }

    public void OnPointerUp(PointerEventData eventData) { }

    // ── Rubber band ────────────────────────────────────────────────────────

    private void StretchDown()
    {
        ResetToRest();
        _scaleTween = transform.DOScale(stretchScale, stretchDuration).SetEase(Ease.OutCubic);
        _moveTween  = _rectTransform.DOAnchorPos(_restPos + Vector2.down * stretchDown, stretchDuration)
                                    .SetEase(Ease.OutCubic);
    }

    // Hover exit — elastic snap with amortisation
    private void SnapBack()
    {
        _scaleTween?.Kill();
        _moveTween?.Kill();
        _scaleTween = transform.DOScale(1f, snapDuration).SetEase(Ease.OutElastic);
        _moveTween  = _rectTransform.DOAnchorPos(_restPos, snapDuration).SetEase(Ease.OutElastic);
    }

    // Click — quiet return, no bounce
    private void SnapBackQuiet()
    {
        _scaleTween?.Kill();
        _moveTween?.Kill();
        _scaleTween = transform.DOScale(1f, 0.18f).SetEase(Ease.OutQuad);
        _moveTween  = _rectTransform.DOAnchorPos(_restPos, 0.18f).SetEase(Ease.OutQuad);
    }

    // Kill all tweens and hard-reset transform to rest state
    private void ResetToRest()
    {
        _scaleTween?.Kill();
        _moveTween?.Kill();
        _scaleTween = null;
        _moveTween = null;
        transform.localScale = Vector3.one;
        _rectTransform.anchoredPosition = _restPos;
    }

    // ── Error ──────────────────────────────────────────────────────────────

    private void PlayErrorAnim()
    {
        DOTween.Kill(_rectTransform);
        _rectTransform.DOShakeAnchorPos(0.35f, new Vector2(12f, 0f), 22, 0, false, true);
    }

    // ── Idle rotation ───────────────────────────────────────────────────────

    private void StartIdleRotation()
    {
        _idleTween?.Kill();
        transform.localEulerAngles = Vector3.zero;
        _idleTween = transform.DOLocalRotate(new Vector3(0f, idleAngle, 0f), idleHalfPeriod)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void OnDestroy()
    {
        _scaleTween?.Kill();
        _moveTween?.Kill();
        _idleTween?.Kill();
    }
}
