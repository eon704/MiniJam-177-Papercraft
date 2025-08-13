#if UNITY_IOS || UNITY_ANDROID

using UnityEngine;
using Unity.Services.LevelPlay;
using UnityEngine.Events;
using System.Collections;

public class AdManager : Singleton<AdManager>
{
    [Header("App Keys")]
    private string androidAppKey = "231f070f5";
    private string iosAppKey = "223b9d07d";

    [Header("Ad Configuration")]
    [SerializeField] private string iosAdUnitId = "fkzi5sh7p8hyeuoc";
    [SerializeField] private string androidAdUnitId = "gigmwhy9jpw7vuys";

    [Header("Privacy Settings")]
    [SerializeField] private bool requireConsent = true;
    [SerializeField] private bool showConsentDialogOnStart = true;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDetailedLogging = false;

    public UnityEvent OnAdLoadedChanged;
    public UnityEvent OnAdRewarded;
    public UnityEvent OnConsentRequired;
    public UnityEvent OnAdDisplayed;
    public UnityEvent OnAdClosed;

    private LevelPlayRewardedAd rewardedAd;
    private bool targetedAdsConsent = false;
    private bool consentChecked = false;
    private int retryAttempts = 0;
    private const int maxRetryAttempts = 5;
    private const float baseRetryDelay = 1f;

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

    private string AppKey
    {
        get
        {
#if UNITY_IOS
            return iosAppKey;
#elif UNITY_ANDROID
            return androidAppKey;
#else
            return iosAppKey; // Fallback for editor
#endif
        }
    }

    protected override void Awake()
    {
        base.Awake();

        InitMetadata();

        // Subscribe to initialization events
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
    }

    private IEnumerator Start()
    {
        yield return null;

        CheckUserConsent();

        yield return new WaitUntil(() => consentChecked);

        LevelPlay.Init(AppKey);
    }

    private void InitMetadata()
    {
        LevelPlay.SetMetaData("is_child_directed", "false");
        LevelPlay.SetMetaData("Yandex_COPPA", "false");

        // CCPA: Force do_not_sell to true for California users
        if (IsCaliforniaUser())
        {
            LevelPlay.SetMetaData("do_not_sell", "true");
        }
        else
        {
            LevelPlay.SetMetaData("do_not_sell", "false");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        LevelPlay.SetMetaData("is_test_suite", "enable");
#endif
    }

    private void OnInitSuccess(LevelPlayConfiguration configuration)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // Launch test suite in development builds
        StartCoroutine(DelayedTestSuiteLaunch());
#endif
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private IEnumerator DelayedTestSuiteLaunch()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("🔧 Launching LevelPlay test suite in development mode");
        LevelPlay.LaunchTestSuite();
    }
#endif

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
        if (enableDetailedLogging)
        {
            Debug.Log($"✅ Rewarded ad loaded successfully. AdInfo: {adInfo}");
        }

        // Reset retry attempts on successful load
        retryAttempts = 0;
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
        if (enableDetailedLogging)
        {
            Debug.Log($"📺 Rewarded ad displayed. AdInfo: {adInfo}");
        }

        OnAdDisplayed?.Invoke();
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
        if (enableDetailedLogging)
        {
            Debug.Log($"🎁 User rewarded! AdInfo: {adInfo}, Reward: {reward.Amount} {reward.Name}");
        }

        // Grant the reward to the user
        GrantRewardToUser(reward);
    }

    private void OnRewardedAdClicked(LevelPlayAdInfo adInfo)
    {
        // Ad clicked
    }

    private void OnRewardedAdClosed(LevelPlayAdInfo adInfo)
    {
        OnAdClosed?.Invoke();
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

        // Optional: Add network check for better UX
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("⚠️ No internet connection - ad load may fail");
        }

        rewardedAd.LoadAd();
    }

    /// <summary>
    /// Shows a rewarded ad if one is available and ready.
    /// </summary>
    /// <returns>True if ad was shown, false if not ready</returns>
    public bool ShowRewardedAd()
    {
        // Check if ad is ready
        if (!IsRewardedAdReady())
        {
            LoadRewardedAd();
            return false;
        }

        try
        {
            rewardedAd.ShowAd();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to show rewarded ad: {e.Message}");
            LoadRewardedAd(); // Reload after failure
            return false;
        }
    }

    /// <summary>
    /// Checks if a rewarded ad is loaded and ready to show.
    /// </summary>
    /// <returns>True if ad is ready, false otherwise</returns>
    public bool IsRewardedAdReady()
    {
        return rewardedAd != null && rewardedAd.IsAdReady();
    }

    #endregion

    #region Privacy & Consent Management
    /// <summary>
    /// Checks if user consent is required and handles consent flow
    /// </summary>
    private void CheckUserConsent()
    {
        if (!requireConsent)
        {
            // Skip consent if not required (e.g., for regions outside GDPR scope)
            targetedAdsConsent = true;
            consentChecked = true;
            SetConsentInSDK(true);
            InitializeRewardedAd();
            return;
        }

        // Load consent from PlayerPrefs (persistent storage)
        LoadConsentFromStorage();

        if (!consentChecked && showConsentDialogOnStart)
        {
            // Trigger consent dialog - other components can listen to this event
            OnConsentRequired?.Invoke();
        }
        else
        {
            // Always initialize ads, but set consent in SDK accordingly
            SetConsentInSDK(targetedAdsConsent);
            InitializeRewardedAd();
        }
    }

    /// <summary>
    /// Call this method when user grants or revokes consent for targeted ads.
    /// Ads will always be served - consent only affects personalization.
    /// </summary>
    public void SetUserConsent(bool consent)
    {
        targetedAdsConsent = consent;
        consentChecked = true;

        // Save consent to persistent storage
        SaveConsentToStorage();

        // Set consent in the ad SDK
        SetConsentInSDK(consent);

        // ALWAYS initialize ads regardless of consent
        // The SDK will serve personalized vs non-personalized ads based on consent flags
        if (rewardedAd == null)
        {
            InitializeRewardedAd();
        }
    }

    /// <summary>
    /// Gets the current consent status
    /// </summary>
    public bool HasUserConsent => targetedAdsConsent;

    /// <summary>
    /// Gets whether consent has been checked/set
    /// </summary>
    public bool IsConsentChecked => consentChecked;

    private void SetConsentInSDK(bool consent)
    {
        // Set GDPR consent - this is the main consent flag
        LevelPlay.SetMetaData("gdpr_consent", consent ? "true" : "false");
        // Set non-personalized ads flag if consent is declined
        LevelPlay.SetMetaData("is_non_personalized", consent ? "false" : "true");
    }

    /// <summary>
    /// Detects California users for CCPA compliance on iOS/Android.
    /// Uses device timezone as primary indicator.
    /// </summary>
    private bool IsCaliforniaUser()
    {
        try
        {
            // Check if device is in Pacific timezone (California's timezone)
            var timezone = System.TimeZoneInfo.Local;
            var utcOffset = timezone.GetUtcOffset(System.DateTime.Now);

            // Pacific Time: UTC-8 (standard) or UTC-7 (daylight saving)
            var offsetHours = utcOffset.TotalHours;
            bool isPacificTime = offsetHours == -8 || offsetHours == -7;

            if (isPacificTime)
            {
                // Double-check with region if available
                var region = System.Globalization.RegionInfo.CurrentRegion;
                if (region?.TwoLetterISORegionName == "US")
                {
                    return true;
                }

                // If region unavailable but timezone matches, assume California for CCPA safety
                return true;
            }

            return false;
        }
        catch (System.Exception)
        {
            // Fallback: assume not California to avoid unnecessary restrictions
            return false;
        }
    }

    private void SaveConsentToStorage()
    {
        PlayerPrefs.SetInt("AdManager_UserConsent", targetedAdsConsent ? 1 : 0);
        PlayerPrefs.SetInt("AdManager_ConsentChecked", consentChecked ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadConsentFromStorage()
    {
        if (PlayerPrefs.HasKey("AdManager_ConsentChecked"))
        {
            consentChecked = PlayerPrefs.GetInt("AdManager_ConsentChecked") == 1;
            targetedAdsConsent = PlayerPrefs.GetInt("AdManager_UserConsent") == 1;
        }
        else
        {
            // First time user - no consent stored
            consentChecked = false;
            targetedAdsConsent = false;
        }
    }

    /// <summary>
    /// Revokes user consent - ads will continue but be non-personalized
    /// </summary>
    public void RevokeConsent()
    {
        SetUserConsent(false);
        // Note: Ads continue serving, just non-personalized
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
        if (retryAttempts >= maxRetryAttempts)
        {
            Debug.LogWarning($"❌ Max retry attempts ({maxRetryAttempts}) reached for rewarded ad loading. Stopping retries.");
            retryAttempts = 0;
            Invoke(nameof(LoadRewardedAd), 120f); // Retry after 2 minutes
            return;
        }

        retryAttempts++;

        // Exponential backoff: delay = baseDelay * 2^(attempts-1)
        // Results in delays: 1s, 2s, 4s, 8s, 16s
        float delay = baseRetryDelay * Mathf.Pow(2, retryAttempts - 1);

        Debug.Log($"🔄 Retrying ad load in {delay}s (attempt {retryAttempts}/{maxRetryAttempts})");

        Invoke(nameof(LoadRewardedAd), delay);
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

        // Add retry logic for initialization
        StartCoroutine(RetryInitialization());
    }

    private IEnumerator RetryInitialization()
    {
        yield return new WaitForSeconds(5f);
        Debug.Log("🔄 Retrying LevelPlay initialization...");
        // Note: Initialization retry would need to be triggered from your main initialization flow
        // since LevelPlay doesn't expose a direct retry method
    }
}
#endif