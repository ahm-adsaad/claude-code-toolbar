namespace ClaudeToolbar.Core.Formatting;

/// <summary>Pace estimates are guesses, so they are rounded coarser the further out they reach.</summary>
public static class PaceFormatter
{
    public static string Format(TimeSpan t, bool approximate = true)
    {
        var m = t.TotalMinutes;
        var rounded =
            m < 10 ? Math.Max(1, Round(m, 1)) :
            m < 57.5 ? Round(m, 5) :
            m < 600 ? Round(m, 30) :
            Round(m, 60);
        return (approximate ? "~" : "") + Compact(rounded);
    }

    /// <summary>How long the rate was measured over, in words.</summary>
    public static string Span(TimeSpan t)
    {
        var m = t.TotalMinutes;
        if (m < 57.5) return Plural(Math.Max(1, Round(m, 5)), "minute");
        var hours = Round(m, 60) / 60;
        return hours < 48 ? Plural(hours, "hour") : Plural(Round(m, 1440) / 1440, "day");
    }

    private static int Round(double minutes, int step) =>
        (int)Math.Round(minutes / step, MidpointRounding.AwayFromZero) * step;

    private static string Compact(int minutes)
    {
        if (minutes < 60) return $"{minutes}m";
        if (minutes < 1440) return minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60}m";
        var hours = minutes % 1440 / 60;
        return hours == 0 ? $"{minutes / 1440}d" : $"{minutes / 1440}d {hours}h";
    }

    private static string Plural(int n, string unit) => n == 1 ? $"1 {unit}" : $"{n} {unit}s";
}
