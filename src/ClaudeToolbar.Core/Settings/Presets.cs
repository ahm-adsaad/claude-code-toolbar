namespace ClaudeToolbar.Core.Settings;

public static class Presets
{
    public const string Custom = "custom";

    public static IReadOnlyList<string> Names { get; } = ["dark", "light", "claude", "mono"];

    public static bool TryApply(string name, AppearanceSettings a)
    {
        var key = name.Trim().ToLowerInvariant();
        switch (key)
        {
            case "dark":
                Set(a, "#CC1E1E1E", "#FFF3F3F3", "#33FFFFFF", "#FF3FB950", "#FFD29922", "#FFF85149");
                break;
            case "light":
                Set(a, "#CCF7F7F7", "#FF1B1B1B", "#22000000", "#FF1A7F37", "#FFB08800", "#FFCF222E");
                break;
            case "claude":
                Set(a, "#CC1F1A17", "#FFF5EDE4", "#33F5EDE4", "#FFD97757", "#FFE8A34F", "#FFE5484D");
                break;
            case "mono":
                Set(a, "#CC111111", "#FFEDEDED", "#22FFFFFF", "#FFBDBDBD", "#FF8A8A8A", "#FFFFFFFF");
                break;
            default:
                return false;
        }

        a.Preset = key;
        return true;
    }

    private static void Set(AppearanceSettings a, string background, string text, string track, string ok, string warn, string crit)
    {
        a.Background = background;
        a.Text = text;
        a.BarTrack = track;
        a.BarOk = ok;
        a.BarWarn = warn;
        a.BarCrit = crit;
    }
}
