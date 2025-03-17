namespace Analytics
{
  public class NewLevelAttempt : Unity.Services.Analytics.Event
  {
    public NewLevelAttempt(int levelIndex, int attemptsCount) : base("NewLevelAttempt")
    {
      SetParameter("levelIndex", levelIndex);
      SetParameter("attemptsCount", attemptsCount);
    }
  }
}