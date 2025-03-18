namespace Analytics
{
  public class LevelPassed : Unity.Services.Analytics.Event
  {
    public LevelPassed(int levelIndex, int attemptsCount, int starsCount) : base("LevelPassed")
    {
      SetParameter("levelIndex", levelIndex);
      SetParameter("attemptsCount", attemptsCount);
      SetParameter("starsCount", starsCount);
    }
  }
}