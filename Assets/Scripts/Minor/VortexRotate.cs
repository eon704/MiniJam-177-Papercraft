using UnityEngine;
using DG.Tweening;

public class RotateSprite : MonoBehaviour
{
    private void Start()
    {
        const float duration = 2f;
        transform.DORotate(new Vector3(0, 0, -360), duration, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Incremental)
            .SetEase(Ease.Linear);
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
