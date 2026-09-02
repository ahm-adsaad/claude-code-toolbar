namespace ClaudeToolbar.Core.Formatting;

public static class RemainingTimeFormatter
{
    public static string Format(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "now";
        if (remaining < TimeSpan.FromMinutes(1)) return "<1m";
        if (remaining < TimeSpan.FromHours(1)) return $"{(int)remaining.TotalMinutes}m";
        if (remaining < TimeSpan.FromDays(1)) return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
    }

    public static string Format(DateTimeOffset resetsAt, DateTimeOffset now) => Format(resetsAt - now);
}
