using UnityEngine;
using UnityEngine.UI;

public class HintButton : MonoBehaviour
{
  private Button _button;
  
  private void Awake()
  {
    _button = GetComponent<Button>();
    //_button.onClick.AddListener();
  }
}