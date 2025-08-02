using UnityEngine;
using Unity.Services.LevelPlay;
using UnityEngine.Events;
using System.Collections;

public class AdManager : Singleton<AdManager>
{
    [Header("Ad Configuration")]
    [SerializeField] private string iosAdUnitId = "fkzi5sh7p8hyeuoc";
    [SerializeField] private string androidAdUnitId = "gigmwhy9jpw7vuys";
    
    [Header("Privacy Settings")]
    [SerializeField] private bool requireConsent = true;
    [SerializeField] private bool showConsentDialogOnStart = true;

    public UnityEvent OnAdLoadedChanged;
    public UnityEvent OnAdRewarded;
    public UnityEvent OnConsentRequired;

    private LevelPlayRewardedAd rewardedAd;
    private bool hasUserConsent = false;
    private bool consentChecked = false;
    
    // Get platform-specific ad unit ID
    private string AdUnitId
    {
        get
        {
#if UNITY_IOS
            return iosAdUnitId;
#elif UNITY_ANDROID
            return androidAdUnitId;
#else
            return iosAdUnitId; // Fallback for editor
#endif
        }
    }

    protected override void Awake()
    {
        base.Awake();

        // Subscribe to initialization events
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
    }

    private void OnInitSuccess(LevelPlayConfiguration configuration)
    {
        // Launch test suite in development builds
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.LaunchTestSuite();
#endif

        // Delay consent check to allow UI components to subscribe to events
        StartCoroutine(DelayedConsentCheck());
    }

    private void InitializeRewardedAd()
    {
        if (string.IsNullOrEmpty(AdUnitId))
        {
            Debug.LogError("Ad Unit ID is not set for current platform!");
            return;
        }

        rewardedAd = new LevelPlayRewardedAd(AdUnitId);
        SubscribeToRewardedAdEvents();
        LoadRewardedAd();
    }

    private void SubscribeToRewardedAdEvents()
    {
        // Subscribe to all rewarded ad events
        rewardedAd.OnAdLoaded += OnRewardedAdLoaded;
        rewardedAd.OnAdLoadFailed += OnRewardedAdLoadFailed;
        rewardedAd.OnAdDisplayed += OnRewardedAdDisplayed;
        rewardedAd.OnAdDisplayFailed += OnRewardedAdDisplayFailed;
        rewardedAd.OnAdRewarded += OnRewardedAdRewarded;
        rewardedAd.OnAdClicked += OnRewardedAdClicked;
        rewardedAd.OnAdClosed += OnRewardedAdClosed;
    }

    #region Rewarded Ad Event Handlers

    private void OnRewardedAdLoaded(LevelPlayAdInfo adInfo)
    {
        OnAdLoadedChanged?.Invoke();
    }

    private void OnRewardedAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogError($"❌ Rewarded ad failed to load. Error: {error}");
        
        // Notify that ad is not available
        OnAdLoadedChanged?.Invoke();
        
        // Implement exponential backoff for retries
        RetryLoadAd();
    }

    private void OnRewardedAdDisplayed(LevelPlayAdInfo adInfo)
    {
        // Ad displayed successfully
    }

    // NOTE: This method uses obsolete API that will be updated in LevelPlay SDK 9.0.0
    // The new signature will be: OnRewardedAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    #pragma warning disable CS0618 // Type or member is obsolete
    private void OnRewardedAdDisplayFailed(LevelPlayAdDisplayInfoError error)
    {
        Debug.LogError($"❌ Rewarded ad display failed. Error: {error}");
        
        // Load a new ad since this one failed to display
        LoadRewardedAd();
    }
    #pragma warning restore CS0618

    private void OnRewardedAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        // Grant the reward to the user
        GrantRewardToUser(reward);
    }

    private void OnRewardedAdClicked(LevelPlayAdInfo adInfo)
    {
        // Ad clicked
    }

    private void OnRewardedAdClosed(LevelPlayAdInfo adInfo)
    {
        // Preload the next ad
        LoadRewardedAd();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads a rewarded ad. Called automatically after ads are shown or failed.
    /// </summary>
    public void LoadRewardedAd()
    {
        if (rewardedAd == null)
        {
            return;
        }

        rewardedAd.LoadAd();
    }

    /// <summary>
    /// Shows a rewarded ad if one is available and ready.
    /// </summary>
    /// <returns>True if ad was shown, false if not ready or no consent</returns>
    public bool ShowRewardedAd()
    {
        // Check consent first
        if (!hasUserConsent)
        {
            return false;
        }

        if (!IsRewardedAdReady())
        {
            LoadRewardedAd();
            return false;
        }

        rewardedAd.ShowAd();
        return true;
    }

    /// <summary>
    /// Checks if a rewarded ad is loaded and ready to show.
    /// </summary>
    /// <returns>True if ad is ready and user has consented, false otherwise</returns>
    public bool IsRewardedAdReady()
    {
        return hasUserConsent && rewardedAd != null && rewardedAd.IsAdReady();
    }

    #endregion

    #region Privacy & Consent Management

    /// <summary>
    /// Delays consent check to allow UI components to subscribe to events first
    /// </summary>
    private System.Collections.IEnumerator DelayedConsentCheck()
    {
        // Wait for end of frame to ensure all Start() methods have been called
        yield return new WaitForEndOfFrame();
        
        // Additional small delay to be extra safe
        yield return new WaitForSeconds(0.1f);
        
        CheckUserConsent();
    }

    /// <summary>
    /// Checks if user consent is required and handles consent flow
    /// </summary>
    private void CheckUserConsent()
    {
        if (!requireConsent)
        {
            // Skip consent if not required (e.g., for regions outside GDPR scope)
            hasUserConsent = true;
            consentChecked = true;
            SetConsentInSDK(true);
            InitializeRewardedAd();
            return;
        }

        // Load consent from PlayerPrefs (persistent storage)
        LoadConsentFromStorage();

        print($"Consent checked: {consentChecked}, User consent: {hasUserConsent}");
        if (!consentChecked && showConsentDialogOnStart)
        {
            // Trigger consent dialog - other components can listen to this event
            OnConsentRequired?.Invoke();
        }
        else if (hasUserConsent)
        {
            SetConsentInSDK(true);
            InitializeRewardedAd();
        }
        else
        {
            SetConsentInSDK(false);
            // Don't initialize ads without consent
        }
    }

    /// <summary>
    /// Call this method when user grants consent
    /// </summary>
    public void SetUserConsent(bool consent)
    {
        hasUserConsent = consent;
        consentChecked = true;
        
        // Save consent to persistent storage
        SaveConsentToStorage();
        
        // Set consent in the ad SDK
        SetConsentInSDK(consent);
        
        if (consent)
        {
            InitializeRewardedAd();
        }
        else
        {
            // Clear any existing ads if consent is revoked
            if (rewardedAd != null)
            {
                UnsubscribeFromRewardedAdEvents();
                rewardedAd = null;
            }
        }
    }

    /// <summary>
    /// Gets the current consent status
    /// </summary>
    public bool HasUserConsent => hasUserConsent;

    /// <summary>
    /// Gets whether consent has been checked/set
    /// </summary>
    public bool IsConsentChecked => consentChecked;

    private void SetConsentInSDK(bool consent)
    {
        // Set GDPR consent in LevelPlay
        LevelPlay.SetMetaData("is_child_directed", "false");
        LevelPlay.SetMetaData("do_not_sell", consent ? "false" : "true");
        
        // Set GDPR consent - this is the main consent flag
        if (consent)
        {
            LevelPlay.SetMetaData("gdpr_consent", "true");
        }
        else
        {
            LevelPlay.SetMetaData("gdpr_consent", "false");
        }
    }

    private void SaveConsentToStorage()
    {
        PlayerPrefs.SetInt("AdManager_UserConsent", hasUserConsent ? 1 : 0);
        PlayerPrefs.SetInt("AdManager_ConsentChecked", consentChecked ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadConsentFromStorage()
    {
        if (PlayerPrefs.HasKey("AdManager_ConsentChecked"))
        {
            consentChecked = PlayerPrefs.GetInt("AdManager_ConsentChecked") == 1;
            hasUserConsent = PlayerPrefs.GetInt("AdManager_UserConsent") == 1;
        }
        else
        {
            // First time user - no consent stored
            consentChecked = false;
            hasUserConsent = false;
        }
    }

    /// <summary>
    /// Revokes user consent - can be called from settings menu
    /// </summary>
    public void RevokeConsent()
    {
        SetUserConsent(false);
    }

    /// <summary>
    /// Shows consent dialog again - can be called from settings menu
    /// </summary>
    public void ShowConsentDialog()
    {
        OnConsentRequired?.Invoke();
    }

    /// <summary>
    /// Manually trigger consent check - can be called by ConsentDialog after it's ready
    /// </summary>
    public void TriggerConsentCheckIfNeeded()
    {
        if (!consentChecked && requireConsent)
        {
            CheckUserConsent();
        }
    }

    #endregion

    #region Private Helper Methods

    private void RetryLoadAd()
    {
        // Simple retry after 3 seconds
        // In production, you might want to implement exponential backoff
        Invoke(nameof(LoadRewardedAd), 3f);
    }

    #endregion

    #region Reward System

    private void GrantRewardToUser(LevelPlayReward reward)
    {
        // Invoke the reward event for other components to respond
        OnAdRewarded?.Invoke();
    }

    #endregion

    #region Cleanup

    private void UnsubscribeFromRewardedAdEvents()
    {
        if (rewardedAd != null)
        {
            rewardedAd.OnAdLoaded -= OnRewardedAdLoaded;
            rewardedAd.OnAdLoadFailed -= OnRewardedAdLoadFailed;
            rewardedAd.OnAdDisplayed -= OnRewardedAdDisplayed;
            rewardedAd.OnAdDisplayFailed -= OnRewardedAdDisplayFailed;
            rewardedAd.OnAdRewarded -= OnRewardedAdRewarded;
            rewardedAd.OnAdClicked -= OnRewardedAdClicked;
            rewardedAd.OnAdClosed -= OnRewardedAdClosed;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        UnsubscribeFromRewardedAdEvents();
        
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;
    }

    #endregion

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"❌ LevelPlay initialization failed: {error}");
    }
}