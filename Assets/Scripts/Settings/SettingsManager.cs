using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }
    
    public float SFXVolume { get; private set; }
    public float SoundtrackVolume { get; private set; }

     public AudioClip mainMenuSoundtrack;
     public AudioClip gameSoundtrack;
    
    private void Awake()
    {
       
        
        if (Instance == null)
        {
            Instance = this; 
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
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