using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

/// <summary>A flyout line; <paramref name="Tint"/> colours it like a bar at that level.</summary>
public sealed record FlyoutLine(string Text, BarLevel? Tint = null);

public sealed record FlyoutModel(IReadOnlyList<FlyoutLine> Lines, string StatusText);

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
    public static FlyoutModel Build(
        MonitorState state,
        DateTimeOffset now,
        Func<DateTimeOffset, string> formatClock,
        IReadOnlyDictionary<UsageLimit, PaceEstimate>? pace = null,
        bool hooksInstalled = true)
    {
        var lines = new List<FlyoutLine>();
        if (state.Snapshot is { } s)
        {
            // As in the widget, an expired login leaves numbers too old to project from.
            var estimates = state.Status == UsageStatus.Expired ? null : pace;
            void Add(string name, UsageLimit limit)
            {
                if (s.Window(limit) is not { } w) return;
                AddLine(lines, name, w, now, formatClock);
                if (estimates?.GetValueOrDefault(limit) is { } e) AddPace(lines, e, hooksInstalled);
            }
            Add("Session", UsageLimit.FiveHour);
            Add("Weekly", UsageLimit.SevenDay);
            Add("Weekly Opus", UsageLimit.SevenDayOpus);
            Add("Weekly Sonnet", UsageLimit.SevenDaySonnet);
            Add("Weekly Fable", UsageLimit.SevenDayFable);
        }
        if (state.LastSuccess is { } last)
            lines.Add(new FlyoutLine($"Updated {AgoFormatter.Format(now - last)} ago"));

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

    private static void AddLine(List<FlyoutLine> lines, string name, UsageWindow w, DateTimeOffset now, Func<DateTimeOffset, string> formatClock)
    {
        var percent = PercentFormatter.Format(w.Utilization);
        if (w.ResetsAt is { } reset)
            lines.Add(new FlyoutLine($"{name} {percent} · resets in {RemainingTimeFormatter.Format(reset, now)} · at {formatClock(reset)}"));
        else
            lines.Add(new FlyoutLine($"{name} {percent}"));
    }

    private static void AddPace(List<FlyoutLine> lines, PaceEstimate e, bool hooksInstalled)
    {
        switch (e.Verdict)
        {
            case PaceVerdict.Learning:
                lines.Add(new FlyoutLine("Learning your pace"));
                return;
            case PaceVerdict.UsedUp:
                return;
            case PaceVerdict.RunsOutFirst when e.WorkLeft is { } left:
                var tint = left < WidgetModelBuilder.CritWorkLeft ? BarLevel.Crit : BarLevel.Warn;
                lines.Add(new FlyoutLine($"At your pace: about {PaceFormatter.Format(left, approximate: false)} of work left, runs out before the reset", tint));
                break;
            default:
                lines.Add(new FlyoutLine("At your pace this lasts until the reset"));
                break;
        }
        if (e.WorkLeftNow is { } now)
            lines.Add(new FlyoutLine($"With {e.SessionsNow} sessions running: about {PaceFormatter.Format(now, approximate: false)}"));
        lines.Add(new FlyoutLine(e.Basis == PaceBasis.Work
            ? $"Based on your last {PaceFormatter.Span(e.Span)} of work"
            : $"Based on clock time over the last {PaceFormatter.Span(e.Span)}" + (hooksInstalled ? "" : "; install the hooks for a better estimate")));
    }
}
