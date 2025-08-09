[System.Serializable]
public class CellData
{
    public TerrainType Terrain;
    public CellItem Item;

    public CellData(TerrainType terrain, CellItem item)
    {
        Terrain = terrain;
        Item = item;
    }

    public static CellData FromChar(char c)
    {
        TerrainType terrain = c switch
        {
            '0' => TerrainType.Empty,
            '+' => TerrainType.Default,
            '1' => TerrainType.Default,
            'W' => TerrainType.Water,
            '2' => TerrainType.Water,
            'S' => TerrainType.Stone,
            '3' => TerrainType.Stone,
            'F' => TerrainType.Fire,
            'x' => TerrainType.Start,
            'y' => TerrainType.End,
            _ => TerrainType.Default
        };

        CellItem item = c switch
        {
            'G' => CellItem.Star,
            '1' => CellItem.Star,
            '2' => CellItem.Star,
            '3' => CellItem.Star,
            _ => CellItem.None
        };

        return new CellData(terrain, item);
    }

    public char ToChar()
    {
        if (Terrain == TerrainType.Start) return 'x';
        if (Terrain == TerrainType.End) return 'y';
        if (Terrain == TerrainType.Empty) return '0';
        if (Terrain == TerrainType.Fire) return 'F';

        if (Item == CellItem.Star)
        {
            return Terrain switch
            {
                TerrainType.Default => 'G',
                TerrainType.Water => '2',
                TerrainType.Stone => '3',
                _ => '+'
            };
        }

        return Terrain switch
        {
            TerrainType.Default => '+',
            TerrainType.Water => 'W',
            TerrainType.Stone => 'S',
            _ => '+'
        };
    }
}
