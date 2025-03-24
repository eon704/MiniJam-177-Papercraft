using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider soundtrackVolumeSlider;

    private void OnEnable()
    {
        sfxVolumeSlider.value = SettingsManager.Instance.SFXVolume;
        soundtrackVolumeSlider.value = SettingsManager.Instance.SoundtrackVolume;
    }

    public void OnSFXVolumeChanged(float volume)
    {
        SettingsManager.Instance.SetSFXVolume(volume);
    }

    public void OnSoundtrackVolumeChanged(float volume)
    {
        SettingsManager.Instance.SetSoundtrackVolume(volume);
    }
}