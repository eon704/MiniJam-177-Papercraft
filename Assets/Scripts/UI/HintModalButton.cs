using UnityEngine;
using UnityEngine.UI;

public class HintModalButton : MonoBehaviour
{
  [SerializeField] private BoardPrefab boardPrefab;
  [SerializeField] private HintModal hintModal;
  private Button _button;

  private void Awake()
  {
    _button = GetComponent<Button>();
    _button.onClick.AddListener(OnClick);
  }

  private void Start()
  {
    // Initialize the hint modal with the board reference
    if (hintModal != null && boardPrefab != null)
    {
      hintModal.Initialize(boardPrefab);
    }
  }

  private void OnClick()
  {
    // Show the hint modal instead of directly showing an ad
    if (hintModal != null)
    {
      hintModal.Show();
    }
  }
}