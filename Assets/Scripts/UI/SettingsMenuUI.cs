using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider soundtrackVolumeSlider;

    [Header("Language")]
    [SerializeField] private TMP_Dropdown localeDropdown;

    // Display settings (PC). All optional: the menu works without them until they are added to SettingsUI.prefab.
    [Header("Display (optional)")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown windowModeDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private TMP_Dropdown frameRateDropdown;

    private void Awake()
    {
        localeDropdown.ClearOptions();
        localeDropdown.AddOptions(new List<string> { "English", "Русский", "Қазақша", "中文" });
        localeDropdown.onValueChanged.AddListener(OnLocaleChanged);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(DisplaySettings.SetResolution);
        if (windowModeDropdown != null) windowModeDropdown.onValueChanged.AddListener(DisplaySettings.SetWindowMode);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(DisplaySettings.SetQualityLevel);
        if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        if (frameRateDropdown != null) frameRateDropdown.onValueChanged.AddListener(DisplaySettings.SetFrameRateLimit);
    }

    private void OnEnable()
    {
        sfxVolumeSlider.value = SettingsManager.Instance.SFXVolume;
        soundtrackVolumeSlider.value = SettingsManager.Instance.SoundtrackVolume;
        localeDropdown.SetValueWithoutNotify(LocalizationManager.Instance.CurrentLocaleIndex);

        RefreshDisplayControls();
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        SaveSystem.FlushIfDirty();
    }

    private void OnDestroy()
    {
        localeDropdown.onValueChanged.RemoveListener(OnLocaleChanged);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(DisplaySettings.SetResolution);
        if (windowModeDropdown != null) windowModeDropdown.onValueChanged.RemoveListener(DisplaySettings.SetWindowMode);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveListener(DisplaySettings.SetQualityLevel);
        if (vSyncToggle != null) vSyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
        if (frameRateDropdown != null) frameRateDropdown.onValueChanged.RemoveListener(DisplaySettings.SetFrameRateLimit);
    }

    public void OnSFXVolumeChanged(float volume)
    {
        SettingsManager.Instance.SetSFXVolume(volume);
    }

    public void OnSoundtrackVolumeChanged(float volume)
    {
        SettingsManager.Instance.SetSoundtrackVolume(volume);
    }

    private void OnLocaleChanged(int index)
    {
        LocalizationManager.Instance.ChangeLocale((LocalizationManager.Locale)index);
    }

    private void OnSelectedLocaleChanged(Locale _) => RefreshDisplayControls();

    private void OnVSyncChanged(bool enabled)
    {
        DisplaySettings.SetVSync(enabled);
        if (frameRateDropdown != null)
            frameRateDropdown.interactable = !enabled;
    }

    private void RefreshDisplayControls()
    {
        if (resolutionDropdown != null)
        {
            var options = new List<string>();
            foreach (Vector2Int r in DisplaySettings.Resolutions)
                options.Add($"{r.x} x {r.y}");
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(DisplaySettings.CurrentResolutionIndex);
        }

        if (windowModeDropdown != null)
        {
            var options = new List<string>();
            foreach (FullScreenMode mode in DisplaySettings.WindowModes)
                options.Add(SettingsLabels.WindowMode(mode));
            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(options);
            windowModeDropdown.SetValueWithoutNotify(DisplaySettings.CurrentWindowModeIndex);
        }

        if (qualityDropdown != null)
        {
            var options = new List<string>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
                options.Add(SettingsLabels.Quality(i, QualitySettings.names[i]));
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(options);
            qualityDropdown.SetValueWithoutNotify(DisplaySettings.CurrentQualityLevel);
        }

        bool vSync = SaveSystem.Settings.vSync;
        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(vSync);

        if (frameRateDropdown != null)
        {
            var options = new List<string>();
            foreach (int limit in DisplaySettings.FrameRateLimits)
                options.Add(limit > 0 ? limit.ToString() : SettingsLabels.Unlimited());
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(options);
            frameRateDropdown.SetValueWithoutNotify(DisplaySettings.CurrentFrameRateLimitIndex);
            frameRateDropdown.interactable = !vSync;
        }
    }
}

/// <summary>
/// Strings for the PC settings rows and generated dropdown options.
/// Looked up in the "Paperbound Localization Table" string table; the inline translations below are
/// the source for Tools → Localization → Add PC settings strings (editor) and the fallback when a key
/// is missing. Order matches LocalizationManager.Locale: en, ru, kk, zh-Hans.
/// </summary>
public static class SettingsLabels
{
    public const string TableName = "Paperbound Localization Table";

    public const string KeyResolution = "Settings.Resolution";
    public const string KeyWindowMode = "Settings.WindowMode";
    public const string KeyQuality = "Settings.Quality";
    public const string KeyFrameCap = "Settings.FrameCap";
    public const string KeyVSync = "Settings.VSync";
    public const string KeyBorderless = "Settings.WindowMode.Borderless";
    public const string KeyFullscreen = "Settings.WindowMode.Fullscreen";
    public const string KeyWindowed = "Settings.WindowMode.Windowed";
    public const string KeyUnlimited = "Settings.FrameCap.Unlimited";
    public const string KeyHint = "HintButton";

    /// <summary>key → (en, ru, kk, zh-Hans).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Entries = new Dictionary<string, string[]>
    {
        [KeyResolution] = new[] { "Resolution", "Разрешение", "Ажыратымдылық", "分辨率" },
        [KeyWindowMode] = new[] { "Display mode", "Режим экрана", "Экран режимі", "显示模式" },
        [KeyQuality] = new[] { "Graphics quality", "Качество графики", "Графика сапасы", "画质" },
        [KeyFrameCap] = new[] { "Frame rate limit", "Лимит FPS", "Кадр жиілігі шегі", "帧率上限" },
        [KeyVSync] = new[] { "VSync", "Вертикальная синхронизация", "Тік синхрондау", "垂直同步" },
        [KeyBorderless] = new[] { "Borderless", "Окно без рамки", "Жиексіз терезе", "无边框窗口" },
        [KeyFullscreen] = new[] { "Fullscreen", "Полный экран", "Толық экран", "全屏" },
        [KeyWindowed] = new[] { "Windowed", "В окне", "Терезеде", "窗口" },
        [KeyUnlimited] = new[] { "Unlimited", "Без ограничения", "Шектеусіз", "无限制" },
        ["Settings.Quality.Very Low"] = new[] { "Very low", "Очень низкое", "Өте төмен", "极低" },
        ["Settings.Quality.Low"] = new[] { "Low", "Низкое", "Төмен", "低" },
        ["Settings.Quality.Medium"] = new[] { "Medium", "Среднее", "Орташа", "中" },
        ["Settings.Quality.High"] = new[] { "High", "Высокое", "Жоғары", "高" },
        ["Settings.Quality.Very High"] = new[] { "Very high", "Очень высокое", "Өте жоғары", "极高" },
        ["Settings.Quality.Ultra"] = new[] { "Ultra", "Ультра", "Ультра", "超高" },
        [KeyHint] = new[] { "Hint", "Подсказка", "Кеңес", "提示" },
    };

    public static readonly string[] LocaleCodes = { "en", "ru", "kk", "zh-Hans" };

    private static int Lang => Mathf.Clamp(LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLocaleIndex : 0, 0, 3);

    public static string Get(string key, string fallback = null)
    {
        try
        {
            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            var entry = table != null ? table.GetEntry(key) : null;
            if (entry != null)
            {
                string value = entry.GetLocalizedString();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        catch (System.Exception)
        {
            // Localization not initialised yet: fall through to the inline text.
        }

        if (Entries.TryGetValue(key, out var texts))
            return texts[Lang];
        return fallback ?? key;
    }

    public static string WindowMode(FullScreenMode mode) => mode switch
    {
        FullScreenMode.ExclusiveFullScreen => Get(KeyFullscreen),
        FullScreenMode.Windowed => Get(KeyWindowed),
        _ => Get(KeyBorderless),
    };

    public static string Unlimited() => Get(KeyUnlimited);

    public static string Quality(int index, string qualityName) => Get("Settings.Quality." + qualityName, qualityName);
}
