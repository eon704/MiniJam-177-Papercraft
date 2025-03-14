using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class StateCardUI : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject cardGlow; 
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();   
    }

    private void Start()
    {
        cardGlow.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_button.interactable)
            return;
        
        cardGlow.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        cardGlow.SetActive(false);
    }
}