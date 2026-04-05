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
    private Vector3 _originalScale;
    private bool _isNextLevel;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    public void Initialize(int index, MainMenuUI mainMenuUI)
    {
        _levelNumber = index;
        button.onClick.AddListener(mainMenuUI.StartGame);
    }

    private void Start()
    {
        button.onClick.AddListener(SetLevelIndex);
        levelText.text = _levelNumber.ToString("D2");

        if (_levelNumber > LevelManager.Instance.NextLevelIndex)
        {
            button.interactable = false;
        }

        var stars = LevelManager.Instance.GetLevelStars(_levelNumber);

        for (int i = 0; i < starImages.Count; i++)
        {
            starImages[i].sprite = i + 1 <= stars ? fullStar : emptyStar;
        }

        _isNextLevel = _levelNumber == LevelManager.Instance.NextLevelIndex;
        if (_isNextLevel)
            StartPulse();
    }

    private void SetLevelIndex()
    {
        LevelManager.Instance.SetCurrentLevel(_levelNumber);
    }

    private void StartPulse()
    {
        DOTween.Kill(transform);
        transform.localScale = _originalScale;
        var seq = DOTween.Sequence().SetId(transform).SetLoops(-1);
        seq.Append(transform.DOScale(_originalScale * 1.05f, 0.18f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOScale(_originalScale, 0.22f).SetEase(Ease.InQuad));
        seq.AppendInterval(0.9f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable) return;
        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxHover);
        DOTween.Kill(transform);
        transform.DOScale(_originalScale * 1.1f, 0.15f).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DOTween.Kill(transform);
        if (_isNextLevel)
            transform.DOScale(_originalScale, 0.2f).SetEase(Ease.OutQuad).OnComplete(StartPulse);
        else
            transform.DOScale(_originalScale, 0.15f).SetEase(Ease.OutQuad);
    }

    private void OnDisable()
    {
        DOTween.Kill(transform);
        transform.localScale = _originalScale;
    }

    private void OnDestroy()
    {
        DOTween.Kill(transform);
    }
}