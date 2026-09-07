namespace ClaudeToolbar.Core.Settings;

public static class MascotMode
{
    public const string Full = "full";
    public const string Hover = "hover";
    public const string Off = "off";

    public static IReadOnlyList<string> All { get; } = [Full, Hover, Off];

    public static string Normalize(string? value)
    {
        var key = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return All.Contains(key) ? key : Full;
    }
}
