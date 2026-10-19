using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

/// <summary>
/// <paramref name="ShortLabel"/> is the two-character name the compact layouts use. <paramref name="Warning"/> is set when
/// the limit would run out before its reset at the user's pace; it takes the time column's place.
/// </summary>
public sealed record WidgetRow(UsageLimit Limit, string Label, string ShortLabel, double Utilization, string PercentText, string TimeText, BarLevel Level, string? Warning = null, BarLevel WarningLevel = BarLevel.Warn);

public sealed record WidgetModel(IReadOnlyList<WidgetRow> Rows, bool Dimmed, bool ShowStaleDot, string? Notice);

public static class WidgetModelBuilder
{
    public const string SignInNotice = "Sign in with claude";
    public const string RunClaudeHint = "↻ run claude";
    public const string NoRowsNotice = "No rows enabled";
    public const string Placeholder = "—";

    /// <summary>Under this much work left the warning takes the critical colour.</summary>
    public static readonly TimeSpan CritWorkLeft = TimeSpan.FromMinutes(30);

    public static WidgetModel Build(MonitorState state, AppSettings settings, DateTimeOffset now, IReadOnlyDictionary<UsageLimit, PaceEstimate>? pace = null)
    {
        if (state.Status == UsageStatus.NoCredentials)
            return new WidgetModel([], false, false, SignInNotice);

        var snapshot = state.Snapshot;
        var a = settings.Appearance;
        var r = settings.Rows;
        var rows = new List<WidgetRow>(5);
        var expired = state.Status == UsageStatus.Expired;

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
            // An expired login means the numbers stopped moving; a warning drawn from them would go stale.
            var runsOut = !expired && pace?.GetValueOrDefault(limit) is { Verdict: PaceVerdict.RunsOutFirst, WorkLeft: { } left } ? left : (TimeSpan?)null;
            rows.Add(new WidgetRow(
                limit,
                label,
                shortLabel,
                window.Utilization,
                PercentFormatter.Format(window.Utilization),
                time,
                BarLevelResolver.Resolve(window.Utilization, a.WarnThreshold, a.CritThreshold),
                runsOut is { } l ? PaceFormatter.Format(l) + " work" : null,
                runsOut < CritWorkLeft ? BarLevel.Crit : BarLevel.Warn));
        }

        Add(r.ShowFiveHour, UsageLimit.FiveHour, "5h", "5h");
        Add(r.ShowSevenDay, UsageLimit.SevenDay, "7d", "7d");
        Add(r.ShowSevenDayOpus, UsageLimit.SevenDayOpus, "7d Opus", "Op");
        Add(r.ShowSevenDaySonnet, UsageLimit.SevenDaySonnet, "7d Sonnet", "So");
        Add(r.ShowSevenDayFable, UsageLimit.SevenDayFable, "7d Fable", "Fa");

        if (expired && rows.Count > 0)
            rows[0] = rows[0] with { TimeText = RunClaudeHint };

        return new WidgetModel(rows, expired, state.Status == UsageStatus.Stale, rows.Count == 0 ? NoRowsNotice : null);
    }
}
