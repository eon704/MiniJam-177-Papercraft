using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Settings;

public class ScanTextComponents
{
    [MenuItem("Tools/Get Used Characters/EN")]
    static void GetChars_EN() => GetCharsForLanguage("en");

    [MenuItem("Tools/Get Used Characters/RU")]
    static void GetChars_RU() => GetCharsForLanguage("ru");

    [MenuItem("Tools/Get Used Characters/KZ")]
    static void GetChars_KZ() => GetCharsForLanguage("kk");

    [MenuItem("Tools/Get Used Characters/ZH Simplified")]
    static void GetChars_ZH() => GetCharsForLanguage("zh-Hans");

    [MenuItem("Tools/Get Used Characters/NonLocalized", priority = 0)]
    static void GetNonLocalizedChars()
    {
        HashSet<char> uiChars = new HashSet<char>();
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if ((go.hideFlags & HideFlags.DontSaveInEditor) != 0)
                continue;
            foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>(true))
                foreach (var c in tmp.text) uiChars.Add(c);
            foreach (var text in go.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                foreach (var c in text.text) uiChars.Add(c);
        }
        Debug.Log("Used characters in UI (not language-specific):\n" + new string(new List<char>(uiChars).ToArray()));
    }

    static void GetCharsForLanguage(string lang)
    {
        var chars = new HashSet<char>();
        var stringTableCollections = LocalizationEditorSettings.GetStringTableCollections();
        foreach (var collection in stringTableCollections)
        {
            foreach (var table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code == lang)
                {
                    foreach (var entry in table.Values)
                    {
                        if (entry != null && entry.Value != null)
                            foreach (var c in entry.Value) chars.Add(c);
                    }
                }
            }
        }
        var charList = new List<char>(chars);
        charList.Sort();
        Debug.Log($"Used characters for language {lang}:\n" + new string(charList.ToArray()));
    }
}