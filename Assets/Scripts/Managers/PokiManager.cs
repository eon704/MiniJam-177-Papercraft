#if UNITY_WEBGL
using UnityEngine;

public class PokiManager : Singleton<PokiManager>
{
    protected override void Awake()
    {
        base.Awake();

        PokiUnitySDK.Instance.init();
    }


}
#endif