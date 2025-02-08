using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class TitleWaveUI : MonoBehaviour
{
  [SerializeField] private List<RectTransform> icons;
  
  private void Start()
  {
    Sequence sequence = DOTween.Sequence();
    
    sequence.AppendInterval(2f);
    foreach (RectTransform icon in this.icons)
    {
      sequence.Append(icon.DOScale(1.1f, 0.25f).SetEase(Ease.InSine));
      sequence.Append(icon.DOScale(1f, 0.25f).SetEase(Ease.OutSine));
    }

    sequence.SetLoops(-1);
    sequence.Play();
  }
}