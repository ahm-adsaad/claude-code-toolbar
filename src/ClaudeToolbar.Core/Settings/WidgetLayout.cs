namespace ClaudeToolbar.Core.Settings;

/// <summary>How the Windows widget draws its limits: rings side by side, upright bars side by side, or stacked rows.</summary>
public static class WidgetLayout
{
    public const string Rings = "rings";
    public const string Bars = "bars";
    public const string Rows = "rows";

    public static IReadOnlyList<string> All { get; } = [Rings, Bars, Rows];

    public static string Normalize(string? value)
    {
        var key = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return All.Contains(key) ? key : Rings;
    }
}
