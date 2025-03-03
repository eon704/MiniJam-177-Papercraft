using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private List<Image> starImages;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;
    
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

        int stars = LevelManager.Instance.GetLevelStars(this.levelNumber);

        for (int i = 0; i < this.starImages.Count; i++)
        {
            this.starImages[i].sprite = i + 1 <= stars ? this.fullStar : this.emptyStar;
        }
    }

    private void SetLevelIndex()
    {
        LevelManager.Instance.SetCurrentLevel(this.levelNumber);
    }
}