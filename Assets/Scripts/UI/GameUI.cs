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

  private int starCount;
  
  public void FinishGame()
  {
    this.StartCoroutine(this.LoadMainMenu());
  }

  private void Awake()
  {
    this.starCount = 0;
  }

  private IEnumerator Start()
  {
    yield return null;
    this.gameController.PlayerPrefab.OnCollectStar.AddListener(this.AddStar);
    this.gameController.PlayerPrefab.OnPlayerWon.AddListener(this.AddStar);
    this.gameController.OnMapReset.AddListener(this.OnReset);
    this.gameController.PlayerPrefab.OnPlayerWon.AddListener(this.OnWin);
    
    yield return this.ForegroundFadeOut();
  }

  private void OnWin()
  {
    this.winScreen.SetActive(true);
    this.WinScreenStarsUI.AnimateStars(this.starCount);
  }

  private void AddStar()
  {
    this.starsUI.AddStar(this.starCount);
    this.starCount++;
  }

  private void OnReset()
  {
    this.starCount = 0;
    this.starsUI.Reset();
  }

  private IEnumerator LoadMainMenu()
  {
    yield return this.ForegroundFadeIn();
    yield return new WaitForSeconds(0.5f);
    AsyncOperation loadSceneAsync = SceneManager.LoadSceneAsync("MainMenu");
    yield return new WaitUntil(() => loadSceneAsync!.isDone);
  }
    
  private IEnumerator ForegroundFadeIn()
  {
    this._foreground.color = new Color(0, 0, 0, 0);
    this._foreground.gameObject.SetActive(true);
        
    Tween tween = this._foreground
                      .DOFade(1, 1)
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
}