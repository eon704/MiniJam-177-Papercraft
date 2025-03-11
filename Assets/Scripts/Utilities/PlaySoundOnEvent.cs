using UnityEngine;
using UnityEngine.EventSystems;

public class PlaySoundOnEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
 public bool isCard = false;
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isCard)
        {
            GlobalSoundManager.PlayRandomSoundByType(SoundType.Card, 1f);
        }
        else
        {
            GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.3f);
        }
      
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Move, 1f);
    }
    
}