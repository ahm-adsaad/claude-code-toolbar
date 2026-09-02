using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

public static class ShellState
{
    public static bool IsFullscreenAppActive()
    {
        if (SHQueryUserNotificationState(out var state) != 0) return false;
        return state is QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE;
    }

    /// <summary>True when window <paramref name="a"/> is above <paramref name="b"/> in the top-level z-order.</summary>
    public static bool IsAbove(IntPtr a, IntPtr b)
    {
        for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
        {
            if (h == a) return true;
            if (h == b) return false;
        }
        return false;
    }
}
