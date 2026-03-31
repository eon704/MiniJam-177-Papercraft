using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BiomeManager : Singleton<BiomeManager>
{
    [SerializeField] private BiomeDatabase database;

    private string _loadedBiomeScene;

    public IEnumerator LoadBiome(BiomeType type)
    {
        if (type == BiomeType.None)
            yield break;

        var biome = database.GetBiome(type);
        var sceneName = biome.SceneNames[Random.Range(0, biome.SceneNames.Length)];
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        if (!string.IsNullOrEmpty(_loadedBiomeScene))
            yield return SceneManager.UnloadSceneAsync(_loadedBiomeScene);

        yield return op;
        _loadedBiomeScene = sceneName;

        if (!biome.AmbientEvent.IsNull)
            GlobalSoundManager.Instance.PlaySoundtrack(biome.AmbientEvent);
    }
}
