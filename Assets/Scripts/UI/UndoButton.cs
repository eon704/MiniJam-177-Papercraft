using UnityEngine;
using UnityEngine.UI;

public class UndoButton : MonoBehaviour
{
  [SerializeField] private Player player;

  private Button _button;
  
  private void Awake()
  {
    _button = GetComponent<Button>();
    _button.onClick.AddListener(player.UndoMove);
    
    player.OnUndoHistoryChange += OnUndoHistoryChange;
  }

  private void OnUndoHistoryChange(int historyCount)
  {
    _button.interactable = historyCount > 1;
  }
}