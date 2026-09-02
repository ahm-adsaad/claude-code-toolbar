using WinForms = System.Windows.Forms;

namespace ClaudeToolbar.App.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;

    public event Action? MenuRequested;
    public event Action? SettingsRequested;

    public TrayIcon(string tooltip)
    {
        _icon = new WinForms.NotifyIcon
        {
            Icon = IconLoader.LoadAppIcon(WinForms.SystemInformation.SmallIconSize.Width),
            Text = Truncate(tooltip),
            Visible = true,
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Right) MenuRequested?.Invoke();
        };
        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
    }

    public void SetTooltip(string text) => _icon.Text = Truncate(text);

    private static string Truncate(string text) => text.Length > 63 ? text[..63] : text;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
