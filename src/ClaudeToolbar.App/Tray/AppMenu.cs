using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ClaudeToolbar.App.Services;

namespace ClaudeToolbar.App.Tray;

public sealed class AppMenu : IDisposable
{
    private readonly ContextMenu _menu = new();
    private readonly MenuItem _startupItem;
    private readonly MenuItem _desktopItem;
    private readonly Window _host;

    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action<bool>? RunAtStartupToggled;
    public event Action<bool>? DesktopShortcutToggled;

    public AppMenu(bool runAtStartup)
    {
        _host = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = true,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Opacity = 0,
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            ResizeMode = ResizeMode.NoResize,
        };

        _menu.Items.Add(Item("Refresh now", () => RefreshRequested?.Invoke()));
        _menu.Items.Add(Item("Settings…", () => SettingsRequested?.Invoke()));
        _startupItem = new MenuItem { Header = "Run at startup", IsCheckable = true, IsChecked = runAtStartup };
        _startupItem.Click += (_, _) => RunAtStartupToggled?.Invoke(_startupItem.IsChecked);
        _menu.Items.Add(_startupItem);
        _desktopItem = new MenuItem { Header = "Desktop shortcut", IsCheckable = true };
        _desktopItem.Click += (_, _) => DesktopShortcutToggled?.Invoke(_desktopItem.IsChecked);
        _menu.Items.Add(_desktopItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Item("Exit", () => ExitRequested?.Invoke()));
        _menu.Closed += (_, _) => _host.Hide();
    }

    public void Show()
    {
        _desktopItem.IsChecked = AppShortcuts.HasDesktop();
        _host.Show();
        _host.Activate();
        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    public void SetRunAtStartup(bool enabled)
    {
        if (_startupItem.IsChecked != enabled) _startupItem.IsChecked = enabled;
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose()
    {
        _menu.IsOpen = false;
        _host.Close();
    }
}
