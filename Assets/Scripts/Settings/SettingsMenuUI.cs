using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider soundtrackVolumeSlider;
    private static GameObject _settingsPanel;

    private void Awake()
    {
        _settingsPanel = transform.GetChild(0).gameObject;
        _settingsPanel.SetActive(false);
    }
    private void Start()
    {
  
        
        sfxVolumeSlider.value = SettingsManager.Instance.SFXVolume;
        soundtrackVolumeSlider.value = SettingsManager.Instance.SoundtrackVolume;

        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        soundtrackVolumeSlider.onValueChanged.AddListener(OnSoundtrackVolumeChanged);
    }

    private void OnSFXVolumeChanged(float volume)
    {
        SettingsManager.Instance.SetSFXVolume(volume);
    }

    private void OnSoundtrackVolumeChanged(float volume)
    {
        SettingsManager.Instance.SetSoundtrackVolume(volume);
    }
    
   
    public static void OpenSettings()
    {
        _settingsPanel.SetActive(true);
        
    }

    public void CloseSettings()
    {
        _settingsPanel.SetActive(false);
    }
}