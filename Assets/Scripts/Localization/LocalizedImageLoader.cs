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
    public Image targetImage;
    public List<LocaleSpritePair> localizedSprites;

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

    private void LoadLocalizedImage()
    {
        var currentLocale = LocalizationSettings.SelectedLocale;
        var localeIdentifier = currentLocale.Identifier.Code;

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
