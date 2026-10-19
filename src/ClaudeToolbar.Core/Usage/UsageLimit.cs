namespace ClaudeToolbar.Core.Usage;

/// <summary>The rate limits the usage endpoint reports, one per row.</summary>
public enum UsageLimit { FiveHour, SevenDay, SevenDayOpus, SevenDaySonnet, SevenDayFable }

public static class UsageLimits
{
    public static UsageWindow? Window(this UsageSnapshot snapshot, UsageLimit limit) => limit switch
    {
        UsageLimit.FiveHour => snapshot.FiveHour,
        UsageLimit.SevenDay => snapshot.SevenDay,
        UsageLimit.SevenDayOpus => snapshot.SevenDayOpus,
        UsageLimit.SevenDaySonnet => snapshot.SevenDaySonnet,
        UsageLimit.SevenDayFable => snapshot.SevenDayFable,
        _ => null,
    };
}
