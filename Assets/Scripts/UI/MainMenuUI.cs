using System;
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


    private void Update()
    {
        if (levelsPanel.gameObject.activeSelf != true)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideLevels();
            }
        }
    }

    public void ShowLevels()
    {
        this.startPanel.interactable = false;

        var sequence = DOTween.Sequence();
        sequence.Append(title.DOScale(0.5f, 1f).SetEase(Ease.InOutCubic));
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

    public void HideLevels()
    {
        this.levelsPanel.interactable = false;

        var sequence = DOTween.Sequence();
        sequence.Append(this.levelsPanel.DOFade(-250, 0.75f).SetEase(Ease.InOutCubic));
        sequence.Append(title.DOScale(0.8f, 0.5f).SetEase(Ease.InOutCubic));
        sequence.Join(this.title.DOAnchorPosY(-330, 0.75f).SetEase(Ease.InOutCubic));
      
        sequence.AppendCallback(() =>
        {
            this.levelsPanel.gameObject.SetActive(false);
            this.startPanel.gameObject.SetActive(true);
            this.startPanel.alpha = 0;
        });
        sequence.AppendInterval(0.2f);
        sequence.Append(this.startPanel.DOFade(1, 0.75f).SetEase(Ease.InOutCubic));
        sequence.AppendCallback(() => this.startPanel.interactable = true);

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

        GlobalSoundManager.Instance.PlaySoundtrack(SettingsManager.Instance.gameSoundtrack);
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