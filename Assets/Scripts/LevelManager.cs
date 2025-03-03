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

  public int LevelsCount => this.levels.Count;
  
  /// <summary>
  /// Playables indexes range: [1, LevelsCount - 1].<br/>
  /// Level 0 is the debug level.
  /// </summary>
  public int CurrentLevelIndex { get; private set; }
  
  public LevelData CurrentLevel
  {
    get
    {
      if (this.CurrentLevelIndex >= this.levels.Count)
        return this.levels[this.levels.Count - 1];
      
      return this.CurrentLevelIndex > 0 ? this.levels[this.CurrentLevelIndex] : LevelData.DefaultLevel;
    }
  }

  protected override void Awake()
  {
    base.Awake();
    
    if (Instance == this)
    {
      this.NextLevelIndex = PlayerPrefs.GetInt("NextLevelIndex", 1);
      this.CurrentLevelIndex = this.NextLevelIndex;
      
      #if UNITY_EDITOR
      if (this.forceLevelIndex >= 0)
      {
        this.CurrentLevelIndex = this.forceLevelIndex;
      }
      #endif
        
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
    if (this.CurrentLevelIndex + 1 <= this.NextLevelIndex)
      return;
    
    this.NextLevelIndex = this.CurrentLevelIndex + 1;
    PlayerPrefs.SetInt("NextLevelIndex", this.NextLevelIndex);
  }
}