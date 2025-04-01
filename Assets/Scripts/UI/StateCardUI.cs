using DG.Tweening;
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
    private Image cardGlowImage;

    private void Awake()
    {
        _button = GetComponent<Button>();
        cardGlowImage = cardGlow.GetComponent<Image>();
    }

    private void Start()
    {
        cardGlow.SetActive(false);
        player.OnMovesLeftChanged.AddListener(OnMove);
        player.OnTransformation.AddListener(OnTransformation);
    }

    private void OnDestroy()
    {
        cardGlowImage.DOKill();
    }

    private void OnTransformation(Player.StateType newType)
    {
        isSelected = newType == stateType;
        
        cardGlow.SetActive(newType == stateType);
        
        cardGlowImage.DOKill();
        if (newType == stateType)
        {
            Color color = cardGlowImage.color;
            color.a = 0;
            cardGlowImage.color = color;
            cardGlowImage.DOFade(1, 0.5f).SetLoops(-1, LoopType.Yoyo);
        }
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