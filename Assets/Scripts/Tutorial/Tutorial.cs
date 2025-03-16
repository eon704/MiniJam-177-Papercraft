using UnityEngine;
using DG.Tweening;
using System.Collections;


public class Tutorial : MonoBehaviour
{
    
    private int _currentStep = 0;
    private int _clickCount = 0;
    private bool _tutorialStarted = false; // Flag to track if the tutorial has started
    private bool _tutorialEnabled = true; // Flag to track if the tutorial is enabled

    public GameObject[] tutorialSteps;
    public int[] clicksPerStep; // Array to store the number of clicks required for each step
    public float fadeDuration = 6f; // Duration of the fade animation
    public float delay = 2f; // Delay before starting the tutorial

    private void Awake()
    {
        foreach (var t in tutorialSteps)
        {
            var canvasGroup = t.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = t.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0;
        }
    }

    private void Start()
    {
        StartCoroutine(StartTutorialWithDelay(delay)); // Start the tutorial with a delay
    }

    private void Update()
    {
        if (_tutorialStarted && _tutorialEnabled && Input.GetMouseButtonDown(0)) // Detect left mouse button click
        {
            CompleteStep();
        }
    }

    private IEnumerator StartTutorialWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartTutorial();
    }

    public void StartTutorial()
    {
        Debug.Log("Starting Level 1 Tutorial");
        _tutorialStarted = true; // Set the flag to true when the tutorial starts
        FadeIn(tutorialSteps[_currentStep]); // Activate the first step with fade-in
    }

    public void CompleteStep()
    {
        if (_currentStep < clicksPerStep.Length)
        {
            _clickCount++;
            if (_clickCount >= clicksPerStep[_currentStep])
            {
                _clickCount = 0;
                _currentStep++;
                if (_currentStep < tutorialSteps.Length)
                {
                    FadeIn(tutorialSteps[_currentStep]); // Fade-in the next step
                }
                else
                {
                    Debug.Log("Tutorial Completed");
                }
            }
        }
    }

    private void FadeIn(GameObject step)
    {
        step.SetActive(true);
        CanvasGroup canvasGroup = step.GetComponent<CanvasGroup>();
        canvasGroup.DOFade(1, fadeDuration);
    }

    public void EnableTutorial()
    {
        _tutorialEnabled = true;
    }

    public void DisableTutorial()
    {
        _tutorialEnabled = false;
    }
}