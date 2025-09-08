using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider soundtrackVolumeSlider;
    [SerializeField] private Button privacyButton;
    [SerializeField] private Button noAdsButton;
    [SerializeField] private Button restorePurchasesButton;

    private void OnEnable()
    {
        sfxVolumeSlider.value = SettingsManager.Instance.SFXVolume;
        soundtrackVolumeSlider.value = SettingsManager.Instance.SoundtrackVolume;

#if UNITY_WEBGL
        privacyButton.gameObject.SetActive(false);
#endif
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