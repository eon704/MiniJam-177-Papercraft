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
    private bool isWindowActive;
    
    Sequence initialStarSequence;

    public void AnimateStars(int totalStars)
    {
        isWindowActive = true;
        initialStarSequence = DOTween.Sequence();
        initialStarSequence.AppendInterval(1f);

        for (var i = 0; i < totalStars; i++)
        {
            var star = stars[i];
            initialStarSequence.AppendCallback(() =>
            {
                if (!isWindowActive) return;
                GlobalSoundManager.PlayRandomSoundByType(SoundType.Ding);
                star.sprite = fullStar;
                star.transform.DOScale(Vector3.one * 1.25f, 0.25f).SetLoops(2, LoopType.Yoyo);
            });
            initialStarSequence.AppendInterval(0.25f);
        }

        initialStarSequence.OnComplete(() =>
        {
            if (!isWindowActive) return;
            for (var i = 0; i < totalStars; i++)
            {
                texts[i].enabled = true;
            }
        });

        initialStarSequence.Play();
    }

    public void CloseWindow()
    {
        isWindowActive = false;
    }

    private void Start()
    {
        foreach (var star in stars)
        {
            star.sprite = emptyStar;
        }

        texts = stars.Select(star => star.GetComponent<TextPulsing>()).ToArray();
    }
    
    private void OnDestroy()
    {
        initialStarSequence?.Kill();
        
        foreach (var star in stars)
        {
            star.DOKill();
        }
    }
}