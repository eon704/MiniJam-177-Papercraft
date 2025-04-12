using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

public class LevelIndexUI : MonoBehaviour
{
  [SerializeField] private TMP_Text levelIndexText;

  private void Awake()
  {
    GetComponent<LocalizeStringEvent>().OnUpdateString.AddListener(this.UpdateText);
  }
  
  private void UpdateText(string localizedString)
  {
    levelIndexText.text = localizedString + ": " + LevelManager.Instance.CurrentLevelIndex.ToString("D2");
  }
}