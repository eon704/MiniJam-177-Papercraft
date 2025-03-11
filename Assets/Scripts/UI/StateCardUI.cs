using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;


public class StateCardUI : MonoBehaviour
{
    [SerializeField] private GameObject cardGlow; 
    private Button _button;

    private void Start()
    {
        _button = GetComponent<Button>(); 
        cardGlow.SetActive(false);
        
    }
    private void Update()
    {
        if (!_button) return;
        if (_button.IsInteractable() && _button.gameObject == UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject)
        {
            cardGlow.SetActive(true);
        }
        else
        {
            cardGlow.SetActive(false);
        }
    }
}