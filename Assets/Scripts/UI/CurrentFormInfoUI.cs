using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class CurrentFormInfoUI : MonoBehaviour
{
    [Serializable]
    private struct FormInfo
    {
        public Player.StateType stateType;
        public Sprite preview;
        public Sprite exhaustedPreview;
        public LocalizedString formName;
        public LocalizedString description;
    }

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private GameController gameController;

    [Header("Panel Fields")]
    [SerializeField] private Image previewImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Image movesImage;
    [SerializeField] private Sprite movesActiveSprite;
    [SerializeField] private Sprite movesExhaustedSprite;

    [Header("Default State (no form selected)")]
    [SerializeField] private Sprite defaultPreview;
    [SerializeField] private LocalizedString defaultName;
    [SerializeField] private LocalizedString defaultDescription;

    [Header("Form Infos")]
    [SerializeField] private List<FormInfo> formInfos;

    private Dictionary<Player.StateType, FormInfo> _formInfoMap;
    private Dictionary<Player.StateType, int> _movesPerForm = new();
    private Player.StateType _currentStateType;
    private bool _formSelected;

    private void Awake()
    {
        _formInfoMap = new Dictionary<Player.StateType, FormInfo>();
        foreach (var info in formInfos)
            _formInfoMap[info.stateType] = info;
    }

    private void OnEnable()
    {
        defaultName.StringChanged += OnDefaultNameChanged;
        defaultDescription.StringChanged += OnDefaultDescriptionChanged;
    }

    private void OnDisable()
    {
        defaultName.StringChanged -= OnDefaultNameChanged;
        defaultDescription.StringChanged -= OnDefaultDescriptionChanged;
    }

    private void Start()
    {
        player.OnTransformation.AddListener(OnTransformation);
        player.OnMovesLeftChanged.AddListener(OnMovesChanged);
        gameController.OnMapReset.AddListener(OnMapReset);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnDefaultNameChanged(string s)
    {
        if (!_formSelected && nameText != null)
            nameText.text = s;
    }

    private void OnDefaultDescriptionChanged(string s)
    {
        if (!_formSelected && descriptionText != null)
            descriptionText.text = s;
    }

    private void OnMapReset()
    {
        _formSelected = false;
        _movesPerForm.Clear();
        UpdateMovesSprite(1); // reset to active look
        ShowDefault();
    }

    private void OnTransformation(Player.StateType stateType)
    {
        _formSelected = true;
        _currentStateType = stateType;
        RefreshForm();
        PunchPreview();
    }

    private void OnMovesChanged(Player.StateType stateType, int movesLeft)
    {
        _movesPerForm[stateType] = movesLeft;

        if (!_formSelected || stateType != _currentStateType)
            return;

        UpdatePreviewSprite();
        if (movesText != null)
            movesText.text = movesLeft.ToString();
        UpdateMovesSprite(movesLeft);
        PunchMovesText();
    }

    private void OnLocaleChanged(Locale _)
    {
        if (_formSelected) RefreshForm();
        // Default name/description are handled automatically via StringChanged
    }

    private void ShowDefault()
    {
        if (previewImage != null && defaultPreview != null)
            previewImage.sprite = defaultPreview;
        if (movesText != null)
            movesText.text = string.Empty;
        // StringChanged callbacks on defaultName/defaultDescription fire automatically,
        // but force an immediate update here in case locale hasn't changed
        defaultName.GetLocalizedStringAsync().Completed +=
            h => { if (!_formSelected && nameText != null) nameText.text = h.Result; };
        defaultDescription.GetLocalizedStringAsync().Completed +=
            h => { if (!_formSelected && descriptionText != null) descriptionText.text = h.Result; };
    }

    private void RefreshForm()
    {
        if (!_formInfoMap.TryGetValue(_currentStateType, out var info))
            return;

        UpdatePreviewSprite();

        int moves = _movesPerForm.TryGetValue(_currentStateType, out var m) ? m : 0;
        if (movesText != null)
            movesText.text = moves.ToString();
        UpdateMovesSprite(moves);

        info.formName.GetLocalizedStringAsync().Completed +=
            handle => { if (_formSelected && nameText != null) nameText.text = handle.Result; };

        info.description.GetLocalizedStringAsync().Completed +=
            handle => { if (_formSelected && descriptionText != null) descriptionText.text = handle.Result; };
    }

    // ── Animations ─────────────────────────────────────────────────────────

    private void PunchPreview()
    {
        if (previewImage == null) return;
        DOTween.Kill(previewImage.transform);
        previewImage.transform.localScale = Vector3.one;
        previewImage.transform.DOPunchScale(Vector3.one * 0.2f, 0.35f, 5, 0.4f);
    }

    private void PunchMovesText()
    {
        if (movesText == null) return;
        DOTween.Kill(movesText.transform);
        movesText.transform.localScale = Vector3.one;
        movesText.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 4, 0.3f);
    }

    // ── Sprites ─────────────────────────────────────────────────────────────

    private void UpdateMovesSprite(int movesLeft)
    {
        if (movesImage == null) return;
        bool exhausted = movesLeft <= 0;
        if (exhausted && movesExhaustedSprite != null) movesImage.sprite = movesExhaustedSprite;
        else if (!exhausted && movesActiveSprite != null) movesImage.sprite = movesActiveSprite;
    }

    private void UpdatePreviewSprite()
    {
        if (previewImage == null) return;
        if (!_formInfoMap.TryGetValue(_currentStateType, out var info)) return;

        bool isExhausted = _movesPerForm.TryGetValue(_currentStateType, out var moves) && moves <= 0;
        previewImage.sprite = isExhausted && info.exhaustedPreview != null
            ? info.exhaustedPreview
            : info.preview;
    }
}
