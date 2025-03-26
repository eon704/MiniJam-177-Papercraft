using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private List<Image> starImages;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;
    [SerializeField] private Sprite defaultLevel;
    [SerializeField] private Sprite finishedLevel;

    private int _levelNumber;

    public void Initialize(int index, MainMenuUI mainMenuUI)
    {
        this._levelNumber = index;
        this.button.onClick.AddListener(mainMenuUI.StartGame);
    }

    private void Start()
    {
        this.button.onClick.AddListener(this.SetLevelIndex);
        this.levelText.text = this._levelNumber.ToString("D2");

        if (this._levelNumber > LevelManager.Instance.NextLevelIndex)
        {
            this.button.interactable = false;
        }

        var stars = LevelManager.Instance.GetLevelStars(this._levelNumber);

        for (int i = 0; i < this.starImages.Count; i++)
        {
            this.starImages[i].sprite = i + 1 <= stars ? this.fullStar : this.emptyStar;
        }
    }

    private void SetLevelIndex()
    {
        LevelManager.Instance.SetCurrentLevel(this._levelNumber);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOScale(1.1f, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
    }
}