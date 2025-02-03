using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PulseImage : MonoBehaviour
{
  private Image border;
  Sequence borderSequence;
  
  private void OnEnable()
  {
    this.border = this.GetComponent<Image>();
    this.borderSequence = DOTween.Sequence();
    this.borderSequence.Append(this.border.DOFade(1f, 1f).SetEase(Ease.InCubic));
    this.borderSequence.Append(this.border.DOFade(0f, 1f).SetEase(Ease.OutCubic));
    this.borderSequence.SetLoops(-1);
    this.borderSequence.Play();
  }

  private void OnDisable()
  {
    this.borderSequence?.Kill();
  }
}