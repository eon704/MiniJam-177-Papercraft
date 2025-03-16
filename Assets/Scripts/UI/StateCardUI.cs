using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class StateCardUI : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Player.StateType stateType;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Player player;
    [SerializeField] private GameObject cardGlow; 
    
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();   
    }

    private void Start()
    {
        cardGlow.SetActive(false);
        player.OnMovesLeftChanged.AddListener(this.OnMove);
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
    
    private void OnMove(Player.StateType moveStateType, int transformationsLeft)
    {
        if (moveStateType != this.stateType) 
            return;
        
        movesText.text = transformationsLeft.ToString("D2");
        _button.interactable = transformationsLeft > 0;
    }
}