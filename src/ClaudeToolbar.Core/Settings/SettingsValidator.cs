using System.Text.RegularExpressions;

namespace ClaudeToolbar.Core.Settings;

public static partial class SettingsValidator
{
    [GeneratedRegex("^#[0-9A-Fa-f]{8}$")]
    private static partial Regex ArgbPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex RgbPattern();

    public static bool IsValidColor(string? value) => value is not null && ArgbPattern().IsMatch(value);

    /// <summary>Returns a valid #AARRGGBB string: accepts #AARRGGBB, expands #RRGGBB to opaque, else falls back.</summary>
    public static string NormalizeColor(string? value, string fallback)
    {
        if (IsValidColor(value)) return value!.ToUpperInvariant();
        if (value is not null && RgbPattern().IsMatch(value)) return ("#FF" + value[1..]).ToUpperInvariant();
        return fallback;
    }

    public static AppSettings Normalize(AppSettings s)
    {
        if (s.Appearance is null) s.Appearance = new AppearanceSettings();
        if (s.Rows is null) s.Rows = new RowSettings();
        if (s.Behavior is null) s.Behavior = new BehaviorSettings();

        var a = s.Appearance;
        var d = new AppearanceSettings();
        a.Background = NormalizeColor(a.Background, d.Background);
        a.Text = NormalizeColor(a.Text, d.Text);
        a.BarTrack = NormalizeColor(a.BarTrack, d.BarTrack);
        a.BarOk = NormalizeColor(a.BarOk, d.BarOk);
        a.BarWarn = NormalizeColor(a.BarWarn, d.BarWarn);
        a.BarCrit = NormalizeColor(a.BarCrit, d.BarCrit);
        a.FontSize = Math.Clamp(a.FontSize, 9, 14);
        a.CornerRadius = Math.Clamp(a.CornerRadius, 0, 12);
        a.WarnThreshold = Math.Clamp(a.WarnThreshold, 1, 99);
        a.CritThreshold = Math.Clamp(a.CritThreshold, 2, 100);
        if (a.WarnThreshold >= a.CritThreshold)
        {
            a.WarnThreshold = d.WarnThreshold;
            a.CritThreshold = d.CritThreshold;
        }
        if (string.IsNullOrWhiteSpace(a.Preset)) a.Preset = Presets.Custom;
        a.Preset = a.Preset.Trim().ToLowerInvariant();

        s.Rows.BarWidth = Math.Clamp(s.Rows.BarWidth, 30, 120);

        var b = s.Behavior;
        b.RefreshIntervalSeconds = Math.Clamp(b.RefreshIntervalSeconds, 30, 300);
        b.TrayGapPx = Math.Clamp(b.TrayGapPx, 0, 24);

        s.Version = 1;
        return s;
    }
}
