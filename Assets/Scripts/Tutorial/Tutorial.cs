using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using System.Collections;


public class Tutorial : MonoBehaviour
{
    
    private int _currentStep = 0;
    private int _clickCount = 0;
    private bool _tutorialStarted = false; // Flag to track if the tutorial has started
    private bool _tutorialEnabled = true; // Flag to track if the tutorial is enabled

    public CanvasGroup[] tutorialSteps;
    public int[] clicksPerStep; // Array to store the number of clicks required for each step
    public float fadeDuration = 6f; // Duration of the fade animation
    public float delay = 2f; // Delay before starting the tutorial

    private void Awake()
    {
        foreach (var canvasGroup in tutorialSteps)
        {
            canvasGroup.alpha = 0;
        }
    }

    private void Start()
    {
        StartCoroutine(StartTutorialWithDelay(delay));
    }

    private void Update()
    {
        bool isClickedMouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool isClickedTap = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        if (_tutorialStarted && _tutorialEnabled && (isClickedMouse || isClickedTap))
        {
            CompleteStep();
        }
    }

    private void OnDestroy()
    {
        foreach (var step in tutorialSteps)
        {
            step.gameObject.transform.DOKill();
        }
    }

    private IEnumerator StartTutorialWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartTutorial();
    }

    public void StartTutorial()
    {
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
                FadeOut(tutorialSteps[_currentStep]); // Fade-out the current step
                _currentStep++;
                if (_currentStep < tutorialSteps.Length)
                {
                    FadeIn(tutorialSteps[_currentStep]); // Fade-in the next step
                }
            }
        }
    }

    private void FadeIn(CanvasGroup step)
    {
        step.DOFade(1, fadeDuration);
    }
    
    private void FadeOut(CanvasGroup step)
    {
        step
            .DOFade(0, fadeDuration)
            .OnComplete(() => step.alpha = 0);
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