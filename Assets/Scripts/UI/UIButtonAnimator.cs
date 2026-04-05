using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonAnimator : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressScale = 0.92f;
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float pressDuration = 0.1f;

    private Vector3 _originalScale;
    private Button _button;
    private bool _isPressed;
    private bool _isHovered;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_button.interactable) return;
        _isHovered = true;
        if (_isPressed) return;
        AnimateTo(_originalScale * hoverScale, hoverDuration, Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
        AnimateTo(_originalScale, hoverDuration, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_button.interactable) return;
        _isPressed = true;
        AnimateTo(_originalScale * pressScale, pressDuration, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        var targetScale = _isHovered ? _originalScale * hoverScale : _originalScale;
        AnimateTo(targetScale, pressDuration, Ease.OutBack);
    }

    private void AnimateTo(Vector3 target, float duration, Ease ease)
    {
        DOTween.Kill(transform);
        transform.DOScale(target, duration).SetEase(ease);
    }

    private void OnDisable()
    {
        _isPressed = false;
        _isHovered = false;
        DOTween.Kill(transform);
        transform.localScale = _originalScale;
    }

    private void OnDestroy()
    {
        DOTween.Kill(transform);
    }
}
