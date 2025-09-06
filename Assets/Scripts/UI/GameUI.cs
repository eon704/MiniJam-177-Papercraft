using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_IOS
using UnityEngine.iOS;
#endif


public class GameUI : MonoBehaviour
{
  [Header("UI Blocker")]
  [SerializeField] private Canvas adBlockerCanvas;
  [Header("Game References")]
  [SerializeField] private GameController gameController;

  [Header("Internal UI References")]
  [SerializeField] private Image _foreground;
  [SerializeField] private CanvasGroup winScreen;
  [SerializeField] private StarsUI starsUI;
  [SerializeField] private WinScreenStarsUI WinScreenStarsUI;
  [SerializeField] private GameObject hintButton;
  [SerializeField] private GameObject finalScreen;

  public void FinishGame()
  {
    // Kill all DOTween animations before transitioning
    DOTween.KillAll();

    gameController.OnLoadingMainMenu();
    StartCoroutine(LoadMainMenu());
  }

  public void ShowFinalScreen()
  {
    if (finalScreen != null)
    {
      finalScreen.SetActive(true);
    }
  }

  public void OnNextLevelButtonPressed()
  {
    if (LevelManager.Instance.IsLastLevel())
    {
      ShowFinalScreen();
    }
    else
    {
      gameController.LoadNextLevel();
    }
  }

  private void Awake()
  {
    _foreground.gameObject.SetActive(true);
    hintButton.gameObject.SetActive(false);
  }

  private IEnumerator Start()
  {
    yield return null;
    gameController.PlayerPrefab.StarAmount.OnChanged += OnStarChange;
    gameController.PlayerPrefab.OnPlayerWon.AddListener(OnWin);

    AdManager.Instance.adEventsInstance.OnAdLoadedChanged.AddListener(OnAdLoadedChanged);
    AdManager.Instance.adEventsInstance.OnAdDisplayed.AddListener(OnAdDisplayed);
    AdManager.Instance.adEventsInstance.OnAdClosed.AddListener(OnAdClosed);
    OnAdLoadedChanged();

    yield return ForegroundFadeOut();
  }

  private void OnDestroy()
  {
    _foreground.DOKill();
    winScreen.DOKill();

    // Kill any animations on WinScreenStarsUI
    if (WinScreenStarsUI != null)
    {
      WinScreenStarsUI.transform.DOKill(true); // Kill all tweens on this transform and its children
    }

    // Unsubscribe from AdManager events
    if (AdManager.Instance != null)
    {
      AdManager.Instance.adEventsInstance.OnAdLoadedChanged.RemoveListener(OnAdLoadedChanged);
      AdManager.Instance.adEventsInstance.OnAdDisplayed.RemoveListener(OnAdDisplayed);
      AdManager.Instance.adEventsInstance.OnAdClosed.RemoveListener(OnAdClosed);
    }
  }

  // Called when ad is shown
  private void OnAdDisplayed()
  {
    adBlockerCanvas.gameObject.SetActive(true);
  }

  // Called when ad is closed
  private void OnAdClosed()
  {
    adBlockerCanvas.gameObject.SetActive(false);
  }

  private void OnWin(int stars)
  {
    winScreen.gameObject.SetActive(true);
    winScreen.alpha = 0;

    winScreen
      .DOFade(1, 0.25f)
      .SetEase(Ease.OutCubic)
      .SetDelay(0.25f)
      .OnComplete(() =>
      {
        if (WinScreenStarsUI != null && WinScreenStarsUI.gameObject != null)
        {
          WinScreenStarsUI.AnimateStars(stars);
        }

#if UNITY_IOS
        if (stars >= 3 && LevelManager.Instance.CurrentLevelIndex % 10 == 0)
        {
          Device.RequestStoreReview();
        }
#endif

#if UNITY_ANDROID
        if (stars >= 3 && LevelManager.Instance.CurrentLevelIndex % 10 == 0) 
        {
          Google.Play.Review.ReviewManager reviewManager = new Google.Play.Review.ReviewManager();
          var requestFlowOperation = reviewManager.RequestReviewFlow();
          requestFlowOperation.Completed += operation =>
          {
            if (operation.Error == Google.Play.Common.PlayAsyncOperationErrorCode.NoError)
            {
              var reviewInfo = operation.GetResult();
              var launchFlowOperation = reviewManager.LaunchReviewFlow(reviewInfo);
              // Optionally, handle launchFlowOperation.Completed for result
            }
            else
            {
              Debug.LogWarning("Review flow request failed: " + operation.Error);
            }
          };
        }
#endif
      });
  }

  private void OnStarChange(Observable<int> stars, int oldVal, int newVal)
  {

    starsUI.OnStarChange(newVal);
  }

  private void OnAdLoadedChanged()
  {
    bool hasAdReady = AdManager.Instance.IsRewardedAdReady();
    bool hasMoreHints = gameController.BoardPrefab.HasUnrevealedHints();

    bool shouldShowButton = hasAdReady && hasMoreHints;
    hintButton.gameObject.SetActive(shouldShowButton);
  }

  private IEnumerator LoadMainMenu()
  {
    yield return ForegroundFadeIn();
    AsyncOperation loadSceneAsync = SceneManager.LoadSceneAsync("MainMenu");
    yield return new WaitUntil(() => loadSceneAsync == null || loadSceneAsync.isDone);
  }

  private IEnumerator ForegroundFadeIn()
  {
    _foreground.color = new Color(0, 0, 0, 0);
    _foreground.gameObject.SetActive(true);

    Tween tween = _foreground
                      .DOFade(1, 0.25f)
                      .SetEase(Ease.OutCubic);

    yield return new WaitForSeconds(0.25f);
  }

  private IEnumerator ForegroundFadeOut()
  {
    _foreground.color = Color.black;
    _foreground.gameObject.SetActive(true);

    Tween tween = _foreground
                      .DOFade(0, 0.25f)
                      .SetEase(Ease.InCubic);

    yield return new WaitForSeconds(0.25f);
    _foreground.gameObject.SetActive(false);
  }
}