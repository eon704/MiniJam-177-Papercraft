using UnityEngine;

public class SettingsManager : Singleton<SettingsManager>
{
    public float SFXVolume        { get; private set; }
    public float SoundtrackVolume { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (Instance == this)
            LoadSettings();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
        FMODAudioManager.Instance.SetVCAVolume("vca:/SFX", volume);
    }

    public void SetSoundtrackVolume(float volume)
    {
        SoundtrackVolume = volume;
        PlayerPrefs.SetFloat("SoundtrackVolume", volume);
        PlayerPrefs.Save();
        FMODAudioManager.Instance.SetVCAVolume("vca:/Music", volume);
    }

    private void LoadSettings()
    {
        SFXVolume        = PlayerPrefs.GetFloat("SFXVolume",        1.0f);
        SoundtrackVolume = PlayerPrefs.GetFloat("SoundtrackVolume", 1.0f);
    }
}
