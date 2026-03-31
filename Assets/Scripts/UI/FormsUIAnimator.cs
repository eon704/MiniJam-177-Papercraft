using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class FormsUIAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardPrefab boardPrefab;

    [Header("Panels (top to bottom drop order)")]
    [SerializeField] private RectTransform infoPanel;
    [SerializeField] private RectTransform previewPanel;
    [SerializeField] private List<RectTransform> formCards; // L to R order

    [Header("Entrance Timing")]
    [SerializeField] private float delayAfterBoardSpawn = 0.2f;
    [SerializeField] private float panelDropDuration = 0.55f;
    [SerializeField] private float cardDropDuration = 0.45f;
    [SerializeField] private float cardStagger = 0.1f;
    [SerializeField] private float previewDelay = 0.15f;

    [Header("Drop Distance (pixels above rest position)")]
    [SerializeField] private float dropDistance = 220f;

    private Vector2 _infoPanelOrigin;
    private Vector2 _previewOrigin;
    private readonly List<Vector2> _cardOrigins = new();

    private void Awake()
    {
        _infoPanelOrigin = infoPanel.anchoredPosition;
        _previewOrigin = previewPanel.anchoredPosition;
        foreach (var card in formCards)
            _cardOrigins.Add(card.anchoredPosition);

        HideAll();
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => boardPrefab.IsSpawnAnimationComplete);
        yield return new WaitForSeconds(delayAfterBoardSpawn);
        PlayEntrance();
    }

    private void HideAll()
    {
        infoPanel.anchoredPosition = _infoPanelOrigin + Vector2.up * dropDistance;
        previewPanel.anchoredPosition = _previewOrigin + Vector2.up * dropDistance;
        for (int i = 0; i < formCards.Count; i++)
            formCards[i].anchoredPosition = _cardOrigins[i] + Vector2.up * dropDistance;
    }

    public void PlayEntrance()
    {
        var seq = DOTween.Sequence();

        // 1. InfoPanel falls first — "hanging sign" bounce
        seq.Append(
            infoPanel.DOAnchorPos(_infoPanelOrigin, panelDropDuration)
                     .SetEase(Ease.OutBounce));

        // 2. Cards fall L to R, overlapping with panel settle
        float cardsStart = panelDropDuration * 0.45f;
        for (int i = 0; i < formCards.Count; i++)
        {
            var card = formCards[i];
            var origin = _cardOrigins[i];
            seq.Insert(cardsStart + i * cardStagger,
                card.DOAnchorPos(origin, cardDropDuration).SetEase(Ease.OutBounce));
        }

        // 3. Preview falls after last card settles
        float lastCardEnd = cardsStart + (formCards.Count - 1) * cardStagger + cardDropDuration * 0.55f;
        seq.Insert(lastCardEnd + previewDelay,
            previewPanel.DOAnchorPos(_previewOrigin, panelDropDuration).SetEase(Ease.OutBounce));
    }
}
