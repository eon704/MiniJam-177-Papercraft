using UnityEngine;
using UnityEngine.UI;

public class HintButton : MonoBehaviour
{
  [SerializeField] private BoardPrefab boardPrefab;
  private Button _button;
  
  private void Awake()
  {
    _button = GetComponent<Button>();
    _button.onClick.AddListener(boardPrefab.RevealNextHint);
  }
}