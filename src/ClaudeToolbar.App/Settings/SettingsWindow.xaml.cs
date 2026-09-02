using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeToolbar.App.Widget;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Settings;

public partial class SettingsWindow : Window
{
    public static readonly BooleanToVisibilityConverter BoolToVisibility = new();

    private readonly SettingsViewModel _vm;
    private readonly UsageRowsControl _preview = new();

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        PreviewHost.Content = _preview;
        _vm.PropertyChanged += OnVmChanged;
        RenderPreview();
    }

    public SettingsViewModel ViewModel => _vm;

    // On this machine, this window's content renders as blank under WPF's default hardware/DWM
    // composition path (confirmed with PrintWindow(PW_RENDERFULLCONTENT), not just a screen-capture
    // artifact), while the app's other windows (which use AllowsTransparency) render fine. Forcing
    // software rendering for just this window's composition target avoids the broken hardware path
    // without affecting the rest of the app.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
            hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e) => RenderPreview();

    private void RenderPreview()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            new UsageWindow(42, now.AddHours(2).AddMinutes(13)),
            new UsageWindow(75, now.AddDays(3).AddHours(4)),
            new UsageWindow(93, now.AddDays(3)),
            new UsageWindow(12, now.AddDays(2)),
            now);
        var state = new MonitorState(UsageStatus.Ok, snapshot, now, null, new CredentialsState.Missing("preview"));
        var model = WidgetModelBuilder.Build(state, _vm.Settings, now);
        _preview.Render(model, _vm.Settings.Rows, WidgetTheme.FromSettings(_vm.Settings.Appearance));
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => _vm.ReloadFrom(AppSettings.CreateDefault());

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Refresh_Click(object sender, RoutedEventArgs e) => App.Current.RefreshNow();

    protected override void OnClosed(EventArgs e)
    {
        _vm.PropertyChanged -= OnVmChanged;
        base.OnClosed(e);
    }
}
