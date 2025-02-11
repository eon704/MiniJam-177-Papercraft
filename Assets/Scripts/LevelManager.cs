using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
  [Header("Note: Level 0 is the debug level")]
  [SerializeField] private List<LevelData> levels;
  public int NextLevelIndex { get; private set; }
  public static LevelManager Instance { get; private set; }

  public int LevelsCount => this.levels.Count;
  
  /// <summary>
  /// Playables indexes range: [1, LevelsCount - 1].<br/>
  /// Level 0 is the debug level.
  /// </summary>
  public int CurrentLevelIndex { get; private set; }
  
  public LevelData CurrentLevel => this.CurrentLevelIndex > 0 ? this.levels[this.CurrentLevelIndex] : LevelData.DefaultLevel;
  
  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      this.NextLevelIndex = PlayerPrefs.GetInt("NextLevelIndex", 1);
      this.CurrentLevelIndex = this.NextLevelIndex;
      DontDestroyOnLoad(this.gameObject);
    }
    else
    {
      Destroy(this.gameObject); 
    }
  }

  public bool IsLastLevel()
  {
    return this.CurrentLevelIndex == this.levels.Count - 1;
  }

  public void SetCurrentLevel(int index)
  {
    this.CurrentLevelIndex = Mathf.Clamp(index, 1, this.levels.Count - 1);
  }

  public void PrepareNextLevel()
  {
    this.SetCurrentLevel(this.CurrentLevelIndex + 1);
  }

  public void SetCurrentLevelComplete()
  {
    this.NextLevelIndex++;
    PlayerPrefs.SetInt("NextLevelIndex", this.NextLevelIndex);
  }
}