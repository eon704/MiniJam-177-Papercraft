using System;
using System.IO;
using UnityEngine;

/// <summary>
/// File-based save for the PC build. Replaces PlayerPrefs (which lives in the Windows registry and
/// cannot be synced by Steam Cloud).
///
/// File: <c>%USERPROFILE%/AppData/LocalLow/&lt;Company&gt;/&lt;Product&gt;/save.json</c> on Windows
/// (Application.persistentDataPath). Steam Auto-Cloud should be pointed at this file.
///
/// Writes are atomic: the new content goes to <c>save.json.tmp</c> and then replaces
/// <c>save.json</c>, keeping the previous version as <c>save.json.bak</c>. If the main file is
/// missing or corrupt, the backup is used; if both are unusable, old PlayerPrefs keys are imported
/// once; otherwise defaults are used.
/// </summary>
public static class SaveSystem
{
    private const string FileName = "save.json";

    /// <summary>Delay used by <see cref="RequestSave"/> to batch frequent changes (volume sliders).</summary>
    private const float DeferredSaveDelay = 0.75f;

    private static SaveData _data;
    private static bool _dirty;
    private static float _saveAt;
    private static string _directoryOverride;

    public static event Action OnLoaded;

    public static SaveData Data
    {
        get
        {
            if (_data == null)
                Load();
            return _data;
        }
    }

    public static ProgressData Progress => Data.progress;
    public static SettingsData Settings => Data.settings;

    /// <summary>
    /// Folder that holds the save. Can be overridden, e.g. to separate saves per Steam user once
    /// Steamworks is integrated.
    /// </summary>
    public static string Directory
    {
        get => string.IsNullOrEmpty(_directoryOverride) ? Application.persistentDataPath : _directoryOverride;
        set
        {
            _directoryOverride = value;
            _data = null;
        }
    }

    public static string SaveFilePath => Path.Combine(Directory, FileName);
    private static string TempFilePath => SaveFilePath + ".tmp";
    private static string BackupFilePath => SaveFilePath + ".bak";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Needed when "Enter Play Mode Options" skips domain reload.
        _data = null;
        _dirty = false;
        _directoryOverride = null;
        OnLoaded = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRunner()
    {
        var go = new GameObject("[SaveSystem]");
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SaveSystemRunner>();
    }

    public static void Load()
    {
        SaveData loaded = TryRead(SaveFilePath) ?? TryRead(BackupFilePath);

        if (loaded == null)
        {
            loaded = PlayerPrefsMigration.TryImport() ?? new SaveData();
            _data = loaded;
            loaded.Sanitize();
            SaveNow();
        }
        else
        {
            _data = loaded;
            int loadedVersion = loaded.version;
            Upgrade(loaded);
            loaded.Sanitize();
            // Persist upgraded layouts right away (the previous file is kept as save.json.bak).
            if (loaded.version != loadedVersion)
                SaveNow();
        }

        OnLoaded?.Invoke();
    }

    /// <summary>Marks the save as changed; it is written shortly after, or on quit/focus loss.</summary>
    public static void RequestSave()
    {
        if (!_dirty)
            _saveAt = Time.unscaledTime + DeferredSaveDelay;
        _dirty = true;
    }

    /// <summary>Writes the save immediately. Use for progress, which must never be lost.</summary>
    public static void SaveNow()
    {
        if (_data == null)
            return;

        _dirty = false;
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string json = JsonUtility.ToJson(_data, prettyPrint: true);
            File.WriteAllText(TempFilePath, json);

            if (File.Exists(SaveFilePath))
                File.Replace(TempFilePath, SaveFilePath, BackupFilePath, ignoreMetadataErrors: true);
            else
                File.Move(TempFilePath, SaveFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to write {SaveFilePath}: {e}");
        }
    }

    public static void FlushIfDirty()
    {
        if (_dirty)
            SaveNow();
    }

    internal static void Tick()
    {
        if (_dirty && Time.unscaledTime >= _saveAt)
            SaveNow();
    }

    private static SaveData TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version <= 0)
                return null;

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Could not read {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>Upgrades older save layouts. Add a step per version bump.</summary>
    private static void Upgrade(SaveData data)
    {
        if (data.version > SaveData.CurrentVersion)
            Debug.LogWarning($"[SaveSystem] Save version {data.version} is newer than the game ({SaveData.CurrentVersion}).");

        if (data.version < 2)
        {
            // v1 keyed progress by position in the level list. The list was replaced by the new Forest
            // levels, so those indexes point at different levels now: drop them rather than move stars
            // onto the wrong levels. Settings are kept.
            data.progress ??= new ProgressData();
            data.progress.ClearLegacy();
            data.progress.levels?.Clear();
            data.version = 2;
        }

        data.version = Mathf.Max(data.version, SaveData.CurrentVersion);
    }

    private sealed class SaveSystemRunner : MonoBehaviour
    {
        private void Update() => Tick();
        private void OnApplicationPause(bool paused) { if (paused) FlushIfDirty(); }
        private void OnApplicationFocus(bool focused) { if (!focused) FlushIfDirty(); }
        private void OnApplicationQuit() => FlushIfDirty();
    }
}

/// <summary>One-time import of the PlayerPrefs settings used before the file save existed.</summary>
internal static class PlayerPrefsMigration
{
    // Keys used by LevelManager, SettingsManager and LocalizationManager up to version 1.2.0.
    private const string SfxKey = "SFXVolume";
    private const string MusicKey = "SoundtrackVolume";
    private const string LocaleKey = "localeIndex";

    public static SaveData TryImport()
    {
        bool any = PlayerPrefs.HasKey(SfxKey) || PlayerPrefs.HasKey(MusicKey) || PlayerPrefs.HasKey(LocaleKey);

        var data = new SaveData();

        // Old progress was stored by level index for a level list that no longer exists, so only
        // settings are imported.
        if (!any)
            return null;

        data.migratedFromPlayerPrefs = true;
        data.settings.sfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
        data.settings.musicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
        data.settings.localeIndex = PlayerPrefs.HasKey(LocaleKey) ? PlayerPrefs.GetInt(LocaleKey, 0) : -1;

        // PlayerPrefs are left in place on purpose: the import is non-destructive.
        Debug.Log("[SaveSystem] Imported settings from PlayerPrefs.");
        return data;
    }
}
