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
/// Labels for generated dropdown options. Temporary inline translations until the strings are
/// moved into a Unity Localization string table (needs the editor).
/// Order matches LocalizationManager.Locale: en, ru, kk, zh.
/// </summary>
internal static class SettingsLabels
{
    private static int Lang => Mathf.Clamp(LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLocaleIndex : 0, 0, 3);

    private static string Pick(string en, string ru, string kk, string zh) => Lang switch
    {
        1 => ru,
        2 => kk,
        3 => zh,
        _ => en
    };

    public static string WindowMode(FullScreenMode mode) => mode switch
    {
        FullScreenMode.ExclusiveFullScreen => Pick("Fullscreen", "Полный экран", "Толық экран", "全屏"),
        FullScreenMode.Windowed => Pick("Windowed", "В окне", "Терезеде", "窗口"),
        _ => Pick("Borderless", "Окно без рамки", "Жиексіз терезе", "无边框窗口"),
    };

    public static string Unlimited() => Pick("Unlimited", "Без ограничения", "Шектеусіз", "无限制");

    public static string Quality(int index, string fallback) => fallback switch
    {
        "Very Low" => Pick("Very low", "Очень низкое", "Өте төмен", "极低"),
        "Low" => Pick("Low", "Низкое", "Төмен", "低"),
        "Medium" => Pick("Medium", "Среднее", "Орташа", "中"),
        "High" => Pick("High", "Высокое", "Жоғары", "高"),
        "Very High" => Pick("Very high", "Очень высокое", "Өте жоғары", "极高"),
        "Ultra" => Pick("Ultra", "Ультра", "Ультра", "超高"),
        _ => fallback
    };
}
