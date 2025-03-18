namespace Analytics
{
  public class LevelQuit : Unity.Services.Analytics.Event
  {
    public LevelQuit(int levelIndex, int attemptsCount) : base("LevelQuit")
    {
      SetParameter("levelIndex", levelIndex);
      SetParameter("attemptsCount", attemptsCount);
    }
  }
}