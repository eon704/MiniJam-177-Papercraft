using UnityEngine.Events;

public class PokiManager : Singleton<PokiManager>, IAdEvents
{
    public UnityEvent OnAdLoadedChanged { get; } = new();

    public UnityEvent OnAdRewarded { get; } = new();

    public UnityEvent OnConsentRequired { get; } = new();

    public UnityEvent OnAdDisplayed { get; } = new();

    public UnityEvent OnAdClosed { get; } = new();

    public bool IsRewardedAdReady() => true;

    public bool ShowRewardedAd()
    {
#if UNITY_WEBGL
        OnAdDisplayed?.Invoke();
        PokiUnitySDK.Instance.rewardedBreakCallBack += rewardedBreakCallback;
        PokiUnitySDK.Instance.rewardedBreak();
#endif
        return true;
    }

    protected override void Awake()
    {
#if UNITY_WEBGL
        base.Awake();
        PokiUnitySDK.Instance.init();
#endif
    }

    private void rewardedBreakCallback(bool withReward)
    {
#if UNITY_WEBGL
        OnAdRewarded?.Invoke();
        OnAdClosed?.Invoke();
#endif
    }
}