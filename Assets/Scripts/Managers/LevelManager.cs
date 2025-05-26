using System.Collections.Generic;
using UnityEngine;

public class LevelManager : Singleton<LevelManager>
{
  [Header("Testing tools")]
  [Tooltip("Disabled if index is -1 or less.")]
  [SerializeField] private int forceLevelIndex = -1;
  
  [Header("Note: Level 0 is the debug level.")]
  [SerializeField] private List<LevelData> levels;
  public int NextLevelIndex { get; private set; }

  public int LevelsCount => levels.Count;
  
  /// <summary>
  /// Playables indexes range: [1, LevelsCount - 1].<br/>
  /// Level 0 is the debug level.
  /// </summary>
  public int CurrentLevelIndex { get; private set; }
  
  public LevelData CurrentLevel
  {
    get
    {
      if (CurrentLevelIndex >= levels.Count)
        return levels[levels.Count - 1];
      
      return CurrentLevelIndex > 0 ? levels[CurrentLevelIndex] : LevelData.DefaultLevel;
    }
  }

  protected override void Awake()
  {
    base.Awake();
    
    if (Instance == this)
    {
      NextLevelIndex = PlayerPrefs.GetInt("NextLevelIndex", 1);
      CurrentLevelIndex = NextLevelIndex;
      
      #if UNITY_EDITOR
      if (forceLevelIndex >= 0)
      {
        CurrentLevelIndex = forceLevelIndex;
      }
      #endif
        
      DontDestroyOnLoad(gameObject);
    }
    else
    {
      Destroy(gameObject); 
    }
  }

  public bool IsLastLevel()
  {
    return CurrentLevelIndex == levels.Count - 1;
  }

  public void SetCurrentLevel(int index)
  {
    CurrentLevelIndex = Mathf.Clamp(index, 1, levels.Count - 1);
  }

  public void PrepareNextLevel()
  {
    SetCurrentLevel(CurrentLevelIndex + 1);
  }

  public int GetLevelStars(int levelIndex)
  {
    string key = "level" + levelIndex + "_stars";
    return PlayerPrefs.GetInt(key, 0);
  }

  public void SetCurrentLevelComplete(int stars)
  {
    string key = "level" + Instance.CurrentLevelIndex + "_stars";
    int bestStars = PlayerPrefs.GetInt(key, 0);
    if (stars > bestStars)
    {
      PlayerPrefs.SetInt(key, stars);
    }
    
    if (CurrentLevelIndex + 1 <= NextLevelIndex)
      return;
    
    NextLevelIndex = CurrentLevelIndex + 1;
    PlayerPrefs.SetInt("NextLevelIndex", NextLevelIndex);
  }
}