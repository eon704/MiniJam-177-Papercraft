using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class PlaySoundOnEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public bool isCard = false;
    private bool isPlayingSound = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isPlayingSound)
        {
            isPlayingSound = true;
            if (isCard)
            {
                GlobalSoundManager.PlayRandomSoundByType(SoundType.Card, 0.6f);
            }
            else
            {
                GlobalSoundManager.PlayRandomSoundByType(SoundType.Click, 0.6f);
            }
            StartCoroutine(ResetSoundFlag());
        }
    }
 
    public void OnPointerClick(PointerEventData eventData)
    {
        GlobalSoundManager.PlayRandomSoundByType(SoundType.Move, 1f);
    }

    private IEnumerator ResetSoundFlag()
    {
        yield return new WaitForSeconds(0.05f); // Adjust the delay to match the sound duration
        isPlayingSound = false;
    }
}