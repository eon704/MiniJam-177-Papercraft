using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PulseImage : MonoBehaviour
{
  private Image border;
  Sequence borderSequence;
  
  private void OnEnable()
  {
    border = GetComponent<Image>();
    borderSequence = DOTween.Sequence();
    borderSequence.Append(border.DOFade(1f, 1f).SetEase(Ease.InCubic));
    borderSequence.Append(border.DOFade(0f, 1f).SetEase(Ease.OutCubic));
    borderSequence.SetLoops(-1);
    borderSequence.Play();
  }

  private void OnDisable()
  {
    borderSequence?.Kill();
  }
}