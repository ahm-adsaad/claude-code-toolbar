using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeToolbar.Core.Settings;

public sealed class AppearanceSettings
{
    public string Preset { get; set; } = "dark";
    public string Background { get; set; } = "#CC1E1E1E";
    public string Text { get; set; } = "#FFF3F3F3";
    public string BarTrack { get; set; } = "#33FFFFFF";
    public string BarOk { get; set; } = "#FF3FB950";
    public string BarWarn { get; set; } = "#FFD29922";
    public string BarCrit { get; set; } = "#FFF85149";
    public double FontSize { get; set; } = 11;
    public double CornerRadius { get; set; } = 6;
    public int WarnThreshold { get; set; } = 70;
    public int CritThreshold { get; set; } = 90;
}

public sealed class RowSettings
{
    public bool ShowFiveHour { get; set; } = true;
    public bool ShowSevenDay { get; set; } = true;
    public bool ShowSevenDayOpus { get; set; }
    public bool ShowSevenDaySonnet { get; set; }
    public bool ShowLabel { get; set; } = true;
    public bool ShowBar { get; set; } = true;
    public bool ShowPercent { get; set; } = true;
    public bool ShowTime { get; set; } = true;
    public double BarWidth { get; set; } = 60;
}

public sealed class BehaviorSettings
{
    public int RefreshIntervalSeconds { get; set; } = 60;
    public int TrayGapPx { get; set; } = 8;
    public bool HideInFullscreen { get; set; } = true;
    public bool RunAtStartup { get; set; } = true;
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public AppearanceSettings Appearance { get; set; } = new();
    public RowSettings Rows { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() => SettingsJson.Deserialize(SettingsJson.Serialize(this));
}

public static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    public static AppSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
}
