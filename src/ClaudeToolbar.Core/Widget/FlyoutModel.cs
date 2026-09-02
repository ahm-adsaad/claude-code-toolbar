using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

public sealed record FlyoutModel(IReadOnlyList<string> Lines, string StatusText);

public static class AgoFormatter
{
    public static string Format(TimeSpan ago)
    {
        if (ago < TimeSpan.Zero) ago = TimeSpan.Zero;
        if (ago < TimeSpan.FromMinutes(1)) return $"{(int)ago.TotalSeconds}s";
        if (ago < TimeSpan.FromHours(1)) return $"{(int)ago.TotalMinutes}m";
        return RemainingTimeFormatter.Format(ago);
    }
}

public static class FlyoutModelBuilder
{
    public static FlyoutModel Build(MonitorState state, DateTimeOffset now, Func<DateTimeOffset, string> formatClock)
    {
        var lines = new List<string>();
        if (state.Snapshot is { } s)
        {
            AddLine(lines, "Session", s.FiveHour, now, formatClock);
            AddLine(lines, "Weekly", s.SevenDay, now, formatClock);
            AddLine(lines, "Weekly Opus", s.SevenDayOpus, now, formatClock);
            AddLine(lines, "Weekly Sonnet", s.SevenDaySonnet, now, formatClock);
        }
        if (state.LastSuccess is { } last)
            lines.Add($"Updated {AgoFormatter.Format(now - last)} ago");

        var status = state.Status switch
        {
            UsageStatus.Ok => "OK",
            UsageStatus.Stale => $"Stale: {state.Message ?? "no connection"}",
            UsageStatus.Expired => "Login expired — run claude",
            UsageStatus.NoCredentials => "Not signed in — run claude",
            _ => "Loading…",
        };
        return new FlyoutModel(lines, status);
    }

    private static void AddLine(List<string> lines, string name, UsageWindow? w, DateTimeOffset now, Func<DateTimeOffset, string> formatClock)
    {
        if (w is null) return;
        var percent = PercentFormatter.Format(w.Utilization);
        if (w.ResetsAt is { } reset)
            lines.Add($"{name} {percent} · resets in {RemainingTimeFormatter.Format(reset, now)} · at {formatClock(reset)}");
        else
            lines.Add($"{name} {percent}");
    }
}
