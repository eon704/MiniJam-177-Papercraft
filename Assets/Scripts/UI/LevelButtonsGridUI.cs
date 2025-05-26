using UnityEngine;

public class LevelButtonsGridUI : MonoBehaviour
{
  [SerializeField] private MainMenuUI mainMenuUI;
  [SerializeField] private LevelButtonUI levelButtonPrefab;
  
  private void Start()
  {
    // Level 0 is the debug level, skip
    for (int i = 1; i < LevelManager.Instance.LevelsCount; i++)
    {
      LevelButtonUI levelButton = Instantiate(levelButtonPrefab, transform);
      levelButton.Initialize(i, mainMenuUI);
    }
  }
}