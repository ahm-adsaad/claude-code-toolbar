using System.Runtime.InteropServices;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Services;

/// <summary>What became of a window the user asked to jump to.</summary>
public enum WindowActivation
{
    /// <summary>The handle was already dead by the time we got to it.</summary>
    Gone,
    /// <summary>Windows refused the foreground switch; the taskbar button flashes instead.</summary>
    Flashed,
    /// <summary>The window is in front.</summary>
    Raised,
}

/// <summary>
/// Brings a window to the front. The widget never owns the foreground (it is a no-activate window),
/// so Windows may refuse the switch; a synthetic Control tap marks this process as the last input
/// source, and if that still fails the target's taskbar button flashes so the user sees where to look.
/// Control rather than Alt on purpose: a bare Alt tap opens the menu bar of whichever app is in front,
/// and since the foreground moves away a moment later that menu is left hanging open.
/// </summary>
public static class WindowActivator
{
    public static WindowActivation Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return WindowActivation.Gone;
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        if (TryForeground(hwnd)) return WindowActivation.Raised;
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        if (TryForeground(hwnd)) return WindowActivation.Raised;
        var flash = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = 3,
            dwTimeout = 0,
        };
        FlashWindowEx(ref flash);
        return WindowActivation.Flashed;
    }

    private static bool TryForeground(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
        return GetForegroundWindow() == hwnd;
    }
}
