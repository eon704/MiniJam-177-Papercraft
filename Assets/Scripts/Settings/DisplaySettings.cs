using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolution, window mode, VSync, frame cap and quality level for the PC build.
/// Values live in <see cref="SaveSystem.Settings"/>; this class only lists options and applies them.
/// Applied once before the first scene loads, then on every change from the settings menu.
/// </summary>
public static class DisplaySettings
{
    /// <summary>Window modes offered in the menu, in display order.</summary>
    public static readonly FullScreenMode[] WindowModes =
    {
        FullScreenMode.FullScreenWindow,
#if UNITY_STANDALONE_WIN
        FullScreenMode.ExclusiveFullScreen,
#endif
        FullScreenMode.Windowed,
    };

    /// <summary>Frame caps offered when VSync is off. 0 = unlimited.</summary>
    public static readonly int[] FrameRateLimits = { 30, 60, 120, 144, 165, 240, 0 };

    private static List<Vector2Int> _resolutions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _resolutions = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        ApplyQuality();
        ApplyFrameRate();
#if !UNITY_EDITOR
        ApplyResolution();
#endif
    }

    /// <summary>Distinct resolutions supported by the current monitor, largest first.</summary>
    public static IReadOnlyList<Vector2Int> Resolutions
    {
        get
        {
            if (_resolutions != null)
                return _resolutions;

            var set = new HashSet<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
                set.Add(new Vector2Int(r.width, r.height));

            if (set.Count == 0)
                set.Add(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));

            _resolutions = new List<Vector2Int>(set);
            _resolutions.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return _resolutions;
        }
    }

    public static Vector2Int NativeResolution
    {
        get
        {
            var res = Resolutions;
            return res.Count > 0 ? res[0] : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        }
    }

    /// <summary>Resolution the game uses: the saved one if the monitor supports it, else native.</summary>
    public static Vector2Int CurrentResolution
    {
        get
        {
            var s = SaveSystem.Settings;
            var saved = new Vector2Int(s.resolutionWidth, s.resolutionHeight);
            if (saved.x > 0 && saved.y > 0)
            {
                foreach (var r in Resolutions)
                    if (r == saved)
                        return saved;
            }
            return NativeResolution;
        }
    }

    public static int CurrentResolutionIndex
    {
        get
        {
            var current = CurrentResolution;
            var res = Resolutions;
            for (int i = 0; i < res.Count; i++)
                if (res[i] == current)
                    return i;
            return 0;
        }
    }

    public static int CurrentWindowModeIndex
    {
        get
        {
            int i = System.Array.IndexOf(WindowModes, SaveSystem.Settings.FullScreenMode);
            return i >= 0 ? i : 0;
        }
    }

    public static int CurrentFrameRateLimitIndex
    {
        get
        {
            int i = System.Array.IndexOf(FrameRateLimits, SaveSystem.Settings.frameRateLimit);
            return i >= 0 ? i : FrameRateLimits.Length - 1;
        }
    }

    public static int CurrentQualityLevel
    {
        get
        {
            int saved = SaveSystem.Settings.qualityLevel;
            return saved >= 0 && saved < QualitySettings.names.Length ? saved : QualitySettings.GetQualityLevel();
        }
    }

    public static void SetResolution(int index)
    {
        var res = Resolutions;
        if (index < 0 || index >= res.Count) return;

        SaveSystem.Settings.resolutionWidth = res[index].x;
        SaveSystem.Settings.resolutionHeight = res[index].y;
        SaveSystem.RequestSave();
        ApplyResolution();
    }

    public static void SetWindowMode(int index)
    {
        if (index < 0 || index >= WindowModes.Length) return;

        SaveSystem.Settings.FullScreenMode = WindowModes[index];
        SaveSystem.RequestSave();
        ApplyResolution();
    }

    public static void SetVSync(bool enabled)
    {
        SaveSystem.Settings.vSync = enabled;
        SaveSystem.RequestSave();
        ApplyFrameRate();
    }

    public static void SetFrameRateLimit(int index)
    {
        if (index < 0 || index >= FrameRateLimits.Length) return;

        SaveSystem.Settings.frameRateLimit = FrameRateLimits[index];
        SaveSystem.RequestSave();
        ApplyFrameRate();
    }

    public static void SetQualityLevel(int level)
    {
        if (level < 0 || level >= QualitySettings.names.Length) return;

        SaveSystem.Settings.qualityLevel = level;
        SaveSystem.RequestSave();
        ApplyQuality();
        // Quality levels carry their own VSync value; re-apply ours on top.
        ApplyFrameRate();
    }

    public static void ApplyResolution()
    {
        Vector2Int r = CurrentResolution;
        FullScreenMode mode = SaveSystem.Settings.FullScreenMode;
#if !UNITY_STANDALONE_WIN
        if (mode == FullScreenMode.ExclusiveFullScreen)
            mode = FullScreenMode.FullScreenWindow;
#endif
        Screen.SetResolution(r.x, r.y, mode);
    }

    public static void ApplyFrameRate()
    {
        var s = SaveSystem.Settings;
        QualitySettings.vSyncCount = s.vSync ? 1 : 0;
        Application.targetFrameRate = s.vSync || s.frameRateLimit <= 0 ? -1 : s.frameRateLimit;
    }

    public static void ApplyQuality()
    {
        int saved = SaveSystem.Settings.qualityLevel;
        if (saved >= 0 && saved < QualitySettings.names.Length && saved != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(saved, applyExpensiveChanges: true);
    }
}
