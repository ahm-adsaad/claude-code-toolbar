using System.Windows.Threading;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Widget;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

/// <summary>Keeps an up-to-date <see cref="TaskbarLayout"/> and raises <see cref="Changed"/> on the UI thread whenever it moves.</summary>
public sealed class TaskbarTracker : IDisposable
{
    private static readonly TimeSpan LocateRetry = TimeSpan.FromSeconds(3);

    private readonly WidgetWindow _window;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly uint _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
    private WinEventHook? _hook;
    private DateTime _lastLocateAttempt = DateTime.MinValue;
    private bool _evaluateQueued;

    public TaskbarTracker(WidgetWindow window)
    {
        _window = window;
        _window.ShellMessage += OnShellMessage;
        _timer.Tick += (_, _) =>
        {
            Evaluate(force: false);
            Sanity?.Invoke();
        };
    }

    public TaskbarLayout? Layout { get; private set; }

    public event Action? Changed;

    /// <summary>Raised once per second regardless of whether the layout changed; the controller re-checks fullscreen and z-order on it.</summary>
    public event Action? Sanity;

    public void Start()
    {
        Relocate();
        _timer.Start();
    }

    private void OnShellMessage(int msg)
    {
        if (msg == (int)_taskbarCreatedMsg || msg is WM_DISPLAYCHANGE or WM_DPICHANGED or WM_SETTINGCHANGE)
            QueueEvaluate(relocate: msg == (int)_taskbarCreatedMsg || msg == WM_DISPLAYCHANGE);
    }

    /// <summary>Forget cached handles and search for the taskbar again.</summary>
    public void Relocate()
    {
        _hook?.Dispose();
        _hook = null;
        _lastLocateAttempt = DateTime.UtcNow;
        Layout = TaskbarLocator.Locate();
        if (Layout is { } l)
        {
            var tray = l.TrayHwnd;
            var notify = l.NotifyHwnd;
            _hook = new WinEventHook(l.ExplorerPid, h => h == tray || h == notify, () => QueueEvaluate(relocate: false));
            Log.Info($"Taskbar located: now={l.TaskbarNow} notify={l.Notify} autohide={l.AutoHide}");
        }
        else
        {
            Log.Info("Taskbar not found; will retry");
        }
        Changed?.Invoke();
    }

    public void Evaluate(bool force)
    {
        if (Layout is null)
        {
            if (DateTime.UtcNow - _lastLocateAttempt >= LocateRetry) Relocate();
            return;
        }

        var fresh = TaskbarLocator.Refresh(Layout);
        if (fresh is null)
        {
            Relocate();
            return;
        }

        if (force || fresh != Layout)
        {
            Layout = fresh;
            Changed?.Invoke();
        }
    }

    private void QueueEvaluate(bool relocate)
    {
        if (_evaluateQueued) return;
        _evaluateQueued = true;
        _window.Dispatcher.InvokeAsync(() =>
        {
            _evaluateQueued = false;
            if (relocate) Relocate();
            else Evaluate(force: true);
        }, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _timer.Stop();
        _window.ShellMessage -= OnShellMessage;
        _hook?.Dispose();
    }
}
