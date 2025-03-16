
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private int _currentLevelIndex;

    [SerializeField] private GameObject[] tutorialLevels;

    private void Start()
    {
        _currentLevelIndex = LevelManager.Instance.CurrentLevelIndex;
    }

    private void Update()
    {
        for (int i = 0; i < tutorialLevels.Length; i++)
        {
            tutorialLevels[i].SetActive(i == _currentLevelIndex - 1);
        }
    }
}