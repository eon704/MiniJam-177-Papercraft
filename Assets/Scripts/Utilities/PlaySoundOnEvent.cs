using UnityEngine;
using UnityEngine.EventSystems;

public class PlaySoundOnEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{

    public void OnPointerEnter(PointerEventData eventData)
    {
       GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.3f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Move, 1f);
    }
    
}