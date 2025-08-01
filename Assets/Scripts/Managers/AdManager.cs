using UnityEngine;
using Unity.Services.LevelPlay;
using Unity.Services.Core;
using Unity.Services.Analytics;

public class AdManager : Singleton<AdManager>
{
    private const string AppKey = "223b9d07d";

    protected override void Awake()
    {
        base.Awake();
        
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        
        // Initialize LevelPlay with User ID from Unity Gaming Services
        InitializeLevelPlayWithUserId();
    }

    private async void InitializeLevelPlayWithUserId()
    {
        // Wait for Unity Gaming Services to be initialized first
        while (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            await System.Threading.Tasks.Task.Yield();
        }
        
        // Get the Unity User ID from Analytics service
        string userId = AnalyticsService.Instance.GetAnalyticsUserID();
        
        // Initialize LevelPlay with the User ID
        LevelPlay.Init(AppKey, userId);
        
        Debug.Log($"LevelPlay initialized with User ID: {userId}");
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