using System.Windows;
using System.Windows.Media;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Settings;
using ClaudeToolbar.Core.Refresh;

namespace ClaudeToolbar.App;

public partial class App
{
    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;

    partial void OpenSettingsCore()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        ApplyTheme();
        _settingsViewModel = new SettingsViewModel(Settings, () =>
        {
            ApplySettingsLive();
            SaveSettingsDebounced();
        });
        _settingsViewModel.UpdateAccount(CurrentState);
        MonitorStateChanged += OnStateForSettings;

        _settingsWindow = new SettingsWindow(_settingsViewModel);
        _settingsWindow.Closed += (_, _) =>
        {
            MonitorStateChanged -= OnStateForSettings;
            _settingsWindow = null;
            _settingsViewModel = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnStateForSettings(MonitorState state) => _settingsViewModel?.UpdateAccount(state);

    private void ApplyTheme()
    {
        var light = SystemTheme.IsLight();
        Resources["WindowBg"] = Brush(light ? "#FFF3F3F3" : "#FF202020");
        Resources["CardBg"] = Brush(light ? "#FFFFFFFF" : "#FF2B2B2B");
        Resources["InputBg"] = Brush(light ? "#FFFFFFFF" : "#FF1E1E1E");
        Resources["TextPrimary"] = Brush(light ? "#FF1B1B1B" : "#FFF3F3F3");
        Resources["TextSecondary"] = Brush(light ? "#FF5F5F5F" : "#FFA6A6A6");
        Resources["Accent"] = Brush("#FFD97757");
        Resources["BorderBrushKey"] = Brush(light ? "#FFE0E0E0" : "#FF3A3A3A");
    }

    private static SolidColorBrush Brush(string argb)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(argb));
        b.Freeze();
        return b;
    }
}
