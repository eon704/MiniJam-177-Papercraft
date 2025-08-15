using UnityEngine;
using UnityEditor;

public static class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts in Selection")]
    public static void FindInSelected()
    {
        GameObject[] go = Selection.gameObjects;
        int go_count = 0, components_count = 0, missing_count = 0;
        foreach (GameObject g in go)
        {
            FindInGameObjectRecursive(g, ref go_count, ref components_count, ref missing_count);
        }
        Debug.Log($"Searched {go_count} GameObjects, {components_count} components, found {missing_count} missing");
    }

    private static void FindInGameObjectRecursive(GameObject g, ref int go_count, ref int components_count, ref int missing_count)
    {
        go_count++;
        Component[] components = g.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            components_count++;
            if (components[i] == null)
            {
                missing_count++;
                Debug.Log(g.name + " has an empty script attached in position: " + i, g);
            }
        }
        foreach (Transform child in g.transform)
        {
            if (child != null && child.gameObject != null)
            {
                FindInGameObjectRecursive(child.gameObject, ref go_count, ref components_count, ref missing_count);
            }
        }
    }
}