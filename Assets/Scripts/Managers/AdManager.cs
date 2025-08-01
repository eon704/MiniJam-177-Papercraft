using UnityEngine;
using Unity.Services.LevelPlay;
using Unity.Services.Core;
using Unity.Services.Analytics;

public class AdManager : Singleton<AdManager>
{
    protected override void Awake()
    {
        base.Awake();
        
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
    }

    private void OnInitSuccess(LevelPlayConfiguration configuration)
    {
        Debug.Log("AdManager initialized successfully.");
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"AdManager initialization failed: {error}");
    }
}