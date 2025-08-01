using UnityEngine;
using UnityEngine.UI;

public class HintButton : MonoBehaviour
{
  [SerializeField] private BoardPrefab boardPrefab;
  private Button _button;
  private bool isWaitingForReward = false;

  private void Awake()
  {
    _button = GetComponent<Button>();
    _button.onClick.AddListener(OnClick);
  }

  private void Start()
  {
    // Subscribe to ad reward events
    AdManager.Instance.OnAdRewarded.AddListener(OnAdRewarded);
  }

  private void OnDestroy()
  {
    // Unsubscribe from ad events
    if (AdManager.Instance != null)
    {
      AdManager.Instance.OnAdRewarded.RemoveListener(OnAdRewarded);
    }
  }

  private void OnClick()
  {
    // Check if we can show an ad
    if (AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady())
    {
      // Disable button while waiting for reward
      _button.interactable = false;
      isWaitingForReward = true;
      
      // Show the rewarded ad
      bool adShown = AdManager.Instance.ShowRewardedAd();
      
      if (!adShown)
      {
        // Re-enable button if ad failed to show
        _button.interactable = true;
        isWaitingForReward = false;
      }
    }
    else
    {
      Debug.LogWarning("No rewarded ad available for hint.");
    }
  }

  private void OnAdRewarded()
  {
    // Only process if we're waiting for a reward
    if (!isWaitingForReward) return;
    
    isWaitingForReward = false;
    
    // Reveal the next hint
    boardPrefab.RevealNextHint(out bool areAllHintsRevealed);
    
    // Hide/show button based on whether there are more hints and if next ad is loaded
    bool hasMoreHints = boardPrefab.HasMoreHints;
    bool hasAdReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
    gameObject.SetActive(hasMoreHints && hasAdReady);
    
    // Re-enable button for future use
    _button.interactable = true;
    
    // If all hints are revealed, keep the button hidden permanently
    if (areAllHintsRevealed)
    {
      // Unsubscribe from ad events since we don't need hints anymore
      if (AdManager.Instance != null)
      {
        AdManager.Instance.OnAdRewarded.RemoveListener(OnAdRewarded);
      }
    }
  }
}