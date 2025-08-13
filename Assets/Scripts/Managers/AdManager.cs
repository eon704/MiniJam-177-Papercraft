using UnityEngine;
using UnityEngine.Events;


public class AdManager : Singleton<AdManager>
{
    [SerializeField] private PokiManager pokiManagerPrefab;
    [SerializeField] private MobileAdManager mobileAdManagerPrefab;

    private IAdEvents adEventsInstance;

    public UnityEvent OnAdLoadedChanged;

    public UnityEvent OnAdRewarded;

    public UnityEvent OnConsentRequired;

    public UnityEvent OnAdDisplayed;

    public UnityEvent OnAdClosed;

    protected override void Awake()
    {
        base.Awake();
#if UNITY_WEBGL
        adEventsInstance = Instantiate(pokiManagerPrefab);
#else
        adEventsInstance = Instantiate(mobileAdManagerPrefab);
#endif
    }

    public bool IsRewardedAdReady()
    {
        return adEventsInstance.IsRewardedAdReady();
    }

    public void ShowRewardedAd()
    {
        adEventsInstance.ShowRewardedAd();
    }
}