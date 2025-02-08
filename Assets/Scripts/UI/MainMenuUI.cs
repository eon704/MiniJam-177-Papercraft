using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private RectTransform title;
    [SerializeField] private CanvasGroup startPanel;
    [SerializeField] private CanvasGroup levelsPanel;
    [SerializeField] private Image foreground;

    public void ShowLevels()
    {
        this.startPanel.interactable = false;
        
        Sequence sequence = DOTween.Sequence();
        sequence.Append(this.title.DOAnchorPosY(-100, 0.75f).SetEase(Ease.InOutCubic));
        sequence.Join(this.startPanel.DOFade(0, 0.75f).SetEase(Ease.InOutCubic));
        sequence.AppendCallback(() =>
        {
            this.startPanel.gameObject.SetActive(false);
            this.levelsPanel.gameObject.SetActive(true);
            this.levelsPanel.alpha = 0;
        });
        sequence.AppendInterval(0.2f);
        sequence.Append(this.levelsPanel.DOFade(1, 0.75f).SetEase(Ease.InOutCubic));
        sequence.AppendCallback(() => this.levelsPanel.interactable = true);

        sequence.Play();
    }
    
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
        this.foreground.color = new Color(0, 0, 0, 0);
        this.foreground.gameObject.SetActive(true);
        
        Tween tween = this.foreground
                          .DOFade(1, 1)
                          .SetEase(Ease.OutCubic);

        yield return tween.WaitForCompletion();
    }
    
    private IEnumerator ForegroundFadeOut()
    {
        this.foreground.color = Color.black;
        this.foreground.gameObject.SetActive(true);
        
        Tween tween = this.foreground
                          .DOFade(0, 1)
                          .SetEase(Ease.InCubic);

        yield return new WaitForSeconds(0.5f);
        yield return tween.WaitForCompletion();
        this.foreground.gameObject.SetActive(false);
    }
}
