using UnityEngine;
using DG.Tweening;

public class TextPulsing : MonoBehaviour
{
    private void OnEnable()
    {
        const float duration = 1f;
        transform.localScale = Vector3.one;
        transform.DOScale(new Vector3(1.05f, 1.05f, 1.05f), duration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.Linear);
    }

    private void OnDisable()
    {
        transform.DOKill();
    }
}
