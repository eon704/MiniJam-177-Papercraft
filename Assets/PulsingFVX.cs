using UnityEngine;
using DG.Tweening;

public class PulsingFVX : MonoBehaviour
{
    private void Start()
    {
        const float duration = 1f;
        transform.DOScale(new Vector3(0.8f, 0.8f, 0.8f), duration) // initiate scaling
            .SetLoops(-1, LoopType.Yoyo) // make it infinite
            .SetEase(Ease.Linear); // ensure the animation is linear
    }
}
