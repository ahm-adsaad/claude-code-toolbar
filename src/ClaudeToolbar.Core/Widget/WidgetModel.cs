using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

public sealed record WidgetRow(string Label, double Utilization, string PercentText, string TimeText, BarLevel Level);

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
        var rows = new List<WidgetRow>(4);

        void Add(bool show, string label, UsageWindow? window)
        {
            if (!show) return;
            if (window is null)
            {
                rows.Add(new WidgetRow(label, 0, Placeholder, string.Empty, BarLevel.Ok));
                return;
            }
            var time = window.ResetsAt is { } reset ? RemainingTimeFormatter.Format(reset, now) : string.Empty;
            rows.Add(new WidgetRow(
                label,
                window.Utilization,
                PercentFormatter.Format(window.Utilization),
                time,
                BarLevelResolver.Resolve(window.Utilization, a.WarnThreshold, a.CritThreshold)));
        }

        Add(r.ShowFiveHour, "5h", snapshot?.FiveHour);
        Add(r.ShowSevenDay, "7d", snapshot?.SevenDay);
        Add(r.ShowSevenDayOpus, "7d Opus", snapshot?.SevenDayOpus);
        Add(r.ShowSevenDaySonnet, "7d Sonnet", snapshot?.SevenDaySonnet);

        var expired = state.Status == UsageStatus.Expired;
        if (expired && rows.Count > 0)
            rows[0] = rows[0] with { TimeText = RunClaudeHint };

        return new WidgetModel(rows, expired, state.Status == UsageStatus.Stale, rows.Count == 0 ? NoRowsNotice : null);
    }
}
