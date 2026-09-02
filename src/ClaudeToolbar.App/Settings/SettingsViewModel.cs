using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Settings;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Action _onChanged;
    private AppSettings _s;

    public SettingsViewModel(AppSettings settings, Action onChanged)
    {
        _s = settings;
        _onChanged = onChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings => _s;

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Changed([CallerMemberName] string? name = null)
    {
        SettingsValidator.Normalize(_s);
        Raise(name);
        _onChanged();
    }

    private void SetColor(Action<string> assign, string value, [CallerMemberName] string? name = null)
    {
        assign(SettingsValidator.NormalizeColor(value, "#FF000000"));
        if (_s.Appearance.Preset != Presets.Custom)
        {
            _s.Appearance.Preset = Presets.Custom;
            Raise(nameof(Preset));
        }
        Changed(name);
    }

    // Appearance
    public string Preset
    {
        get => _s.Appearance.Preset;
        set
        {
            if (!Presets.TryApply(value, _s.Appearance)) return;
            Changed();
            RaiseAllColors();
        }
    }

    public string Background { get => _s.Appearance.Background; set => SetColor(v => _s.Appearance.Background = v, value); }
    public string Text { get => _s.Appearance.Text; set => SetColor(v => _s.Appearance.Text = v, value); }
    public string BarTrack { get => _s.Appearance.BarTrack; set => SetColor(v => _s.Appearance.BarTrack = v, value); }
    public string BarOk { get => _s.Appearance.BarOk; set => SetColor(v => _s.Appearance.BarOk = v, value); }
    public string BarWarn { get => _s.Appearance.BarWarn; set => SetColor(v => _s.Appearance.BarWarn = v, value); }
    public string BarCrit { get => _s.Appearance.BarCrit; set => SetColor(v => _s.Appearance.BarCrit = v, value); }

    public double FontSize { get => _s.Appearance.FontSize; set { _s.Appearance.FontSize = value; Changed(); } }
    public double CornerRadius { get => _s.Appearance.CornerRadius; set { _s.Appearance.CornerRadius = value; Changed(); } }

    public double WarnThreshold
    {
        get => _s.Appearance.WarnThreshold;
        set
        {
            var warn = (int)Math.Round(value);
            _s.Appearance.WarnThreshold = warn;
            if (warn >= _s.Appearance.CritThreshold)
            {
                _s.Appearance.CritThreshold = Math.Min(100, warn + 1);
                Raise(nameof(CritThreshold));
            }
            Changed();
        }
    }

    public double CritThreshold
    {
        get => _s.Appearance.CritThreshold;
        set
        {
            var crit = (int)Math.Round(value);
            _s.Appearance.CritThreshold = crit;
            if (crit <= _s.Appearance.WarnThreshold)
            {
                _s.Appearance.WarnThreshold = Math.Max(1, crit - 1);
                Raise(nameof(WarnThreshold));
            }
            Changed();
        }
    }

    // Rows
    public bool ShowFiveHour { get => _s.Rows.ShowFiveHour; set { _s.Rows.ShowFiveHour = value; Changed(); } }
    public bool ShowSevenDay { get => _s.Rows.ShowSevenDay; set { _s.Rows.ShowSevenDay = value; Changed(); } }
    public bool ShowSevenDayOpus { get => _s.Rows.ShowSevenDayOpus; set { _s.Rows.ShowSevenDayOpus = value; Changed(); } }
    public bool ShowSevenDaySonnet { get => _s.Rows.ShowSevenDaySonnet; set { _s.Rows.ShowSevenDaySonnet = value; Changed(); } }
    public bool ShowLabel { get => _s.Rows.ShowLabel; set { _s.Rows.ShowLabel = value; Changed(); } }
    public bool ShowBar { get => _s.Rows.ShowBar; set { _s.Rows.ShowBar = value; Changed(); } }
    public bool ShowPercent { get => _s.Rows.ShowPercent; set { _s.Rows.ShowPercent = value; Changed(); } }
    public bool ShowTime { get => _s.Rows.ShowTime; set { _s.Rows.ShowTime = value; Changed(); } }
    public double BarWidth { get => _s.Rows.BarWidth; set { _s.Rows.BarWidth = value; Changed(); } }

    // Behaviour
    public double RefreshIntervalSeconds { get => _s.Behavior.RefreshIntervalSeconds; set { _s.Behavior.RefreshIntervalSeconds = (int)Math.Round(value); Changed(); } }
    public double TrayGapPx { get => _s.Behavior.TrayGapPx; set { _s.Behavior.TrayGapPx = (int)Math.Round(value); Changed(); } }
    public bool HideInFullscreen { get => _s.Behavior.HideInFullscreen; set { _s.Behavior.HideInFullscreen = value; Changed(); } }
    public bool RunAtStartup { get => _s.Behavior.RunAtStartup; set { _s.Behavior.RunAtStartup = value; Changed(); } }

    // Account (read-only, fed by UpdateAccount)
    public string CredentialsPath { get; private set; } = string.Empty;
    public string TokenStateText { get; private set; } = "Unknown";
    public string SubscriptionText { get; private set; } = "—";
    public string LastUpdateText { get; private set; } = "Never";
    public string HintText { get; private set; } = string.Empty;
    public bool HintVisible { get; private set; }

    public void UpdateAccount(MonitorState? state)
    {
        if (state is null) return;
        CredentialsPath = state.Credentials switch
        {
            CredentialsState.Missing m => m.Path,
            CredentialsState.Invalid i => i.Path,
            CredentialsState.Expired e => e.Path,
            CredentialsState.Valid v => v.Path,
            _ => string.Empty,
        };
        TokenStateText = state.Credentials switch
        {
            CredentialsState.Valid v => $"Valid until {v.ExpiresAt.ToLocalTime():HH:mm}",
            CredentialsState.Expired e => $"Expired at {e.ExpiresAt.ToLocalTime():HH:mm}",
            CredentialsState.Invalid i => $"Unreadable: {i.Reason}",
            _ => "Not found",
        };
        SubscriptionText = state.Credentials switch
        {
            CredentialsState.Valid v => v.SubscriptionType ?? "—",
            CredentialsState.Expired e => e.SubscriptionType ?? "—",
            _ => "—",
        };
        LastUpdateText = state.LastSuccess is { } last ? $"{last.ToLocalTime():HH:mm:ss}" : "Never";
        HintVisible = state.Status is UsageStatus.Expired or UsageStatus.NoCredentials;
        HintText = HintVisible ? "Run `claude` in a terminal to refresh your login." : string.Empty;
        Raise(nameof(CredentialsPath));
        Raise(nameof(TokenStateText));
        Raise(nameof(SubscriptionText));
        Raise(nameof(LastUpdateText));
        Raise(nameof(HintText));
        Raise(nameof(HintVisible));
    }

    /// <summary>Copies values from another settings object into the live one and refreshes every binding.</summary>
    public void ReloadFrom(AppSettings source)
    {
        var json = SettingsJson.Serialize(source);
        var fresh = SettingsValidator.Normalize(SettingsJson.Deserialize(json));
        _s.Appearance = fresh.Appearance;
        _s.Rows = fresh.Rows;
        _s.Behavior = fresh.Behavior;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        _onChanged();
    }

    private void RaiseAllColors()
    {
        foreach (var n in new[] { nameof(Background), nameof(Text), nameof(BarTrack), nameof(BarOk), nameof(BarWarn), nameof(BarCrit) })
            Raise(n);
    }
}
