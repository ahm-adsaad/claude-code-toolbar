using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaudeToolbar.Core.Layout;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

public static class TaskbarLocator
{
    public static TaskbarLayout? Locate()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return null;

        GetWindowThreadProcessId(tray, out var pid);
        if (!IsExplorer(pid)) return null;

        var notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify == IntPtr.Zero) return null;

        return ReadRects(tray, notify, pid);
    }

    /// <summary>Re-reads rects for a previously located taskbar. Returns null if its windows are gone.</summary>
    public static TaskbarLayout? Refresh(TaskbarLayout previous)
    {
        if (!IsWindow(previous.TrayHwnd) || !IsWindow(previous.NotifyHwnd)) return null;
        return ReadRects(previous.TrayHwnd, previous.NotifyHwnd, previous.ExplorerPid);
    }

    private static TaskbarLayout? ReadRects(IntPtr tray, IntPtr notify, uint pid)
    {
        if (!GetWindowRect(tray, out var trayNow) || !GetWindowRect(notify, out var notifyRect)) return null;

        var pos = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        var hasPos = SHAppBarMessage(ABM_GETTASKBARPOS, ref pos) != UIntPtr.Zero;
        var taskbar = hasPos ? ToRect(pos.rc) : ToRect(trayNow);

        var state = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        var autoHide = (SHAppBarMessage(ABM_GETSTATE, ref state).ToUInt32() & ABS_AUTOHIDE) != 0;

        var monitorHandle = MonitorFromWindow(tray, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        var monitor = GetMonitorInfo(monitorHandle, ref info) ? ToRect(info.rcMonitor) : taskbar;

        return new TaskbarLayout(tray, notify, taskbar, ToRect(trayNow), ToRect(notifyRect), monitor, autoHide, pid);
    }

    private static RectI ToRect(RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);

    private static bool IsExplorer(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
