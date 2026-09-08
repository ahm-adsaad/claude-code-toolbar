using System.Runtime.InteropServices;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Services;

/// <summary>
/// Brings a window to the front. The widget never owns the foreground (it is a no-activate window),
/// so Windows may refuse the switch; a synthetic Alt tap marks this process as the last input source,
/// and if that still fails the target's taskbar button flashes so the user sees where to look.
/// </summary>
public static class WindowActivator
{
    public static bool Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        if (TryForeground(hwnd)) return true;
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        if (TryForeground(hwnd)) return true;
        var flash = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = 3,
            dwTimeout = 0,
        };
        FlashWindowEx(ref flash);
        return false;
    }

    private static bool TryForeground(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
        return GetForegroundWindow() == hwnd;
    }
}
