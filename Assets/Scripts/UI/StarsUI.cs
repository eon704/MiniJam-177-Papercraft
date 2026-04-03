using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class StarsUI : MonoBehaviour
{
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;

    public void OnStarChange(int newStar)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            bool wasFull = stars[i].sprite == fullStar;
            stars[i].sprite = i < newStar ? fullStar : emptyStar;

            if (!wasFull && i < newStar)
                AnimateCollected(stars[i]);
        }
    }

    private void AnimateCollected(Image star)
    {
        star.transform.DOKill();
        star.transform.localScale = Vector3.one;
        star.transform.localRotation = Quaternion.identity;

        var seq = DOTween.Sequence();
        seq.Append(star.transform.DOScale(1.35f, 0.1f).SetEase(Ease.OutQuad));
        seq.Append(star.transform.DORotate(new Vector3(0f, 0f, -720f), 0.5f, RotateMode.FastBeyond360).SetEase(Ease.InOutQuad));
        seq.Append(star.transform.DOScale(1f, 0.1f).SetEase(Ease.InQuad));
    }

    private void Start()
    {
        OnStarChange(0);
    }
}