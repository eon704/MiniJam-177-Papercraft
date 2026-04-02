using FMODUnity;
using UnityEngine;
using EventInstance = FMOD.Studio.EventInstance;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using VCA = FMOD.Studio.VCA;

public class FMODAudioManager : Singleton<FMODAudioManager>
{
    [Header("SFX Events")]
    [SerializeField] private EventReference sfxMove;
    [SerializeField] private EventReference sfxChangeState;
    [SerializeField] private EventReference sfxClick;
    [SerializeField] private EventReference sfxCard;
    [SerializeField] private EventReference sfxBubble;
    [SerializeField] private EventReference sfxPop;
    [SerializeField] private EventReference sfxWin;
    [SerializeField] private EventReference sfxLose;
    [SerializeField] private EventReference sfxBoardSpawn;
    [SerializeField] private EventReference sfxExplosion;
    [SerializeField] private EventReference sfxFireSplash;
    [SerializeField] private EventReference sfxCellShake;

    [Header("Music Events")]
    [SerializeField] private EventReference musicForest;
    [SerializeField] private EventReference musicDesert;
    [SerializeField] private EventReference musicSnow;
    [SerializeField] private EventReference musicCave;
    [SerializeField] private EventReference musicOcean;

    private EventInstance _currentMusicInstance;
    private BiomeType _currentBiome = BiomeType.None;

    private const string VcaSfx   = "vca:/SFX";
    private const string VcaMusic = "vca:/Music";

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (Instance != this) return;

        SetVCAVolume(VcaSfx,   SettingsManager.Instance.SFXVolume);
        SetVCAVolume(VcaMusic, SettingsManager.Instance.SoundtrackVolume);
    }

    private void OnDestroy()
    {
        if (_currentMusicInstance.isValid())
        {
            _currentMusicInstance.stop(STOP_MODE.IMMEDIATE);
            _currentMusicInstance.release();
        }
    }

    // ── SFX ───────────────────────────────────────────────────────────────

    public void PlayMove()        => PlayOneShot(sfxMove);
    public void PlayChangeState() => PlayOneShot(sfxChangeState);
    public void PlayClick()       => PlayOneShot(sfxClick);
    public void PlayCard()        => PlayOneShot(sfxCard);
    public void PlayBubble()      => PlayOneShot(sfxBubble);
    public void PlayPop()         => PlayOneShot(sfxPop);
    public void PlayWin()         => PlayOneShot(sfxWin);
    public void PlayLose()        => PlayOneShot(sfxLose);
    public void PlayBoardSpawn()  => PlayOneShot(sfxBoardSpawn);
    public void PlayExplosion()   => PlayOneShot(sfxExplosion);
    public void PlayFireSplash()  => PlayOneShot(sfxFireSplash);
    public void PlayCellShake()   => PlayOneShot(sfxCellShake);

    private void PlayOneShot(EventReference eventRef)
    {
        if (eventRef.IsNull) return;
        RuntimeManager.PlayOneShot(eventRef);
    }

    // ── Music ─────────────────────────────────────────────────────────────

    public void PlayMusicForBiome(BiomeType biome)
    {
        if (biome == _currentBiome) return;
        _currentBiome = biome;

        StopMusic(fadeOut: false);

        EventReference musicRef = GetMusicRef(biome);
        if (musicRef.IsNull) return;

        _currentMusicInstance = RuntimeManager.CreateInstance(musicRef);
        _currentMusicInstance.start();
    }

    public void StopMusic(bool fadeOut = true)
    {
        if (!_currentMusicInstance.isValid()) return;
        _currentMusicInstance.stop(fadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
        _currentMusicInstance.release();
        _currentMusicInstance = default;
    }

    private EventReference GetMusicRef(BiomeType biome) => biome switch
    {
        BiomeType.Forest  => musicForest,
        BiomeType.Desert  => musicDesert,
        BiomeType.Snow    => musicSnow,
        BiomeType.Cave    => musicCave,
        BiomeType.Ocean   => musicOcean,
        _                 => default
    };

    // ── Volume ────────────────────────────────────────────────────────────

    public void SetVCAVolume(string vcaPath, float volume)
    {
        VCA vca = RuntimeManager.GetVCA(vcaPath);
        vca.setVolume(volume);
    }
}
