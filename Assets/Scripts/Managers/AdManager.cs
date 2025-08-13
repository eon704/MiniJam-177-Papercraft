using UnityEngine;
using UnityEngine.Events;


public class AdManager : Singleton<AdManager>
{
    [SerializeField] private PokiManager pokiManagerPrefab;
    [SerializeField] private MobileAdManager mobileAdManagerPrefab;

    private IAdEvents adEventsInstance;

    [HideInInspector]
    public UnityEvent OnAdLoadedChanged;
    [HideInInspector]
    public UnityEvent OnAdRewarded;

    [HideInInspector]
    public UnityEvent OnConsentRequired;

    [HideInInspector]
    public UnityEvent OnAdDisplayed;
    [HideInInspector]
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