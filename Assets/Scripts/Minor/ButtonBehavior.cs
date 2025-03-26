using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ButtonBehavior : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOScale(1.1f, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
    }
    
    private void OnDestroy()
    {
        DOTween.Kill(this);
    }
}
