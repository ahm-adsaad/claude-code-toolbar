using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Mascot;

public readonly record struct TrackedRow(string Key, BarLevel Level, DateTimeOffset? ResetsAt);

public static class TrackedRows
{
    /// <summary>Pairs each visible widget row with the reset date of the window it shows, keyed by the row label.</summary>
    public static IReadOnlyList<TrackedRow> From(WidgetModel widget, UsageSnapshot? snapshot)
    {
        var rows = new List<TrackedRow>(widget.Rows.Count);
        foreach (var row in widget.Rows)
        {
            var window = row.Label switch
            {
                "5h" => snapshot?.FiveHour,
                "7d" => snapshot?.SevenDay,
                "7d Opus" => snapshot?.SevenDayOpus,
                "7d Sonnet" => snapshot?.SevenDaySonnet,
                _ => null,
            };
            rows.Add(new TrackedRow(row.Label, row.Level, window?.ResetsAt));
        }
        return rows;
    }
}

/// <summary>Emits a cue when a row's level rises within the same reset period. The first observation of a key only primes it.</summary>
public sealed class ThresholdCueTracker
{
    private readonly Dictionary<string, TrackedRow> _last = new();

    public MascotCue? Observe(IEnumerable<TrackedRow> rows)
    {
        MascotCue? cue = null;
        var seen = new HashSet<string>();
        foreach (var row in rows)
        {
            seen.Add(row.Key);
            if (_last.TryGetValue(row.Key, out var previous) && previous.ResetsAt == row.ResetsAt && row.Level > previous.Level)
            {
                var candidate = row.Level == BarLevel.Crit ? MascotCue.Crit : MascotCue.Warn;
                if (cue is null || candidate > cue) cue = candidate;
            }
            _last[row.Key] = row;
        }

        foreach (var stale in _last.Keys.Where(k => !seen.Contains(k)).ToList())
            _last.Remove(stale);

        return cue;
    }

    public void Reset() => _last.Clear();
}
