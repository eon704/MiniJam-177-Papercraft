using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class WinScreenStarsUI : MonoBehaviour
{
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;

    private TextPulsing[] texts;

    private Sequence initialStarSequence;

    public void AnimateStars(int totalStars)
    {
        initialStarSequence = DOTween.Sequence();
        initialStarSequence.AppendInterval(0.5f);

        for (var i = 0; i < totalStars; i++)
        {
            var star = stars[i];
            initialStarSequence.AppendCallback(() =>
            {
                if (star == null || star.transform == null)
                    return;
                star.sprite = fullStar;
                var seq = DOTween.Sequence();
                seq.Append(star.transform.DOScale(1.35f, 0.1f).SetEase(Ease.OutQuad));
                seq.Append(star.transform.DORotate(new Vector3(0f, 0f, -720f), 0.5f, RotateMode.FastBeyond360).SetEase(Ease.InOutQuad));
                seq.Append(star.transform.DOScale(1f, 0.1f).SetEase(Ease.InQuad));
                FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxStarReveal);
            });
            initialStarSequence.AppendInterval(0.5f);
        }

        initialStarSequence.AppendInterval(0.5f);
        initialStarSequence.OnComplete(() =>
        {
            for (var i = 0; i < totalStars; i++)
            {
                var star = stars[i];
                if (star == null || star.transform == null)
                    continue;
                star.transform.DOKill();
                star.transform.localScale = Vector3.one;
                if (texts != null && i < texts.Length && texts[i] != null)
                    texts[i].enabled = true;
            }
        });

        initialStarSequence.Play();
    }

    private void OnEnable()
    {
        texts = stars.Select(star => star.GetComponent<TextPulsing>()).ToArray();

        foreach (var star in stars)
            star.sprite = emptyStar;

        foreach (var textPulsing in texts)
            textPulsing.enabled = false;
    }

    private void OnDisable()
    {
        initialStarSequence?.Kill();

        foreach (var star in stars)
        {
            if (star != null && star.transform != null)
                star.transform.DOKill();
        }
    }
}
