using UnityEngine;

/// <summary>
/// Audio settings. Values are stored in the file save (<see cref="SaveSystem.Settings"/>);
/// display settings live in <see cref="DisplaySettings"/>.
/// </summary>
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
        SFXVolume = Mathf.Clamp01(volume);
        SaveSystem.Settings.sfxVolume = SFXVolume;
        SaveSystem.RequestSave();
        FMODAudioManager.Instance.SetVCAVolume("vca:/SFX", SFXVolume);
    }

    public void SetSoundtrackVolume(float volume)
    {
        SoundtrackVolume = Mathf.Clamp01(volume);
        SaveSystem.Settings.musicVolume = SoundtrackVolume;
        SaveSystem.RequestSave();
        FMODAudioManager.Instance.SetVCAVolume("vca:/Music", SoundtrackVolume);
    }

    private void LoadSettings()
    {
        SFXVolume        = SaveSystem.Settings.sfxVolume;
        SoundtrackVolume = SaveSystem.Settings.musicVolume;
    }
}
