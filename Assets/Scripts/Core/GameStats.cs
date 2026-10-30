// stats for the victory screen, static so they don't reset when the next level loads
public static class GameStats
{
    public static int Deaths;
    public static int Kills;
    public static int ShotsFired;
    public static int Hits;
    public static float RunTime;

    public static void Reset()
    {
        Deaths = 0;
        Kills = 0;
        ShotsFired = 0;
        Hits = 0;
        RunTime = 0f;
    }

    public static string FormattedTime()
    {
        int minutes = (int)(RunTime / 60f);
        float seconds = RunTime - minutes * 60f;
        return $"{minutes}:{seconds:00.00}";
    }
}
