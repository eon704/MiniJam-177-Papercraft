using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class LevelCompleteTextUI : MonoBehaviour
{
  private TMP_Text _levelIndexText;
  [SerializeField] private LocalizedString localizedString;

  private void Awake()
  {
    _levelIndexText = GetComponent<TMP_Text>();
    localizedString.Arguments = new object[]
      { new Dictionary<string, string> { { "index", LevelManager.Instance.CurrentLevelIndex.ToString("D2") } } };
    localizedString.GetLocalizedStringAsync().Completed += handle =>
    {
      _levelIndexText.text = handle.Result;
    };
  }
}