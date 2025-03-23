using Analytics;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

public class UgsManager : Singleton<UgsManager>
{
    public bool IsInitialized { get; private set; }
    
    public void RecordLevelPassedEvent(int levelIndex, int attemptsCount, int starsCount)
    {
        var levelPassed = new LevelPassed(levelIndex, attemptsCount, starsCount);
        AnalyticsService.Instance.RecordEvent(levelPassed);
    }
    
    public void RecordLevelQuitEvent(int levelIndex, int attemptsCount)
    {
        var levelQuit = new LevelQuit(levelIndex, attemptsCount);
        AnalyticsService.Instance.RecordEvent(levelQuit);
    }
    
    public void RecordNewLevelAttemptEvent(int levelIndex, int attemptsCount)
    {
        var newLevelAttempt = new NewLevelAttempt(levelIndex, attemptsCount);
        AnalyticsService.Instance.RecordEvent(newLevelAttempt);
    }
    
    protected override async void Awake()
    {
        base.Awake();
        
        if (Instance != this)
        {
            return;
        }
        
        var options = new InitializationOptions();
        options.SetEnvironmentName("dev");
        await UnityServices.InitializeAsync(options);
        AnalyticsService.Instance.StartDataCollection();
        IsInitialized = true;
    }
}