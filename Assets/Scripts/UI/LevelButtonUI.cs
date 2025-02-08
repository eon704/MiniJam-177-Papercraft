using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelButtonUI : MonoBehaviour
{
    [SerializeField] private MainMenuUI mainMenuUI;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private int levelNumber;
    
    private void Start()
    {
        this.button.onClick.AddListener(this.SetLevelIndex);
        this.button.onClick.AddListener(this.mainMenuUI.StartGame);
        this.levelText.text = this.levelNumber.ToString("D2");
    }

    private void SetLevelIndex()
    {
        LevelManager.Instance.SetLevel(this.levelNumber);
    }
}