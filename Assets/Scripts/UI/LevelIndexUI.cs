using TMPro;
using UnityEngine;

public class LevelIndexUI : MonoBehaviour
{
  [SerializeField] private TMP_Text levelIndexText;

  private void Start()
  {
    this.levelIndexText.text = $"LEVEL:  {LevelManager.Instance.NextLevelIndex + 1}";
  }
}