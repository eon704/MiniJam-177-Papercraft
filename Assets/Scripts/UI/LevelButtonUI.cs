using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text levelText;
    private int levelNumber;

    public void Initialize(int index, MainMenuUI mainMenuUI)
    {
        this.levelNumber = index;
        this.button.onClick.AddListener(mainMenuUI.StartGame);
    }
    
    private void Start()
    {
        this.button.onClick.AddListener(this.SetLevelIndex);
        this.levelText.text = this.levelNumber.ToString("D2");
        
        if (this.levelNumber > LevelManager.Instance.NextLevelIndex)
        {
            this.button.interactable = false;
        }
    }

    private void SetLevelIndex()
    {
        print("Setting level index to " + this.levelNumber);
        LevelManager.Instance.SetCurrentLevel(this.levelNumber);
    }
}