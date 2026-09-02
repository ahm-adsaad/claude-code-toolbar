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
    private WidgetController? _controller;

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
        _controller = new WidgetController(_widget, new TaskbarTracker(_widget), () => Settings);
        _controller.Start();
    }

    partial void OnExitCore()
    {
        _controller?.Dispose();
        _widget?.Close();
    }
}
