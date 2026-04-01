using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider soundtrackVolumeSlider;
    [SerializeField] private TMP_Dropdown localeDropdown;

    private void Awake()
    {
        localeDropdown.ClearOptions();
        localeDropdown.AddOptions(new List<string> { "English", "Русский", "Қазақша", "中文" });
        localeDropdown.onValueChanged.AddListener(OnLocaleChanged);
    }

    private void OnEnable()
    {
        sfxVolumeSlider.value = SettingsManager.Instance.SFXVolume;
        soundtrackVolumeSlider.value = SettingsManager.Instance.SoundtrackVolume;
        localeDropdown.SetValueWithoutNotify(LocalizationManager.Instance.CurrentLocaleIndex);
    }

    private void OnDestroy()
    {
        localeDropdown.onValueChanged.RemoveListener(OnLocaleChanged);
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
}