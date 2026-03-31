using UnityEngine;
using DG.Tweening;

public class PulsingVFX : MonoBehaviour
{
    private void Start()
    {
        const float duration = 1f;
        transform.DOScale(new Vector3(0.8f, 0.8f, 0.8f), duration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.Linear);
    }

    private void OnDisable()
    {
        transform.DOKill();
    }
}
