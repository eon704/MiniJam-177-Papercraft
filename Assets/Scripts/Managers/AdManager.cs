using UnityEngine;
using UnityEngine.Events;


public class AdManager : Singleton<AdManager>
{
    [SerializeField] private PokiManager pokiManagerPrefab;
    [SerializeField] private MobileAdManager mobileAdManagerPrefab;

    public IAdEvents ADEventsInstance { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (AdManager.Instance != this)
        {
            return;
        }

#if ITCH
        ADEventsInstance = null;
        return;
#elif POKI
        adEventsInstance = Instantiate(pokiManagerPrefab);
#elif UNITY_IOS || UNITY_ANDROID
        adEventsInstance = Instantiate(mobileAdManagerPrefab);
#endif
        ADEventsInstance.OnAdClosed.AddListener(UnmuteAll);
    }

    public bool IsRewardedAdReady()
    {
        return ADEventsInstance?.IsRewardedAdReady() ?? false;
    }

    public bool ShowRewardedAd()
    {
        if (ADEventsInstance == null)
            return false; 
        
        GlobalSoundManager.Instance.MuteAll();
        return ADEventsInstance.ShowRewardedAd();
    }

    private void UnmuteAll()
    {
        GlobalSoundManager.Instance.UnmuteAll();
    }
}