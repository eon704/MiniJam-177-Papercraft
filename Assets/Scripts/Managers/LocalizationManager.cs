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

    var localeIndex = PlayerPrefs.GetInt("localeIndex", 0);
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
}