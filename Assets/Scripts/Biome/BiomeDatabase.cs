using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BiomeDatabase", menuName = "Game/Biome Database")]
public class BiomeDatabase : ScriptableObject
{
    public List<BiomeData> Biomes;

    public BiomeData GetBiome(BiomeType type)
    {
        return Biomes?.Find(b => b.Type == type);
    }
}
