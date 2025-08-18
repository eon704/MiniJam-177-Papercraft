
using UnityEngine;
using UnityEditor;


public class MaterialReplacerWindow : EditorWindow
{
    Material targetMaterial;
    Material newMaterial;
    SceneAsset targetScene;

    [MenuItem("Tools/Material Replacer Window")]
    public static void ShowWindow()
    {
        GetWindow<MaterialReplacerWindow>("Material Replacer");
    }


    void OnGUI()
    {
        GUILayout.Label("Replace SpriteRenderer Materials", EditorStyles.boldLabel);
        targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", targetMaterial, typeof(Material), false);
        newMaterial = (Material)EditorGUILayout.ObjectField("New Material", newMaterial, typeof(Material), false);
        targetScene = (SceneAsset)EditorGUILayout.ObjectField("Target Scene", targetScene, typeof(SceneAsset), false);

        GUILayout.Space(10);
        if (GUILayout.Button("Replace In All Prefabs (Assets)"))
        {
            if (targetMaterial == null || newMaterial == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign both materials.", "OK");
                return;
            }
            ReplaceMaterialsInPrefabs(targetMaterial, newMaterial);
        }

        if (GUILayout.Button("Replace In Assigned Scene"))
        {
            if (targetMaterial == null || newMaterial == null || targetScene == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign both materials and a target scene.", "OK");
                return;
            }
            ReplaceMaterialsInScene(targetMaterial, newMaterial, targetScene);
        }
    }


    static void ReplaceMaterialsInPrefabs(Material target, Material replacement)
    {
        int totalChanged = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var srs = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            bool changed = false;
            Undo.RegisterCompleteObjectUndo(prefab, "Bulk Replace SpriteRenderer Materials (Prefab)");
            foreach (var sr in srs)
            {
                if (sr.sharedMaterial == target)
                {
                    sr.sharedMaterial = replacement;
                    EditorUtility.SetDirty(sr);
                    changed = true;
                    totalChanged++;
                }
            }
            if (changed)
            {
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
            }
        }
        Debug.Log($"Replaced {totalChanged} SpriteRenderer materials in prefabs.");
    }

    static void ReplaceMaterialsInScene(Material target, Material replacement, SceneAsset sceneAsset)
    {
        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        var srs = GameObject.FindObjectsByType<SpriteRenderer>(UnityEngine.FindObjectsSortMode.None);
        bool changed = false;
        Undo.RegisterCompleteObjectUndo(srs, "Bulk Replace SpriteRenderer Materials (Scene)");
        int totalChanged = 0;
        foreach (var sr in srs)
        {
            if (sr.sharedMaterial == target)
            {
                sr.sharedMaterial = replacement;
                EditorUtility.SetDirty(sr);
                changed = true;
                totalChanged++;
            }
        }
        if (changed)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"Replaced {totalChanged} SpriteRenderer materials in scene {sceneAsset.name}.");
    }
}