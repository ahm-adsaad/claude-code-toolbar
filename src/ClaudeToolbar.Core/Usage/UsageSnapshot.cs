namespace ClaudeToolbar.Core.Usage;

public sealed record UsageSnapshot(
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    UsageWindow? SevenDayOpus,
    UsageWindow? SevenDaySonnet,
    UsageWindow? SevenDayFable,
    DateTimeOffset FetchedAt)
{
    public IEnumerable<UsageWindow> Windows
    {
        get
        {
            if (FiveHour is not null) yield return FiveHour;
            if (SevenDay is not null) yield return SevenDay;
            if (SevenDayOpus is not null) yield return SevenDayOpus;
            if (SevenDaySonnet is not null) yield return SevenDaySonnet;
            if (SevenDayFable is not null) yield return SevenDayFable;
        }
    }

    public DateTimeOffset? NextReset
    {
        get
        {
            var resets = Windows.Where(w => w.ResetsAt is not null).Select(w => w.ResetsAt!.Value).ToList();
            return resets.Count == 0 ? null : resets.Min();
        }
    }
}
