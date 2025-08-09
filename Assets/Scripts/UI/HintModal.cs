using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

public class HintModal : MonoBehaviour
{
    [SerializeField] private HintButton hintButtonPrefab;
    [SerializeField] private Transform buttonsGrid;
    [SerializeField] private GameObject modalBackground;
    
    private BoardPrefab boardPrefab;
    private List<HintButton> hintButtons = new List<HintButton>();
    private int currentHintRequest = -1;

    public void Initialize(BoardPrefab board)
    {
        boardPrefab = board;
        CreateHintButtons();
        Hide();
        
        // Subscribe to ad reward events
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnAdRewarded.AddListener(OnAdRewarded);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from ad events
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnAdRewarded.RemoveListener(OnAdRewarded);
        }
    }

    private void CreateHintButtons()
    {
        // Clear existing buttons
        foreach (Transform child in buttonsGrid)
        {
            if (child.gameObject != hintButtonPrefab.gameObject)
                Destroy(child.gameObject);
        }
        hintButtons.Clear();

        // Create buttons for each hint (excluding start and end positions)
        int totalHints = boardPrefab.Board.TotalSolutionSteps - 2; // -2 to exclude start and final positions

        for (int i = 1; i <= totalHints; i++) // Start from 1 to skip the start position
        {
            HintButton hintButton = Instantiate(hintButtonPrefab, buttonsGrid);
            int hintIndex = i; // Capture for closure
            hintButton.Initialize(i, () => OnHintButtonClicked(hintIndex));
            hintButtons.Add(hintButton);
        }
    }

    private void OnHintButtonClicked(int hintNumber)
    {
        // Hide the modal first
        Hide();
        
        // Check if we can show an ad
        if (AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady())
        {
            // Store which hint was requested for after the ad
            currentHintRequest = hintNumber;
            
            // Show the rewarded ad
            bool adShown = AdManager.Instance.ShowRewardedAd();
            
            if (!adShown)
            {
                // Reset if ad failed to show
                currentHintRequest = -1;
            }
        }
    }

    private void OnAdRewarded()
    {
        // Only process if we have a pending hint request
        if (currentHintRequest <= 0) return;
        
        // Reveal the specific hint
        boardPrefab.RevealSpecificHint(currentHintRequest);
        
        // Reset the request
        currentHintRequest = -1;
    }

    public void Show()
    {
        // Update button states before showing
        UpdateButtonStates();
        modalBackground.SetActive(true);
    }

    public void Hide()
    {
        modalBackground.SetActive(false);
    }

    private void UpdateButtonStates()
    {
        // Update each button's visual state based on whether the hint is revealed
        foreach (HintButton hintButton in hintButtons)
        {
            int hintIndex = hintButton.GetHintIndex();
            
            // Check if this hint step is revealed by looking at the solution
            if (hintIndex < boardPrefab.Board.LevelData.CachedSolution.Count)
            {
                Vector2Int hintPosition = boardPrefab.Board.LevelData.CachedSolution[hintIndex].Position;
                Cell hintCell = boardPrefab.Board.GetCell(hintPosition);
                bool isRevealed = hintCell.IsHintRevealed.Value > -1;
                
                hintButton.UpdateVisualState(isRevealed);
            }
        }
    }
}