using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Plays a "drop from above with bounce" entrance animation for a list of UI panels.
/// Automatically plays on OnEnable. Assign items top-to-bottom in the Inspector.
/// </summary>
public class DropInAnimator : MonoBehaviour
{
    [SerializeField] private List<RectTransform> items;

    [Header("Timing")]
    [SerializeField] private float dropDuration = 0.45f;
    [SerializeField] private float stagger = 0.08f;

    [Header("Drop Distance (pixels above rest position)")]
    [SerializeField] private float dropDistance = 220f;

    private readonly List<Vector2> _origins = new();
    private Sequence _sequence;

    private void Awake()
    {
        foreach (var item in items)
            _origins.Add(item.anchoredPosition);

        HideAll();
    }

    private void OnEnable()
    {
        HideAll();
        PlayEntrance();
    }

    private void OnDisable()
    {
        _sequence?.Kill();
    }

    private void HideAll()
    {
        for (int i = 0; i < items.Count; i++)
            items[i].anchoredPosition = _origins[i] + Vector2.up * dropDistance;
    }

    public void PlayEntrance()
    {
        _sequence?.Kill();
        _sequence = DOTween.Sequence();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var origin = _origins[i];
            _sequence.Insert(i * stagger,
                item.DOAnchorPos(origin, dropDuration).SetEase(Ease.OutBounce));
        }

        _sequence.Play();
    }

    /// <summary>Total duration of the full entrance animation.</summary>
    public float TotalDuration => (items.Count - 1) * stagger + dropDuration;
}
