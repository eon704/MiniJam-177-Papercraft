using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LevelIndexUI : MonoBehaviour
{
  private TMP_Text _levelIndexText;
  [SerializeField] private LocalizedString localizedString;

  private string levelIndex;
  
  private void Awake()
  {
    LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    
    levelIndex = LevelManager.Instance.CurrentLevelIndex.ToString("D2");
    _levelIndexText = GetComponent<TMP_Text>();
    localizedString.Arguments = new object[]
    { new Dictionary<string, string> { { "index", levelIndex } } };
    localizedString.GetLocalizedStringAsync().Completed += handle =>
    {
      _levelIndexText.text = handle.Result;
    };
  }
  
  private void OnDestroy()
  {
    LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
  }
  
  private void OnLocaleChanged(Locale newLocale)
  {
    localizedString.Arguments = new object[]
    { new Dictionary<string, string> { { "index", levelIndex } } };
    localizedString.GetLocalizedStringAsync().Completed += handle =>
    {
      _levelIndexText.text = handle.Result;
    };
  }
}