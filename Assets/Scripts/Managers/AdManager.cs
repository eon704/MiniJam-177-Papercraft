using UnityEngine;
using Unity.Services.LevelPlay;
using UnityEngine.Events;

public class AdManager : Singleton<AdManager>
{
    [Header("Ad Configuration")]
    [SerializeField] private string iosAdUnitId = "fkzi5sh7p8hyeuoc";
    [SerializeField] private string androidAdUnitId = "gigmwhy9jpw7vuys";

    public UnityEvent OnAdLoadedChanged;
    public UnityEvent OnAdRewarded;

    private LevelPlayRewardedAd rewardedAd;
    
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

        Debug.Log("AdManager initialized successfully.");
        InitializeRewardedAd();
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
        Debug.Log($"✅ Rewarded ad loaded successfully. Ad Unit: {AdUnitId}");
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
        Debug.Log($"📺 Rewarded ad displayed. Revenue: {adInfo.Revenue}");
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
        Debug.Log($"🎁 User earned reward: {reward.Name} x{reward.Amount}");
        
        // Grant the reward to the user
        GrantRewardToUser(reward);
    }

    private void OnRewardedAdClicked(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"👆 Rewarded ad clicked");
    }

    private void OnRewardedAdClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"❌ Rewarded ad closed");
        
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
            Debug.LogWarning("⚠️ Rewarded ad not initialized yet.");
            return;
        }

        Debug.Log("🔄 Loading rewarded ad...");
        rewardedAd.LoadAd();
    }

    /// <summary>
    /// Shows a rewarded ad if one is available and ready.
    /// </summary>
    /// <returns>True if ad was shown, false if not ready</returns>
    public bool ShowRewardedAd()
    {
        if (!IsRewardedAdReady())
        {
            Debug.LogWarning("⚠️ Rewarded ad is not ready to show. Loading new ad...");
            LoadRewardedAd();
            return false;
        }

        Debug.Log("🎬 Showing rewarded ad...");
        rewardedAd.ShowAd();
        return true;
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
        Debug.Log($"🎁 Granting reward to user: {reward.Name} x{reward.Amount}");
        
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