using UnityEngine;

public class SettingsManager : Singleton<SettingsManager>
{
    public float SFXVolume { get; private set; }
    public float SoundtrackVolume { get; private set; }

    public AudioClip mainMenuSoundtrack;
    public AudioClip gameSoundtrack;

    protected override void Awake()
    {
        base.Awake();
        
        if (Instance == this)
            LoadSettings();
    }

    private void Start()
    {
        GlobalSoundManager.Instance.PlaySoundtrack(mainMenuSoundtrack);
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
        GlobalSoundManager.Instance.UpdateSFXVolume();
    }

    public void SetSoundtrackVolume(float volume)
    {
        SoundtrackVolume = volume;
        PlayerPrefs.SetFloat("SoundtrackVolume", volume);
        PlayerPrefs.Save();
        GlobalSoundManager.Instance.UpdateSoundtrackVolume();
    }

    private void LoadSettings()
    {
        SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        SoundtrackVolume = PlayerPrefs.GetFloat("SoundtrackVolume", 1.0f);
    }
}