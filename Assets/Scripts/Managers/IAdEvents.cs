using UnityEngine.Events;

public interface IAdEvents
{
    UnityEvent OnAdLoadedChanged { get; }
    UnityEvent OnAdRewarded { get; }
    UnityEvent OnConsentRequired { get; }
    UnityEvent OnAdDisplayed { get; }
    UnityEvent OnAdClosed { get; }

    public bool ShowRewardedAd();
    public bool IsRewardedAdReady();
}
