using FMODUnity;
using UnityEngine;
using EventInstance = FMOD.Studio.EventInstance;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using VCA = FMOD.Studio.VCA;

public class FMODAudioManager : Singleton<FMODAudioManager>
{
    [Header("SFX Events")]
    [SerializeField] public EventReference sfxChangeState;
    [SerializeField] public EventReference sfxClick;
    [SerializeField] public EventReference sfxUndo;
    [SerializeField] public EventReference sfxReset;
    [SerializeField] public EventReference sfxError;
    [SerializeField] public EventReference sfxHover;
    [SerializeField] public EventReference sfxStateCardUnselected;
    

    [Header("Step Sounds (per terrain)")]
    [SerializeField] public EventReference sfxStepDefault;
    [SerializeField] public EventReference sfxStepFragile;
    [SerializeField] public EventReference sfxStepWater;
    [SerializeField] public EventReference sfxStepStone;
    [SerializeField] public EventReference sfxStepFire;
    [SerializeField] public EventReference sfxStepLava;
    [SerializeField] public EventReference sfxStepIce;
    [SerializeField] public EventReference sfxCellCollapse;
    [SerializeField] public EventReference sfxStarPickUp;
    [SerializeField] public EventReference sfxPop;
    [SerializeField] public EventReference sfxFireSplash;
    [SerializeField] public EventReference sfxExplosion;
    [SerializeField] public EventReference sfxVolcanoLaunch;
    [SerializeField] public EventReference sfxIceFreeze;
    [SerializeField] public EventReference sfxBoardSpawn;
    [SerializeField] public EventReference sfxUIEntrance;
    [SerializeField] public EventReference sfxPanelShow;
    [SerializeField] public EventReference sfxPanelHide;
    [SerializeField] public EventReference sfxCardPunch;
    [SerializeField] public EventReference sfxMovesDecrease;
    [SerializeField] public EventReference sfxStarReveal;
    [SerializeField] public EventReference sfxWinScreen;
    [SerializeField] public EventReference sfxWin;
    [SerializeField] public EventReference sfxLose;

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

    public void PlayOneShot(EventReference eventRef)
    {
        if (eventRef.IsNull) return;
        RuntimeManager.PlayOneShot(eventRef);
    }

    public void PlayStepSound(TerrainType terrain, bool isFragile = false)
    {
        if (isFragile)
        {
            PlayOneShot(sfxStepFragile);
            return;
        }
        EventReference sfx = terrain switch
        {
            TerrainType.Water => sfxStepWater,
            TerrainType.Stone => sfxStepStone,
            TerrainType.Fire  => sfxStepFire,
            TerrainType.Lava  => sfxStepLava,
            TerrainType.Ice   => sfxStepIce,
            _                 => sfxStepDefault,
        };
        PlayOneShot(sfx);
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
