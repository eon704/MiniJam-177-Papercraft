using UnityEngine;
using System;

public enum SoundType
{
    Click,
    ChangeState,
    Move,
    Win,
    Lose,
    Fail
}

[RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
public class GlobalSoundManager : MonoBehaviour
{
    public static GlobalSoundManager Instance { get; private set; }

   
    
    [SerializeField] private SoundList[] soundList;
    [SerializeField] private AudioSource soundtrackSource;

    private AudioSource _audioSource;

    private void OnEnable()
    {
        var soundTypeNames = Enum.GetNames(typeof(SoundType));
        Array.Resize(ref soundList, soundTypeNames.Length);

        for (var i = 0; i < soundList.Length; i++)
        {
            soundList[i].name = soundTypeNames[i];
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _audioSource = GetComponent<AudioSource>();
        UpdateSFXVolume();
        UpdateSoundtrackVolume();
    }

    public static void PlayRandomSoundByType(SoundType sound, float volume = 1)
    {
        var clips = Instance.soundList[(int)sound].Sounds;
        var randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];
        Instance._audioSource.PlayOneShot(randomClip, volume * SettingsManager.Instance.SFXVolume);
    }

    public void UpdateSFXVolume()
    {
        if (_audioSource != null)
        {
            _audioSource.volume = SettingsManager.Instance.SFXVolume;
        }
    }

    public void UpdateSoundtrackVolume()
    {
        if (soundtrackSource != null)
        {
            soundtrackSource.volume = SettingsManager.Instance.SoundtrackVolume;
        }
    }

    public void PlaySoundtrack(AudioClip clip)
    {
        if (soundtrackSource != null)
        {
            soundtrackSource.clip = clip;
            soundtrackSource.Play();
        }
    }

    [Serializable]
    public struct SoundList
    {
        public AudioClip[] Sounds
        {
            get => sounds;
        }

        public string name;
        [SerializeField] private AudioClip[] sounds;
    }
}