namespace ClaudeToolbar.Core.Formatting;

public static class PercentFormatter
{
    public static string Format(double utilization)
    {
        var clamped = Math.Clamp(utilization, 0, 100);
        var rounded = (int)Math.Round(clamped, MidpointRounding.AwayFromZero);
        return $"{rounded}%";
    }
}
