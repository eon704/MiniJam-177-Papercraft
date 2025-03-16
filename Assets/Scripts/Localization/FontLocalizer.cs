using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class FontLocalizer : MonoBehaviour
{
    public TMP_Text textElement; // Reference to the TextMeshPro text element
    public string fontKey = "Font"; // Font key in the Asset Table

    void Start()
    {
        ApplyLocalizedFont();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        ApplyLocalizedFont();
    }

    public void ApplyLocalizedFont()
    {
        // Get the current locale
        Locale currentLocale = LocalizationSettings.SelectedLocale;

        // Load the asset table
        var assetTable = LocalizationSettings.AssetDatabase.GetTable("FontAssets");

        // Get the entry from the table by key and current locale
        var entry = assetTable.GetEntry(fontKey);
        if (entry != null)
        {
            // Load the font asynchronously
            var operation = Addressables.LoadAssetAsync<TMP_FontAsset>(entry.Guid);
            operation.Completed += (handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    // Apply the font to the text element
                    textElement.font = handle.Result;
                }
                else
                {
                    Debug.LogError("Failed to load font for locale: " + currentLocale.Identifier);
                }
            };
        }
        else
        {
            Debug.LogError("No entry found in the asset table for key: " + fontKey);
        }
    }
}