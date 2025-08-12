using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class LocalizationManager : Singleton<LocalizationManager>
{
  public enum Locale
  {
    English,
    Russian,
    Kazakh,
    Chinese
  }

  private IEnumerator Start()
  {
    yield return LocalizationSettings.InitializationOperation;

    int localeIndex;
    if (!PlayerPrefs.HasKey("localeIndex"))
    {
      localeIndex = GetLocaleIndexFromSystemLanguage(Application.systemLanguage);
    }
    else
    {
      localeIndex = PlayerPrefs.GetInt("localeIndex", 0);
    }

    ChangeLocale(localeIndex);
  }

  public void ChangeLocale(Locale locale)
  {
    var localeIndex = locale switch
    {
      Locale.English => 0,
      Locale.Russian => 1,
      Locale.Kazakh => 2,
      Locale.Chinese => 3,
      _ => 0
    };
    ChangeLocale(localeIndex);
  }

  private void ChangeLocale(int localeIndex)
  {
    var selectedLocale = LocalizationSettings.AvailableLocales.Locales[localeIndex];
    LocalizationSettings.SelectedLocale = selectedLocale;
    PlayerPrefs.SetInt("localeIndex", localeIndex);
  }

  private int GetLocaleIndexFromSystemLanguage(SystemLanguage language)
  {
    switch (language)
    {
      case SystemLanguage.Russian:
        return 1;
      case SystemLanguage.Chinese:
      case SystemLanguage.ChineseSimplified:
      case SystemLanguage.ChineseTraditional:
        return 3;
      default:
        return 0; // English fallback
    }
  }
}