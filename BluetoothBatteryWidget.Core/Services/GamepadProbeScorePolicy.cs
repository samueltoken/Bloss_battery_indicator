namespace BluetoothBatteryWidget.Core.Services;

public static class GamepadProbeScorePolicy
{
    public const int StrictMinimumScore = 70;
    public const int AggressiveMinimumScore = 82;

    public static int GetMinimumScore(bool aggressiveFallback)
    {
        return aggressiveFallback ? AggressiveMinimumScore : StrictMinimumScore;
    }

    public static bool IsAccepted(int score, bool aggressiveFallback)
    {
        return score >= GetMinimumScore(aggressiveFallback);
    }
}
