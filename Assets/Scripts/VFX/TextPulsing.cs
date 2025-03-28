using UnityEngine;
using DG.Tweening;


public class TextPulsing : MonoBehaviour
{
    private void Start()
    {
        const float duration = 1f;
        transform.DOScale(new Vector3(1.1f, 1.1f, 1.1f), duration) // initiate scaling
            .SetLoops(-1, LoopType.Yoyo) // make it infinite
            .SetEase(Ease.Linear); // ensure the animation is linear
    }

    private void OnDisable()
    {
        transform.DOKill();
    }
}
