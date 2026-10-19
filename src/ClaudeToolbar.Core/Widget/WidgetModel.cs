using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

/// <summary><paramref name="ShortLabel"/> is the two-character name the compact layouts use.</summary>
public sealed record WidgetRow(UsageLimit Limit, string Label, string ShortLabel, double Utilization, string PercentText, string TimeText, BarLevel Level);

public sealed record WidgetModel(IReadOnlyList<WidgetRow> Rows, bool Dimmed, bool ShowStaleDot, string? Notice);

public static class WidgetModelBuilder
{
    public const string SignInNotice = "Sign in with claude";
    public const string RunClaudeHint = "↻ run claude";
    public const string NoRowsNotice = "No rows enabled";
    public const string Placeholder = "—";

    public static WidgetModel Build(MonitorState state, AppSettings settings, DateTimeOffset now)
    {
        if (state.Status == UsageStatus.NoCredentials)
            return new WidgetModel([], false, false, SignInNotice);

        var snapshot = state.Snapshot;
        var a = settings.Appearance;
        var r = settings.Rows;
        var rows = new List<WidgetRow>(5);

        void Add(bool show, UsageLimit limit, string label, string shortLabel)
        {
            if (!show) return;
            var window = snapshot?.Window(limit);
            if (window is null)
            {
                // Plans without a Fable limit would otherwise carry a permanent "—" row.
                if (limit == UsageLimit.SevenDayFable) return;
                rows.Add(new WidgetRow(limit, label, shortLabel, 0, Placeholder, string.Empty, BarLevel.Ok));
                return;
            }
            var time = window.ResetsAt is { } reset ? RemainingTimeFormatter.Format(reset, now) : string.Empty;
            rows.Add(new WidgetRow(
                limit,
                label,
                shortLabel,
                window.Utilization,
                PercentFormatter.Format(window.Utilization),
                time,
                BarLevelResolver.Resolve(window.Utilization, a.WarnThreshold, a.CritThreshold)));
        }

        Add(r.ShowFiveHour, UsageLimit.FiveHour, "5h", "5h");
        Add(r.ShowSevenDay, UsageLimit.SevenDay, "7d", "7d");
        Add(r.ShowSevenDayOpus, UsageLimit.SevenDayOpus, "7d Opus", "Op");
        Add(r.ShowSevenDaySonnet, UsageLimit.SevenDaySonnet, "7d Sonnet", "So");
        Add(r.ShowSevenDayFable, UsageLimit.SevenDayFable, "7d Fable", "Fa");

        var expired = state.Status == UsageStatus.Expired;
        if (expired && rows.Count > 0)
            rows[0] = rows[0] with { TimeText = RunClaudeHint };

        return new WidgetModel(rows, expired, state.Status == UsageStatus.Stale, rows.Count == 0 ? NoRowsNotice : null);
    }
}
