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

    public void AnimateStars(int totalStars)
    {
        isWindowActive = true;
        var sequence = DOTween.Sequence();
        sequence.AppendInterval(1f);

        for (var i = 0; i < totalStars; i++)
        {
            var star = this.stars[i];
            sequence.AppendCallback(() =>
            {
                if (!isWindowActive) return;
                GlobalSoundManager.PlayRandomSoundByType(SoundType.Ding);
                star.sprite = this.fullStar;
                star.transform.DOScale(Vector3.one * 1.5f, 0.5f).SetLoops(2, LoopType.Yoyo);
            });
            sequence.AppendInterval(1f);
        }

        sequence.OnComplete(() =>
        {
            if (!isWindowActive) return;
            for (var i = 0; i < totalStars; i++)
            {
                this.texts[i].enabled = true;
            }
        });

        sequence.Play();
    }

    public void CloseWindow()
    {
        isWindowActive = false;
        // Additional logic to close the window
    }

    private void Start()
    {
        foreach (var star in this.stars)
        {
            star.sprite = this.emptyStar;
        }

        this.texts = this.stars.Select(star => star.GetComponent<TextPulsing>()).ToArray();
    }
    
    private void OnDestroy()
    {
        DOTween.Kill(this);
    }
}