using System.Windows;
using ClaudeToolbar.App.Interop;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Widget;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Layout;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App;

public partial class App
{
    private WidgetWindow? _widget;

    partial void OnStartupCore(StartupEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            new UsageWindow(42, now.AddHours(2).AddMinutes(13)),
            new UsageWindow(18, now.AddDays(3).AddHours(4)),
            null, null, now);
        var state = new MonitorState(UsageStatus.Ok, snapshot, now, null, new CredentialsState.Missing("sample"));
        var model = WidgetModelBuilder.Build(state, Settings, now);

        _widget = new WidgetWindow();
        _widget.Render(model, Settings.Rows, WidgetTheme.FromSettings(Settings.Appearance));
        _widget.ShowNoActivate();

        var layout = TaskbarLocator.Locate();
        if (layout is null)
        {
            Log.Error("Taskbar not found");
            return;
        }
        _widget.SetMaxPhysicalHeight(WidgetPlacement.MaxWidgetHeight(layout.TaskbarNow));
        _widget.UpdateLayout();
        var (w, h) = _widget.PhysicalSize();
        var target = WidgetPlacement.Compute(layout.TaskbarNow, layout.Notify, w, h, Settings.Behavior.TrayGapPx);
        _widget.MoveTo(target);
        Log.Info($"Widget placed at {target} (size {w}x{h})");
    }

    partial void OnExitCore()
    {
        _widget?.Close();
    }
}
