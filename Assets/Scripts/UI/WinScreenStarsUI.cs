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
    
    public void AnimateStars(int totalStars)
    {
        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(0.2f);

        for (int i = 0; i < totalStars; i++)
        {
            Image star = this.stars[i]; 
            sequence.AppendCallback(() => star.sprite = this.fullStar);
            sequence.AppendInterval(0.5f);
        }

        sequence.OnComplete(() =>
        {
            for (int i = 0; i < totalStars; i++)
            {
                this.texts[i].enabled = true;
            }
        });

        sequence.Play();
    }

    private void Start()
    {
        foreach (Image star in this.stars)
        {
            star.sprite = this.emptyStar;
        }

        this.texts = this.stars.Select(star => star.GetComponent<TextPulsing>()).ToArray();
    }
}