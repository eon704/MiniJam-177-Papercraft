using UnityEngine;
using System.Collections;
using FMODUnity;
using StopMode = FMOD.Studio.STOP_MODE;

public class GlobalSoundManager : Singleton<GlobalSoundManager>
{
    private const string BusSFX   = "bus:/SFX";
    private const string BusMusic = "bus:/Music";

    private FMOD.Studio.EventInstance _musicInstance;

    private IEnumerator Start()
    {
        while (!RuntimeManager.HaveAllBanksLoaded)
            yield return null;

        yield return null;

        ApplySFXVolume(SettingsManager.Instance.SFXVolume);
        ApplyMusicVolume(SettingsManager.Instance.SoundtrackVolume);
    }

    private void OnDestroy()
    {
        StopMusic(false);
    }

    // ── Music ─────────────────────────────────────────────────────────────

    public void PlaySoundtrack(EventReference ev)
    {
        StopMusic();
        if (ev.IsNull) return;
        _musicInstance = RuntimeManager.CreateInstance(ev);
        _musicInstance.start();
    }

    public void StopMusic(bool allowFade = true)
    {
        _musicInstance.stop(allowFade ? StopMode.ALLOWFADEOUT : StopMode.IMMEDIATE);
        _musicInstance.release();
        _musicInstance = default;
    }

    // ── Volume ────────────────────────────────────────────────────────────

    public void UpdateSFXVolume()        => ApplySFXVolume(SettingsManager.Instance.SFXVolume);
    public void UpdateSoundtrackVolume() => ApplyMusicVolume(SettingsManager.Instance.SoundtrackVolume);

    private static void ApplySFXVolume(float v)  => SetBusVolume(BusSFX, v);
    private static void ApplyMusicVolume(float v) => SetBusVolume(BusMusic, v);

    private static void SetBusVolume(string busPath, float v)
    {
        try { RuntimeManager.GetBus(busPath).setVolume(v); }
        catch { Debug.LogWarning($"[FMOD] Bus '{busPath}' not found."); }
    }

    private static void SetBusMute(string busPath, bool mute)
    {
        try { RuntimeManager.GetBus(busPath).setMute(mute); }
        catch { }
    }

    // ── Mute ──────────────────────────────────────────────────────────────

    public void MuteAll()   => SetBusMute("bus:/", true);
    public void UnmuteAll() => SetBusMute("bus:/", false);
}
