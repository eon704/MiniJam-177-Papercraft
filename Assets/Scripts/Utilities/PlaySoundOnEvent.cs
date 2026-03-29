using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using FMODUnity;

public class PlaySoundOnEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public bool isCard = false;
    private bool isPlayingSound = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isPlayingSound) return;
        isPlayingSound = true;
        var ev = isCard ? FMODEvents.Instance.card : FMODEvents.Instance.click;
        if (!ev.IsNull) RuntimeManager.PlayOneShot(ev);
        StartCoroutine(ResetSoundFlag());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!FMODEvents.Instance.click.IsNull) RuntimeManager.PlayOneShot(FMODEvents.Instance.ding);
    }

    private IEnumerator ResetSoundFlag()
    {
        yield return new WaitForSeconds(0.05f);
        isPlayingSound = false;
    }
}
