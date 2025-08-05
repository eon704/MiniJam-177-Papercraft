using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PointerBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(originalScale * 1.1f, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(originalScale * 1.1f, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(originalScale * 1f, 0.2f).SetEase(Ease.OutQuad);
    }

    private void OnDestroy()
    {
        transform.DOKill(true);
    }
}
