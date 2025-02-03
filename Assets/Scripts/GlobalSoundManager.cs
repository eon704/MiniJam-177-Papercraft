using UnityEngine;
using System;


public enum SoundType // DON'T CHANGE THE ORDER OF THIS ENUM !!!!!!
{
    Click,
    ChangeState,
    Move,
    Win,
    Lose
}


[RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
public class GlobalSoundManager : MonoBehaviour
{
    private static GlobalSoundManager Instance { get; set; }

    [SerializeField] private SoundList[] soundList;

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
        Initialize();
    }

    private void Initialize()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }


        _audioSource = GetComponent<AudioSource>();
    }

    public static void PlayRandomSoundByType(SoundType sound, float volume = 1)
    {
        var clips = Instance.soundList[(int)sound].Sounds;
        var randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];
        Instance._audioSource.PlayOneShot(randomClip, volume);
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