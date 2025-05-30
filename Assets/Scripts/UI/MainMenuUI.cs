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

    private float startPosY;
    
    public void ShowLevels()
    {
        startPanel.interactable = false;

        var sequence = DOTween.Sequence();
        sequence.Append(title.DOAnchorPosY(-250, 0.5f).SetEase(Ease.InOutCubic));
        sequence.Join(startPanel.DOFade(0, 0.2f).SetEase(Ease.InOutCubic));

        sequence.AppendCallback(() =>
        {
            startPanel.gameObject.SetActive(false);
            levelsPanel.gameObject.SetActive(true);
            levelsPanel.alpha = 0;
        });
        sequence.AppendInterval(0.2f);
        sequence.Append(levelsPanel.DOFade(1, 0.25f).SetEase(Ease.InOutCubic));
        sequence.AppendCallback(() => levelsPanel.interactable = true);

        sequence.Play();
    }

    public void HideLevels()
    {
        levelsPanel.interactable = false;

        var sequence = DOTween.Sequence();
        sequence.Append(levelsPanel.DOFade(0, 0.3f).SetEase(Ease.InOutCubic));
        sequence.Append(title.DOScale(0.4f, 0.3f).SetEase(Ease.InOutCubic));
        sequence.Join(title.DOAnchorPosY(startPosY, 0.3f).SetEase(Ease.InOutCubic));
      
        sequence.AppendCallback(() =>
        {
            levelsPanel.gameObject.SetActive(false);
            startPanel.gameObject.SetActive(true);
            startPanel.alpha = 0;
        });
        sequence.AppendInterval(0.2f);
        sequence.Append(startPanel.DOFade(1, 0.3f).SetEase(Ease.InOutCubic));
        sequence.AppendCallback(() => startPanel.interactable = true);

        sequence.Play();
    }

    public void StartGame()
    {
        StartCoroutine(LoadGame());
    }

    private IEnumerator Start()
    {
        startPosY = title.anchoredPosition.y;
        Application.targetFrameRate = 60;
        yield return ForegroundFadeOut();
    }

    private IEnumerator LoadGame()
    {
        yield return ForegroundFadeIn();
        AsyncOperation loadSceneAsync = SceneManager.LoadSceneAsync("Game");
        yield return new WaitUntil(() => loadSceneAsync!.isDone);

        GlobalSoundManager.Instance.PlaySoundtrack(SettingsManager.Instance.gameSoundtrack);
    }

    private IEnumerator ForegroundFadeIn()
    {
        foreground.color = new Color(0, 0, 0, 0);
        foreground.gameObject.SetActive(true);

        Tween tween = foreground
            .DOFade(1, 0.25f)
            .SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator ForegroundFadeOut()
    {
        foreground.color = Color.black;
        foreground.gameObject.SetActive(true);

        Tween tween = foreground
            .DOFade(0, 0.25f)
            .SetEase(Ease.InCubic);

        yield return new WaitForSeconds(0.25f);
        foreground.gameObject.SetActive(false);
    }
}