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
  [SerializeField] private GameObject winScreen;
  [SerializeField] private StarsUI starsUI;
  [SerializeField] private WinScreenStarsUI WinScreenStarsUI;
  
  public void FinishGame()
  {
    gameController.OnLoadingMainMenu();
    this.StartCoroutine(this.LoadMainMenu());
  }

  private void Awake()
  {
    _foreground.gameObject.SetActive(true);
  }

  private IEnumerator Start()
  {
    yield return null;
    this.gameController.PlayerPrefab.StarAmount.OnChanged += this.OnStarChange;
    this.gameController.PlayerPrefab.OnPlayerWon.AddListener(this.OnWin);
    
    yield return this.ForegroundFadeOut();
  }

  private void OnDestroy()
  {
    this._foreground.DOKill(); 
  }

  private void OnWin(int stars)
  {
    this.winScreen.SetActive(true);
    this.WinScreenStarsUI.AnimateStars(stars);
  }

  private void OnStarChange(Observable<int> stars, int oldVal, int newVal)
  {
    
    this.starsUI.OnStarChange(newVal);
  }

  private IEnumerator LoadMainMenu()
  {
    yield return this.ForegroundFadeIn();
    yield return new WaitForSeconds(0.5f);
    AsyncOperation loadSceneAsync = SceneManager.LoadSceneAsync("MainMenu");
    yield return new WaitUntil(() => loadSceneAsync == null || loadSceneAsync.isDone);
  }
    
  private IEnumerator ForegroundFadeIn()
  {
    this._foreground.color = new Color(0, 0, 0, 0);
    this._foreground.gameObject.SetActive(true);
        
    Tween tween = this._foreground
                      .DOFade(1, 2)
                      .SetEase(Ease.OutCubic);

    yield return tween.WaitForCompletion();
  }
    
  private IEnumerator ForegroundFadeOut()
  {
    this._foreground.color = Color.black;
    this._foreground.gameObject.SetActive(true);
        
    Tween tween = this._foreground
                      .DOFade(0, 1)
                      .SetEase(Ease.InCubic);

    yield return new WaitForSeconds(0.5f);
    yield return tween.WaitForCompletion();
    this._foreground.gameObject.SetActive(false);
  }
  
  private void onDestroy()
  {
    DOTween.Kill(this);
  }
}