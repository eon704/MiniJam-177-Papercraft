using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// Adds the PC settings and hint strings (<see cref="SettingsLabels.Entries"/>) to the
/// "Paperbound Localization Table" for en, ru, kk and zh-Hans. Existing translations are kept,
/// only missing keys or empty values are filled. Safe to run more than once; also callable from
/// batchmode: -executeMethod PcSettingsStrings.AddAll
/// </summary>
public static class PcSettingsStrings
{
    [MenuItem("Tools/Localization/Add PC settings strings")]
    public static void AddAll()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(SettingsLabels.TableName);
        if (collection == null)
        {
            Debug.LogError($"[PcSettingsStrings] String table collection '{SettingsLabels.TableName}' not found.");
            return;
        }

        int added = 0;
        foreach (var pair in SettingsLabels.Entries)
        {
            if (collection.SharedData.GetEntry(pair.Key) == null)
                collection.SharedData.AddKey(pair.Key);

            for (int i = 0; i < SettingsLabels.LocaleCodes.Length; i++)
            {
                if (collection.GetTable(SettingsLabels.LocaleCodes[i]) is not StringTable table)
                {
                    Debug.LogWarning($"[PcSettingsStrings] No '{SettingsLabels.LocaleCodes[i]}' table in {SettingsLabels.TableName}.");
                    continue;
                }

                StringTableEntry entry = table.GetEntry(pair.Key);
                if (entry != null && !string.IsNullOrEmpty(entry.Value))
                    continue;

                table.AddEntry(pair.Key, pair.Value[i]);
                EditorUtility.SetDirty(table);
                added++;
            }
        }

        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PcSettingsStrings] Added or filled {added} entries.");
    }
}
