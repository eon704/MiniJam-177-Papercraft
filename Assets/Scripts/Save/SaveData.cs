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
    public const int CurrentVersion = 1;

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
public class ProgressData
{
    /// <summary>First level that is not completed yet. Level 0 is the debug level, so playable levels start at 1.</summary>
    public int nextLevelIndex = 1;

    /// <summary>Best star count per level, indexed by level index (index 0 is unused).</summary>
    public List<int> levelStars = new();

    public int GetStars(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levelStars.Count)
            return 0;
        return levelStars[levelIndex];
    }

    /// <summary>Stores the star count if it beats the previous best. Returns true when the value changed.</summary>
    public bool SetBestStars(int levelIndex, int stars)
    {
        if (levelIndex < 0)
            return false;

        while (levelStars.Count <= levelIndex)
            levelStars.Add(0);

        if (stars <= levelStars[levelIndex])
            return false;

        levelStars[levelIndex] = stars;
        return true;
    }

    public void Reset()
    {
        nextLevelIndex = 1;
        levelStars.Clear();
    }

    public void Sanitize()
    {
        levelStars ??= new List<int>();
        if (nextLevelIndex < 1)
            nextLevelIndex = 1;
        for (int i = 0; i < levelStars.Count; i++)
            levelStars[i] = Mathf.Clamp(levelStars[i], 0, 3);
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
