using System.Windows;
using ClaudeToolbar.App.Services;

namespace ClaudeToolbar.App;

public partial class App
{
    partial void OpenSettingsCore() => Log.Info("Settings requested (window not implemented yet)");
    partial void RefreshNowCore() => Log.Info("Refresh requested (monitor not wired yet)");
}
