using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Image _foreground;
    
    public void StartGame()
    {
        this.StartCoroutine(this.LoadGame());
    }
    
    private IEnumerator Start()
    {
        yield return this.ForegroundFadeOut();
    }

    private IEnumerator LoadGame()
    {
        yield return this.ForegroundFadeIn();
        yield return new WaitForSeconds(0.5f);
        AsyncOperation loadSceneAsync = SceneManager.LoadSceneAsync("Game");
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
