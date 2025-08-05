using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class HintButton : MonoBehaviour
{
    [SerializeField] private TMP_Text hintNumber;
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color revealedColor = Color.gray;

    private int hintIndex;
    private UnityAction originalOnClick;

    public void Initialize(int number, UnityAction onClick)
    {
        hintIndex = number;
        hintNumber.text = number.ToString();
        originalOnClick = onClick;
        button.onClick.AddListener(onClick);
        
        // Set initial visual state
        UpdateVisualState(false);
    }

    public void UpdateVisualState(bool isRevealed)
    {
        if (buttonImage != null)
        {
            buttonImage.color = isRevealed ? revealedColor : normalColor;
        }
        
        // Disable button if hint is already revealed
        button.interactable = !isRevealed;
    }

    public int GetHintIndex()
    {
        return hintIndex;
    }
}