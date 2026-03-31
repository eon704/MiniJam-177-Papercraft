using System;
using DG.Tweening;
using UnityEngine;

public class VolcanoProjectile : MonoBehaviour
{
    [SerializeField] private float arcHeight = 3f;
    [SerializeField] private float duration = 1f;

    // Launch with custom arc parameters
    public void Launch(Vector3 from, Vector3 to, float overrideArcHeight, float overrideDuration, Action onLanded)
    {
        transform.position = from;

        Vector3 mid = Vector3.Lerp(from, to, 0.5f) + Vector3.up * overrideArcHeight;

        transform
            .DOPath(new[] { mid, to }, overrideDuration, PathType.CatmullRom)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                onLanded?.Invoke();
                Destroy(gameObject);
            });
    }

    // Launch with serialized defaults
    public void Launch(Vector3 from, Vector3 to, Action onLanded)
    {
        Launch(from, to, arcHeight, duration, onLanded);
    }
}
