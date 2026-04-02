using UnityEngine;

[CreateAssetMenu(fileName = "BiomeData", menuName = "Game/Biome Data")]
public class BiomeData : ScriptableObject
{
    public BiomeType Type;

    [Tooltip("Names of scenes that belong to this biome. One will be picked randomly on level load.")]
    public string[] SceneNames;
}
