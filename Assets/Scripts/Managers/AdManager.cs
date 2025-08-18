using UnityEngine;
using UnityEngine.Events;


public class AdManager : Singleton<AdManager>
{
    [SerializeField] private PokiManager pokiManagerPrefab;
    [SerializeField] private MobileAdManager mobileAdManagerPrefab;

    public IAdEvents adEventsInstance { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (AdManager.Instance != this)
        {
            return;
        }

#if UNITY_WEBGL
        adEventsInstance = Instantiate(pokiManagerPrefab);
#elif UNITY_IOS || UNITY_ANDROID
        adEventsInstance = Instantiate(mobileAdManagerPrefab);
#endif
        adEventsInstance.OnAdClosed.AddListener(UnmuteAll);
    }

    public bool IsRewardedAdReady()
    {
        return adEventsInstance.IsRewardedAdReady();
    }

    public bool ShowRewardedAd()
    {
        GlobalSoundManager.Instance.MuteAll();
        return adEventsInstance.ShowRewardedAd();
    }

    private void UnmuteAll()
    {
        GlobalSoundManager.Instance.UnmuteAll();
    }
}