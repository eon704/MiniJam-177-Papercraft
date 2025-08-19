using System.Collections.Generic;

public static class ListExtensions
{
    public static T GetRandom<T>(this IList<T> list)
    {
        if (list == null || list.Count == 0)
        {
            return default;
        }

        int randomIndex = UnityEngine.Random.Range(0, list.Count);
        return list[randomIndex];
    }
}