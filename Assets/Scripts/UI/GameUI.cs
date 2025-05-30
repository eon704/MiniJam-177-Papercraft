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
  
  public void FinishGame()
  {
    gameController.OnLoadingMainMenu();
    StartCoroutine(LoadMainMenu());
  }

  private void Awake()
  {
    _foreground.gameObject.SetActive(true);
  }

  private IEnumerator Start()
  {
    yield return null;
    gameController.PlayerPrefab.StarAmount.OnChanged += OnStarChange;
    gameController.PlayerPrefab.OnPlayerWon.AddListener(OnWin);
    
    yield return ForegroundFadeOut();
  }

  private void OnDestroy()
  {
    _foreground.DOKill(); 
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