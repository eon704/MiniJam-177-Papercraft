using TMPro;
using UnityEngine;

public class LevelIndexUI : MonoBehaviour
{
  [SerializeField] private TMP_Text levelIndexText;

  private int _levelIndex;
  private void Start()
  {
    _levelIndex = LevelManager.Instance.NextLevelIndex;
    this.levelIndexText.text = $"LEVEL: {_levelIndex -1}";
  }
}