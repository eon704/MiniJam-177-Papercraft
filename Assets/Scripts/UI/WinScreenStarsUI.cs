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
    
    Sequence initialStarSequence;

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
                // UnityEngine.Object overrides == to check for destroyed objects
                if (star.Equals(null) || star.transform.Equals(null))
                    return;
                GlobalSoundManager.PlayRandomSoundByType(SoundType.Ding);
                star.sprite = fullStar;
                star.transform.DOScale(Vector3.one * 1.25f, 0.5f).SetLoops(2, LoopType.Yoyo);
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
                if (star.Equals(null) || star.transform.Equals(null))
                    continue;
                star.transform.DOKill();
                star.transform.localScale = Vector3.one;
                if (texts != null && i < texts.Length && texts[i] != null && !texts[i].Equals(null))
                    texts[i].enabled = true;
            }
        });

        initialStarSequence.Play();
    }

    private void OnEnable()
    {
        texts = stars.Select(star => star.GetComponent<TextPulsing>()).ToArray();
        
        foreach (var star in stars)
        {
            star.sprite = emptyStar;
        }

        foreach (var textPulsing in texts)
        {
            textPulsing.enabled = false;
        }
    }
    
    private void OnDisable()
    {
        initialStarSequence?.Kill();

        foreach (var star in stars)
        {
            if (star != null && star.transform != null && !star.Equals(null) && !star.transform.Equals(null))
            {
                star.transform.DOKill();
            }
        }
    }
}