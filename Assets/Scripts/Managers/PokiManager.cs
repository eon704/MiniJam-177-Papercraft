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
        OnAdDisplayed?.Invoke();
        PokiUnitySDK.Instance.rewardedBreakCallBack += rewardedBreakCallback;
        PokiUnitySDK.Instance.rewardedBreak();
        return true;
    }

    protected override void Awake()
    {
        base.Awake();
        PokiUnitySDK.Instance.init();
    }

    private void rewardedBreakCallback(bool withReward)
    {
        OnAdRewarded?.Invoke();
        OnAdClosed?.Invoke();
    }
}