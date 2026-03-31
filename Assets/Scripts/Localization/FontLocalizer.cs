using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class FontLocalizer : MonoBehaviour
{
    public TMP_Text textElement;
    [SerializeField] private string fontKey = "Font";

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        ApplyLocalizedFont();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        ApplyLocalizedFont();
    }

    private void ApplyLocalizedFont()
    {
        var assetTable = LocalizationSettings.AssetDatabase.GetTable("FontAssets");
        var entry = assetTable.GetEntry(fontKey);
        if (entry != null)
        {
            var operation = Addressables.LoadAssetAsync<TMP_FontAsset>(entry.Guid);
            operation.Completed += (handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    textElement.font = handle.Result;
                else
                    Debug.LogError("Failed to load font for locale: " + LocalizationSettings.SelectedLocale.Identifier);
            };
        }
        else
        {
            Debug.LogError("No entry found in the asset table for key: " + fontKey);
        }
    }
}
