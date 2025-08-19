using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BackgroundUI : MonoBehaviour
{
    [Header("Background")]
    [SerializeField] private Image _background;
    [SerializeField] private List<Sprite> _backgroundSprites;

    private void Awake()
    {
        int levelIndex = LevelManager.Instance.CurrentLevelIndex - 1;
        int backgroundIndex = levelIndex / 10;
        _background.sprite = _backgroundSprites[backgroundIndex];
    }
}