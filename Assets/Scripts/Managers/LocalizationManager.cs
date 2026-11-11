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

    int localeIndex = SaveSystem.Settings.localeIndex;
    if (localeIndex < 0)
      localeIndex = GetLocaleIndexFromSystemLanguage(Application.systemLanguage);

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

  public int CurrentLocaleIndex
  {
    get
    {
      int index = SaveSystem.Settings.localeIndex;
      return index >= 0 ? index : GetLocaleIndexFromSystemLanguage(Application.systemLanguage);
    }
  }

  private void ChangeLocale(int localeIndex)
  {
    var locales = LocalizationSettings.AvailableLocales.Locales;
    if (localeIndex < 0 || localeIndex >= locales.Count)
      localeIndex = 0;

    LocalizationSettings.SelectedLocale = locales[localeIndex];
    if (SaveSystem.Settings.localeIndex != localeIndex)
    {
      SaveSystem.Settings.localeIndex = localeIndex;
      SaveSystem.RequestSave();
    }
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