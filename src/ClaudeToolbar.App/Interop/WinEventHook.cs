using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

/// <summary>Out-of-context location-change hook scoped to one process. Callbacks arrive on the creating (UI) thread.</summary>
public sealed class WinEventHook : IDisposable
{
    private readonly WinEventDelegate _callback;
    private readonly Func<IntPtr, bool> _filter;
    private readonly Action _onEvent;
    private IntPtr _hook;

    public WinEventHook(uint pid, Func<IntPtr, bool> filter, Action onEvent)
    {
        _filter = filter;
        _onEvent = onEvent;
        _callback = Callback;
        _hook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _callback, pid, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void Callback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject == OBJID_WINDOW && _filter(hwnd)) _onEvent();
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
