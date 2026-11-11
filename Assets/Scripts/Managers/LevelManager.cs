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
    /// Playable indexes range: [1, LevelsCount - 1]. Level 0 is the debug level.
    /// </summary>
    public int CurrentLevelIndex { get; private set; }

    public LevelData CurrentLevel
    {
        get
        {
            if (CurrentLevelIndex >= levels.Count)
                return levels[levels.Count - 1];

            LevelData source = CurrentLevelIndex > 0 ? levels[CurrentLevelIndex] : LevelData.DefaultLevel;
            return Instantiate(source);
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if (Instance == this)
        {
            RecomputeNextLevelIndex();
            CurrentLevelIndex = NextLevelIndex;

#if UNITY_EDITOR
            if (forceLevelIndex >= 0)
                CurrentLevelIndex = forceLevelIndex;
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

    /// <summary>
    /// Stable id used in the save file: the LevelData asset name. Progress is stored by id, not by
    /// position, so reordering or replacing the level list never moves stars onto other levels.
    /// </summary>
    public string GetLevelId(int levelIndex)
    {
        if (levelIndex <= 0 || levelIndex >= levels.Count || levels[levelIndex] == null)
            return null;
        return levels[levelIndex].name;
    }

    public int GetLevelStars(int levelIndex)
    {
        return SaveSystem.Progress.GetStars(GetLevelId(levelIndex));
    }

    public void UnlockAllLevels()
    {
        SaveSystem.Progress.allUnlocked = true;
        RecomputeNextLevelIndex();
        SaveSystem.SaveNow();
    }

    public void ResetProgress()
    {
        SaveSystem.Progress.Reset();
        RecomputeNextLevelIndex();
        CurrentLevelIndex = 1;
        SaveSystem.SaveNow();
    }

    public void SetCurrentLevelComplete(int stars)
    {
        if (SaveSystem.Progress.Complete(GetLevelId(CurrentLevelIndex), stars))
            SaveSystem.SaveNow();
        RecomputeNextLevelIndex();
    }

    /// <summary>Levels unlock in order: the next level is the first one in the list not completed yet.</summary>
    private void RecomputeNextLevelIndex()
    {
        int next = levels.Count;
        for (int i = 1; i < levels.Count; i++)
        {
            if (!SaveSystem.Progress.IsCompleted(GetLevelId(i)))
            {
                next = i;
                break;
            }
        }

        if (SaveSystem.Progress.allUnlocked)
            next = Mathf.Max(next, levels.Count - 1);

        NextLevelIndex = Mathf.Max(1, next);
    }
}
