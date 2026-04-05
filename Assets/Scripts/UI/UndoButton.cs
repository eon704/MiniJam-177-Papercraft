using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UndoButton : MonoBehaviour
{
    [SerializeField] private Player player;

    private Button _button;
    private Tween _hintTween;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
        player.OnUndoHistoryChange += OnUndoHistoryChange;
        player.OnNoMovesAvailable.AddListener(ShowHint);
        player.OnAllMovesExhausted.AddListener(ShowHint);
    }

    private void OnClick()
    {
        StopHint();
        player.UndoMove();
    }

    private void OnUndoHistoryChange(int historyCount)
    {
        _button.interactable = historyCount > 1;
        if (historyCount <= 1)
            StopHint();
    }

    private void ShowHint()
    {
        if (!_button.interactable) return;
        _hintTween?.Kill();
        transform.localScale = Vector3.one;
        _hintTween = transform
            .DOScale(1.18f, 0.35f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopHint()
    {
        _hintTween?.Kill();
        _hintTween = null;
        transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        _hintTween?.Kill();
    }
}
