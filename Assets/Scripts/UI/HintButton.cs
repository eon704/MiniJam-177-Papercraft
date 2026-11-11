using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Hint" button in GameUI. Each press reveals the next move of the level's cached solution
/// (Board.RevealNextHint): the target cell shows its hint marker and pulses, and the form card
/// used for that move gets a short punch. Hints follow the solution from the level start, so they
/// stay correct as long as the player has not left that route (Restart brings them back on it).
/// Also triggered by H / gamepad Select through <see cref="PcInputController"/>.
/// </summary>
[RequireComponent(typeof(Button))]
public class HintButton : MonoBehaviour
{
    [SerializeField] private GameController gameController;
    [SerializeField] private Color pulseColor = new(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private float pulseDuration = 0.9f;

    public static HintButton Active { get; private set; }

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(Reveal);
        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();
    }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(Reveal);
    }

    private void Update()
    {
        if (_button == null || gameController == null) return;
        var board = gameController.BoardPrefab != null ? gameController.BoardPrefab.Board : null;
        _button.interactable = board != null && board.HasMoreHints && board.TotalSolutionSteps > 1;
    }

    public void Reveal()
    {
        if (gameController == null) return;
        BoardPrefab boardPrefab = gameController.BoardPrefab;
        Board board = boardPrefab != null ? boardPrefab.Board : null;
        if (board == null || !board.HasMoreHints) return;

        int step = board.CurrentHintStep;
        (Cell cell, int depth) = board.RevealNextHint(out _);
        if (cell == null) return;

        CellPrefab cellPrefab = boardPrefab.GetCellPrefab(cell);
        if (cellPrefab != null)
        {
            var hints = cellPrefab.HintObjects;
            if (hints != null && hints.Count > 0)
                hints[Mathf.Clamp(depth, 0, hints.Count - 1)].SetActive(true);
            cellPrefab.DoPulse(pulseDuration, pulseColor);
        }

        var solution = boardPrefab.LevelData != null ? boardPrefab.LevelData.CachedSolution : null;
        if (solution != null && step >= 0 && step < solution.Count)
            PunchFormCard(solution[step].State);

        FMODAudioManager.Instance.PlayOneShot(FMODAudioManager.Instance.sfxClick);
    }

    private static void PunchFormCard(Player.StateType form)
    {
        foreach (var card in FindObjectsByType<StateCardUI>(FindObjectsSortMode.None))
        {
            if (card.StateType != form || !card.isActiveAndEnabled) continue;
            card.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.6f);
        }
    }
}
