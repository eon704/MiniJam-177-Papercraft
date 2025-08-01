using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
  [Header("Game References")]
  [SerializeField] private GameController gameController;
  
  [Header("Internal UI References")]
  [SerializeField] private Image _foreground;
  [SerializeField] private CanvasGroup winScreen;
  [SerializeField] private StarsUI starsUI;
  [SerializeField] private WinScreenStarsUI WinScreenStarsUI;
  [SerializeField] private HintButton hintButton;
  
  public void FinishGame()
  {
    gameController.OnLoadingMainMenu();
    StartCoroutine(LoadMainMenu());
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
    
    AdManager.Instance.OnAdLoadedChanged.AddListener(OnAdLoadedChanged);
    OnAdLoadedChanged();
    
    yield return ForegroundFadeOut();
  }

  private void OnDestroy()
  {
    _foreground.DOKill();
    
    // Unsubscribe from AdManager events
    if (AdManager.Instance != null)
    {
      AdManager.Instance.OnAdLoadedChanged.RemoveListener(OnAdLoadedChanged);
    }
  }

  private void OnWin(int stars)
  {
    winScreen.gameObject.SetActive(true);
    winScreen.alpha = 0;
    
    winScreen
      .DOFade(1, 0.25f)
      .SetEase(Ease.OutCubic)
      .SetDelay(0.25f)
      .OnComplete(() => WinScreenStarsUI.AnimateStars(stars));
  }

  private void OnStarChange(Observable<int> stars, int oldVal, int newVal)
  {
    
    starsUI.OnStarChange(newVal);
  }

  private void OnAdLoadedChanged()
  {
    bool hasAdReady = AdManager.Instance.IsRewardedAdReady();
    bool hasMoreHints = gameController.BoardPrefab.HasMoreHints;
    
    bool shouldShowButton = hasAdReady && hasMoreHints;
    Debug.Log($"Ad ready: {hasAdReady}, More hints available: {hasMoreHints}, Show button: {shouldShowButton}");
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
  
  private void onDestroy()
  {
    DOTween.Kill(this);
  }
}