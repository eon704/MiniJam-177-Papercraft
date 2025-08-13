using UnityEngine;

public class AdManager : Singleton<AdManager>
{
    [SerializeField] private MobileAdManager mobileAdManagerPrefab;
    [SerializeField] private PokiManager pokiManagerPrefab;

    protected override void Awake()
    {
        base.Awake();
    }
}