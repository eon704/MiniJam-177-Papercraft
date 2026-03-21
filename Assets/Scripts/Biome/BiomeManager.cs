using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BiomeManager : Singleton<BiomeManager>
{
    [SerializeField] private BiomeDatabase database;

    private string _loadedBiomeScene;

    protected override void Awake()
    {
        base.Awake();
        if (database == null)
            Debug.LogError("[Biome] BiomeDatabase is NULL — assign in BiomeManager Inspector");
        else
            Debug.Log($"[Biome] BiomeManager ready. Database has {database.Biomes?.Count ?? 0} biomes");
    }

    public IEnumerator LoadBiome(BiomeType type)
    {
        Debug.Log($"[Biome] LoadBiome called with type={type}");

        if (type == BiomeType.None)
        {
            Debug.Log("[Biome] BiomeType.None — skipping load");
            yield break;
        }

        if (database == null)
        {
            Debug.LogError("[Biome] Database is NULL, cannot load biome");
            yield break;
        }

        BiomeData biome = database.GetBiome(type);
        if (biome == null)
        {
            Debug.LogWarning($"[Biome] No BiomeData found for type={type} in database");
            yield break;
        }

        if (biome.SceneNames == null || biome.SceneNames.Length == 0)
        {
            Debug.LogWarning($"[Biome] BiomeData for {type} has no scene names configured");
            yield break;
        }

        string sceneName = biome.SceneNames[Random.Range(0, biome.SceneNames.Length)];
        Debug.Log($"[Biome] Loading scene '{sceneName}' additively...");

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[Biome] LoadSceneAsync returned null for '{sceneName}' — is the scene added to Build Settings?");
            yield break;
        }

        yield return op;
        _loadedBiomeScene = sceneName;
        Debug.Log($"[Biome] Scene '{sceneName}' loaded successfully");

        if (biome.AmbientClip != null)
        {
            GlobalSoundManager.Instance.PlaySoundtrack(biome.AmbientClip);
            Debug.Log($"[Biome] Playing ambient: {biome.AmbientClip.name}");
        }
        else
        {
            Debug.Log("[Biome] No ambient clip assigned for this biome");
        }
    }
}
