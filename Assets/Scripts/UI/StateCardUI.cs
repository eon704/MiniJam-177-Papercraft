using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class StateCardUI : MonoBehaviour
{
    [SerializeField] private Player.StateType stateType;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Player player;
    [SerializeField] private GameObject cardGlow; 
    
    private Button _button;
    private bool isSelected;

    private void Awake()
    {
        _button = GetComponent<Button>();   
    }

    private void Start()
    {
        cardGlow.SetActive(false);
        player.OnMovesLeftChanged.AddListener(OnMove);
        player.OnTransformation.AddListener(OnTransformation);
    }

    private void OnTransformation(Player.StateType newType)
    {
        cardGlow.SetActive(newType == stateType);
        isSelected = newType == stateType;
    }
    
    private void OnMove(Player.StateType moveStateType, int transformationsLeft)
    {
        if (moveStateType != this.stateType) 
            return;
        
        movesText.text = transformationsLeft.ToString("D2");
        _button.interactable = transformationsLeft > 0;
        cardGlow.SetActive(isSelected && transformationsLeft > 0);
    }
}