using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
  [SerializeField] private List<LevelData> levels;
  public int NextLevelIndex { get; private set; }
  public static LevelManager Instance { get; private set; }

  public LevelData CurrentLevel => this.currentLevel ?? LevelData.DefaultLevel;
  private LevelData currentLevel;
  
  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      this.NextLevelIndex = PlayerPrefs.GetInt("NextLevelIndex", 0);
      this.SetLevel(this.NextLevelIndex);
    }
    else
    {
      Destroy(this.gameObject); 
    }
  }

  private void OnDestroy()
  {
    Instance = null;
  }

  public bool IsLastLevel()
  {
    return this.NextLevelIndex >= this.levels.Count - 1;
  }

  public void SetLevel(int index)
  {
    index = Mathf.Clamp(index, 0, this.levels.Count - 1);
    this.currentLevel = this.levels[index];
  }

  public void SetCurrentLevelComplete()
  {
    this.NextLevelIndex++;
    PlayerPrefs.SetInt("NextLevelIndex", this.NextLevelIndex);
  }
}