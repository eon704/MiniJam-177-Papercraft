
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

[System.Serializable]
public class LocaleSpritePair
{
    public string localeIdentifier;
    public Sprite sprite;
}

public class LocalizedImageLoader : MonoBehaviour
{
    public Image targetImage; // UI Image to change
    public List<LocaleSpritePair> localizedSprites; // List to map locale identifiers to sprites

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        LoadLocalizedImage();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        LoadLocalizedImage();
    }

    void LoadLocalizedImage()
    {
        // Get the current locale
        var currentLocale = LocalizationSettings.SelectedLocale;
        var localeIdentifier = currentLocale.Identifier.Code;

        // Find the sprite for the current locale
        foreach (var pair in localizedSprites)
        {
            if (pair.localeIdentifier == localeIdentifier)
            {
                targetImage.sprite = pair.sprite;
                return;
            }
        }

        Debug.LogError($"No localized sprite found for locale: {localeIdentifier}");
    }
}
