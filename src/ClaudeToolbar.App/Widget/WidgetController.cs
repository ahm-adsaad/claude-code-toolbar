using ClaudeToolbar.App.Interop;
using ClaudeToolbar.Core.Layout;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Widget;

/// <summary>Applies the tracked taskbar layout to the widget window: show/hide, place, keep on top.</summary>
public sealed class WidgetController : IDisposable
{
    private readonly WidgetWindow _window;
    private readonly TaskbarTracker _tracker;
    private readonly Func<AppSettings> _settings;

    public WidgetController(WidgetWindow window, TaskbarTracker tracker, Func<AppSettings> settings)
    {
        _window = window;
        _tracker = tracker;
        _settings = settings;
        _tracker.Changed += Reposition;
        _window.SizeChanged += (_, _) => Reposition();
    }

    public void Start() => _tracker.Start();

    /// <summary>Forget cached taskbar handles and search again (explorer restart, display change, resume).</summary>
    public void Relocate() => _tracker.Relocate();

    public void Reposition()
    {
        var layout = _tracker.Layout;
        if (layout is null)
        {
            _window.HideWidget();
            return;
        }

        var behavior = _settings().Behavior;
        var fullscreen = behavior.HideInFullscreen && ShellState.IsFullscreenAppActive();
        var taskbarHidden = layout.AutoHide && WidgetPlacement.IsTaskbarMostlyHidden(layout.TaskbarNow, layout.Monitor);
        if (fullscreen || taskbarHidden)
        {
            _window.HideWidget();
            return;
        }

        _window.SetMaxPhysicalHeight(WidgetPlacement.MaxWidgetHeight(layout.TaskbarNow));
        _window.ShowNoActivate();
        _window.UpdateLayout();

        var (w, h) = _window.PhysicalSize();
        if (w == 0 || h == 0) return;

        var target = WidgetPlacement.Compute(layout.TaskbarNow, layout.Notify, w, h, behavior.TrayGapPx);
        if (target != _window.CurrentRect())
            _window.MoveTo(target);
        else if (ShellState.IsAbove(layout.TrayHwnd, _window.Handle))
            _window.AssertTopmost();
    }

    public void Dispose()
    {
        _tracker.Changed -= Reposition;
        _tracker.Dispose();
    }
}
