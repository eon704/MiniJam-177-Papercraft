using System;
using UnityEngine;
using DG.Tweening;

public class RotateSprite : MonoBehaviour
{
    private void Start()
    {
        const float duration = 2f;
        transform.DORotate(new Vector3(0, 0, -360), duration, RotateMode.FastBeyond360) // initiate rotation
            .SetLoops(-1, LoopType.Incremental) // make it infinite
            .SetEase(Ease.Linear); // ensure the animation is linear
    }

    private void OnDestroy()
    {
        transform.DOKill(); // kill the tween once this object is destroyed on scene change
    }
}