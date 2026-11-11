using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything the game persists between launches. Serialized with JsonUtility into
/// <see cref="SaveSystem.SaveFilePath"/>. Bump <see cref="CurrentVersion"/> and add a step to
/// <see cref="SaveSystem"/> migration when the layout changes.
/// </summary>
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;

    /// <summary>True when this save was created by importing the old PlayerPrefs keys.</summary>
    public bool migratedFromPlayerPrefs;

    public ProgressData progress = new();
    public SettingsData settings = new();

    public void Sanitize()
    {
        progress ??= new ProgressData();
        settings ??= new SettingsData();
        progress.Sanitize();
        settings.Sanitize();
    }
}

[Serializable]
public class LevelProgress
{
    /// <summary>Stable level id: the LevelData asset name. Survives reordering of the level list.</summary>
    public string id;
    public bool completed;
    public int stars;
}

[Serializable]
public class ProgressData
{
    /// <summary>Per-level results, keyed by level id (not by list index).</summary>
    public List<LevelProgress> levels = new();

    /// <summary>Debug "unlock all levels".</summary>
    public bool allUnlocked;

    // Version 1 stored progress by list index. Kept only so old files deserialize; cleared on upgrade.
    public int nextLevelIndex;
    public List<int> levelStars = new();

    public LevelProgress Find(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return null;
        foreach (var entry in levels)
            if (entry != null && entry.id == levelId)
                return entry;
        return null;
    }

    public bool IsCompleted(string levelId) => Find(levelId)?.completed ?? false;

    public int GetStars(string levelId) => Find(levelId)?.stars ?? 0;

    /// <summary>Marks the level completed and keeps the best star count. Returns true when anything changed.</summary>
    public bool Complete(string levelId, int stars)
    {
        if (string.IsNullOrEmpty(levelId)) return false;
        stars = Mathf.Clamp(stars, 0, 3);

        var entry = Find(levelId);
        if (entry == null)
        {
            levels.Add(new LevelProgress { id = levelId, completed = true, stars = stars });
            return true;
        }

        bool changed = !entry.completed || stars > entry.stars;
        entry.completed = true;
        entry.stars = Mathf.Max(entry.stars, stars);
        return changed;
    }

    public void Reset()
    {
        levels.Clear();
        allUnlocked = false;
    }

    public void ClearLegacy()
    {
        nextLevelIndex = 0;
        levelStars?.Clear();
    }

    public void Sanitize()
    {
        levels ??= new List<LevelProgress>();
        levels.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));
        foreach (var e in levels)
            e.stars = Mathf.Clamp(e.stars, 0, 3);
    }
}

[Serializable]
public class SettingsData
{
    public float sfxVolume = 1f;
    public float musicVolume = 1f;

    /// <summary>-1 means "not chosen yet": pick the locale from the system language.</summary>
    public int localeIndex = -1;

    /// <summary>0 means "not chosen yet": use the monitor's native resolution.</summary>
    public int resolutionWidth;
    public int resolutionHeight;

    /// <summary>Stored as int so the JSON stays readable; see <see cref="FullScreenMode"/>.</summary>
    public int fullScreenMode = (int)FullScreenMode.FullScreenWindow;

    public bool vSync = true;

    /// <summary>Frame cap used when VSync is off. 0 means unlimited.</summary>
    public int frameRateLimit;

    /// <summary>-1 means "not chosen yet": keep the project's default quality level.</summary>
    public int qualityLevel = -1;

    public FullScreenMode FullScreenMode
    {
        get => (FullScreenMode)fullScreenMode;
        set => fullScreenMode = (int)value;
    }

    public void Sanitize()
    {
        sfxVolume = Mathf.Clamp01(sfxVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        if (localeIndex < -1) localeIndex = -1;
        if (resolutionWidth < 0 || resolutionHeight < 0)
            resolutionWidth = resolutionHeight = 0;
        if (!Enum.IsDefined(typeof(FullScreenMode), fullScreenMode))
            fullScreenMode = (int)FullScreenMode.FullScreenWindow;
        if (frameRateLimit < 0) frameRateLimit = 0;
        if (qualityLevel < -1) qualityLevel = -1;
    }
}
