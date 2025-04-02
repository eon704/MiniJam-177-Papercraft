using TMPro;
using UnityEngine;

public class LevelIndexUI : MonoBehaviour
{
  [SerializeField] private TextMeshProUGUI levelIndexText;

  private void Start()
  {
    this.levelIndexText.text = $"LEVEL: " + LevelManager.Instance.CurrentLevelIndex.ToString("D2");
  }
}